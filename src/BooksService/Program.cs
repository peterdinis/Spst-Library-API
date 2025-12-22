using Microsoft.EntityFrameworkCore;
using BooksService.Data;
using BooksService.Validators;
using FluentValidation;
using BooksService.Services;
using BooksService.Interfaces;
using BooksService.Repositories;
using BooksService.Dtos;
using FluentValidation.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Database context
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigin",
        builder =>
        {
            builder.WithOrigins(
                    "http://localhost:3000")
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        });
    
    // Alebo úplne otvorená politika pre vývoj
    options.AddPolicy("AllowAll",
        builder =>
        {
            builder.AllowAnyOrigin()
                   .AllowAnyMethod()
                   .AllowAnyHeader();
        });
});

// Controllers
builder.Services.AddControllers();

// FluentValidation
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<BookValidator>();

// Services
builder.Services.AddScoped<IRabbitMQService, RabbitMQService>();
builder.Services.AddSingleton<IResiliencePolicyService, ResiliencePolicyService>();

builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IBookRepository, BookRepository>();
builder.Services.AddScoped<IValidator<CreateBookDto>, CreateBookDtoValidator>();
builder.Services.AddScoped<IValidator<UpdateBookDto>, UpdateBookDtoValidator>();

// Optional: Swagger/OpenAPI for development
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

// Configure the HTTP request pipeline

// Enable CORS - musí byť pred UseAuthorization a MapControllers
app.UseCors("AllowSpecificOrigin"); // Použi konkrétnu politiku
// alebo: app.UseCors("AllowAll"); // Pre vývoj

app.UseAuthorization();

app.MapControllers();

// Apply migrations automatically
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();
}

app.Run();