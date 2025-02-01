namespace Rf69CaptureAnalyzer;

internal enum RecordType { Disabled, Enabled, Result }

internal abstract class State
{
    protected readonly Analyzer Analyzer;

    protected State(Analyzer analyzer, State state)
    {
        Analyzer = analyzer;
        Instructions = state.Instructions;
    }

    protected State(Analyzer analyzer)
    {
        Analyzer = analyzer;
        Instructions = new List<Instruction>();
    }

    public abstract State ProcessRecord(RecordType recordType, byte? mosi, byte? miso);

    public List<Instruction> Instructions { get; }
}

internal class StartState(Analyzer analyzer) : State(analyzer)
{
    public override State ProcessRecord(RecordType recordType, byte? mosi, byte? miso)
    {
        return recordType == RecordType.Enabled ?
            new EnabledState(Analyzer, this) :
            this;
    }
}

internal class EnabledState(Analyzer analyzer, State state) : State(analyzer, state)
{
    public override State ProcessRecord(RecordType recordType, byte? mosi, byte? miso)
    {
        switch (recordType)
        {
            case RecordType.Result: // Start of a new instruction
                if (!mosi.HasValue)
                    return new ErrorState(Analyzer, this);

                Instructions.Add(new Instruction(mosi.Value));
                return new ResultState(Analyzer, this);

            case RecordType.Disabled:
                return new DisabledState(Analyzer, this);

            default:
                return new ErrorState(Analyzer, this);
        }
    }
}

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

internal class DisabledState(Analyzer analyzer, State state) : State(analyzer, state)
{
    public override State ProcessRecord(RecordType recordType, byte? mosi, byte? miso)
    {
        if (recordType == RecordType.Enabled)
        {
            return new EnabledState(Analyzer, this);
        }
        else
        {
            return new ErrorState(Analyzer, this);
        }
    }
}

internal class ErrorState(Analyzer analyzer, State state) : State(analyzer, state)
{
    public override State ProcessRecord(RecordType recordType, byte? mosi, byte? miso)
    {
        return this;
    }
}