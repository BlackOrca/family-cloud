namespace FamilyCloud.Photos.Security;

/// <summary>Encrypts/decrypts the shared Immich API key at rest via ASP.NET Core Data Protection — same
/// pattern as <c>ICalDavPasswordProtector</c> in FamilyCloud.Calendar, kept local to this feature rather
/// than shared, consistent with how Data Protection usage is scoped per owning feature project.</summary>
public interface IImmichCredentialProtector
{
    string Encrypt(string plainSecret);

    string Decrypt(string encryptedSecret);
}
