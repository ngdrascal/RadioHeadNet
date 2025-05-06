namespace Rf69CaptureAnalyzer;

internal class ResultState(Analyzer analyzer, State state) : State(analyzer, state)
{
    public override State ProcessRecord(CaptureRecord record)
    {
        switch (record.RecordType)
        {
            case RecordTypes.Result when !record.Mosi.HasValue || !record.Miso.HasValue:
                return new ErrorState(Analyzer, this);

            case RecordTypes.Result:
                var data = Instructions[^1].Operation == Operations.Write ? (byte)record.Mosi : (byte)record.Miso;
                Instructions[^1].AddData(data);
                return this;

            case RecordTypes.Disabled:
                return new DisabledState(Analyzer, this);

            default:
                return new ErrorState(Analyzer, this);
        }
    }
}