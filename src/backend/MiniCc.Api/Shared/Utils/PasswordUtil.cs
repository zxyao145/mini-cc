using System.Security.Cryptography;
using System.Text;

namespace MiniCc.Api.Shared.Utils;

public sealed class PasswordUtil
{
    #region asp.net core中的密码加密思路

    /// <summary>
    /// 计算密码的哈希(asp.net core)
    /// </summary>
    /// <param name="password"></param>
    /// <param name="saltSize"></param>
    /// <returns></returns>
    public static string HashPassword(string? password, int saltSize = 16)
    {
        if (password != null)
        {
            byte[] salt;
            byte[] bytes;
            using (Rfc2898DeriveBytes rfc2898DeriveBytes
                   = new(password, saltSize, 1000, HashAlgorithmName.SHA1))
            {
                salt = rfc2898DeriveBytes.Salt;
                bytes = rfc2898DeriveBytes.GetBytes(32);
            }

            byte[] array = new byte[49];
            Buffer.BlockCopy(salt, 0, array, 1, 16);
            Buffer.BlockCopy(bytes, 0, array, 17, 32);

            return Convert.ToBase64String(array);
        }
        else
        {
            throw new ArgumentNullException("password");
        }
    }

    /// <summary>
    /// 验证密码是否正确(asp.net core)
    /// </summary>
    /// <param name="hashedPassword"></param>
    /// <param name="password"></param>
    /// <returns></returns>
    public static bool VerifyHashedPassword(string hashedPassword, string password, int saltSize = 16)
    {
        if (hashedPassword == null)
        {
            return false;
        }
        if (password == null)
        {
            throw new ArgumentNullException("password");
        }

        byte[] array = Convert.FromBase64String(hashedPassword);
        if (array.Length != 49 || array[0] != 0)
        {
            return false;
        }

        byte[] array2 = new byte[saltSize];
        Buffer.BlockCopy(array, 1, array2, 0, array2.Length);
        byte[] array3 = new byte[32];
        Buffer.BlockCopy(array, 17, array3, 0, array3.Length);
        byte[] bytes;
        using (Rfc2898DeriveBytes rfc2898DeriveBytes
               = new(password, array2, 1000, HashAlgorithmName.SHA1))
        {
            bytes = rfc2898DeriveBytes.GetBytes(32);
        }

        return ByteArraysEqual(array3, bytes);
    }

    #endregion

    public static string GetPwd(string password, string salt = "")
    {
        using var md5 = MD5.Create();

        if (!string.IsNullOrEmpty(salt))
        {
            password = password + "{" + salt.Trim() + "}";
        }

        var bt = Encoding.Default.GetBytes(password);
        var b = md5.ComputeHash(bt);

        return b.Aggregate("", (current, t) => current + t.ToString("X").PadLeft(2, '0'));
    }

    public static string GetPwdSha1(string password, string salt = "")
    {
        using var hash = SHA1.Create();

        if (!string.IsNullOrEmpty(salt))
        {
            password = password + "{" + salt.Trim() + "}";
        }

        var bt = Encoding.Default.GetBytes(password);
        var b = hash.ComputeHash(bt);

        return b.Aggregate("", (current, t) => current + t.ToString("X").PadLeft(2, '0'));
    }

    /// <summary>
    /// 逐字节比较两个byte数组是否相等
    /// </summary>
    /// <param name="bytes1"></param>
    /// <param name="bytes2"></param>
    /// <returns></returns>
    private static bool ByteArraysEqual(byte[]? bytes1, byte[]? bytes2)
    {
        if (bytes1 == null && bytes2 == null)
        {
            return true;
        }
        if (bytes1 == null || bytes2 == null)
        {
            return false;
        }

        if (bytes1.Length != bytes2.Length)
        {
            return false;
        }

        return bytes1.SequenceEqual(bytes2);
    }
}