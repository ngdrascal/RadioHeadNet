using System.Text;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Rf69CaptureAnalyzer;

internal static class InstructionExtensions
{
    private static readonly Dictionary<Registers, int[]> Formats = new()
    {
        { Registers.OpMode, [1, 1, 1, 3, 2] },                // 0x01
        { Registers.PaLevel, [1, 1, 1, 5] },                  // 0x17
        { Registers.RxBw, [1, 1, 3, 3] },                     // 0x19
        { Registers.AfcBw, [3, 2, 3] },                       // 0x1A
        { Registers.IrqFlags1, [1, 1, 1, 1, 1, 1, 1, 1] },    // 0x27
        { Registers.SyncConfig, [1, 1, 3, 3] },               // 0x2E
        { Registers.PacketConfig1, [1, 2, 1, 1, 2, 1] },      // 0x37
        { Registers.FifoThresh, [1, 7]},                      // 0x3c
        { Registers.PacketConfig2, [4, 1, 1, 1, 1]},          // 0x3D
    };

    public static string Print(this Instruction instruction)
    {
        var opMnemonic = instruction.Operation.ToString()[0];
        var sb = new StringBuilder($"{opMnemonic}: {instruction.RegisterName,-14}");

        sb.Append(PrintData(instruction.Register, instruction.Data[0]));

        var burstReg = instruction.Register;
        foreach (var data in instruction.Data[1..])
        {
            if (instruction.Register != Registers.Fifo)
               burstReg += 1;
            sb.Append(PrintData(burstReg, data));
        }

        return sb.ToString();
    }

    private static string PrintData(Registers register, byte value)
    {
        var sb = new StringBuilder();
        if (Formats.TryGetValue(register, out var format0))
            sb.Append(value.AsBitFields(format0));
        else
            sb.Append($" {value:X2}");

        return sb.ToString();
    }

    private static string AsBitFields(this byte value, int[] sizes)
    {
        if (sizes.Sum() != 8)
            throw new ArgumentException("The sum of the sizes must be 8.", nameof(sizes));

        var binary = Convert.ToString(value, 2).PadLeft(8, '0');
        var sb = new StringBuilder(" [");

        var startIndex = 0;
        var bitStrings = new List<string>();
        foreach (var size in sizes)
        {
            bitStrings.Add(binary.Substring(startIndex, size));
            startIndex += size;
        }

        sb.AppendJoin(' ', bitStrings);
        sb.Append(']');

        return sb.ToString();
    }
}
