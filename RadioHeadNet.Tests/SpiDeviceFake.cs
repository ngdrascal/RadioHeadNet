using Microsoft.Extensions.Logging;
using System.Device.Gpio;
using System.Device.Spi;

namespace RadioHeadNet.Tests;

internal class SpiDeviceFake : SpiDevice
{
    private readonly SpiConnectionSettings _connectionSettings;
    private readonly Rf69RegistersFake _rf69Registers;
    private readonly GpioPin? _chipSelectPin;
    private readonly ILogger _logger;

    public SpiDeviceFake(SpiConnectionSettings connectionSettings, GpioController gpioController,
        Rf69RegistersFake rf69Registers, ILoggerFactory loggerFactory)
    {
        _connectionSettings = connectionSettings;
        _rf69Registers = rf69Registers;
        _logger = loggerFactory.CreateLogger(nameof(SpiDeviceFake));

        if (_connectionSettings.ChipSelectLine != -1)
        {
            _chipSelectPin = gpioController.OpenPin(_connectionSettings.ChipSelectLine);
        }
    }

    public override void Read(Span<byte> buffer)
    {
        EnableChipSelect();

        for (var i = 0; i < buffer.Length; i++)
        {
            buffer[i] = _rf69Registers.Read();
        }

        DisableChipSelect();

        _logger.LogDebug("{0}.{1}: [{2}]", nameof(SpiDeviceFake), nameof(Read),
            ByteSpanToString(buffer.ToArray()));

    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        _logger.LogDebug("{0}.{1}: [{2}]", nameof(SpiDeviceFake), nameof(Write),
            ByteSpanToString(buffer.ToArray()));

        EnableChipSelect();

        foreach (var b in buffer)
        {
            _rf69Registers.Write(b);
        }

        DisableChipSelect();
    }

    public override void TransferFullDuplex(ReadOnlySpan<byte> writeBuffer, Span<byte> readBuffer)
    {
        throw new NotImplementedException();
    }

    public override SpiConnectionSettings ConnectionSettings => _connectionSettings;

    private void EnableChipSelect()
    {
        _chipSelectPin?.Write(PinValue.Low);
    }

    private void DisableChipSelect()
    {
        _chipSelectPin?.Write(PinValue.High);
    }

    private string ByteSpanToString(byte[] values)
    {
        var strings = new List<string>();
        strings.AddRange(values.Select(b => b.ToString("X2")));

        return string.Join(',', strings);
    }
}
