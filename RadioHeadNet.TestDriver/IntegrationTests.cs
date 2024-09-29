using System.Device.Gpio;
using System.Device.Spi;
using Iot.Device.Board;
using Iot.Device.FtCommon;
using Microsoft.Extensions.Logging;
using UnitTestLogger;

namespace RadioHeadNet.TestDriver;

public class IntegrationTests
{
    private ILoggerFactory _loggerFactory;
    private Board _hostBoard;
    private GpioController _gpioController;
    private SpiConnectionSettings _spiSettings;
    private SpiDevice _spiDevice;
    private GpioPin _deviceSelectPin;
    private GpioPin _resetPin;
    private RhRf69 _radio;

    [SetUp]
    public void Setup()
    {
        _loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddUnitTestLogger(config => config.ShowLogLevel = false);
            builder.SetMinimumLevel(LogLevel.Trace);
        });

        var allFtx232H = Ftx232HDevice.GetFtx232H();
        if (allFtx232H.Count == 0)
            throw new ApplicationException("No FT232 device found.");

        _hostBoard = allFtx232H[0];
        ((Ftx232HDevice)_hostBoard).Reset();

        _gpioController = _hostBoard.CreateGpioController();

        _spiSettings = new SpiConnectionSettings(0, 3)
        {
            ClockFrequency = 1_000_000,
            DataBitLength = 8,
            ChipSelectLineActiveState = PinValue.Low,
            Mode = SpiMode.Mode0
        };

        _spiDevice = _hostBoard.CreateSpiDevice(_spiSettings);

        // configure the device select pin
        _deviceSelectPin = _gpioController.OpenPin(5);
        _deviceSelectPin.SetPinMode(PinMode.Output);

        // configure the reset pin
        _resetPin = _gpioController.OpenPin(6);
        _resetPin.SetPinMode(PinMode.Output);

        _radio = new RhRf69(_deviceSelectPin, _spiDevice);

        // run reset sequence
        _resetPin.Write(PinValue.Low);

        _resetPin.Write(PinValue.High);
        Thread.Sleep(TimeSpan.FromMicroseconds(100));

        _resetPin.Write(PinValue.Low);
        Thread.Sleep(TimeSpan.FromMilliseconds(5));

        // configure the radio
        _radio.Init();
        _radio.SetTxPower(20, true);
        _radio.SetFrequency(915.0f);
    }

    [TearDown]
    public void TearDown()
    {
        _gpioController.Dispose();
        _spiDevice.Dispose();
        _hostBoard.Dispose();
        _loggerFactory.Dispose();
    }

    [Ignore("Integration test.  Requires a hardware connection.")]
    [Test]
    public void Test1()
    {
        // Arrange
        var tempBuf = BitConverter.GetBytes(12.3f);
        var humBuf = BitConverter.GetBytes(45.6f);
        var message = tempBuf.Concat(humBuf).ToArray();

        // var message = "Hello, world!"u8.ToArray();

        // Act
        _radio.Send(message);

        // Assert
        Assert.Pass();
    }
}
