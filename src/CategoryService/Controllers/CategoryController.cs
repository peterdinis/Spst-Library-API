using Microsoft.AspNetCore.Mvc;
using FluentValidation;
using Polly.CircuitBreaker;
using CategoryService.Dtos;
using CategoryService.Entities;
using CategoryService.Interfaces;
using Polly;
using CategoryService.Repositories;
using Polly.Timeout;

namespace CategoryService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CategoriesController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<CategoriesController> _logger;
        private readonly IBookService _bookService;
        private readonly IAsyncPolicy _resiliencePolicy;
        private readonly IValidator<CreateCategoryDto> _createValidator;
        private readonly IValidator<UpdateCategoryDto> _updateValidator;

        public CategoriesController(
            IUnitOfWork unitOfWork,
            ILogger<CategoriesController> logger,
            IBookService bookService,
            IValidator<CreateCategoryDto> createValidator,
            IValidator<UpdateCategoryDto> updateValidator)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
            _bookService = bookService;
            _createValidator = createValidator;
            _updateValidator = updateValidator;
            _resiliencePolicy = CreateResiliencePolicy();
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<CategoryDto>>> GetCategories([FromQuery] bool includeBooks = false)
        {
            _logger.LogInformation("Getting all categories, IncludeBooks: {IncludeBooks}", includeBooks);

            var categories = await _unitOfWork.Categories.GetAllAsync();
            var categoryDtos = categories.Select(MapToDto).ToList();

            if (includeBooks)
            {
                await LoadBooksForCategoriesAsync(categoryDtos);
            }
            else
            {
                await LoadBooksCountForCategoriesAsync(categoryDtos);
            }

            _logger.LogInformation("Retrieved {Count} categories", categoryDtos.Count);
            return Ok(categoryDtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<CategoryDto>> GetCategory(int id, [FromQuery] bool includeBooks = false)
        {
            _logger.LogInformation("Getting category with ID {CategoryId}, IncludeBooks: {IncludeBooks}", id, includeBooks);

            var category = await _unitOfWork.Categories.GetByIdAsync(id);
            if (category == null)
            {
                _logger.LogWarning("Category with ID {CategoryId} not found", id);
                return NotFound(new { message = "Category not found" });
            }

            var categoryDto = MapToDto(category);

            if (includeBooks)
            {
                await LoadBooksForCategoryAsync(id, categoryDto);
            }
            else
            {
                await LoadBooksCountForCategoryAsync(id, categoryDto);
            }

            _logger.LogInformation("Category with ID {CategoryId} found: {CategoryTitle}", id, category.Title);
            return Ok(categoryDto);
        }

        [HttpPost]
        public async Task<ActionResult<CategoryDto>> CreateCategory(CreateCategoryDto dto)
        {
            // Validate input
            var validationResult = await _createValidator.ValidateAsync(dto);
            if (!validationResult.IsValid)
            {
                _logger.LogWarning("Validation failed for creating category: {Errors}",
                    string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage)));
                return BadRequest(new { errors = validationResult.Errors.Select(e => e.ErrorMessage) });
            }

            _logger.LogInformation("Creating new category with title: {CategoryTitle}", dto.Title);

            // Check for duplicate title
            var existingCategory = await _unitOfWork.Categories.GetByTitleAsync(dto.Title);
            if (existingCategory != null)
            {
                _logger.LogWarning("Category with title '{Title}' already exists", dto.Title);
                return Conflict(new { message = $"Category with title '{dto.Title}' already exists" });
            }

            var category = new Category
            {
                Title = dto.Title,
                Description = dto.Description
            };

            try
            {
                await _unitOfWork.Categories.AddAsync(category);
                await _unitOfWork.CompleteAsync();

                _logger.LogInformation("Category created successfully with ID {CategoryId}", category.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while creating category with title: {CategoryTitle}", dto.Title);
                return StatusCode(500, new { message = "Error creating category", details = ex.Message });
            }

            return CreatedAtAction(nameof(GetCategory), new { id = category.Id }, MapToDto(category));
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateCategory(int id, UpdateCategoryDto dto)
        {
            // Validate input
            var validationResult = await _updateValidator.ValidateAsync(dto);
            if (!validationResult.IsValid)
            {
                _logger.LogWarning("Validation failed for updating category with ID {CategoryId}: {Errors}",
                    id, string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage)));
                return BadRequest(new { errors = validationResult.Errors.Select(e => e.ErrorMessage) });
            }

            _logger.LogInformation("Updating category with ID {CategoryId}", id);

            var category = await _unitOfWork.Categories.GetByIdAsync(id);
            if (category == null)
            {
                _logger.LogWarning("Category with ID {CategoryId} not found for update", id);
                return NotFound(new { message = "Category not found" });
            }

            // Check for duplicate title (if title is being changed)
            if (category.Title != dto.Title)
            {
                var titleExists = await _unitOfWork.Categories.TitleExistsAsync(dto.Title, id);
                if (titleExists)
                {
                    _logger.LogWarning("Category with title '{Title}' already exists", dto.Title);
                    return Conflict(new { message = $"Category with title '{dto.Title}' already exists" });
                }
            }

            _logger.LogInformation("Updating category from: {OldTitle} to: {NewTitle}", category.Title, dto.Title);

            category.Title = dto.Title;
            category.Description = dto.Description;

            try
            {
                await _unitOfWork.Categories.UpdateAsync(category);
                _logger.LogInformation("Category with ID {CategoryId} updated successfully", id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while updating category with ID {CategoryId}", id);
                return StatusCode(500, new { message = "Error updating category", details = ex.Message });
            }

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            _logger.LogInformation("Deleting category with ID {CategoryId}", id);

            var category = await _unitOfWork.Categories.GetByIdAsync(id);
            if (category == null)
            {
                _logger.LogWarning("Category with ID {CategoryId} not found for deletion", id);
                return NotFound(new { message = "Category not found" });
            }

            // Check if category has books before deletion
            try
            {
                var booksCount = await _resiliencePolicy.ExecuteAsync(async () =>
                    await _bookService.GetBooksCountByCategoryAsync(id));

                if (booksCount > 0)
                {
                    _logger.LogWarning("Cannot delete category {CategoryId} with {BooksCount} associated books", id, booksCount);
                    return BadRequest(new { message = $"Cannot delete category with {booksCount} associated books" });
                }
            }
            catch (TimeoutRejectedException ex)
            {
                _logger.LogWarning(ex, "Timeout while verifying books count for category {CategoryId}", id);
                return StatusCode(503, new { message = "Cannot verify book service availability. Deletion postponed." });
            }
            catch (BrokenCircuitException ex)
            {
                _logger.LogWarning(ex, "Circuit breaker open while verifying books count for category {CategoryId}", id);
                return StatusCode(503, new { message = "Book service is unavailable. Please try again later." });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not verify books count for category {CategoryId}, proceeding with deletion", id);
                // Continue with deletion if we can't verify book count, but log as warning
            }

            _logger.LogInformation("Deleting category: {CategoryTitle} (ID: {CategoryId})", category.Title, category.Id);

            try
            {
                await _unitOfWork.Categories.DeleteAsync(category);
                _logger.LogInformation("Category with ID {CategoryId} deleted successfully", id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while deleting category with ID {CategoryId}", id);
                return StatusCode(500, new { message = "Error deleting category", details = ex.Message });
            }

            return NoContent();
        }

        [HttpGet("search")]
        public async Task<ActionResult<IEnumerable<CategoryDto>>> SearchCategories(
            [FromQuery] string? keyword,
            [FromQuery] bool includeBooks = false,
            [FromQuery] string? sortBy = "title",
            [FromQuery] string? sortOrder = "asc",
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            _logger.LogInformation(
                "Searching categories with keyword: {Keyword}, IncludeBooks: {IncludeBooks}, " +
                "SortBy: {SortBy}, SortOrder: {SortOrder}, Page: {Page}, PageSize: {PageSize}",
                keyword, includeBooks, sortBy, sortOrder, page, pageSize);

            // Validate pagination parameters
            if (page < 1)
            {
                _logger.LogWarning("Invalid page number: {Page}, defaulting to 1", page);
                page = 1;
            }

            if (pageSize < 1 || pageSize > 100)
            {
                _logger.LogWarning("Invalid page size: {PageSize}, defaulting to 20", pageSize);
                pageSize = 20;
            }

            // Get search results using repository
            var categories = await _unitOfWork.Categories.SearchCategoriesAsync(
                keyword, page, pageSize, sortBy!, sortOrder!);

            var totalCount = await _unitOfWork.Categories.GetSearchCountAsync(keyword);
            var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
            
            var categoryDtos = categories.Select(MapToDto).ToList();

            // Load books information if requested
            if (includeBooks)
            {
                await LoadBooksForCategoriesAsync(categoryDtos);
            }
            else
            {
                await LoadBooksCountForCategoriesAsync(categoryDtos);
            }

            // Prepare response with pagination metadata
            var response = new
            {
                Data = categoryDtos,
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
                    Keyword = keyword,
                    IncludeBooks = includeBooks,
                    SortBy = sortBy,
                    SortOrder = sortOrder
                }
            };

            _logger.LogInformation(
                "Search completed. Found {TotalCount} categories, returning {PageCount} on page {Page}. Total pages: {TotalPages}",
                totalCount, categoryDtos.Count, page, totalPages);

            return Ok(response);
        }

        [HttpGet("popular/{count}")]
        public async Task<ActionResult<IEnumerable<CategoryDto>>> GetPopularCategories(int count = 5)
        {
            _logger.LogInformation("Getting top {Count} popular categories", count);

            if (count < 1 || count > 20)
            {
                _logger.LogWarning("Invalid count: {Count}, must be between 1 and 20", count);
                return BadRequest(new { message = "Count must be between 1 and 20" });
            }

            var categories = await _unitOfWork.Categories.GetPopularCategoriesAsync(count);
            var categoryDtos = categories.Select(MapToDto).ToList();

            // Load books count for each category
            await LoadBooksCountForCategoriesAsync(categoryDtos);

            // Sort by books count descending
            categoryDtos = categoryDtos.OrderByDescending(c => c.BooksCount).ToList();

            _logger.LogInformation("Retrieved {Count} popular categories", categoryDtos.Count);
            return Ok(categoryDtos);
        }

        [HttpGet("exists/{title}")]
        public async Task<ActionResult<bool>> CategoryExists(string title)
        {
            _logger.LogInformation("Checking if category with title '{Title}' exists", title);

            if (string.IsNullOrWhiteSpace(title))
            {
                return BadRequest(new { message = "Title is required" });
            }

            var category = await _unitOfWork.Categories.GetByTitleAsync(title);
            var exists = category != null;

            _logger.LogInformation("Category with title '{Title}' exists: {Exists}", title, exists);
            return Ok(exists);
        }

        private static CategoryDto MapToDto(Category category) => new CategoryDto
        {
            Id = category.Id,
            Title = category.Title,
            Description = category.Description
        };

        private async Task LoadBooksForCategoriesAsync(List<CategoryDto> categories)
        {
            foreach (var category in categories)
            {
                try
                {
                    var books = await _resiliencePolicy.ExecuteAsync(async () =>
                        await _bookService.GetBooksByCategoryAsync(category.Id));

                    // Konvertovať z Messages.BookDto na Dtos.BookDto
                    category.Books = books?.Select(b => new BookDto
                    {
                        Id = b.Id,
                        Title = b.Title,
                        Author = b.Author,
                        Year = b.Year,
                        IsAvailable = b.IsAvailable
                    }).ToList() ?? new List<BookDto>();
                    
                    category.BooksCount = category.Books.Count;
                }
                catch (TimeoutRejectedException ex)
                {
                    _logger.LogWarning(ex, "Timeout while fetching books for category {CategoryId}", category.Id);
                    category.Books = new List<BookDto>();
                    category.BooksCount = 0;
                }
                catch (BrokenCircuitException ex)
                {
                    _logger.LogWarning(ex, "Circuit breaker open while fetching books for category {CategoryId}", category.Id);
                    category.Books = new List<BookDto>();
                    category.BooksCount = 0;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while fetching books for category {CategoryId}", category.Id);
                    category.Books = new List<BookDto>();
                    category.BooksCount = 0;
                }
            }
        }

        private async Task LoadBooksCountForCategoriesAsync(List<CategoryDto> categories)
        {
            foreach (var category in categories)
            {
                try
                {
                    category.BooksCount = await _resiliencePolicy.ExecuteAsync(async () =>
                        await _bookService.GetBooksCountByCategoryAsync(category.Id));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to get books count for category {CategoryId}", category.Id);
                    category.BooksCount = 0;
                }
            }
        }

        private async Task LoadBooksForCategoryAsync(int id, CategoryDto categoryDto)
        {
            try
            {
                var books = await _resiliencePolicy.ExecuteAsync(async () =>
                    await _bookService.GetBooksByCategoryAsync(id));
                
                // Konvertovať z Messages.BookDto na Dtos.BookDto
                categoryDto.Books = books?.Select(b => new BookDto
                {
                    Id = b.Id,
                    Title = b.Title,
                    Author = b.Author,
                    Year = b.Year,
                    IsAvailable = b.IsAvailable
                }).ToList() ?? new List<BookDto>();
                
                categoryDto.BooksCount = categoryDto.Books.Count;
                
                _logger.LogInformation("Retrieved {BooksCount} books for category {CategoryId}", categoryDto.BooksCount, id);
            }
            catch (TimeoutRejectedException ex)
            {
                _logger.LogWarning(ex, "Book service timeout while fetching books for category {CategoryId}", id);
                categoryDto.Books = new List<BookDto>();
                categoryDto.BooksCount = 0;
                throw;
            }
            catch (BrokenCircuitException ex)
            {
                _logger.LogWarning(ex, "Circuit breaker open for book service while fetching books for category {CategoryId}", id);
                categoryDto.Books = new List<BookDto>();
                categoryDto.BooksCount = 0;
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while fetching books for category {CategoryId}", id);
                categoryDto.Books = new List<BookDto>();
                categoryDto.BooksCount = 0;
            }
        }

        private async Task LoadBooksCountForCategoryAsync(int id, CategoryDto categoryDto)
        {
            try
            {
                categoryDto.BooksCount = await _resiliencePolicy.ExecuteAsync(async () =>
                    await _bookService.GetBooksCountByCategoryAsync(id));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get books count for category {CategoryId}", id);
                categoryDto.BooksCount = 0;
            }
        }

        private IAsyncPolicy CreateResiliencePolicy()
        {
            // Retry policy: 2 pokusy s exponenciálnym backoff-om
            var retryPolicy = Policy
                .Handle<Exception>(ex => ex is not BrokenCircuitException)
                .WaitAndRetryAsync(
                    retryCount: 2,
                    sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(1.5, retryAttempt)),
                    onRetry: (exception, timeSpan, retryCount, context) =>
                    {
                        _logger.LogWarning(
                            "Retry {RetryCount} after {TimeSpan} due to: {ExceptionMessage}",
                            retryCount, timeSpan, exception.Message);
                    });

            // Timeout policy: 3 sekundy timeout
            var timeoutPolicy = Policy.TimeoutAsync(
                timeout: TimeSpan.FromSeconds(3),
                timeoutStrategy: TimeoutStrategy.Pessimistic,
                onTimeoutAsync: (context, timeSpan, task) =>
                {
                    _logger.LogWarning("Timeout after {TimeSpan} for book service operation", timeSpan);
                    return Task.CompletedTask;
                });

            // Circuit breaker: otvorí sa po 3 zlyhaniach za 20 sekúnd
            var circuitBreakerPolicy = Policy
                .Handle<Exception>()
                .CircuitBreakerAsync(
                    exceptionsAllowedBeforeBreaking: 3,
                    durationOfBreak: TimeSpan.FromSeconds(20),
                    onBreak: (exception, duration) =>
                    {
                        _logger.LogWarning(
                            "Circuit breaker opened for book service for {Duration} due to: {ExceptionType}",
                            duration, exception.GetType().Name);
                    },
                    onReset: () =>
                    {
                        _logger.LogInformation("Circuit breaker for book service reset");
                    },
                    onHalfOpen: () =>
                    {
                        _logger.LogInformation("Circuit breaker for book service half-open");
                    });

            // Kombinácia politík: timeout → circuit breaker → retry
            return Policy.WrapAsync(timeoutPolicy, circuitBreakerPolicy, retryPolicy);
        }
    }
}