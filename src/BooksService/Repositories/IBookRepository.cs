using BooksService.Entities;

namespace BooksService.Repositories
{
    public interface IBookRepository : IRepository<Book>
    {
        Task<Book?> GetBookWithDetailsAsync(int id);
        Task<IEnumerable<Book>> SearchBooksAsync(
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
            int pageSize = 20);
        Task<int> GetSearchCountAsync(
            string? title,
            string? author,
            int? categoryId,
            int? yearFrom,
            int? yearTo,
            bool? isAvailable,
            string? language);
        Task<bool> IsbnExistsAsync(string isbn, int? excludeId = null);
        Task<IEnumerable<Book>> GetBooksByAuthorAsync(int authorId);
        Task<int> GetBooksCountByAuthorAsync(int authorId);
        Task<IEnumerable<Book>> GetBooksByCategoryAsync(int categoryId);
        Task<IEnumerable<Book>> GetAvailableBooksAsync();
        Task<IEnumerable<Book>> GetBooksAddedAfterDateAsync(DateTime date);
    }
}