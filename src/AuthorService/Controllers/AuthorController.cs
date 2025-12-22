using Microsoft.AspNetCore.Mvc;
using FluentValidation;
using Polly.CircuitBreaker;
using AuthorService.Repositories;
using AuthorService.Dtos;
using AuthorService.Entities;
using AuthorService.Interfaces;
using AuthorService.Messages;
using AuthorService.Services;

namespace AuthorService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthorsController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<AuthorsController> _logger;
        private readonly IValidator<CreateAuthorDto> _createValidator;
        private readonly IValidator<UpdateAuthorDto> _updateValidator;
        private readonly IBookService _bookService;
        private readonly IResiliencePolicyService _policyService;

        public AuthorsController(
            IUnitOfWork unitOfWork,
            ILogger<AuthorsController> logger,
            IValidator<CreateAuthorDto> createValidator,
            IValidator<UpdateAuthorDto> updateValidator,
            IBookService bookService,
            IResiliencePolicyService policyService)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
            _createValidator = createValidator;
            _updateValidator = updateValidator;
            _bookService = bookService;
            _policyService = policyService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<AuthorDto>>> GetAuthors([FromQuery] bool includeBooks = false)
        {
            _logger.LogInformation("Getting all authors, IncludeBooks: {IncludeBooks}", includeBooks);

            var authors = await _unitOfWork.Authors.GetAllAsync();
            var authorDtos = authors.Select(MapToDto).ToList();

            if (includeBooks)
            {
                await LoadBooksForAuthorsAsync(authorDtos);
            }
            else
            {
                await LoadBooksCountForAuthorsAsync(authorDtos);
            }

            _logger.LogInformation("Retrieved {Count} authors", authorDtos.Count);
            return Ok(authorDtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<AuthorDto>> GetAuthor(int id, [FromQuery] bool includeBooks = false)
        {
            _logger.LogInformation("Getting author with ID {AuthorId}, IncludeBooks: {IncludeBooks}", id, includeBooks);

            var author = await _unitOfWork.Authors.GetByIdAsync(id);
            if (author == null)
            {
                _logger.LogWarning("Author with ID {AuthorId} not found", id);
                return NotFound(new { message = "Author not found" });
            }

            var authorDto = MapToDto(author);

            if (includeBooks)
            {
                await LoadBooksForAuthorAsync(id, authorDto);
            }
            else
            {
                await LoadBooksCountForAuthorAsync(id, authorDto);
            }

            _logger.LogInformation("Author with ID {AuthorId} found: {AuthorName}", id, author.FullName);
            return Ok(authorDto);
        }

        [HttpPost]
        public async Task<ActionResult<AuthorDto>> CreateAuthor(CreateAuthorDto dto)
        {
            var validationResult = await _createValidator.ValidateAsync(dto);
            if (!validationResult.IsValid)
            {
                _logger.LogWarning("Validation failed for creating author: {Errors}",
                    string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage)));
                return BadRequest(new { errors = validationResult.Errors.Select(e => e.ErrorMessage) });
            }

            _logger.LogInformation("Creating new author: {FirstName} {LastName}", dto.FirstName, dto.LastName);

            // Check for duplicate email
            var existingAuthor = await _unitOfWork.Authors.GetByEmailAsync(dto.Email.Trim());
            if (existingAuthor != null)
            {
                _logger.LogWarning("Author with email {Email} already exists", dto.Email);
                return Conflict(new { message = "An author with this email already exists" });
            }

            var author = new Author
            {
                FirstName = dto.FirstName.Trim(),
                LastName = dto.LastName.Trim(),
                Email = dto.Email.Trim(),
                DateOfBirth = dto.DateOfBirth,
                DateOfDeath = dto.DateOfDeath,
                Country = dto.Country.Trim(),
                IsActive = true,
                CreatedDate = DateTime.UtcNow,
                LastModified = DateTime.UtcNow
            };

            try
            {
                await _unitOfWork.Authors.AddAsync(author);
                await _unitOfWork.CompleteAsync();

                _logger.LogInformation("Author created successfully with ID {AuthorId}: {FirstName} {LastName}",
                    author.AuthorId, author.FirstName, author.LastName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while creating author: {FirstName} {LastName}",
                    dto.FirstName, dto.LastName);
                return StatusCode(500, new { message = "Error creating author", details = ex.Message });
            }

            return CreatedAtAction(nameof(GetAuthor), new { id = author.AuthorId }, MapToDto(author));
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateAuthor(int id, UpdateAuthorDto dto)
        {
            var validationResult = await _updateValidator.ValidateAsync(dto);
            if (!validationResult.IsValid)
            {
                _logger.LogWarning("Validation failed for updating author with ID {AuthorId}: {Errors}",
                    id, string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage)));
                return BadRequest(new { errors = validationResult.Errors.Select(e => e.ErrorMessage) });
            }

            if (id != dto.AuthorId)
            {
                _logger.LogWarning("ID mismatch in URL ({UrlId}) and body ({BodyId})", id, dto.AuthorId);
                return BadRequest(new { message = "ID in URL does not match ID in body" });
            }

            _logger.LogInformation("Updating author with ID {AuthorId}", id);

            var author = await _unitOfWork.Authors.GetByIdAsync(id);
            if (author == null)
            {
                _logger.LogWarning("Author with ID {AuthorId} not found for update", id);
                return NotFound(new { message = "Author not found" });
            }

            // Check for duplicate email (if email is being changed)
            if (author.Email != dto.Email.Trim())
            {
                var existingAuthor = await _unitOfWork.Authors.GetByEmailAsync(dto.Email.Trim());
                if (existingAuthor != null && existingAuthor.AuthorId != id)
                {
                    _logger.LogWarning("Another author with email {Email} already exists", dto.Email);
                    return Conflict(new { message = "Another author with this email already exists" });
                }
            }

            _logger.LogInformation("Updating author from: {OldFirstName} {OldLastName} to: {NewFirstName} {NewLastName}",
                author.FirstName, author.LastName, dto.FirstName, dto.LastName);

            // Update properties
            author.FirstName = dto.FirstName.Trim();
            author.LastName = dto.LastName.Trim();
            author.Biography = dto.Biography?.Trim()!;
            author.Email = dto.Email.Trim();
            author.DateOfBirth = dto.DateOfBirth;
            author.DateOfDeath = dto.DateOfDeath;
            author.Country = dto.Country.Trim();
            author.Website = dto.Website?.Trim()!;
            author.IsActive = dto.IsActive;
            author.LastModified = DateTime.UtcNow;

            try
            {
                await _unitOfWork.Authors.UpdateAsync(author);
                _logger.LogInformation("Author with ID {AuthorId} updated successfully", id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while updating author with ID {AuthorId}", id);
                return StatusCode(500, new { message = "Error updating author", details = ex.Message });
            }

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteAuthor(int id)
        {
            _logger.LogInformation("Deleting author with ID {AuthorId}", id);

            var author = await _unitOfWork.Authors.GetByIdAsync(id);
            if (author == null)
            {
                _logger.LogWarning("Author with ID {AuthorId} not found for deletion", id);
                return NotFound(new { message = "Author not found" });
            }

            // Check if author has books before deletion
            try
            {
                var policy = _policyService.GetPolicy<int>();
                var booksCount = await policy.ExecuteAsync(async () =>
                    await _bookService.GetBooksCountByAuthorAsync(id));

                if (booksCount > 0)
                {
                    _logger.LogWarning("Cannot delete author {AuthorId} with {BooksCount} associated books", id, booksCount);
                    return BadRequest(new { message = $"Cannot delete author with {booksCount} associated books" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not verify books count for author {AuthorId}, proceeding with deletion", id);
                // Continue with deletion if we can't verify book count
            }

            _logger.LogInformation("Deleting author: {FirstName} {LastName} (ID: {AuthorId})",
                author.FirstName, author.LastName, author.AuthorId);

            try
            {
                await _unitOfWork.Authors.DeleteAsync(author);
                _logger.LogInformation("Author with ID {AuthorId} deleted successfully", id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while deleting author with ID {AuthorId}", id);
                return StatusCode(500, new { message = "Error deleting author", details = ex.Message });
            }

            return NoContent();
        }

        [HttpGet("search")]
        public async Task<ActionResult<IEnumerable<AuthorDto>>> SearchAuthors(
            [FromQuery] string? name,
            [FromQuery] string? country,
            [FromQuery] bool? isActive,
            [FromQuery] DateTime? bornAfter,
            [FromQuery] DateTime? bornBefore,
            [FromQuery] string? sortBy = "name",
            [FromQuery] string? sortOrder = "asc",
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] bool includeBooks = false)
        {
            _logger.LogInformation(
                "Searching authors with filters - Name: {Name}, Country: {Country}, IsActive: {IsActive}, " +
                "BornAfter: {BornAfter}, BornBefore: {BornBefore}, Page: {Page}, PageSize: {PageSize}, IncludeBooks: {IncludeBooks}",
                name, country, isActive, bornAfter, bornBefore, page, pageSize, includeBooks);

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
            var authors = await _unitOfWork.Authors.SearchAuthorsAsync(
                name, country, isActive, bornAfter, bornBefore,
                sortBy!, sortOrder!, page, pageSize);

            var totalCount = await _unitOfWork.Authors.GetSearchCountAsync(
                name, country, isActive, bornAfter, bornBefore);

            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            var authorDtos = authors.Select(MapToDto).ToList();

            // Get books information if requested
            if (includeBooks && authorDtos.Count != 0)
            {
                await LoadBooksForAuthorsAsync(authorDtos);
            }
            else if (!includeBooks && authorDtos.Count != 0)
            {
                await LoadBooksCountForAuthorsAsync(authorDtos);
            }

            // Prepare response with pagination metadata
            var response = new
            {
                Data = authorDtos,
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
                    Name = name,
                    Country = country,
                    IsActive = isActive,
                    BornAfter = bornAfter,
                    BornBefore = bornBefore,
                    SortBy = sortBy,
                    SortOrder = sortOrder,
                    IncludeBooks = includeBooks
                }
            };

            _logger.LogInformation(
                "Search completed. Found {TotalCount} authors, returning {PageCount} on page {Page}. Total pages: {TotalPages}",
                totalCount, authorDtos.Count, page, totalPages);

            return Ok(response);
        }

        [HttpGet("{id}/books")]
        public async Task<ActionResult<List<AuthorBookDto>>> GetAuthorBooks(int id)
        {
            _logger.LogInformation("Getting books for author with ID {AuthorId}", id);

            var authorExists = await _unitOfWork.Authors.ExistsAsync(id);
            if (!authorExists)
            {
                _logger.LogWarning("Author with ID {AuthorId} not found", id);
                return NotFound(new { message = "Author not found" });
            }

            try
            {
                var policy = _policyService.GetPolicy<List<AuthorBookDto>>();
                var books = await policy.ExecuteAsync(async () =>
                    await _bookService.GetBooksByAuthorAsync(id));

                _logger.LogInformation("Retrieved {BooksCount} books for author {AuthorId}",
                    books?.Count ?? 0, id);

                return Ok(books ?? new List<AuthorBookDto>());
            }
            catch (BrokenCircuitException ex)
            {
                _logger.LogError(ex, "Circuit is open for book service for author {AuthorId}", id);
                return StatusCode(503, new
                {
                    message = "Book service is temporarily unavailable due to high failure rate",
                    details = "Circuit breaker is open"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while fetching books for author {AuthorId}", id);
                return StatusCode(500, new { message = "Error retrieving books information" });
            }
        }

        private static AuthorDto MapToDto(Author author) => new AuthorDto
        {
            AuthorId = author.AuthorId,
            FirstName = author.FirstName,
            LastName = author.LastName,
            Biography = author.Biography,
            Email = author.Email,
            DateOfBirth = author.DateOfBirth,
            DateOfDeath = author.DateOfDeath,
            Country = author.Country,
            Website = author.Website,
            IsActive = author.IsActive,
            CreatedDate = author.CreatedDate,
            LastModified = author.LastModified,
            Age = author.Age,
            Status = author.Status
        };

        private async Task LoadBooksForAuthorsAsync(List<AuthorDto> authors)
        {
            foreach (var author in authors)
            {
                try
                {
                    var policy = _policyService.GetPolicy<List<AuthorBookDto>>();
                    var books = await policy.ExecuteAsync(async () =>
                        await _bookService.GetBooksByAuthorAsync(author.AuthorId));

                    author.Books = books ?? new List<AuthorBookDto>();
                    author.BooksCount = books?.Count ?? 0;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to get books for author {AuthorId}", author.AuthorId);
                    author.Books = new List<AuthorBookDto>();
                    author.BooksCount = 0;
                }
            }
        }

        private async Task LoadBooksCountForAuthorsAsync(List<AuthorDto> authors)
        {
            foreach (var author in authors)
            {
                try
                {
                    var policy = _policyService.GetPolicy<int>();
                    author.BooksCount = await policy.ExecuteAsync(async () =>
                        await _bookService.GetBooksCountByAuthorAsync(author.AuthorId));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to get books count for author {AuthorId}", author.AuthorId);
                    author.BooksCount = 0;
                }
            }
        }

        private async Task LoadBooksForAuthorAsync(int id, AuthorDto authorDto)
        {
            try
            {
                var policy = _policyService.GetPolicy<List<AuthorBookDto>>();
                var books = await policy.ExecuteAsync(async () =>
                    await _bookService.GetBooksByAuthorAsync(id));

                authorDto.Books = books ?? new List<AuthorBookDto>();
                authorDto.BooksCount = books?.Count ?? 0;
                _logger.LogInformation("Retrieved {BooksCount} books for author {AuthorId}", authorDto.BooksCount, id);
            }
            catch (BrokenCircuitException ex)
            {
                _logger.LogError(ex, "Circuit is open for book service for author {AuthorId}", id);
                // Note: In the GetAuthor method, this exception is handled differently
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while fetching books for author {AuthorId}", id);
                authorDto.Books = new List<AuthorBookDto>();
                authorDto.BooksCount = 0;
            }
        }

        private async Task LoadBooksCountForAuthorAsync(int id, AuthorDto authorDto)
        {
            try
            {
                var policy = _policyService.GetPolicy<int>();
                authorDto.BooksCount = await policy.ExecuteAsync(async () =>
                    await _bookService.GetBooksCountByAuthorAsync(id));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get books count for author {AuthorId}", id);
                authorDto.BooksCount = 0;
            }
        }
    }
}