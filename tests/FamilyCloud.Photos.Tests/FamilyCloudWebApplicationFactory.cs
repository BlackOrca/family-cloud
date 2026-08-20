using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using FamilyCloud.Contracts.Auth;
using FamilyCloud.Photos.Immich;

namespace FamilyCloud.Photos.Tests;

/// <summary>
/// Boots the real Program.cs pipeline (migrations, seed admin/family included) against an isolated
/// SQLite file + data directory per factory instance. Mirrors FamilyCloud.Lists.Tests' copy of the same
/// factory, plus swaps the real <see cref="IImmichClient"/> for <see cref="FakeImmichClient"/> since
/// tests run with no live Immich instance (see Photos:ProvisionImmich=false below).
/// </summary>
public sealed class FamilyCloudWebApplicationFactory : WebApplicationFactory<Program>
{
    public string SeedAdminUserName { get; } = "test-admin";

    public string SeedAdminPassword { get; } = "Sup3r-Secret-Test-Password!";

    private readonly string dataDirectory =
        Path.Combine(Path.GetTempPath(), "familycloud-photos-tests", Guid.NewGuid().ToString("N"));

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
        builder.UseSetting("Photos:ProvisionImmich", "false");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IImmichClient>();
            services.AddSingleton<IImmichClient, FakeImmichClient>();
        });
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
