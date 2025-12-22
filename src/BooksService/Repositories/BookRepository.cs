using Microsoft.EntityFrameworkCore;
using BooksService.Data;
using BooksService.Entities;

namespace BooksService.Repositories
{
    public class BookRepository(ApplicationDbContext context) : Repository<Book>(context), IBookRepository
    {
        public async Task<Book?> GetBookWithDetailsAsync(int id)
        {
            return await _context.Books
                .FirstOrDefaultAsync(b => b.Id == id);
        }

        public async Task<IEnumerable<Book>> SearchBooksAsync(
            string? title,
            string? author,
            int? categoryId,
            int? yearFrom,
            int? yearTo,
            bool? isAvailable,
            string? language,
            string sortBy = "title",
            string sortOrder = "asc",
            int page = 1,
            int pageSize = 20)
        {
            var query = BuildSearchQuery(title, author, categoryId, yearFrom, yearTo, isAvailable, language);
            
            query = ApplySorting(query, sortBy, sortOrder);
            
            return await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<int> GetSearchCountAsync(
            string? title,
            string? author,
            int? categoryId,
            int? yearFrom,
            int? yearTo,
            bool? isAvailable,
            string? language)
        {
            var query = BuildSearchQuery(title, author, categoryId, yearFrom, yearTo, isAvailable, language);
            return await query.CountAsync();
        }

        public async Task<bool> IsbnExistsAsync(string isbn, int? excludeId = null)
        {
            var query = _context.Books.Where(b => b.ISBN == isbn);
            
            if (excludeId.HasValue)
            {
                query = query.Where(b => b.Id != excludeId.Value);
            }
            
            return await query.AnyAsync();
        }

        public async Task<IEnumerable<Book>> GetBooksByAuthorAsync(int authorId)
        {
            return await _context.Books
                .Where(b => b.AuthorId == authorId)
                .OrderBy(b => b.Title)
                .ToListAsync();
        }

        public async Task<int> GetBooksCountByAuthorAsync(int authorId)
        {
            return await _context.Books
                .Where(b => b.AuthorId == authorId)
                .CountAsync();
        }

        public async Task<IEnumerable<Book>> GetBooksByCategoryAsync(int categoryId)
        {
            return await _context.Books
                .Where(b => b.CategoryId == categoryId)
                .OrderBy(b => b.Title)
                .ToListAsync();
        }

        public async Task<IEnumerable<Book>> GetAvailableBooksAsync()
        {
            return await _context.Books
                .Where(b => b.IsAvailable)
                .OrderBy(b => b.Title)
                .ToListAsync();
        }

        public async Task<IEnumerable<Book>> GetBooksAddedAfterDateAsync(DateTime date)
        {
            return await _context.Books
                .Where(b => b.AddedToLibrary > date)
                .OrderByDescending(b => b.AddedToLibrary)
                .ToListAsync();
        }

        private IQueryable<Book> BuildSearchQuery(
            string? title,
            string? author,
            int? categoryId,
            int? yearFrom,
            int? yearTo,
            bool? isAvailable,
            string? language)
        {
            var query = _context.Books.AsQueryable();

            if (!string.IsNullOrWhiteSpace(title))
            {
                var titleTerm = title.Trim().ToLower();
                query = query.Where(b => b.Title.ToLower().Contains(titleTerm));
            }

            if (!string.IsNullOrWhiteSpace(author))
            {
                var authorTerm = author.Trim().ToLower();
                query = query.Where(b => b.Author.ToLower().Contains(authorTerm));
            }

            if (categoryId.HasValue)
            {
                query = query.Where(b => b.CategoryId == categoryId.Value);
            }

            if (yearFrom.HasValue)
            {
                query = query.Where(b => b.Year >= yearFrom.Value);
            }

            if (yearTo.HasValue)
            {
                query = query.Where(b => b.Year <= yearTo.Value);
            }

            if (isAvailable.HasValue)
            {
                query = query.Where(b => b.IsAvailable == isAvailable.Value);
            }

            if (!string.IsNullOrWhiteSpace(language))
            {
                var languageTerm = language.Trim().ToLower();
                query = query.Where(b => b.Language.ToLower() == languageTerm);
            }

            return query;
        }

        private IQueryable<Book> ApplySorting(IQueryable<Book> query, string sortBy, string sortOrder)
        {
            return sortBy?.ToLower() switch
            {
                "title" => sortOrder?.ToLower() == "desc"
                    ? query.OrderByDescending(b => b.Title)
                    : query.OrderBy(b => b.Title),
                "author" => sortOrder?.ToLower() == "desc"
                    ? query.OrderByDescending(b => b.Author)
                    : query.OrderBy(b => b.Author),
                "year" => sortOrder?.ToLower() == "desc"
                    ? query.OrderByDescending(b => b.Year)
                    : query.OrderBy(b => b.Year),
                "added" => sortOrder?.ToLower() == "desc"
                    ? query.OrderByDescending(b => b.AddedToLibrary)
                    : query.OrderBy(b => b.AddedToLibrary),
                "pages" => sortOrder?.ToLower() == "desc"
                    ? query.OrderByDescending(b => b.Pages)
                    : query.OrderBy(b => b.Pages),
                _ => query.OrderBy(b => b.Title)
            };
        }
    }
}