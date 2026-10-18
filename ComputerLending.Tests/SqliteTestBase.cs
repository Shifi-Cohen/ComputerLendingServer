using ComputerLending.Core.Entities;
using ComputerLending.Core.Enums;
using ComputerLending.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ComputerLending.Tests;

/// <summary>
/// SQLite in-memory (ולא InMemory של EF, שלא אוכף concurrency tokens).
/// חיבור אחד משותף לכל ה-DbContext-ים של הבדיקה, ולכן שניים מהם רואים את אותו מסד.
/// </summary>
public abstract class SqliteTestBase : IDisposable
{
    protected const int FirstUserId = 1;
    protected const int SecondUserId = 2;
    protected const int ComputerId = 1;

    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    protected SqliteTestBase()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;

        using var setup = NewContext();
        setup.Database.EnsureCreated();

        var now = DateTime.UtcNow;
        setup.Users.AddRange(
            new User { Id = FirstUserId, FullName = "First", Email = "first@test.com", PasswordHash = "x", Role = UserRole.Borrower, CreatedAtUtc = now },
            new User { Id = SecondUserId, FullName = "Second", Email = "second@test.com", PasswordHash = "x", Role = UserRole.Borrower, CreatedAtUtc = now });
        setup.Computers.Add(
            new Computer { Id = ComputerId, AssetTag = "LAP-001", Name = "Test PC", Status = ComputerStatus.Available, CreatedAtUtc = now });
        setup.SaveChanges();
    }

    protected AppDbContext NewContext() => new(_options);

    public void Dispose()
    {
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }
}
