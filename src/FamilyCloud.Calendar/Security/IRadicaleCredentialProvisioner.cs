namespace FamilyCloud.Calendar.Security;

/// <summary>Keeps the bundled Radicale instance's htpasswd file in sync with the managed credential.</summary>
public interface IRadicaleCredentialProvisioner
{
    Task ProvisionAsync(string username, string plainPassword, CancellationToken ct = default);
}
