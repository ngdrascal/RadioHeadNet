namespace Rf69CaptureAnalyzer;

internal class DisabledState(Analyzer analyzer, State state) : State(analyzer, state)
{
    public override State ProcessRecord(RecordTypes recordTypes, byte? mosi, byte? miso)
    {
        if (recordTypes == RecordTypes.Enabled)
        {
            return new EnabledState(Analyzer, this);
        }
        else
        {
            return new ErrorState(Analyzer, this);
        }
    }
}