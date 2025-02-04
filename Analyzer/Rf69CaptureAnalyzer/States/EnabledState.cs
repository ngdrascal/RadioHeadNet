namespace Rf69CaptureAnalyzer;

internal class EnabledState(Analyzer analyzer, State state) : State(analyzer, state)
{
    public override State ProcessRecord(RecordTypes recordTypes, byte? mosi, byte? miso)
    {
        switch (recordTypes)
        {
            case RecordTypes.Result: // Start of a new instruction
                if (!mosi.HasValue)
                    return new ErrorState(Analyzer, this);

                Instructions.Add(new Instruction(mosi.Value));
                return new ResultState(Analyzer, this);

            case RecordTypes.Disabled:
                return new DisabledState(Analyzer, this);

            default:
                return new ErrorState(Analyzer, this);
        }
    }
}