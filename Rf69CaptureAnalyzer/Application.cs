using System.Text;

namespace Rf69CaptureAnalyzer;

internal class Application(Analyzer analyzer)
{
    public void Run(FileStream fileStream, ParseOptions options)
    {
        var fileData = ParseFile(fileStream, options);

        foreach (var row in fileData)
        {
            analyzer.ProcessRecords(row.recordType, row.Mosi, row.Miso);
        }

        foreach (var instruction in analyzer.Instructions)
        {
            Console.WriteLine(instruction.Print());
        }
    }

    private static List<(RecordType recordType, byte? Mosi, byte? Miso)> ParseFile(FileStream fileStream, ParseOptions options)
    {
        var data = new List<(RecordType RecordType, byte? Mosi, byte? Miso)>();

        using StreamReader sr = new(fileStream);
        if (!sr.EndOfStream)
            sr.ReadLine(); // Skip header

        while (!sr.EndOfStream)
        {
            var line = sr.ReadLine() ?? string.Empty;
            var (recordType, mosi, miso) = line.ParseLine(options);
            data.Add((recordType, mosi, miso));
        }

        return data;
    }
}
