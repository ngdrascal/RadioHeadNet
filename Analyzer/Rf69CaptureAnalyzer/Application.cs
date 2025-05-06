namespace Rf69CaptureAnalyzer;

internal class Application(Analyzer analyzer)
{
    public void Run(string fileName, FileStream fileStream, ParseOptions options)
    {
        var fileData = CsvFileParser.Parse(fileStream, options);

        var index = 1;
        foreach (var row in fileData)
        {
            analyzer.ProcessRecords(row);
        }

        Console.WriteLine($"#: {fileName}");

        Instruction? lastInstruction = null;
        foreach (var instruction in analyzer.Instructions)
        {
            if (instruction.Register == lastInstruction?.Register &&
                instruction.Operation == lastInstruction.Operation &&
                instruction.Data.Length == lastInstruction.Data.Length &&
                instruction.Data[0] == lastInstruction.Data[0])
            {
                lastInstruction = instruction;
                continue;
            }

            lastInstruction = instruction;

            Console.WriteLine(instruction.Print());
        }
    }
}