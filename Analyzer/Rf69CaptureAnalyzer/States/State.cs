namespace Rf69CaptureAnalyzer;

internal abstract class State
{
    protected readonly Analyzer Analyzer;
    protected int Index;

    protected State(Analyzer analyzer, State state)
    {
        Analyzer = analyzer;
        Instructions = state.Instructions;
        Index = state.Index;
    }

    protected State(Analyzer analyzer)
    {
        Analyzer = analyzer;
        Instructions = new List<Instruction>();
        Index = 1;
    }

    public abstract State ProcessRecord(CaptureRecord record);

    public List<Instruction> Instructions { get; }
}