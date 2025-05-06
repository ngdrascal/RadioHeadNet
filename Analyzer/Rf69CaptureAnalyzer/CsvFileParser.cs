namespace Rf69CaptureAnalyzer;

internal static class CsvFileParser
{
    public static List<CaptureRecord> Parse(FileStream fileStream, ParseOptions options)
    {
        var data = new List<CaptureRecord>();

        using StreamReader sr = new(fileStream);
        if (!sr.EndOfStream)
            sr.ReadLine(); // Skip header

        while (!sr.EndOfStream)
        {
            var line = sr.ReadLine() ?? string.Empty;
            var record = line.ParseLine(options);
            data.Add(record);
        }

        return data;
    }

    private static CaptureRecord ParseLine(this string line, ParseOptions options)
    {
        var parts = line.Split(',');

        var recordType = parts[options.RecordTypeIndex].ToRecordType();
        var start = double.Parse(parts[options.StartIndex].Trim());
        var mosi = parts[options.MosiIndex].ToByteOrNull();
        var miso = parts[options.MisoIndex].ToByteOrNull();

        return new CaptureRecord(recordType, start, mosi, miso);
    }

    private static byte? ToByteOrNull(this string str)
    {
        return string.IsNullOrEmpty(str) ? null : Convert.ToByte(str, 16);
    }

    private static RecordTypes ToRecordType(this string str)
    {
        var cleansed = str.Replace('"', ' ').Trim();

        return cleansed.ToLower() switch
        {
            "disable" => RecordTypes.Disabled,
            "enable" => RecordTypes.Enabled,
            "result" => RecordTypes.Result,
            "error" => RecordTypes.Error,
            _ => throw new InvalidOperationException($"Unrecognized record type: {(string.IsNullOrEmpty(str) ? "<null>" : str)}")

        };
    }
}