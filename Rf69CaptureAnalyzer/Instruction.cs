namespace Rf69CaptureAnalyzer;

internal class Instruction(byte commandValue)
{
    private readonly List<byte> _data = [];

    public void AddData(byte data)
    {
        _data.Add(data);
    }

    public Operations Operation => commandValue.OperationOf();
    public string RegisterName => commandValue.RegisterNameOf();
    public byte RegisterAddress => commandValue.RegisterAddress();
    public byte[] Data => _data.ToArray();
}
