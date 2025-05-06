namespace Rf69CaptureAnalyzer;

internal class Analyzer
{
    private State _state;

    public Analyzer()
    {
        _state = new StartState(this);
    }

    public void ProcessRecords(CaptureRecord record)
    {
        _state = _state.ProcessRecord(record);
    }

    public IEnumerable<Instruction> Instructions => _state.Instructions;
}