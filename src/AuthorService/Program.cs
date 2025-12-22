using Microsoft.EntityFrameworkCore;
using AuthorService.Data;
using AuthorService.Validators;
using FluentValidation;
using FluentValidation.AspNetCore;
using AuthorService.Services;
using AuthorService.Interfaces;
using AuthorService.Repositories;

var builder = WebApplication.CreateBuilder(args);

// Database context
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add CORS services
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        policy => policy
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader());
    
    // For more restrictive policy (recommended for production)
    options.AddPolicy("AllowSpecificOrigins",
        policy =>
        {
            var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                ?? new[] { "http://localhost:3000", "https://localhost:3000", "http://localhost:5173" };
            
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        });
});

// Controllers and validation
builder.Services.AddControllers();
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<AuthorValidator>();

// Services
builder.Services.AddScoped<IBookService, BookService>();
builder.Services.AddSingleton<IResiliencePolicyService, ResiliencePolicyService>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IAuthorRepository, AuthorRepository>();

// Optional: Add Swagger for API documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline

// Use CORS - MUST be before UseAuthorization and MapControllers
if (app.Environment.IsDevelopment())
{
    app.UseCors("AllowAll");
    
    // Enable Swagger UI in development
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Author Service API V1");
        options.RoutePrefix = "swagger";
    });
}
else
{
    app.UseCors("AllowSpecificOrigins");
}

app.UseAuthorization();
app.MapControllers();

// Apply migrations automatically
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();
}

app.Run();