using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using FamilyCloud.Contracts.Auth;

namespace FamilyCloud.Family.Tests;

/// <summary>
/// Boots the real Program.cs pipeline (migrations, seed admin/family included) against an isolated
/// SQLite file + data directory per factory instance. Mirrors FamilyCloud.Server.Tests'
/// FamilyCloudWebApplicationFactory — kept as a separate copy rather than a shared reference since
/// that one lives in the Server.Tests test project, which Family.Tests doesn't reference.
/// </summary>
public sealed class FamilyCloudWebApplicationFactory : WebApplicationFactory<Program>
{
    public string SeedAdminUserName { get; } = "test-admin";

    public string SeedAdminPassword { get; } = "Sup3r-Secret-Test-Password!";

    private readonly string dataDirectory =
        Path.Combine(Path.GetTempPath(), "familycloud-family-tests", Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Directory.CreateDirectory(dataDirectory);

        builder.UseEnvironment("Development");
        builder.UseSetting("Database:Provider", "Sqlite");
        builder.UseSetting("ConnectionStrings:DefaultConnection", $"Data Source={Path.Combine(dataDirectory, "familycloud.db")}");
        builder.UseSetting("Jwt:SigningKey", "test-only-signing-key-at-least-32-characters-long");
        builder.UseSetting("SeedAdmin:UserName", SeedAdminUserName);
        builder.UseSetting("SeedAdmin:Password", SeedAdminPassword);
        builder.UseSetting("Radicale:HtpasswdPath", Path.Combine(dataDirectory, "radicale-htpasswd", "users"));
        builder.UseSetting("Radicale:BaseUrl", "http://localhost:5232/");
        // No live Immich instance in tests — Immich provisioning needs actual network round-trips
        // (unlike Radicale's pure file write above), so skip it here rather than eating its retry delay.
        builder.UseSetting("Photos:ProvisionImmich", "false");
    }

    public async Task<HttpClient> CreateAuthenticatedClientAsync(string? userName = null, string? password = null)
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/auth/login", new LoginRequest(userName ?? SeedAdminUserName, password ?? SeedAdminPassword));
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", body!.Token);
        return client;
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
