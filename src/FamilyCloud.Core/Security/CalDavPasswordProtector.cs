using Microsoft.AspNetCore.DataProtection;

namespace FamilyCloud.Core.Security;

public class CalDavPasswordProtector : ICalDavPasswordProtector
{
    private readonly IDataProtector protector;

    public CalDavPasswordProtector(IDataProtectionProvider provider)
    {
        protector = provider.CreateProtector("FamilyCloud.CalDavAppPassword");
    }

    public string Encrypt(string plainPassword) => protector.Protect(plainPassword);

    public string Decrypt(string encryptedPassword) => protector.Unprotect(encryptedPassword);
}
