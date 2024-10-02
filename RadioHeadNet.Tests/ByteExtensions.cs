using System.Diagnostics.CodeAnalysis;

namespace RadioHeadNet.Tests;

[ExcludeFromCodeCoverage]
internal static class ByteExtensions
{
    public static bool IsSet(this byte b, int pos)
    {
        return (b & (1 << pos)) != 0;
    }

    public static bool IsClear(this byte b, int pos)
    {
        return (b & (1 << pos)) == 0;
    }

    public static byte ReadField(this byte value, int offset, int bitCount)
    {
        if (offset is < 0 or > 7)
            throw new ArgumentOutOfRangeException(nameof(offset), "Offset must be between 0 and 7");
        if (bitCount is < 1 or > 8)
            throw new ArgumentOutOfRangeException(nameof(bitCount), "Bit count must be between 1 and 8");
        if (offset + bitCount > 8)
            throw new ArgumentOutOfRangeException(nameof(bitCount), "Offset + bit count must be less than or equal to 8");

        var mask = 0;
        for (var i = 0; i < bitCount; i++)
        {
            mask <<= 1;
            mask |= 1;
        }
        mask <<= offset;

        return (byte)((value & mask) >> offset);
    }
}
