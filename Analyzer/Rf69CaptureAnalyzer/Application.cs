namespace Rf69CaptureAnalyzer;

internal class Application(Analyzer analyzer)
{
    public void Run(string fileName, FileStream fileStream, ParseOptions options)
    {
        var fileData = CsvFileParser.Parse(fileStream, options);

        foreach (var row in fileData)
        {
            analyzer.ProcessRecords(row.recordType, row.Mosi, row.Miso);
        }

        Console.WriteLine($"#: {fileName}");
        foreach (var instruction in analyzer.Instructions)
        {
            Console.WriteLine(instruction.Print());
        }
    }
}
