using Microsoft.AspNetCore.DataProtection;

namespace FamilyCloud.Storage.Security;

public class OpenCloudCredentialProtector : IOpenCloudCredentialProtector
{
    private readonly IDataProtector protector;

    public OpenCloudCredentialProtector(IDataProtectionProvider provider)
    {
        protector = provider.CreateProtector("FamilyCloud.OpenCloudPassword");
    }

    public string Encrypt(string plainSecret) => protector.Protect(plainSecret);

    public string Decrypt(string encryptedSecret) => protector.Unprotect(encryptedSecret);
}
