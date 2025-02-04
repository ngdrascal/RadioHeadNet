namespace Rf69CaptureAnalyzer;

internal class StartState(Analyzer analyzer) : State(analyzer)
{
    public override State ProcessRecord(RecordTypes recordTypes, byte? mosi, byte? miso)
    {
        return recordTypes == RecordTypes.Enabled ?
            new EnabledState(Analyzer, this) :
            this;
    }
}