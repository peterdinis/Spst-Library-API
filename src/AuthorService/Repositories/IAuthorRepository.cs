using AuthorService.Entities;

namespace AuthorService.Repositories
{
    public interface IAuthorRepository : IRepository<Author>
    {
        Task<Author?> GetByIdWithDetailsAsync(int id);
        Task<IEnumerable<Author>> GetAuthorsWithBooksCountAsync();
        Task<IEnumerable<Author>> SearchAuthorsAsync(
            string? name,
            string? country,
            bool? isActive,
            DateTime? bornAfter,
            DateTime? bornBefore,
            string sortBy = "name",
            string sortOrder = "asc",
            int page = 1,
            int pageSize = 20);
        Task<int> GetSearchCountAsync(
            string? name,
            string? country,
            bool? isActive,
            DateTime? bornAfter,
            DateTime? bornBefore);
        Task<bool> AuthorExistsAsync(int id);
        Task<Author?> GetByEmailAsync(string email);
    }
}