using System.Device.Gpio;
using System.Device.Spi;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace RadioHeadIot.Tests;

[ExcludeFromCodeCoverage]
internal class SpiDeviceFake : SpiDevice
{
    private readonly SpiConnectionSettings _connectionSettings;
    private readonly RfRegistersFake _rfRegisters;
    private readonly GpioPin? _chipSelectPin;
    private readonly ILogger _logger;

    [ExcludeFromCodeCoverage]
    public SpiDeviceFake(SpiConnectionSettings connectionSettings, GpioController gpioController,
        RfRegistersFake rfRegisters, ILoggerFactory loggerFactory)
    {
        _connectionSettings = connectionSettings;
        _rfRegisters = rfRegisters;
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
            buffer[i] = _rfRegisters.Read();
        }

        DisableChipSelect();

        _logger.LogDebug("{0}.{1}: [{2}]", nameof(SpiDeviceFake), nameof(Read),
            ByteSpanToString(buffer.ToArray()));

    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        _logger.LogDebug("{0}.{1}([{2}])", nameof(SpiDeviceFake), nameof(Write),
            ByteSpanToString(buffer.ToArray()));

        EnableChipSelect();

        foreach (var b in buffer)
        {
            _rfRegisters.Write(b);
        }

        DisableChipSelect();
    }

    public override void TransferFullDuplex(ReadOnlySpan<byte> writeBuffer, Span<byte> readBuffer)
    {
        
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
