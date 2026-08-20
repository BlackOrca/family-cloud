using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FamilyCloud.Server.Data;

/// <summary>
/// Lets `dotnet ef migrations add`/`dotnet ef database update` build a <see cref="FamilyCloudDbContext"/>
/// directly, without booting the full app (which would otherwise fail design-time tooling on the
/// Jwt:SigningKey startup check in Program.cs, and doesn't need a live database connection just to
/// generate migration SQL).
/// </summary>
public class FamilyCloudDbContextFactory : IDesignTimeDbContextFactory<FamilyCloudDbContext>
{
    public FamilyCloudDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<FamilyCloudDbContext>()
            .UseNpgsql("Host=localhost;Database=familycloud;Username=familycloud;Password=familycloud")
            .Options;
        return new FamilyCloudDbContext(options);
    }
}
