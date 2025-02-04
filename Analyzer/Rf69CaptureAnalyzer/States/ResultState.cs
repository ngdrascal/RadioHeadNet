namespace Rf69CaptureAnalyzer;

internal class ResultState(Analyzer analyzer, State state) : State(analyzer, state)
{
    public override State ProcessRecord(RecordTypes recordTypes, byte? mosi, byte? miso)
    {
        switch (recordTypes)
        {
            case RecordTypes.Result when !mosi.HasValue || !miso.HasValue:
                return new ErrorState(Analyzer, this);

            case RecordTypes.Result:
                var data = Instructions[^1].Operation == Operations.Write ? (byte)mosi : (byte)miso;
                Instructions[^1].AddData(data);
                return this;

            case RecordTypes.Disabled:
                return new DisabledState(Analyzer, this);

            default:
                return new ErrorState(Analyzer, this);
        }
    }
}