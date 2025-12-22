using Microsoft.EntityFrameworkCore;
using CategoryService.Entities;

namespace CategoryService.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<Category> Categories { get; set; }
}