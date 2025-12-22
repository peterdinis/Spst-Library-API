using Microsoft.AspNetCore.Mvc;
using FluentValidation;
using Polly.CircuitBreaker;
using BooksService.Dtos;
using BooksService.Entities;
using BooksService.Interfaces;
using Polly;
using Polly.Timeout;
using Polly.Retry;
using BooksService.Repositories;

namespace BooksService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BooksController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<BooksController> _logger;
        private readonly IRabbitMQService _rabbitMQService;
        private readonly IAsyncPolicy _resiliencePolicy;
        private readonly IValidator<CreateBookDto> _createValidator;
        private readonly IValidator<UpdateBookDto> _updateValidator;

        public BooksController(
            IUnitOfWork unitOfWork,
            ILogger<BooksController> logger, 
            IRabbitMQService rabbitMQService,
            IValidator<CreateBookDto> createValidator,
            IValidator<UpdateBookDto> updateValidator)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
            _rabbitMQService = rabbitMQService;
            _createValidator = createValidator;
            _updateValidator = updateValidator;
            
            _resiliencePolicy = CreateResiliencePolicy();
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<BookDto>>> GetBooks()
        {
            _logger.LogInformation("Getting all books");

            var books = await _unitOfWork.Books.GetAllAsync();
            var bookDtos = books.Select(MapToDto).ToList();

            _logger.LogInformation("Retrieved {Count} books", bookDtos.Count);
            return Ok(bookDtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<BookDto>> GetBook(int id)
        {
            _logger.LogInformation("Getting book with ID {BookId}", id);

            var book = await _unitOfWork.Books.GetByIdAsync(id);
            if (book == null)
            {
                _logger.LogWarning("Book with ID {BookId} not found", id);
                return NotFound(new { message = "Book not found" });
            }

            _logger.LogInformation("Book with ID {BookId} found: {BookTitle}", id, book.Title);
            return Ok(MapToDto(book));
        }

        [HttpPost]
        public async Task<ActionResult<BookDto>> CreateBook(CreateBookDto dto)
        {
            // Validate input
            var validationResult = await _createValidator.ValidateAsync(dto);
            if (!validationResult.IsValid)
            {
                _logger.LogWarning("Validation failed for creating book: {Errors}",
                    string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage)));
                return BadRequest(new { errors = validationResult.Errors.Select(e => e.ErrorMessage) });
            }

            _logger.LogInformation("Creating new book with title: {BookTitle}", dto.Title);

            // Check if ISBN already exists
            var isbnExists = await _unitOfWork.Books.IsbnExistsAsync(dto.ISBN);
            if (isbnExists)
            {
                _logger.LogWarning("Book with ISBN {ISBN} already exists", dto.ISBN);
                return Conflict(new { message = $"Book with ISBN {dto.ISBN} already exists" });
            }

            try
            {
                // Verify category exists using resilience policy
                var categoryResponse = await _resiliencePolicy.ExecuteAsync(async () => 
                {
                    return await _rabbitMQService.GetCategoryAsync(dto.CategoryId);
                });
                
                if (!categoryResponse.Exists)
                {
                    _logger.LogWarning("Category with ID {CategoryId} does not exist", dto.CategoryId);
                    return BadRequest(new { message = $"Category with ID {dto.CategoryId} does not exist" });
                }

                // Verify author exists using resilience policy
                var authorResponse = await _resiliencePolicy.ExecuteAsync(async () => 
                {
                    return await _rabbitMQService.GetAuthorAsync(dto.AuthorId);
                });
                
                if (!authorResponse.Exists)
                {
                    _logger.LogWarning("Author with ID {AuthorId} does not exist", dto.AuthorId);
                    return BadRequest(new { message = $"Author with ID {dto.AuthorId} does not exist" });
                }

                var book = new Book
                {
                    Title = dto.Title,
                    AuthorId = dto.AuthorId,
                    Author = authorResponse.AuthorName,
                    Publisher = dto.Publisher,
                    Year = dto.Year,
                    ISBN = dto.ISBN,
                    Pages = dto.Pages,
                    CategoryId = dto.CategoryId,
                    Category = categoryResponse.CategoryTitle,
                    Language = dto.Language,
                    Description = dto.Description,
                    PhotoPath = dto.PhotoPath,
                    IsAvailable = true,
                    AddedToLibrary = DateTime.UtcNow
                };

                await _unitOfWork.Books.AddAsync(book);
                await _unitOfWork.CompleteAsync();

                _logger.LogInformation("Book created successfully with ID {BookId}: {BookTitle}", book.Id, book.Title);

                return CreatedAtAction(nameof(GetBook), new { id = book.Id }, MapToDto(book));
            }
            catch (TimeoutRejectedException ex)
            {
                _logger.LogWarning(ex, "External service timeout while creating book");
                return StatusCode(503, new { message = "External service is temporarily unavailable" });
            }
            catch (BrokenCircuitException ex)
            {
                _logger.LogWarning(ex, "Circuit breaker is open for external service");
                return StatusCode(503, new { message = "External service is currently unavailable. Please try again later." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while creating book with title: {BookTitle}", dto.Title);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateBook(int id, UpdateBookDto dto)
        {
            // Validate input
            var validationResult = await _updateValidator.ValidateAsync(dto);
            if (!validationResult.IsValid)
            {
                _logger.LogWarning("Validation failed for updating book with ID {BookId}: {Errors}",
                    id, string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage)));
                return BadRequest(new { errors = validationResult.Errors.Select(e => e.ErrorMessage) });
            }

            _logger.LogInformation("Updating book with ID {BookId}", id);

            var book = await _unitOfWork.Books.GetByIdAsync(id);
            if (book == null)
            {
                _logger.LogWarning("Book with ID {BookId} not found for update", id);
                return NotFound(new { message = "Book not found" });
            }

            // Check if ISBN already exists (excluding current book)
            if (book.ISBN != dto.ISBN)
            {
                var isbnExists = await _unitOfWork.Books.IsbnExistsAsync(dto.ISBN, id);
                if (isbnExists)
                {
                    _logger.LogWarning("Another book with ISBN {ISBN} already exists", dto.ISBN);
                    return Conflict(new { message = $"Another book with ISBN {dto.ISBN} already exists" });
                }
            }

            try
            {
                // Check if category exists when CategoryId is being updated
                if (book.CategoryId != dto.CategoryId)
                {
                    var categoryResponse = await _resiliencePolicy.ExecuteAsync(async () => 
                    {
                        return await _rabbitMQService.GetCategoryAsync(dto.CategoryId);
                    });
                    
                    if (!categoryResponse.Exists)
                    {
                        _logger.LogWarning("Category with ID {CategoryId} does not exist", dto.CategoryId);
                        return BadRequest(new { message = $"Category with ID {dto.CategoryId} does not exist" });
                    }
                    book.Category = categoryResponse.CategoryTitle;
                }

                // Check if author exists when AuthorId is being updated
                if (book.AuthorId != dto.AuthorId)
                {
                    var authorResponse = await _resiliencePolicy.ExecuteAsync(async () => 
                    {
                        return await _rabbitMQService.GetAuthorAsync(dto.AuthorId);
                    });
                    
                    if (!authorResponse.Exists)
                    {
                        _logger.LogWarning("Author with ID {AuthorId} does not exist", dto.AuthorId);
                        return BadRequest(new { message = $"Author with ID {dto.AuthorId} does not exist" });
                    }
                    book.Author = authorResponse.AuthorName;
                }

                _logger.LogInformation("Updating book from: {OldTitle} to: {NewTitle}", book.Title, dto.Title);

                book.Title = dto.Title;
                book.AuthorId = dto.AuthorId;
                book.Publisher = dto.Publisher;
                book.Year = dto.Year;
                book.ISBN = dto.ISBN;
                book.Pages = dto.Pages;
                book.CategoryId = dto.CategoryId;
                book.Language = dto.Language;
                book.Description = dto.Description;
                book.PhotoPath = dto.PhotoPath;
                book.IsAvailable = dto.IsAvailable;

                await _unitOfWork.Books.UpdateAsync(book);
                _logger.LogInformation("Book with ID {BookId} updated successfully", id);

                return NoContent();
            }
            catch (TimeoutRejectedException ex)
            {
                _logger.LogWarning(ex, "External service timeout while updating book with ID {BookId}", id);
                return StatusCode(503, new { message = "External service is temporarily unavailable" });
            }
            catch (BrokenCircuitException ex)
            {
                _logger.LogWarning(ex, "Circuit breaker is open for external service");
                return StatusCode(503, new { message = "External service is currently unavailable. Please try again later." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while updating book with ID {BookId}", id);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteBook(int id)
        {
            _logger.LogInformation("Deleting book with ID {BookId}", id);

            var book = await _unitOfWork.Books.GetByIdAsync(id);
            if (book == null)
            {
                _logger.LogWarning("Book with ID {BookId} not found for deletion", id);
                return NotFound(new { message = "Book not found" });
            }

            _logger.LogInformation("Deleting book: {BookTitle} by {BookAuthor} (ID: {BookId})",
                book.Title, book.Author, book.Id);

            try
            {
                await _unitOfWork.Books.DeleteAsync(book);
                _logger.LogInformation("Book with ID {BookId} deleted successfully", id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while deleting book with ID {BookId}", id);
                return StatusCode(500, new { message = "Error deleting book" });
            }

            return NoContent();
        }

        [HttpGet("search")]
        public async Task<ActionResult<IEnumerable<BookDto>>> SearchBooks(
            [FromQuery] string? title,
            [FromQuery] string? author,
            [FromQuery] int? categoryId,
            [FromQuery] int? yearFrom,
            [FromQuery] int? yearTo,
            [FromQuery] bool? isAvailable,
            [FromQuery] string? language,
            [FromQuery] string? sortBy = "title",
            [FromQuery] string? sortOrder = "asc",
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            _logger.LogInformation(
                "Searching books with filters - Title: {Title}, Author: {Author}, CategoryId: {CategoryId}, " +
                "YearFrom: {YearFrom}, YearTo: {YearTo}, IsAvailable: {IsAvailable}, Language: {Language}, " +
                "Page: {Page}, PageSize: {PageSize}",
                title, author, categoryId, yearFrom, yearTo, isAvailable, language, page, pageSize);

            // Validate pagination parameters
            if (page < 1)
            {
                _logger.LogWarning("Invalid page number: {Page}", page);
                return BadRequest(new { message = "Page must be greater than 0" });
            }

            if (pageSize < 1 || pageSize > 100)
            {
                _logger.LogWarning("Invalid page size: {PageSize}", pageSize);
                return BadRequest(new { message = "Page size must be between 1 and 100" });
            }

            // Get search results using repository
            var books = await _unitOfWork.Books.SearchBooksAsync(
                title, author, categoryId, yearFrom, yearTo, isAvailable, language,
                sortBy!, sortOrder!, page, pageSize);

            var totalCount = await _unitOfWork.Books.GetSearchCountAsync(
                title, author, categoryId, yearFrom, yearTo, isAvailable, language);

            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            var bookDtos = books.Select(MapToDto).ToList();

            // Prepare response with pagination metadata
            var response = new
            {
                Data = bookDtos,
                Pagination = new
                {
                    Page = page,
                    PageSize = pageSize,
                    TotalCount = totalCount,
                    TotalPages = totalPages,
                    HasPreviousPage = page > 1,
                    HasNextPage = page < totalPages
                },
                Filters = new
                {
                    Title = title,
                    Author = author,
                    CategoryId = categoryId,
                    YearFrom = yearFrom,
                    YearTo = yearTo,
                    IsAvailable = isAvailable,
                    Language = language,
                    SortBy = sortBy,
                    SortOrder = sortOrder
                }
            };

            _logger.LogInformation(
                "Search completed. Found {TotalCount} books, returning {PageCount} on page {Page}. Total pages: {TotalPages}",
                totalCount, bookDtos.Count, page, totalPages);

            return Ok(response);
        }

        [HttpGet("author/{authorId}")]
        public async Task<ActionResult<IEnumerable<BookDto>>> GetBooksByAuthor(int authorId)
        {
            _logger.LogInformation("Getting books for author with ID {AuthorId}", authorId);

            try
            {
                // Verify author exists
                var authorResponse = await _resiliencePolicy.ExecuteAsync(async () => 
                {
                    return await _rabbitMQService.GetAuthorAsync(authorId);
                });
                
                if (!authorResponse.Exists)
                {
                    _logger.LogWarning("Author with ID {AuthorId} does not exist", authorId);
                    return NotFound(new { message = $"Author with ID {authorId} does not exist" });
                }

                var books = await _unitOfWork.Books.GetBooksByAuthorAsync(authorId);
                var bookDtos = books.Select(MapToDto).ToList();

                _logger.LogInformation("Retrieved {Count} books for author {AuthorId}", bookDtos.Count, authorId);
                return Ok(bookDtos);
            }
            catch (BrokenCircuitException ex)
            {
                _logger.LogError(ex, "Circuit breaker is open for external service");
                // Continue with getting books even if author service is down
                var books = await _unitOfWork.Books.GetBooksByAuthorAsync(authorId);
                var bookDtos = books.Select(MapToDto).ToList();
                return Ok(bookDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while getting books for author {AuthorId}", authorId);
                return StatusCode(500, new { message = "Error retrieving books" });
            }
        }

        [HttpGet("available")]
        public async Task<ActionResult<IEnumerable<BookDto>>> GetAvailableBooks()
        {
            _logger.LogInformation("Getting all available books");

            var books = await _unitOfWork.Books.GetAvailableBooksAsync();
            var bookDtos = books.Select(MapToDto).ToList();

            _logger.LogInformation("Retrieved {Count} available books", bookDtos.Count);
            return Ok(bookDtos);
        }

        private static BookDto MapToDto(Book book) => new()
        {
            Id = book.Id,
            Title = book.Title,
            Author = book.Author,
            AuthorId = book.AuthorId,
            Publisher = book.Publisher,
            Year = book.Year,
            ISBN = book.ISBN,
            Pages = book.Pages,
            CategoryId = book.CategoryId,
            Category = book.Category,
            Language = book.Language,
            Description = book.Description,
            PhotoPath = book.PhotoPath,
            IsAvailable = book.IsAvailable,
            AddedToLibrary = book.AddedToLibrary
        };

        private IAsyncPolicy CreateResiliencePolicy()
        {
            // Retry policy: 3 pokusy s exponenciálnym backoff-om
            var retryPolicy = Policy
                .Handle<Exception>(ex => ex is not BrokenCircuitException)
                .WaitAndRetryAsync(
                    retryCount: 3,
                    sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    onRetry: (exception, timeSpan, retryCount, context) =>
                    {
                        _logger.LogWarning(
                            "Retry {RetryCount} after {TimeSpan} due to: {ExceptionMessage}",
                            retryCount, timeSpan, exception.Message);
                    });

            // Timeout policy: 5 sekúnd timeout
            var timeoutPolicy = Policy.TimeoutAsync(
                timeout: TimeSpan.FromSeconds(5),
                timeoutStrategy: TimeoutStrategy.Pessimistic,
                onTimeoutAsync: (context, timeSpan, task) =>
                {
                    _logger.LogWarning("Timeout after {TimeSpan}", timeSpan);
                    return Task.CompletedTask;
                });

            // Circuit breaker: otvorí sa po 5 zlyhaniach za 30 sekúnd
            var circuitBreakerPolicy = Policy
                .Handle<Exception>()
                .CircuitBreakerAsync(
                    exceptionsAllowedBeforeBreaking: 5,
                    durationOfBreak: TimeSpan.FromSeconds(30),
                    onBreak: (exception, duration) =>
                    {
                        _logger.LogWarning(
                            "Circuit breaker opened for {Duration} due to: {ExceptionMessage}",
                            duration, exception.Message);
                    },
                    onReset: () =>
                    {
                        _logger.LogInformation("Circuit breaker reset");
                    },
                    onHalfOpen: () =>
                    {
                        _logger.LogInformation("Circuit breaker half-open");
                    });

            // Kombinácia všetkých policy: timeout → circuit breaker → retry
            return Policy.WrapAsync(timeoutPolicy, circuitBreakerPolicy, retryPolicy);
        }
    }
}