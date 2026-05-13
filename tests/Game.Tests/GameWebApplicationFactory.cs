using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Game.Infrastructure;

namespace Game.Tests;

/// <summary>
/// A <see cref="WebApplicationFactory{TEntryPoint}"/> that replaces the file-based SQLite
/// database with an in-memory SQLite database scoped to the factory instance.
/// This ensures full test isolation — each factory gets a clean, empty database.
/// </summary>
public sealed class GameWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;
    private readonly SqliteConnection _keepAliveConnection;

    public GameWebApplicationFactory()
    {
        // Use a unique named in-memory database per factory instance.
        // Shared-cache mode ensures all connections within the same process
        // see the same in-memory database as long as at least one connection is open.
        var dbName = $"test_{Guid.NewGuid():N}";
        _connectionString = $"Data Source={dbName};Mode=Memory;Cache=Shared";

        // Keep a connection open so the in-memory database is not destroyed
        // while the factory is alive.
        _keepAliveConnection = new SqliteConnection(_connectionString);
        _keepAliveConnection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove the file-based DbContext registration added in Program.cs.
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<GameDbContext>));
            if (descriptor is not null)
                services.Remove(descriptor);

            // Replace with an in-memory SQLite database unique to this factory instance.
            services.AddDbContext<GameDbContext>(options =>
                options.UseSqlite(_connectionString));
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _keepAliveConnection.Dispose();
        base.Dispose(disposing);
    }
}
