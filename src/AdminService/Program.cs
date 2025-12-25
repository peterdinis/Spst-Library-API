using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using AdminService.Data;
using AdminService.Entities;

var builder = WebApplication.CreateBuilder(args);

// Add SQLite Database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add Identity with custom User model
builder.Services.AddIdentity<ApplicationAdmin, IdentityRole>(options =>
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

// Configure cookie authentication
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.LoginPath = "/api/admin/auth/login";
    options.AccessDeniedPath = "/api/admin/auth/access-denied";
    options.SlidingExpiration = true;
    options.Cookie.Name = "AdminCookie";
    
    // CORS settings for cookies
    options.Cookie.SameSite = SameSiteMode.None; // Pre CORS a rôzne domény
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always; // HTTPS only
});

// Add CORS with specific configuration for authentication
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        policy => policy
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader()
            .WithExposedHeaders("Set-Cookie") // Pre autentifikačné cookies
            .AllowCredentials()); // Povoliť credentials pre autentifikáciu
    
    // Alternatívna politika pre špecifické domény (odporúčané pre produkciu)
    options.AddPolicy("AllowSpecificOrigins",
        policy =>
        {
            var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() 
                ?? new[] { "http://localhost:3000", "https://localhost:3000" };
            
            policy.WithOrigins(allowedOrigins)
                   .AllowAnyMethod()
                   .AllowAnyHeader()
                   .WithExposedHeaders("Set-Cookie")
                   .AllowCredentials();
        });
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

// Configure the HTTP request pipeline

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    
    // In development, use AllowAll CORS policy
    app.UseCors("AllowAll");
}
else
{
    app.UseExceptionHandler("/error");
    app.UseHsts();
    
    // In production, use specific origins
    app.UseCors("AllowSpecificOrigins");
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();