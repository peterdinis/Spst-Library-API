using Microsoft.EntityFrameworkCore;
using AuthorService.Entities;
using AuthorService.Data;

namespace AuthorService.Repositories
{
    public class AuthorRepository : Repository<Author>, IAuthorRepository
    {
        public AuthorRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<Author?> GetByIdWithDetailsAsync(int id)
        {
            return await _context.Authors
                .FirstOrDefaultAsync(a => a.AuthorId == id);
        }

        public async Task<IEnumerable<Author>> GetAuthorsWithBooksCountAsync()
        {
            return await _context.Authors
                .OrderBy(a => a.LastName)
                .ThenBy(a => a.FirstName)
                .ToListAsync();
        }

        public async Task<IEnumerable<Author>> SearchAuthorsAsync(
            string? name,
            string? country,
            bool? isActive,
            DateTime? bornAfter,
            DateTime? bornBefore,
            string sortBy = "name",
            string sortOrder = "asc",
            int page = 1,
            int pageSize = 20)
        {
            var query = BuildSearchQuery(name, country, isActive, bornAfter, bornBefore);
            
            query = ApplySorting(query, sortBy, sortOrder);
            
            return await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<int> GetSearchCountAsync(
            string? name,
            string? country,
            bool? isActive,
            DateTime? bornAfter,
            DateTime? bornBefore)
        {
            var query = BuildSearchQuery(name, country, isActive, bornAfter, bornBefore);
            return await query.CountAsync();
        }

        public async Task<bool> AuthorExistsAsync(int id)
        {
            return await _context.Authors.AnyAsync(a => a.AuthorId == id);
        }

        public async Task<Author?> GetByEmailAsync(string email)
        {
            return await _context.Authors
                .FirstOrDefaultAsync(a => a.Email == email);
        }

        private IQueryable<Author> BuildSearchQuery(
            string? name,
            string? country,
            bool? isActive,
            DateTime? bornAfter,
            DateTime? bornBefore)
        {
            var query = _context.Authors.AsQueryable();

            if (!string.IsNullOrWhiteSpace(name))
            {
                var searchTerm = name.Trim().ToLower();
                query = query.Where(a =>
                    a.FirstName.ToLower().Contains(searchTerm) ||
                    a.LastName.ToLower().Contains(searchTerm) ||
                    (a.FirstName + " " + a.LastName).ToLower().Contains(searchTerm) ||
                    (a.Biography != null && a.Biography.ToLower().Contains(searchTerm)));
            }

            if (!string.IsNullOrWhiteSpace(country))
            {
                var countryTerm = country.Trim().ToLower();
                query = query.Where(a => a.Country.ToLower().Contains(countryTerm));
            }

            if (isActive.HasValue)
            {
                query = query.Where(a => a.IsActive == isActive.Value);
            }

            if (bornAfter.HasValue)
            {
                query = query.Where(a => a.DateOfBirth >= bornAfter.Value);
            }

            if (bornBefore.HasValue)
            {
                query = query.Where(a => a.DateOfBirth <= bornBefore.Value);
            }

            return query;
        }

        private IQueryable<Author> ApplySorting(IQueryable<Author> query, string sortBy, string sortOrder)
        {
            return sortBy?.ToLower() switch
            {
                "name" => sortOrder?.ToLower() == "desc"
                    ? query.OrderByDescending(a => a.LastName).ThenByDescending(a => a.FirstName)
                    : query.OrderBy(a => a.LastName).ThenBy(a => a.FirstName),
                "country" => sortOrder?.ToLower() == "desc"
                    ? query.OrderByDescending(a => a.Country).ThenBy(a => a.LastName)
                    : query.OrderBy(a => a.Country).ThenBy(a => a.LastName),
                "birthdate" => sortOrder?.ToLower() == "desc"
                    ? query.OrderByDescending(a => a.DateOfBirth)
                    : query.OrderBy(a => a.DateOfBirth),
                "created" => sortOrder?.ToLower() == "desc"
                    ? query.OrderByDescending(a => a.CreatedDate)
                    : query.OrderBy(a => a.CreatedDate),
                _ => query.OrderBy(a => a.LastName).ThenBy(a => a.FirstName)
            };
        }
    }
}