namespace Rf69CaptureAnalyzer;

internal class StartState(Analyzer analyzer) : State(analyzer)
{
    public override State ProcessRecord(CaptureRecord record)
    {
        return record.RecordType == RecordTypes.Enabled ?
            new EnabledState(Analyzer, this) :
            this;
    }
}