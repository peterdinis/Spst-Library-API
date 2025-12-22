using Microsoft.EntityFrameworkCore;
using CategoryService.Data;
using CategoryService.Validators;
using FluentValidation;
using FluentValidation.AspNetCore;
using CategoryService.Interfaces;
using CategoryService.Services;
using CategoryService.Repositories;

var builder = WebApplication.CreateBuilder(args);

// Database context
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add CORS services
builder.Services.AddCors(options =>
{
    // Development policy - allow all
    options.AddPolicy("AllowAll",
        builder => builder
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader());
    
    // Production policy - specific origins
    options.AddPolicy("AllowSpecificOrigins",
        builder =>
        {
            // Get allowed origins from configuration or use defaults
            var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                ?? new[] { 
                    "http://localhost:3000", 
                    "https://localhost:3000", 
                    "http://localhost:5173", 
                    "https://localhost:5173" 
                };
            
            builder.WithOrigins(allowedOrigins)
                   .AllowAnyMethod()
                   .AllowAnyHeader()
                   .AllowCredentials();
        });
});

// Controllers
builder.Services.AddControllers();

// FluentValidation
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<CategoryValidator>();

// Services
builder.Services.AddScoped<IBookService, BookService>();
builder.Services.AddSingleton<IResiliencePolicyService, ResiliencePolicyService>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
builder.Services.AddScoped<CreateCategoryDtoValidator>();
builder.Services.AddScoped<UpdateCategoryDtoValidator>();

// Add Swagger for API documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Category Service API", Version = "v1" });
});

var app = builder.Build();

// Configure the HTTP request pipeline

// Use CORS - Must be before UseAuthorization and MapControllers
if (app.Environment.IsDevelopment())
{
    app.UseCors("AllowAll");
    
    // Enable Swagger UI in development
    app.UseSwagger();
    app.UseSwaggerUI(c => 
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Category Service API v1");
        c.RoutePrefix = "swagger";
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