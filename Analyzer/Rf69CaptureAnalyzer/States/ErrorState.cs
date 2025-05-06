namespace Rf69CaptureAnalyzer;

internal class ErrorState(Analyzer analyzer, State state) : State(analyzer, state)
{
    public override State ProcessRecord(CaptureRecord record)
    {
        return this;
    }
}