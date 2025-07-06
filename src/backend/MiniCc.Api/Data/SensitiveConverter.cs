using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using MiniCc.Api.Services;


namespace MiniCc.Api.Data;

// 2. 创建加密值转换器
public class SensitiveConverter : ValueConverter<string, string>
{
    public SensitiveConverter(IEncryptionService encryptionService)
        : base(
            plainText => encryptionService.Encrypt(plainText),
            cipherText => encryptionService.Decrypt(cipherText))
    {
    }
}