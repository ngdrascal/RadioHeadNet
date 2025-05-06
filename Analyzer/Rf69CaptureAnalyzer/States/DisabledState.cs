namespace Rf69CaptureAnalyzer;

internal class DisabledState(Analyzer analyzer, State state) : State(analyzer, state)
{
    public override State ProcessRecord(CaptureRecord record)
    {
        if (record.RecordType == RecordTypes.Enabled)
        {
            return new EnabledState(Analyzer, this);
        }

        return new ErrorState(Analyzer, this);
    }
}