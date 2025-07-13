using System.Security.Cryptography;

namespace MiniCc.Api.Shared.Data.Common;

public static class UuidUtil
{
    public static Guid NewGuidV7()
    {
        return NewUuidV7();
    }

    private const byte Variant10xxMask = 0xC0;
    private const byte Variant10xxValue = 0x80;

    private const ushort VersionMask = 0xF000;
    private const ushort Version7Value = 0x7000;

    private static Guid NewUuidV7()
    {
        var unixTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        byte[] timeBytes = BitConverter.GetBytes(unixTime);

        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(timeBytes);
        }

        byte[] guidBytes = new byte[16];
        Array.Copy(timeBytes, 0, guidBytes, 0, 6);

        guidBytes[6] = (byte)(guidBytes[6] & 0x0F | 0x70);
        guidBytes[8] = (byte)(guidBytes[8] & 0x3F | 0x80);

        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(guidBytes, 10, 6);
        }

        return new Guid(guidBytes);
    }
}