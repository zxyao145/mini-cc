using System.Security.Cryptography;
using System.Text;

namespace MiniCc.Api.Shared.Utils;

public class KeyGen
{
    private const string DefaultCharSet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

    public static string Generate(int length = 32, string? charSet = null)
    {
        if (length <= 0) throw new ArgumentException("Length must be greater than 0.", nameof(length));

        charSet ??= DefaultCharSet;
        var key = new StringBuilder(length);
        using var rng = RandomNumberGenerator.Create();

        var buffer = new byte[sizeof(uint)];

        for (int i = 0; i < length; i++)
        {
            rng.GetBytes(buffer);
            uint num = BitConverter.ToUInt32(buffer, 0);
            key.Append(charSet[(int)(num % (uint)charSet.Length)]);
        }

        return key.ToString();
    }
}
