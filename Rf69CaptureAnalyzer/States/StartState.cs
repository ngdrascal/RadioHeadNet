namespace Rf69CaptureAnalyzer;

internal class StartState(Analyzer analyzer) : State(analyzer)
{
    public override State ProcessRecord(RecordType recordType, byte? mosi, byte? miso)
    {
        return recordType == RecordType.Enabled ?
            new EnabledState(Analyzer, this) :
            this;
    }
}