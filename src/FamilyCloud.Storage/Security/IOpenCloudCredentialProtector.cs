namespace FamilyCloud.Storage.Security;

public interface IOpenCloudCredentialProtector
{
    string Encrypt(string plainSecret);

    string Decrypt(string encryptedSecret);
}
