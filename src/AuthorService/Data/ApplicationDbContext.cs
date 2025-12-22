using Microsoft.EntityFrameworkCore;
using AuthorService.Entities;

namespace AuthorService.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<Author> Authors { get; set; }
}