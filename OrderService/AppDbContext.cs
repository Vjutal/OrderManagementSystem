using Microsoft.EntityFrameworkCore;
using OrderService.Data.Types;

public class AppDbContext : DbContext
{
    public DbSet<Order> Orders { get; set; } = default!;

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply a global query filter to exclude soft-deleted records
        modelBuilder.Entity<Order>().HasQueryFilter(o => !o.IsDeleted);
    }
}