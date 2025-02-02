namespace Rf69CaptureAnalyzer;

internal class DisabledState(Analyzer analyzer, State state) : State(analyzer, state)
{
    public override State ProcessRecord(RecordType recordType, byte? mosi, byte? miso)
    {
        if (recordType == RecordType.Enabled)
        {
            return new EnabledState(Analyzer, this);
        }
        else
        {
            return new ErrorState(Analyzer, this);
        }
    }
}