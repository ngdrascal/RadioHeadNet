namespace Rf69CaptureAnalyzer;

internal static class ByteExtensions
{
    public static Operations OperationOf(this byte mosi)
    {
        return (mosi & 0b1000_0000) == 0 ? Operations.Read : Operations.Write;
    }

    public static string RegisterNameOf(this byte mosi)
    {
        return Enum.GetName(typeof(Registers), mosi & 0b01111111) ?? "<undefined>";
    }

    public static byte RegisterAddress(this byte mosi)
    {
        return (byte)(mosi & 0x7F);
    }
}
