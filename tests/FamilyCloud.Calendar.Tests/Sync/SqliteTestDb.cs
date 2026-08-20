using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using FamilyCloud.Server.Data;

namespace FamilyCloud.Calendar.Tests.Sync;

/// <summary>
/// A real (in-memory) SQLite-backed <see cref="FamilyCloudDbContext"/> — exercises actual relational
/// behavior/LINQ translation rather than the EF InMemory provider's looser semantics. The connection
/// must stay open for the context's lifetime (SQLite's in-memory database is dropped once the last
/// connection to it closes), so both are disposed together. Deliberately still SQLite even though
/// production runs on PostgreSQL (see the Phase 1 architecture roadmap): keeps these unit tests fast
/// and self-contained without requiring a live Postgres server for `dotnet test` to pass.
/// </summary>
internal sealed class SqliteTestDb : IDisposable
{
    /// <summary>A test calendar's <c>FamilyId</c> must reference a real row now that Calendar.FamilyId
    /// is a real foreign key — this is that row's id, seeded below.</summary>
    public static readonly Guid TestFamilyId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private readonly SqliteConnection connection;

    public FamilyCloudDbContext Context { get; }

    public SqliteTestDb()
    {
        connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<FamilyCloudDbContext>().UseSqlite(connection).Options;
        Context = new FamilyCloudDbContext(options);
        Context.Database.EnsureCreated();

        Context.Families.Add(new FamilyCloud.Family.Domain.Family
        {
            Id = TestFamilyId,
            Name = "Testfamilie",
            CreatedUtc = DateTimeOffset.UtcNow,
        });
        Context.SaveChanges();
    }

    public void Dispose()
    {
        Context.Dispose();
        connection.Dispose();
    }
}
