namespace Rf69CaptureAnalyzer;

internal class Application(Analyzer analyzer)
{
    public void Run(FileStream fileStream, ParseOptions options)
    {
        var fileData = CsvFileParser.Parse(fileStream, options);

        foreach (var row in fileData)
        {
            analyzer.ProcessRecords(row.recordType, row.Mosi, row.Miso);
        }

        foreach (var instruction in analyzer.Instructions)
        {
            Console.WriteLine(instruction.Print());
        }
    }
}
