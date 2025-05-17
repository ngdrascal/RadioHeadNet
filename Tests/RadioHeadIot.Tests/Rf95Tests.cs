using System.Device.Gpio;
using System.Device.Spi;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using RadioHead;
using RadioHead.RhRf95;
using UnitTestLogger;

namespace RadioHeadIot.Tests;

[TestFixture, ExcludeFromCodeCoverage]
public class Rf95Tests
{
    private readonly byte[] _initialRegValues =
    [
        //         0     1     2     3     4     5     6     7     8     9     A     B     C     D     E     F
        /* 0 */ 0x00, 0x04, 0x00, 0x1A, 0x0B, 0x00, 0x52, 0xE4, 0xC0, 0x00, 0x41, 0x00, 0x02, 0x92, 0xF5, 0x20,
        /* 1 */ 0x24, 0x9F, 0x09, 0x1A, 0x40, 0xB0, 0x7B, 0x9B, 0x08, 0x86, 0x8A, 0x40, 0x80, 0x06, 0x10, 0x00,
        /* 2 */ 0x00, 0x00, 0x00, 0x02, 0xFF, 0x00, 0x05, 0x80, 0x00, 0xFF, 0x00, 0x00, 0x00, 0x03, 0x98, 0x00,
        /* 3 */ 0x10, 0x40, 0x00, 0x00, 0x00, 0x0F, 0x02, 0x00, 0x01, 0x00, 0x1B, 0x55, 0x70, 0x00, 0x00
    ];

    private ILoggerFactory _loggerFactory;
    private RfRegistersFake _registers;
    private Rf95 _radio;
    private GpioPin _interruptPin;

    private static byte InvertByte(byte input)
    {
        return (byte)~input;
    }

    [SetUp]
    public void Setup()
    {
        _loggerFactory =
            LoggerFactory.Create(builder =>
            {
                builder.AddUnitTestLogger(config => config.ShowLogLevel = false);
                builder.SetMinimumLevel(LogLevel.Trace);
                builder.AddFilter(nameof(RfRegistersFake) + ".states", LogLevel.None);
            });

        var driver = new GpioDriverFake();
        var controller = new GpioController(PinNumberingScheme.Board, driver);
        var deviceSelectPin = controller.OpenPin(5);
        _registers = new RfRegistersFake(_initialRegValues, deviceSelectPin, _loggerFactory);
        var spiConnSetting = new SpiConnectionSettings(0);
        var spiDevice = new SpiDeviceFake(spiConnSetting, controller, _registers, _loggerFactory);
        var spiDriver = new RhSpiDriver(deviceSelectPin, spiDevice);
        var logger = _loggerFactory.CreateLogger<Rf95>();
        _radio = new Rf95(spiDriver, ChangeDetectionMode.Interrupt, logger);

        _interruptPin = controller.OpenPin(6);
        _interruptPin.ValueChanged += _radio.HandleInterrupt;
    }

    [TearDown]
    public void TearDown()
    {
        _loggerFactory.Dispose();
    }

    // GIVEN: an instance of the Rf95 class
    //        AND the radio is not initialized
    //        AND the radio doesn't go into sleep mode
    // WHEN: Init() is call
    // THEN: return false
    [Test]
    public void Init01()
    {
        // ARRANGE:
        _registers.DoAfterWrite(Rf95.REG_01_OP_MODE, 
                                _ => _registers.Poke(Rf95.REG_01_OP_MODE, Rf95.MODE_TX));

        // ACT:
        var actual = _radio.Init();

        // ASSERT:
        Assert.That(actual, Is.False);
    }

    // GIVEN: an instance of the Rf95 class
    //        AND the radio is not initialized
    // WHEN: Init() is call
    // THEN: return true
    //       AND the TX base address equals 0x00
    //       AND the RX base address equals 0x00
    //       AND the mode equals MODE_STDBY
    //       AND the radio modem configuration equals Bw125Cr45Sf128
    //       AND the preamble length equals 8
    //       AND the frequency equals 433.0 MHz
    //       AND the power equals 13 dBm
    [Test]
    public void Init02()
    {
        // ARRANGE:
        var config = _radio.ModemConfigTable[(byte)Rf95.ModemConfigChoice.Bw125Cr45Sf128];

        // ACT:
        var actual = _radio.Init();

        // ASSERT:
        // Assert that the radio is initialized successfully
        Assert.That(actual, Is.True);

        // the TX base address equals 0x00
        Assert.That(_registers.Peek(Rf95.REG_0E_FIFO_TX_BASE_ADDR), Is.EqualTo(0x00));
        
        // the RX base address equals 0x00
        Assert.That(_registers.Peek(Rf95.REG_0F_FIFO_RX_BASE_ADDR), Is.EqualTo(0x00));
        
        // the mode equals MODE_STDBY
        Assert.That(_registers.Peek(Rf95.REG_01_OP_MODE), Is.EqualTo(Rf95.MODE_STDBY));
        
        // the radio modem configuration equals Bw125Cr45Sf128
        Assert.That(_registers.Peek(Rf95.REG_1D_MODEM_CONFIG1), Is.EqualTo(config.Reg1D));
        Assert.That(_registers.Peek(Rf95.REG_1E_MODEM_CONFIG2), Is.EqualTo(config.Reg1E));
        Assert.That(_registers.Peek(Rf95.REG_26_MODEM_CONFIG3), Is.EqualTo(config.Reg26));
        
        // the preamble length equals 8
        Assert.That(_registers.Peek(Rf95.REG_20_PREAMBLE_MSB), Is.EqualTo(0x00));
        Assert.That(_registers.Peek(Rf95.REG_21_PREAMBLE_LSB), Is.EqualTo(0x08));

        // the power equals 13 dBm
        Assert.That(_registers.Peek(Rf95.REG_4D_PA_DAC), Is.EqualTo(Rf95.PA_DAC_DISABLE));
        Assert.That(_registers.Peek(Rf95.REG_09_PA_CONFIG), Is.EqualTo((byte)(Rf95.PA_SELECT | (13 - 2))));
    }
}
