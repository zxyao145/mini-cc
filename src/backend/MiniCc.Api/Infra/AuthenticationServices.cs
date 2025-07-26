using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;

namespace MiniCc.Api.Infra;


// 1. 创建加密服务
public interface IEncryptionService
{
    string Encrypt(string plainText);
    string Decrypt(string cipherText);
}

public class AesKey
{
    [Required]
    public string Id { get; set; } = ""; // 版本号或标识符
    [Required]
    public string Key { get; set; } = ""; // Base64 编码的 AES 密钥
    [Required]
    public string IV { get; set; } = ""; // Base64 编码的初始化向量 (IV)

    public byte[] KeyBytes { get; private set; } = new byte[] { };
    public byte[] IVBytes { get; private set; } = new byte[] { };

    public void CalcBytes()
    {
        KeyBytes = Convert.FromBase64String(Key);
        IVBytes = Convert.FromBase64String(IV);
    }
}

public class AesEncryptionService : IEncryptionService
{
    private readonly List<AesKey> _keys;
    private readonly Dictionary<string, AesKey> _keyDict;

    public AesEncryptionService(IConfiguration configuration)
    {
        var aesKeys = configuration.GetSection("AesKeys");
        List<AesKey> keys = aesKeys.Get<List<AesKey>>() ?? new List<AesKey>();
        _keys = keys
            .Where(x => !string.IsNullOrWhiteSpace(x.Id)
                && !string.IsNullOrWhiteSpace(x.Key)
                && !string.IsNullOrWhiteSpace(x.IV)
            )
            .Select(x =>
            {
                x.CalcBytes();
                return x;
            })
            .OrderByDescending(x => x.Id)
            .ToList();

        _keyDict = _keys.ToDictionary(x => x.Id, x => x);
    }

    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return plainText;

        var keyIndex = plainText.GetHashCode() % _keys.Count;
        var aesKey = _keys[keyIndex];

        using var aes = Aes.Create();
        aes.Key = aesKey.KeyBytes;
        aes.IV = aesKey.IVBytes;

        using var encryptor = aes.CreateEncryptor();
        using var msEncrypt = new MemoryStream();
        using var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write);
        using var swEncrypt = new StreamWriter(csEncrypt);

        swEncrypt.Write(plainText);
        swEncrypt.Close();

        return aesKey.Id + "_" + Convert.ToBase64String(msEncrypt.ToArray());
    }

    public string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText))
            return cipherText;

        var cipherInfo = cipherText.Split("_", 2);
        var aesKey = _keyDict[cipherInfo[0]];

        using var aes = Aes.Create();
        aes.Key = aesKey.KeyBytes;
        aes.IV = aesKey.IVBytes;

        using var decryptor = aes.CreateDecryptor();
        using var msDecrypt = new MemoryStream(Convert.FromBase64String(cipherInfo[1]));
        using var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read);
        using var srDecrypt = new StreamReader(csDecrypt);

        return srDecrypt.ReadToEnd();
    }
}
