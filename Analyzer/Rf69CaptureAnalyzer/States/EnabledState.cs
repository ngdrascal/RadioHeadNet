namespace Rf69CaptureAnalyzer;

internal class EnabledState(Analyzer analyzer, State state) : State(analyzer, state)
{
    public override State ProcessRecord(CaptureRecord record)
    {
        switch (record.RecordType)
        {
            case RecordTypes.Result: // Start of a new instruction
                if (!record.Mosi.HasValue)
                    return new ErrorState(Analyzer, this);

                Instructions.Add(new Instruction(Index++, record.Start, record.Mosi.Value));
                return new ResultState(Analyzer, this);

            case RecordTypes.Disabled:
                return new DisabledState(Analyzer, this);

            default:
                return new ErrorState(Analyzer, this);
        }
    }
}