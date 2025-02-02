using System.Text;

namespace Rf69CaptureAnalyzer;

internal static class InstructionExtensions
{
    public static string Print(this Instruction instruction)
    {
        var opMnemonic = instruction.Operation.ToString()[0];
        var sb = new StringBuilder($"{opMnemonic}: {instruction.RegisterName,-14}");
        foreach (var data in instruction.Data)
        {
            switch (instruction.Register)
            {
                case Registers.OpMode:
                    sb.Append($" {data.AsOpMode()}");
                    break;
                case Registers.IrqFlags1:
                    sb.Append($" {data.AsIrqFlags1()}");
                    break;
                default:
                    sb.Append($" {data:X2}");
                    break;
            }
        }

        return sb.ToString();
    }

    private static string AsOpMode(this byte miso)
    {
        var binary = Convert.ToString(miso, 2).PadLeft(8, '0');

        var sequenceOff = binary[..1];
        var listenOn = binary.Substring(1, 1);
        var listenAbort = binary.Substring(2, 1);
        var mode = binary.Substring(3, 3);
        var unused = binary.Substring(6, 2);

        return $"[{sequenceOff} {listenOn} {listenAbort} {mode} {unused}]";
    }

    private static string AsIrqFlags1(this byte miso)
    {
        var binary = Convert.ToString(miso, 2).PadLeft(8, '0');

        var modeReady = binary.Substring(0, 1);
        var rxReady = binary.Substring(1, 1);
        var txReady = binary.Substring(2, 1);
        var pllLock = binary.Substring(3, 1);
        var rssi = binary.Substring(4, 1);
        var timeout = binary.Substring(5, 1);
        var autoMode = binary.Substring(6, 1);
        var syncAddressMatch = binary.Substring(7, 1);

        return $"[{modeReady} {rxReady} {txReady} {pllLock} {rssi} {timeout} {autoMode} {syncAddressMatch}]";
    }
}