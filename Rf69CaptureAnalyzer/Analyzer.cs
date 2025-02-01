namespace Rf69CaptureAnalyzer;

internal class Analyzer
{
    private State _state;

    public Analyzer()
    {
        _state = new StartState(this);
    }

    public void ProcessRecords(RecordType recordType, byte? mosi, byte? miso)
    {
        _state = _state.ProcessRecord(recordType, mosi, miso);
    }

    public IEnumerable<Instruction> Instructions => _state.Instructions;
}