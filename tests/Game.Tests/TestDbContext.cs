using Game.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Game.Tests;

/// <summary>
/// Creates an in-memory SQLite <see cref="GameDbContext"/> suitable for unit tests.
/// The database is created fresh for each instance; dispose to release the connection.
/// </summary>
public sealed class TestDbContext : IDisposable
{
    private readonly SqliteConnection connection;

    public GameDbContext Db { get; }

    public TestDbContext()
    {
        // Keep the connection open for the lifetime of the test so the in-memory DB survives
        connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<GameDbContext>()
            .UseSqlite(connection)
            .Options;

        Db = new GameDbContext(options);
        Db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        Db.Dispose();
        connection.Dispose();
    }
}
