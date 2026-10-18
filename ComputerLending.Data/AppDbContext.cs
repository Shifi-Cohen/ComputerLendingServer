using ComputerLending.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace ComputerLending.Data;

public class AppDbContext : DbContext 
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Computer> Computers => Set<Computer>();
    public DbSet<Loan> Loans => Set<Loan>();
    public DbSet<Tag> Tags => Set<Tag>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<Computer>().Where(e => e.State == EntityState.Modified))
            entry.Entity.Version++;

        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }
}
