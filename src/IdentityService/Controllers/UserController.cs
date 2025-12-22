using IdentityService.Dtos;
using IdentityService.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IdentityService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
    public class UsersController(UserManager<ApplicationUser> userManager) : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager = userManager;

        [HttpGet]
        public async Task<IActionResult> GetAllUsers()
        {
            var users = _userManager.Users.ToList();
            var userDtos = new List<UserResponseDto>();

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                userDtos.Add(new UserResponseDto
                {
                    Id = user.Id,
                    Email = user.Email!,
                    FullName = user.FullName,
                    Roles = [.. roles],
                });
            }

            return Ok(userDtos);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
                return NotFound();

            var roles = await _userManager.GetRolesAsync(user);

            return Ok(new UserResponseDto
            {
                Id = user.Id,
                Email = user.Email!,
                FullName = user.FullName,
                Roles = [.. roles],
            });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
                return NotFound();

            var result = await _userManager.DeleteAsync(user);
            
            if (result.Succeeded)
                return Ok(new { message = "User deleted successfully" });

            return BadRequest(result.Errors);
        }

        [HttpGet("search")]
        public async Task<IActionResult> SearchUsers(
            [FromQuery] string? searchTerm,
            [FromQuery] string? role,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            // Validácia parametrov
            if (page < 1)
                page = 1;
            
            if (pageSize < 1 || pageSize > 100)
                pageSize = 20;

            // Vytvorenie základného query
            var query = _userManager.Users.AsQueryable();

            // Aplikovanie filtra podľa search term - SQLite kompatibilný
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.ToLower().Trim();
                
                // Pre SQLite použijeme EF.Functions.Like alebo EF.Functions.Collate
                // Alternatívne môžeme načítať všetko a filtrovať v pamäti pre menšie datasety
                query = query.Where(u =>
                    EF.Functions.Like(u.Email.ToLower(), $"%{term}%") ||
                    EF.Functions.Like(u.FullName.ToLower(), $"%{term}%") ||
                    EF.Functions.Like(u.UserName.ToLower(), $"%{term}%"));
            }

            // Aplikovanie filtra podľa role
            if (!string.IsNullOrWhiteSpace(role))
            {
                // Pre SQLite, ak máme menší počet používateľov, môžeme použiť tento prístup
                var usersInRole = await _userManager.GetUsersInRoleAsync(role);
                var userIdsInRole = usersInRole.Select(u => u.Id).ToList();
                
                if (userIdsInRole.Any())
                {
                    query = query.Where(u => userIdsInRole.Contains(u.Id));
                }
                else
                {
                    // Ak žiadny používateľ nemá túto rolu, vrátiť prázdny výsledok
                    return Ok(new 
                    { 
                        users = new List<UserResponseDto>(),
                        pagination = new
                        {
                            totalCount = 0,
                            totalPages = 0,
                            currentPage = page,
                            pageSize,
                            hasPrevious = false,
                            hasNext = false
                        }
                    });
                }
            }

            // Výpočet celkového počtu záznamov pre pagination
            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

            // Kontrola, či existujú stránky
            if (page > totalPages && totalPages > 0)
                page = totalPages;

            // Aplikovanie pagination a order by
            var users = await query
                .OrderBy(u => u.Email)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Mapovanie na DTO s rolami
            var userDtos = new List<UserResponseDto>();
            
            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                userDtos.Add(new UserResponseDto
                {
                    Id = user.Id,
                    Email = user.Email!,
                    FullName = user.FullName,
                    Roles = [.. roles],
                });
            }

            // Vytvorenie pagination response
            var paginationMetadata = new
            {
                totalCount,
                totalPages,
                currentPage = page,
                pageSize,
                hasPrevious = page > 1,
                hasNext = page < totalPages
            };

            Response.Headers.Add("X-Pagination", System.Text.Json.JsonSerializer.Serialize(paginationMetadata));

            return Ok(new 
            { 
                users = userDtos,
                pagination = paginationMetadata
            });
        }

        [HttpGet("search/suggest")]
        public async Task<IActionResult> SearchSuggestions(
            [FromQuery] string term,
            [FromQuery] int limit = 10)
        {
            if (string.IsNullOrWhiteSpace(term) || term.Length < 2)
            {
                return BadRequest(new { message = "Search term must be at least 2 characters long" });
            }

            if (limit < 1 || limit > 50)
            {
                limit = 10;
            }

            var searchTerm = term.ToLower().Trim();
            
            // Pre SQLite použijeme EF.Functions.Like
            var suggestions = await _userManager.Users
                .Where(u => EF.Functions.Like(u.Email.ToLower(), $"%{searchTerm}%") ||
                           EF.Functions.Like(u.FullName.ToLower(), $"%{searchTerm}%") ||
                           EF.Functions.Like(u.UserName.ToLower(), $"%{searchTerm}%"))
                .OrderBy(u => u.Email)
                .Take(limit)
                .Select(u => new UserSuggestionDto
                {
                    Id = u.Id,
                    Email = u.Email!,
                    FullName = u.FullName,
                    UserName = u.UserName!
                })
                .ToListAsync();

            return Ok(suggestions);
        }

        [HttpGet("search/stats")]
        public async Task<IActionResult> GetSearchStats()
        {
            // Pre SQLite môžeme použiť toto - funguje dobre
            var totalUsers = await _userManager.Users.CountAsync();
            
            // Získanie všetkých používateľov a počítanie rolí v pamäti (pre menšie datasety)
            var allUsers = await _userManager.Users.ToListAsync();
            var adminCount = 0;
            var userCount = 0;
            
            foreach (var user in allUsers)
            {
                var roles = await _userManager.GetRolesAsync(user);
                if (roles.Contains("Admin"))
                    adminCount++;
                if (roles.Contains("User"))
                    userCount++;
            }
            
            // Získanie najnovších používateľov
            var recentUsers = await _userManager.Users
                .OrderByDescending(u => u.CreatedAt)
                .Take(5)
                .Select(u => new
                {
                    u.Id,
                    u.Email,
                    u.FullName,
                    u.CreatedAt
                })
                .ToListAsync();

            var stats = new
            {
                totalUsers,
                byRole = new
                {
                    admin = adminCount,
                    user = userCount,
                    other = totalUsers - adminCount - userCount
                },
                recentUsers,
                usersWithFullName = await _userManager.Users.CountAsync(u => !string.IsNullOrEmpty(u.FullName)),
                usersWithEmailConfirmed = await _userManager.Users.CountAsync(u => u.EmailConfirmed),
                averageUsersPerDay = totalUsers > 0 ? 
                    Math.Round((double)totalUsers / (DateTime.UtcNow - allUsers.Min(u => u.CreatedAt)).Days, 2) : 0
            };

            return Ok(stats);
        }
    }
}