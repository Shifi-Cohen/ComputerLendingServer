using ComputerLending.Core.Entities;
using ComputerLending.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace ComputerLending.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(
        AppDbContext db,
        string? adminPassword,
        string? borrowerPassword,
        CancellationToken cancellationToken = default)
    {
        if (await db.Users.AsNoTracking().AnyAsync(cancellationToken))
            return;

        if (string.IsNullOrWhiteSpace(adminPassword) || string.IsNullOrWhiteSpace(borrowerPassword))
        {
            throw new InvalidOperationException(
                "Seed passwords are missing. Set Seed:AdminPassword and Seed:BorrowerPassword.");
        }

        var now = DateTime.UtcNow;

        var admin = new User
        {
            FullName = "Admin Demo",
            Email = "admin@demo.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(adminPassword),
            Role = UserRole.Admin,
            CreatedAtUtc = now
        };

        var borrower = new User
        {
            FullName = "User Demo",
            Email = "user@demo.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(borrowerPassword),
            Role = UserRole.Borrower,
            CreatedAtUtc = now
        };

        var windows = new Tag { Name = "Windows" };
        var linux = new Tag { Name = "Linux" };
        var ram16 = new Tag { Name = "16GB" };
        var lab = new Tag { Name = "Lab" };

        var computers = new List<Computer>
        {
            new()
            {
                AssetTag = "LAP-001",
                Name = "Dell Latitude 5440",
                Specs = "Intel i5, 16GB RAM",
                Status = ComputerStatus.Available,
                CreatedAtUtc = now,
                Tags = { windows, ram16 }
            },
            new()
            {
                AssetTag = "LAP-002",
                Name = "HP EliteBook 840",
                Specs = "Intel i7, 8GB RAM",
                Status = ComputerStatus.Available,
                CreatedAtUtc = now,
                Tags = { windows }
            },
            new()
            {
                AssetTag = "LAP-003",
                Name = "Lenovo ThinkPad T14",
                Specs = "AMD Ryzen 5, 16GB RAM",
                Status = ComputerStatus.Available,
                CreatedAtUtc = now,
                Tags = { linux, lab }
            },
            new()
            {
                AssetTag = "LAP-004",
                Name = "Apple MacBook Pro 14",
                Specs = "M3, 16GB RAM",
                Status = ComputerStatus.Available,
                CreatedAtUtc = now,
                Tags = { ram16 }
            },
            new()
            {
                AssetTag = "LAP-005",
                Name = "Dell XPS 15",
                Specs = "Intel i7, 16GB RAM",
                Status = ComputerStatus.Available,
                CreatedAtUtc = now,
                Tags = { windows, ram16 }
            },
            new()
            {
                AssetTag = "LAP-006",
                Name = "Lenovo IdeaPad 3",
                Specs = "Intel i3, 8GB RAM",
                Status = ComputerStatus.Available,
                CreatedAtUtc = now,
                Tags = { lab }
            },
            new()
            {
                AssetTag = "DESK-001",
                Name = "Dell OptiPlex 7010",
                Specs = "Intel i5, 8GB RAM",
                Status = ComputerStatus.Available,
                CreatedAtUtc = now,
                Tags = { windows, lab }
            },
            new()
            {
                AssetTag = "LAP-007",
                Name = "HP ProBook 450",
                Specs = "Intel i5, 8GB RAM",
                Status = ComputerStatus.Available,
                CreatedAtUtc = now,
                Tags = { linux }
            }
        };

        await db.Users.AddRangeAsync([admin, borrower], cancellationToken);
        await db.Tags.AddRangeAsync([windows, linux, ram16, lab], cancellationToken);
        await db.Computers.AddRangeAsync(computers, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }
}
