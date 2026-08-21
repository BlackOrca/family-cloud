using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.EntityFrameworkCore;
using FamilyCloud.Storage.Domain;
using FamilyCloud.Storage.Security;

namespace FamilyCloud.Storage.OpenCloud;

/// <summary>See <see cref="IOpenCloudClient"/>. Every call authenticates fresh from the decrypted
/// <see cref="OpenCloudServiceAccount"/> password rather than caching a token, mirroring
/// <c>FamilyCloud.Photos.Immich.ImmichClient</c>'s per-request credential lookup.</summary>
public class OpenCloudClient(HttpClient http, DbContext db, IOpenCloudCredentialProtector protector) : IOpenCloudClient
{
    private sealed record MeResponse(string DisplayName);

    private sealed record CreateUserRequest(string DisplayName, string Mail, string OnPremisesSamAccountName, PasswordProfile PasswordProfile);

    private sealed record PasswordProfile(string Password);

    private sealed record CreateUserResponse(string Id);

    private sealed record GetUserResponse(string Id);

    private sealed record UpdatePasswordRequest(PasswordProfile PasswordProfile);

    private sealed record CreateDriveRequest(string Name);

    private sealed record CreateDriveResponse(string Id);

    private sealed record GetDriveResponse(string Id, List<DrivePermission>? Permissions);

    private sealed record DrivePermission(string Id, GrantedToV2? GrantedToV2, List<string>? Roles);

    private sealed record GrantedToV2(GrantedUser? User);

    private sealed record GrantedUser(string Id);

    private sealed record InviteRecipient(string ObjectId, [property: System.Text.Json.Serialization.JsonPropertyName("@libre.graph.recipient.type")] string RecipientType);

    private sealed record InviteRequest(List<InviteRecipient> Recipients, List<string> Roles);

    private sealed record InviteResponse(List<InvitePermission> Value);

    private sealed record InvitePermission(string Id);

    private sealed record UpdateRoleRequest(List<string> Roles);

    private sealed record RoleDefinition(string DisplayName, string Id, List<RolePermissionEntry> RolePermissions);

    private sealed record RolePermissionEntry(string? Condition);

    public async Task VerifyServiceAccountAsync(CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "graph/v1.0/me");
        await AddAuthAsync(request, ct);
        var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        await response.Content.ReadFromJsonAsync<MeResponse>(ct);
    }

    public async Task<string> CreateUserAsync(string username, string email, string plainPassword, CancellationToken ct = default)
    {
        var response = await SendAsync(HttpMethod.Post, "graph/v1.0/users",
            new CreateUserRequest(username, email, username, new PasswordProfile(plainPassword)), ct);

        // Verified against a real instance: OpenCloud's own built-in "admin" account (created by the
        // container itself at first boot, not by us — see OpenCloudServiceAccount) already owns that
        // username. A FamilyCloud household naming their seed admin "admin" is a realistic, likely
        // choice, not just a test-config coincidence — so a 409 here means "this account already
        // exists", not a real failure, mirroring how ImmichProvisioner swallows a 400 on repeat
        // admin-sign-up. The existing account's password won't match the new one in that specific case
        // (OpenCloud's own admin password is fixed at first boot), which is an accepted limitation of
        // this edge case, not solved here.
        if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            var existing = await SendAsync<object>(HttpMethod.Get, $"graph/v1.0/users/{username}", body: null, ct);
            existing.EnsureSuccessStatusCode();
            var existingUser = await existing.Content.ReadFromJsonAsync<GetUserResponse>(ct)
                ?? throw new InvalidOperationException("OpenCloud get-user response did not include an id.");
            return existingUser.Id;
        }

        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<CreateUserResponse>(ct)
            ?? throw new InvalidOperationException("OpenCloud user-creation response did not include an id.");
        return created.Id;
    }

    public async Task UpdatePasswordAsync(string openCloudUserId, string newPlainPassword, CancellationToken ct = default)
    {
        // Not verified against a live instance (unlike every other call in this class) — the analogous
        // shape to CreateUserAsync's passwordProfile, but confirm against the real Graph API on first use.
        var response = await SendAsync(HttpMethod.Patch, $"graph/v1.0/users/{openCloudUserId}",
            new UpdatePasswordRequest(new PasswordProfile(newPlainPassword)), ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task<string> CreateDriveAsync(string name, CancellationToken ct = default)
    {
        var response = await SendAsync(HttpMethod.Post, "graph/v1.0/drives", new CreateDriveRequest(name), ct);
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<CreateDriveResponse>(ct)
            ?? throw new InvalidOperationException("OpenCloud drive-creation response did not include an id.");
        return created.Id;
    }

    public async Task<string> InviteAsync(string driveId, string openCloudUserId, StorageRole role, CancellationToken ct = default)
    {
        var roleId = await ResolveRoleIdAsync(role, ct);
        var request = new InviteRequest(
            [new InviteRecipient(openCloudUserId, "user")],
            [roleId]);
        var response = await SendAsync(HttpMethod.Post, $"graph/v1beta1/drives/{driveId}/root/invite", request, ct);
        response.EnsureSuccessStatusCode();
        var invited = await response.Content.ReadFromJsonAsync<InviteResponse>(ct)
            ?? throw new InvalidOperationException("OpenCloud invite response was empty.");
        return invited.Value.Single().Id;
    }

    public async Task UpdateRoleAsync(string driveId, string permissionId, StorageRole role, CancellationToken ct = default)
    {
        var roleId = await ResolveRoleIdAsync(role, ct);
        var response = await SendAsync(HttpMethod.Patch, $"graph/v1beta1/drives/{driveId}/root/permissions/{permissionId}",
            new UpdateRoleRequest([roleId]), ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task RevokeAsync(string driveId, string permissionId, CancellationToken ct = default)
    {
        var response = await SendAsync<object>(HttpMethod.Delete, $"graph/v1beta1/drives/{driveId}/root/permissions/{permissionId}", body: null, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task<string?> FindPermissionIdAsync(string driveId, string openCloudUserId, CancellationToken ct = default)
    {
        var drive = await GetDriveAsync(driveId, ct);
        return drive.Permissions?.FirstOrDefault(p => p.GrantedToV2?.User?.Id == openCloudUserId)?.Id;
    }

    public async Task<bool> HasManagerAccessAsync(string driveId, string openCloudUserId, CancellationToken ct = default)
    {
        var managerRoleId = await ResolveRoleIdAsync(StorageRole.Manager, ct);
        var drive = await GetDriveAsync(driveId, ct);
        var permission = drive.Permissions?.FirstOrDefault(p => p.GrantedToV2?.User?.Id == openCloudUserId);
        return permission?.Roles?.Contains(managerRoleId) ?? false;
    }

    private async Task<GetDriveResponse> GetDriveAsync(string driveId, CancellationToken ct)
    {
        var response = await SendAsync<object>(HttpMethod.Get, $"graph/v1.0/drives/{driveId}", body: null, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<GetDriveResponse>(ct)
            ?? throw new InvalidOperationException("OpenCloud get-drive response was empty.");
    }

    /// <summary>Resolves a <see cref="StorageRole"/> to OpenCloud's internal role GUID by matching
    /// displayName among the space-root-level roles (condition "exists @Resource.Root") — not hard-coded,
    /// since these are the same fixed-but-unofficial ids the Phase 4 roadmap warns could change across
    /// OpenCloud versions. Re-resolved on every call rather than cached: these endpoints are low-frequency
    /// admin operations (root creation/sharing), not worth the staleness risk of caching across a
    /// potential OpenCloud upgrade while the server keeps running.</summary>
    private async Task<string> ResolveRoleIdAsync(StorageRole role, CancellationToken ct)
    {
        var displayName = role switch
        {
            StorageRole.Viewer => "Can view",
            StorageRole.Editor => "Can edit",
            StorageRole.Manager => "Can manage",
            _ => throw new ArgumentOutOfRangeException(nameof(role)),
        };

        var response = await SendAsync<object>(HttpMethod.Get, "graph/v1beta1/roleManagement/permissions/roleDefinitions", body: null, ct);
        response.EnsureSuccessStatusCode();
        var definitions = await response.Content.ReadFromJsonAsync<List<RoleDefinition>>(ct)
            ?? throw new InvalidOperationException("OpenCloud roleDefinitions response was empty.");

        var match = definitions.FirstOrDefault(d =>
            d.DisplayName == displayName && d.RolePermissions.Any(p => p.Condition == "exists @Resource.Root"));
        return match?.Id
            ?? throw new InvalidOperationException($"Could not resolve OpenCloud's \"{displayName}\" space-root role.");
    }

    private async Task<HttpResponseMessage> SendAsync<TBody>(HttpMethod method, string requestUri, TBody? body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, requestUri);
        await AddAuthAsync(request, ct);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }
        return await http.SendAsync(request, ct);
    }

    private async Task AddAuthAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var account = await db.Set<OpenCloudServiceAccount>().FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException(
                "No OpenCloud service account has been provisioned yet — see OpenCloudProvisioner and the seed-admin startup flow in Program.cs.");
        var password = protector.Decrypt(account.EncryptedAdminPassword);
        var bytes = Encoding.UTF8.GetBytes($"admin:{password}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(bytes));
    }
}
