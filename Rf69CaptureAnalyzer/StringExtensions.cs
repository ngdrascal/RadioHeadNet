namespace Rf69CaptureAnalyzer;

internal static class StringExtensions
{
    public static (RecordType recordType, byte? Mosi, byte? Miso) ParseLine(this string line, ParseOptions options)
    {
        var parts = line.Split(',');

        var recordType = parts[options.RecordTypeIndex].ToRecordType();
        var mosi = parts[options.MosiIndex].ToByteOrNull();
        var miso = parts[options.MisoIndex].ToByteOrNull();

        return (recordType, mosi, miso);
    }

    private static byte? ToByteOrNull(this string str)
    {
        return string.IsNullOrEmpty(str) ? null : Convert.ToByte(str, 16);
    }

    private static RecordType ToRecordType(this string str)
    {
        var cleansed = str.Replace('"', ' ').Trim();

        return cleansed.ToLower() switch
        {
            "disable" => RecordType.Disabled,
            "enable" => RecordType.Enabled,
            "result" => RecordType.Result,
            _ => throw new InvalidOperationException($"Unrecognized record type: {(string.IsNullOrEmpty(str) ? "<null>" : str)}")

        };
    }
}
