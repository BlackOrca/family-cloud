namespace FamilyCloud.Core.Security;

/// <summary>
/// Writes the single "username:bcrypt-hash" line the bundled Radicale container's htpasswd auth
/// expects (see the mounted /config/config in AppHost.cs). Radicale itself offers no API or env-var
/// way to manage users — only a mounted htpasswd file — so the server owns writing it directly.
/// </summary>
public class RadicaleCredentialProvisioner(string htpasswdFilePath) : IRadicaleCredentialProvisioner
{
    public async Task ProvisionAsync(string username, string plainPassword, CancellationToken ct = default)
    {
        var hash = BCrypt.Net.BCrypt.HashPassword(plainPassword);
        var directory = Path.GetDirectoryName(Path.GetFullPath(htpasswdFilePath))
            ?? throw new InvalidOperationException($"Could not determine a directory from htpasswd path '{htpasswdFilePath}'.");
        Directory.CreateDirectory(directory);

        // Single-user file: always rewritten wholesale rather than parsed/patched, since the bundled
        // Radicale only ever has this one managed credential.
        var tempPath = $"{htpasswdFilePath}.tmp";
        await File.WriteAllTextAsync(tempPath, $"{username}:{hash}\n", ct);
        File.Move(tempPath, htpasswdFilePath, overwrite: true);
    }
}
