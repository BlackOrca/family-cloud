using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace OurLive.Server.Tests;

/// <summary>
/// Boots the real Program.cs pipeline (migrations, seed admin, Radicale provisioning included)
/// against an isolated SQLite file + data directory per factory instance, so tests exercise the
/// actual startup/auth/API wiring rather than a hand-assembled substitute host.
/// </summary>
public sealed class OurLiveWebApplicationFactory : WebApplicationFactory<Program>
{
    public string SeedAdminUserName { get; } = "test-admin";

    public string SeedAdminPassword { get; } = "Sup3r-Secret-Test-Password!";

    private readonly string dataDirectory =
        Path.Combine(Path.GetTempPath(), "ourlive-server-tests", Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Directory.CreateDirectory(dataDirectory);

        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:DefaultConnection", $"Data Source={Path.Combine(dataDirectory, "ourlive.db")}");
        builder.UseSetting("Jwt:SigningKey", "test-only-signing-key-at-least-32-characters-long");
        builder.UseSetting("SeedAdmin:UserName", SeedAdminUserName);
        builder.UseSetting("SeedAdmin:Password", SeedAdminPassword);
        builder.UseSetting("Radicale:HtpasswdPath", Path.Combine(dataDirectory, "radicale-htpasswd", "users"));
        builder.UseSetting("Radicale:BaseUrl", "http://localhost:5232/");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        try
        {
            Directory.Delete(dataDirectory, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort cleanup only — a lingering temp directory isn't worth failing the test run over.
        }
    }
}
