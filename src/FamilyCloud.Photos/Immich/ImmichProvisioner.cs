using System.Net.Http.Json;

namespace FamilyCloud.Photos.Immich;

public class ImmichProvisioner(HttpClient http, ILogger<ImmichProvisioner> logger) : IImmichProvisioner
{
    private sealed record AdminSignUpRequest(string Email, string Password, string Name);
    private sealed record LoginRequest(string Email, string Password);
    private sealed record LoginResponse(string AccessToken);
    private sealed record CreateApiKeyRequest(string Name, string[] Permissions);
    private sealed record CreateApiKeyResponse(string Secret);

    public async Task<string> ProvisionAsync(string adminEmail, string adminPassword, CancellationToken ct = default)
    {
        // Immich runs its own DB migrations on first boot and doesn't answer requests until they're
        // done — unlike Radicale (a static htpasswd file the server writes to directly, no readiness
        // window), so provisioning has to tolerate Immich still starting up even though AppHost/compose
        // already order this container after Immich via WaitFor/depends_on.
        const int maxAttempts = 10;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                // Only succeeds once, the very first time any admin signs up — on every later startup
                // (server restart against an already-provisioned Immich) this 400s, which just means
                // the admin account already exists, so it's swallowed rather than treated as fatal.
                await http.PostAsJsonAsync("api/auth/admin-sign-up",
                    new AdminSignUpRequest(adminEmail, adminPassword, "FamilyCloud"), ct);

                var loginResponse = await http.PostAsJsonAsync("api/auth/login", new LoginRequest(adminEmail, adminPassword), ct);
                loginResponse.EnsureSuccessStatusCode();
                var login = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>(ct)
                    ?? throw new InvalidOperationException("Immich login response did not include an access token.");

                using var apiKeyRequest = new HttpRequestMessage(HttpMethod.Post, "api/api-keys")
                {
                    Content = JsonContent.Create(new CreateApiKeyRequest("FamilyCloud", ["all"])),
                };
                apiKeyRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", login.AccessToken);
                var apiKeyResponse = await http.SendAsync(apiKeyRequest, ct);
                apiKeyResponse.EnsureSuccessStatusCode();
                var apiKey = await apiKeyResponse.Content.ReadFromJsonAsync<CreateApiKeyResponse>(ct)
                    ?? throw new InvalidOperationException("Immich API-key response did not include a secret.");

                return apiKey.Secret;
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                logger.LogInformation(ex, "Immich not ready yet (attempt {Attempt}/{MaxAttempts}), retrying...", attempt, maxAttempts);
                await Task.Delay(TimeSpan.FromSeconds(3), ct);
            }
        }

        throw new InvalidOperationException($"Could not provision the Immich service account after {maxAttempts} attempts.");
    }
}
