using Microsoft.EntityFrameworkCore;
using BooksService.Entities;

namespace BooksService.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<Book> Books { get; set; }
}