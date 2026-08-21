using FamilyCloud.Storage.OpenCloud;

namespace FamilyCloud.Storage.Tests;

/// <summary>
/// In-memory stand-in for <see cref="IOpenCloudClient"/> — tests run against no live OpenCloud instance
/// (see FamilyCloudWebApplicationFactory, which swaps this in for the real OpenCloudClient), so this
/// mimics just enough of OpenCloud's user/drive/sharing behavior for StorageEndpoints' round trips to work.
/// </summary>
internal sealed class FakeOpenCloudClient : IOpenCloudClient
{
    private sealed record FakePermission(string PermissionId, string OpenCloudUserId, StorageRole Role);

    private readonly Dictionary<string, string> usersByUsername = [];
    private readonly Dictionary<string, List<FakePermission>> permissionsByDrive = [];
    private int nextId;

    public Task VerifyServiceAccountAsync(CancellationToken ct = default) => Task.CompletedTask;

    public Task<string> CreateUserAsync(string username, string email, string plainPassword, CancellationToken ct = default)
    {
        if (!usersByUsername.TryGetValue(username, out var id))
        {
            id = $"oc-user-{++nextId}";
            usersByUsername[username] = id;
        }
        return Task.FromResult(id);
    }

    public Task UpdatePasswordAsync(string openCloudUserId, string newPlainPassword, CancellationToken ct = default) =>
        Task.CompletedTask;

    public Task<string> CreateDriveAsync(string name, CancellationToken ct = default)
    {
        var id = $"oc-drive-{++nextId}";
        permissionsByDrive[id] = [];
        return Task.FromResult(id);
    }

    public Task<string> InviteAsync(string driveId, string openCloudUserId, StorageRole role, CancellationToken ct = default)
    {
        var permissionId = $"oc-permission-{++nextId}";
        if (!permissionsByDrive.TryGetValue(driveId, out var permissions))
        {
            permissions = [];
            permissionsByDrive[driveId] = permissions;
        }
        permissions.Add(new FakePermission(permissionId, openCloudUserId, role));
        return Task.FromResult(permissionId);
    }

    public Task UpdateRoleAsync(string driveId, string permissionId, StorageRole role, CancellationToken ct = default)
    {
        var permissions = permissionsByDrive[driveId];
        var index = permissions.FindIndex(p => p.PermissionId == permissionId);
        permissions[index] = permissions[index] with { Role = role };
        return Task.CompletedTask;
    }

    public Task RevokeAsync(string driveId, string permissionId, CancellationToken ct = default)
    {
        permissionsByDrive[driveId].RemoveAll(p => p.PermissionId == permissionId);
        return Task.CompletedTask;
    }

    public Task<string?> FindPermissionIdAsync(string driveId, string openCloudUserId, CancellationToken ct = default)
    {
        var match = permissionsByDrive.TryGetValue(driveId, out var permissions)
            ? permissions.FirstOrDefault(p => p.OpenCloudUserId == openCloudUserId)
            : null;
        return Task.FromResult(match?.PermissionId);
    }

    public Task<bool> HasManagerAccessAsync(string driveId, string openCloudUserId, CancellationToken ct = default)
    {
        var hasManager = permissionsByDrive.TryGetValue(driveId, out var permissions)
            && permissions.Any(p => p.OpenCloudUserId == openCloudUserId && p.Role == StorageRole.Manager);
        return Task.FromResult(hasManager);
    }

    public Task<IReadOnlyList<DrivePermissionInfo>> ListPermissionsAsync(string driveId, CancellationToken ct = default)
    {
        var result = permissionsByDrive.TryGetValue(driveId, out var permissions)
            ? permissions.Select(p => new DrivePermissionInfo(p.PermissionId, p.OpenCloudUserId, p.Role != StorageRole.Viewer)).ToList()
            : [];
        return Task.FromResult<IReadOnlyList<DrivePermissionInfo>>(result);
    }
}
