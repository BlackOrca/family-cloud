using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using FamilyCloud.Core.Data;

namespace FamilyCloud.Core.Tests.Sync;

/// <summary>
/// A real (in-memory) SQLite-backed <see cref="FamilyCloudDbContext"/> — exercises actual relational
/// behavior/LINQ translation rather than the EF InMemory provider's looser semantics. The connection
/// must stay open for the context's lifetime (SQLite's in-memory database is dropped once the last
/// connection to it closes), so both are disposed together.
/// </summary>
internal sealed class SqliteTestDb : IDisposable
{
    private readonly SqliteConnection connection;

    public FamilyCloudDbContext Context { get; }

    public SqliteTestDb()
    {
        connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<FamilyCloudDbContext>().UseSqlite(connection).Options;
        Context = new FamilyCloudDbContext(options);
        Context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        Context.Dispose();
        connection.Dispose();
    }
}
