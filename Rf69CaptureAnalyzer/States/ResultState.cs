namespace Rf69CaptureAnalyzer;

internal class ResultState(Analyzer analyzer, State state) : State(analyzer, state)
{
    public override State ProcessRecord(RecordTypes recordTypes, byte? mosi, byte? miso)
    {
        switch (recordTypes)
        {
            case RecordTypes.Result when !miso.HasValue:
                return new ErrorState(Analyzer, this);

            case RecordTypes.Result:
                Instructions[^1].AddData(miso.Value);

                return this;
            case RecordTypes.Disabled:
                return new DisabledState(Analyzer, this);

            default:
                return new ErrorState(Analyzer, this);
        }
    }
}