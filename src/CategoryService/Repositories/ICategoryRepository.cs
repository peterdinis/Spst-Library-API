using CategoryService.Entities;

namespace CategoryService.Repositories
{
    public interface ICategoryRepository : IRepository<Category>
    {
        Task<Category?> GetCategoryWithDetailsAsync(int id);
        Task<IEnumerable<Category>> SearchCategoriesAsync(
            string? keyword,
            int page = 1,
            int pageSize = 20,
            string sortBy = "title",
            string sortOrder = "asc");
        Task<int> GetSearchCountAsync(string? keyword);
        Task<Category?> GetByTitleAsync(string title);
        Task<bool> TitleExistsAsync(string title, int? excludeId = null);
        Task<IEnumerable<Category>> GetCategoriesWithBooksCountAsync();
        Task<IEnumerable<Category>> GetPopularCategoriesAsync(int count);
    }
}