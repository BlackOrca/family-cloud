using FamilyCloud.Calendar.Security;

namespace FamilyCloud.Calendar.Tests.Security;

public class RadicaleCredentialProvisionerTests : IDisposable
{
    private readonly string tempDirectory = Path.Combine(Path.GetTempPath(), "familycloud-radicale-provisioner-tests", Guid.NewGuid().ToString("N"));

    private string HtpasswdPath => Path.Combine(tempDirectory, "auth", "users");

    [Fact]
    public async Task ProvisionAsync_writes_a_username_and_bcrypt_hash_line()
    {
        var sut = new RadicaleCredentialProvisioner(HtpasswdPath);

        await sut.ProvisionAsync("household", "correct horse battery staple");

        var content = await File.ReadAllTextAsync(HtpasswdPath);
        var line = Assert.Single(content.TrimEnd('\n').Split('\n'));
        var parts = line.Split(':', 2);
        Assert.Equal("household", parts[0]);
        Assert.True(BCrypt.Net.BCrypt.Verify("correct horse battery staple", parts[1]));
    }

    [Fact]
    public async Task ProvisionAsync_creates_missing_parent_directories()
    {
        var sut = new RadicaleCredentialProvisioner(HtpasswdPath);

        await sut.ProvisionAsync("household", "irrelevant");

        Assert.True(File.Exists(HtpasswdPath));
    }

    [Fact]
    public async Task ProvisionAsync_overwrites_a_previous_credential_for_the_same_file()
    {
        var sut = new RadicaleCredentialProvisioner(HtpasswdPath);

        await sut.ProvisionAsync("household", "old-password");
        await sut.ProvisionAsync("household", "new-password");

        var content = await File.ReadAllTextAsync(HtpasswdPath);
        var line = Assert.Single(content.TrimEnd('\n').Split('\n'));
        var hash = line.Split(':', 2)[1];
        Assert.False(BCrypt.Net.BCrypt.Verify("old-password", hash));
        Assert.True(BCrypt.Net.BCrypt.Verify("new-password", hash));
    }

    public void Dispose()
    {
        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }
}
