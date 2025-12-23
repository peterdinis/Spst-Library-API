using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using IdentityService.Data;
using IdentityService.Entities;

var builder = WebApplication.CreateBuilder(args);

// Add SQLite Database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add Identity with custom User model
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    // Password settings
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequiredLength = 6;

    // Lockout settings
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.MaxFailedAccessAttempts = 5;

    // User settings
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Configure cookie authentication for CORS
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.LoginPath = "/api/auth/login";
    options.AccessDeniedPath = "/api/auth/access-denied";
    options.SlidingExpiration = true;
    options.Cookie.Name = "SchoolLibraryAuth";

    // Important for CORS with authentication
    options.Cookie.SameSite = SameSiteMode.None;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy",
        policy =>
        {
            // Start with default development origins
            var allowedOrigins = new List<string> { 
                "http://localhost:3000", 
                "https://localhost:3000",
                "http://localhost:5173",
                "https://localhost:5173",
                "http://localhost:5000",
                "https://localhost:5000",
                "http://localhost:5001",
                "https://localhost:5001"
            };
            
            // Add any from configuration if available
            var configOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
            if (configOrigins != null && configOrigins.Length > 0)
            {
                allowedOrigins.AddRange(configOrigins);
            }
            
            policy.WithOrigins(allowedOrigins.Distinct().ToArray())
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials()
                  .WithExposedHeaders("Set-Cookie", "Access-Control-Allow-Credentials");
        });
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

// Use CORS
app.UseCors("CorsPolicy");

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();