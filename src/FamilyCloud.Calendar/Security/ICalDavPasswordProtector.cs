namespace FamilyCloud.Calendar.Security;

/// <summary>Encrypts/decrypts CalDAV app passwords at rest via ASP.NET Core Data Protection.</summary>
public interface ICalDavPasswordProtector
{
    string Encrypt(string plainPassword);

    string Decrypt(string encryptedPassword);
}
