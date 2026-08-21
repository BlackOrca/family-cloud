using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.EntityFrameworkCore;
using FamilyCloud.Storage.Domain;
using FamilyCloud.Storage.Security;

namespace FamilyCloud.Storage.OpenCloud;

public class OpenCloudProvisioner(
    HttpClient http, IOpenCloudClient client, DbContext db, IOpenCloudCredentialProtector protector, ILogger<OpenCloudProvisioner> logger)
    : IOpenCloudProvisioner
{
    private sealed record MeResponse(string DisplayName);

    public async Task ProvisionServiceAccountAsync(string adminPassword, CancellationToken ct = default)
    {
        // Verifying the admin credential (not creating it — the container already did that via
        // IDM_ADMIN_PASSWORD at first boot) needs its own HttpClient authenticated with the password
        // passed in directly, since IOpenCloudClient reads its credential from the OpenCloudServiceAccount
        // row this method is about to create — using it here would be circular.
        const int maxAttempts = 10;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, "graph/v1.0/me");
                var bytes = Encoding.UTF8.GetBytes($"admin:{adminPassword}");
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(bytes));
                var response = await http.SendAsync(request, ct);
                response.EnsureSuccessStatusCode();
                await response.Content.ReadFromJsonAsync<MeResponse>(ct);

                var existing = await db.Set<OpenCloudServiceAccount>().FirstOrDefaultAsync(ct);
                if (existing is null)
                {
                    db.Set<OpenCloudServiceAccount>().Add(new OpenCloudServiceAccount
                    {
                        EncryptedAdminPassword = protector.Encrypt(adminPassword),
                        ProvisionedUtc = DateTimeOffset.UtcNow,
                    });
                }
                else
                {
                    existing.EncryptedAdminPassword = protector.Encrypt(adminPassword);
                }
                return;
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                logger.LogInformation(ex, "OpenCloud not ready yet (attempt {Attempt}/{MaxAttempts}), retrying...", attempt, maxAttempts);
                await Task.Delay(TimeSpan.FromSeconds(3), ct);
            }
        }

        throw new InvalidOperationException($"Could not verify the OpenCloud service account after {maxAttempts} attempts.");
    }

    public async Task ProvisionUserAsync(Guid userId, string username, string? email, string plainPassword, CancellationToken ct = default)
    {
        var effectiveEmail = string.IsNullOrWhiteSpace(email) ? $"{username}@familycloud.local" : email;

        var existing = await db.Set<OpenCloudAccount>().FirstOrDefaultAsync(a => a.UserId == userId, ct);
        if (existing is null)
        {
            var openCloudUserId = await client.CreateUserAsync(username, effectiveEmail, plainPassword, ct);
            db.Set<OpenCloudAccount>().Add(new OpenCloudAccount
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Username = username,
                OpenCloudUserId = openCloudUserId,
                ProvisionedUtc = DateTimeOffset.UtcNow,
            });
        }
        else
        {
            await client.UpdatePasswordAsync(existing.OpenCloudUserId, plainPassword, ct);
        }
    }
}
