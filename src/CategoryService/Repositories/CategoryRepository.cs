using Microsoft.EntityFrameworkCore;
using CategoryService.Data;
using CategoryService.Entities;

namespace CategoryService.Repositories
{
    public class CategoryRepository : Repository<Category>, ICategoryRepository
    {
        public CategoryRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<Category?> GetCategoryWithDetailsAsync(int id)
        {
            return await _context.Categories
                .FirstOrDefaultAsync(c => c.Id == id);
        }

        public async Task<IEnumerable<Category>> SearchCategoriesAsync(
            string? keyword,
            int page = 1,
            int pageSize = 20,
            string sortBy = "title",
            string sortOrder = "asc")
        {
            var query = BuildSearchQuery(keyword);
            query = ApplySorting(query, sortBy, sortOrder);
            
            return await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<int> GetSearchCountAsync(string? keyword)
        {
            var query = BuildSearchQuery(keyword);
            return await query.CountAsync();
        }

        public async Task<Category?> GetByTitleAsync(string title)
        {
            return await _context.Categories
                .FirstOrDefaultAsync(c => c.Title == title);
        }

        public async Task<bool> TitleExistsAsync(string title, int? excludeId = null)
        {
            var query = _context.Categories.Where(c => c.Title == title);
            
            if (excludeId.HasValue)
            {
                query = query.Where(c => c.Id != excludeId.Value);
            }
            
            return await query.AnyAsync();
        }

        public async Task<IEnumerable<Category>> GetCategoriesWithBooksCountAsync()
        {
            return await _context.Categories
                .OrderBy(c => c.Title)
                .ToListAsync();
        }

        public async Task<IEnumerable<Category>> GetPopularCategoriesAsync(int count)
        {
            // This would typically require a join with books table
            // For now, return categories ordered by title
            return await _context.Categories
                .OrderBy(c => c.Title)
                .Take(count)
                .ToListAsync();
        }

        private IQueryable<Category> BuildSearchQuery(string? keyword)
        {
            var query = _context.Categories.AsQueryable();

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var searchTerm = keyword.ToLower().Trim();
                query = query.Where(c =>
                    c.Title.ToLower().Contains(searchTerm) ||
                    (c.Description != null && c.Description.ToLower().Contains(searchTerm)));
            }

            return query;
        }

        private IQueryable<Category> ApplySorting(IQueryable<Category> query, string sortBy, string sortOrder)
        {
            return sortBy?.ToLower() switch
            {
                "title" => sortOrder?.ToLower() == "desc"
                    ? query.OrderByDescending(c => c.Title)
                    : query.OrderBy(c => c.Title),
                "id" => sortOrder?.ToLower() == "desc"
                    ? query.OrderByDescending(c => c.Id)
                    : query.OrderBy(c => c.Id),
                _ => query.OrderBy(c => c.Title)
            };
        }
    }
}