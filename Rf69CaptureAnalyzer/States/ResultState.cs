namespace Rf69CaptureAnalyzer;

internal class ResultState(Analyzer analyzer, State state) : State(analyzer, state)
{
    public override State ProcessRecord(RecordType recordType, byte? mosi, byte? miso)
    {
        switch (recordType)
        {
            case RecordType.Result when !miso.HasValue:
                return new ErrorState(Analyzer, this);

            case RecordType.Result:
                Instructions[^1].AddData(miso.Value);

                return this;
            case RecordType.Disabled:
                return new DisabledState(Analyzer, this);

            default:
                return new ErrorState(Analyzer, this);
        }
    }
}