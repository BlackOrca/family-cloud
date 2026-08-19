using Microsoft.AspNetCore.DataProtection;

namespace OurLive.Core.Security;

public class CalDavPasswordProtector : ICalDavPasswordProtector
{
    private readonly IDataProtector protector;

    public CalDavPasswordProtector(IDataProtectionProvider provider)
    {
        protector = provider.CreateProtector("OurLive.CalDavAppPassword");
    }

    public string Encrypt(string plainPassword) => protector.Protect(plainPassword);

    public string Decrypt(string encryptedPassword) => protector.Unprotect(encryptedPassword);
}
