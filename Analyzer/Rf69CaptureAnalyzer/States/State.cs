namespace Rf69CaptureAnalyzer;

internal abstract class State
{
    protected readonly Analyzer Analyzer;

    protected State(Analyzer analyzer, State state)
    {
        Analyzer = analyzer;
        Instructions = state.Instructions;
    }

    protected State(Analyzer analyzer)
    {
        Analyzer = analyzer;
        Instructions = new List<Instruction>();
    }

    public abstract State ProcessRecord(RecordTypes recordTypes, byte? mosi, byte? miso);

    public List<Instruction> Instructions { get; }
}