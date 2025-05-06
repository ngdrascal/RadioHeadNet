namespace Rf69CaptureAnalyzer;

internal class Instruction
{
    private readonly List<byte> _data = [];
    private readonly byte _commandValue;

    public Instruction(int index, double start, byte commandValue)
    {
        Index = index;
        _commandValue = commandValue;
        Start = start;
    }

    public void AddData(byte data)
    {
        _data.Add(data);
    }

    public int Index { get; }

    public double Start { get; }

    public Operations Operation => _commandValue.OperationOf();

    public Registers Register => (Registers)_commandValue.RegisterAddress();
    
    public string RegisterName => _commandValue.RegisterNameOf();
    
    public byte RegisterAddress => _commandValue.RegisterAddress();
    
    public byte[] Data => _data.ToArray();
}
