using System.Security.Cryptography;

namespace MiniCc.Api.Common;

public class UuidUtil
{
    public static Guid NewGuidV7()
    {
        return NewUuidV7();
        // return CreateVersion7(DateTimeOffset.UtcNow);
    }

    private const byte Variant10xxMask = 0xC0;
    private const byte Variant10xxValue = 0x80;

    private const ushort VersionMask = 0xF000;
    private const ushort Version7Value = 0x7000;

    /// <summary>
    /// dotnet 9 patch 
    /// </summary>
    /// <param name="timestamp"></param>
    /// <returns></returns>
    private static Guid CreateVersion7(DateTimeOffset timestamp)
    {
        Guid result = Guid.NewGuid();

        // 2^48 is roughly 8925.5 years, which from the Unix Epoch means we won't
        // overflow until around July of 10,895. So there isn't any need to handle
        // it given that DateTimeOffset.MaxValue is December 31, 9999. However, we
        // can't represent timestamps prior to the Unix Epoch since UUIDv7 explicitly
        // stores a 48-bit unsigned value, so we do need to throw if one is passed in.

        long unix_ts_ms = timestamp.ToUnixTimeMilliseconds();
        ArgumentOutOfRangeException.ThrowIfNegative(unix_ts_ms, nameof(timestamp));
        var a = (int)(unix_ts_ms >> 16);
        var b = (short)unix_ts_ms;

        // 从 Guid result 中解析出原来的 c、d 值
        var guidBytes = result.ToByteArray();
        var originC = BitConverter.ToInt16(guidBytes, 6); // 第6和7字节为c
        var originD = guidBytes[8]; // 第8字节为d

        var c = (short)(originC & ~VersionMask | Version7Value);
        var d = (byte)(originD & ~Variant10xxMask | Variant10xxValue);


        // 将 a, b, c, d 写回到 guidBytes 中
        Array.Copy(BitConverter.GetBytes(a), 0, guidBytes, 0, 4); // 前4字节为a
        Array.Copy(BitConverter.GetBytes(b), 0, guidBytes, 4, 2); // 第4和5字节为b
        Array.Copy(BitConverter.GetBytes(c), 0, guidBytes, 6, 2); // 第6和7字节为c
        guidBytes[8] = d; // 第8字节为d

        result.TryWriteBytes(guidBytes);
        return result;
    }


    private static Guid NewUuidV7()
    {
        var unixTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        byte[] timeBytes = BitConverter.GetBytes(unixTime);

        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(timeBytes);
        }

        byte[] guidBytes = new byte[16];
        Array.Copy(timeBytes, 0, guidBytes, 0, 6); // 前6字节是时间

        // 填充版本和变体位
        guidBytes[6] = (byte)(guidBytes[6] & 0x0F | 0x70); // 设置版本为7
        guidBytes[8] = (byte)(guidBytes[8] & 0x3F | 0x80); // 设置变体

        // 剩余字节用随机数填充
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(guidBytes, 10, 6);
        }

        return new Guid(guidBytes);
    }
}
