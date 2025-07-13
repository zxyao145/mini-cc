namespace MiniCc.Api.Infra;

public interface IEncryptionService
{
    string Encrypt(string plainText);

    string Decrypt(string cipherText);
}

public class AesEncryptionService : IEncryptionService
{
    public string Encrypt(string plainText)
    {
        // Implement AES encryption
        return plainText; // Placeholder
    }

    public string Decrypt(string cipherText)
    {
        // Implement AES decryption
        return cipherText; // Placeholder
    }
}

