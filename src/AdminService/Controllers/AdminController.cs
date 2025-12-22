using AdminService.Dtos;
using AdminService.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AdminService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AdminController : ControllerBase
    {
        private readonly UserManager<ApplicationAdmin> _userManager;
        private readonly SignInManager<ApplicationAdmin> _signInManager;

        public AdminController(
            UserManager<ApplicationAdmin> userManager,
            SignInManager<ApplicationAdmin> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var user = new ApplicationAdmin
            {
                UserName = model.Email,
                Email = model.Email,
                FullName = model.FullName,
            };

            var result = await _userManager.CreateAsync(user, model.Password);

            if (result.Succeeded)
            {
                // Assign role
                if (!string.IsNullOrEmpty(model.Role) && 
                    (model.Role == "Admin" || model.Role == "Teacher" || model.Role == "Student"))
                {
                    await _userManager.AddToRoleAsync(user, model.Role);
                }

                return Ok(new { message = "User registered successfully" });
            }

            return BadRequest(result.Errors);
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
                return Unauthorized(new { message = "Invalid credentials" });

            var result = await _signInManager.PasswordSignInAsync(
                user, 
                model.Password, 
                model.RememberMe, 
                lockoutOnFailure: true);

            if (result.Succeeded)
            {
                var roles = await _userManager.GetRolesAsync(user);
                
                return Ok(new UserResponseDto
                {
                    Id = user.Id,
                    Email = user.Email!,
                    FullName = user.FullName,
                    Roles = [.. roles],
                });
            }

            if (result.IsLockedOut)
                return StatusCode(423, new { message = "Account locked due to multiple failed attempts" });

            return Unauthorized(new { message = "Invalid credentials" });
        }

        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return Ok(new { message = "Logged out successfully" });
        }

        [HttpGet("current-user")]
        [Authorize]
        public async Task<IActionResult> GetCurrentUser()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Unauthorized();

            var roles = await _userManager.GetRolesAsync(user);

            return Ok(new UserResponseDto
            {
                Id = user.Id,
                Email = user.Email!,
                FullName = user.FullName,
                Roles = [.. roles],
            });
        }

        [HttpGet("access-denied")]
        public IActionResult AccessDenied()
        {
            return StatusCode(403, new { message = "Access denied" });
        }

        // Generate a new admin code for the current user
        [HttpPost("generate-admin-code")]
        [Authorize]
        public async Task<IActionResult> GenerateAdminCode([FromBody] GenerateAdminCodeDto request)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Unauthorized();

            // Optional: Check if user has specific role to generate admin codes
            var roles = await _userManager.GetRolesAsync(user);
            if (!roles.Contains("Admin") && !roles.Contains("SuperAdmin"))
            {
                return StatusCode(403, new { message = "Insufficient permissions to generate admin codes" });
            }

            user.GenerateAdminCode(request.ExpiryHours ?? 24);
            var result = await _userManager.UpdateAsync(user);

            if (result.Succeeded)
            {
                return Ok(new
                {
                    message = "Admin code generated successfully",
                    adminCode = user.AdminCode,
                    expiresAt = user.AdminCodeExpiry
                });
            }

            return BadRequest(result.Errors);
        }

        // Validate an admin code
        [HttpPost("validate-admin-code")]
        [Authorize]
        public async Task<IActionResult> ValidateAdminCode([FromBody] ValidateAdminCodeDto request)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Unauthorized();

            if (string.IsNullOrEmpty(request.AdminCode))
                return BadRequest(new { message = "Admin code is required" });

            if (user.IsAdminCodeValid(request.AdminCode))
            {
                // Optional: Clear the code after successful validation
                if (request.ClearAfterValidation)
                {
                    user.ClearAdminCode();
                    await _userManager.UpdateAsync(user);
                }

                return Ok(new { message = "Admin code is valid" });
            }

            if (user.IsAdminCodeExpired())
                return BadRequest(new { message = "Admin code has expired" });

            return BadRequest(new { message = "Invalid admin code" });
        }

        // Get current admin code status
        [HttpGet("admin-code-status")]
        [Authorize]
        public async Task<IActionResult> GetAdminCodeStatus()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Unauthorized();

            return Ok(new
            {
                hasCode = !string.IsNullOrEmpty(user.AdminCode),
                isExpired = user.IsAdminCodeExpired(),
                expiresAt = user.AdminCodeExpiry
            });
        }

        // Clear admin code
        [HttpPost("clear-admin-code")]
        [Authorize]
        public async Task<IActionResult> ClearAdminCode()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Unauthorized();

            user.ClearAdminCode();
            var result = await _userManager.UpdateAsync(user);

            if (result.Succeeded)
                return Ok(new { message = "Admin code cleared successfully" });

            return BadRequest(result.Errors);
        }
    }
}