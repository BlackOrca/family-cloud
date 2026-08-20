using Microsoft.AspNetCore.DataProtection;

namespace FamilyCloud.Photos.Security;

public class ImmichCredentialProtector : IImmichCredentialProtector
{
    private readonly IDataProtector protector;

    public ImmichCredentialProtector(IDataProtectionProvider provider)
    {
        protector = provider.CreateProtector("FamilyCloud.ImmichApiKey");
    }

    public string Encrypt(string plainSecret) => protector.Protect(plainSecret);

    public string Decrypt(string encryptedSecret) => protector.Unprotect(encryptedSecret);
}
