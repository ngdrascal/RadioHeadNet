using System.Device.Gpio;
using System.Device.Spi;
using Microsoft.Extensions.Logging;
using UnitTestLogger;

namespace RadioHeadNet.Tests;

[TestFixture]
public class RhRf69Tests
{
    private ILoggerFactory _loggerFactory;
    private Rf69RegistersFake _registers;
    private RhRf69 _radio;

    [SetUp]
    public void Setup()
    {
        _loggerFactory =
            LoggerFactory.Create(builder =>
            {
                builder.AddUnitTestLogger(config => config.ShowLogLevel = false);
                builder.SetMinimumLevel(LogLevel.Trace);
            });

        var driver = new GpioDriverFake();
        var controller = new GpioController(PinNumberingScheme.Board, driver);
        var deviceSelectPin = controller.OpenPin(0);
        _registers = new Rf69RegistersFake(deviceSelectPin, _loggerFactory);
        var spiConnSetting = new SpiConnectionSettings(0);
        var spiDevice = new SpiDeviceFake(spiConnSetting, controller, _registers, _loggerFactory);
        _radio = new RhRf69(deviceSelectPin, spiDevice);
    }

    [TearDown]
    public void TearDown()
    {
        _loggerFactory.Dispose();
    }

    // GIVEN: an instance of RhRf69Tests
    // WHEN: TemperatureRead() is called
    // THEN: the temperature registers are read
    [Test]
    public void TemperatureRead()
    {
        // ARRANGE:
        _registers.Poke(RhRf69.REG_4E_TEMP1, 0xFF);
        _registers.DoAfterWrite(RhRf69.REG_4E_TEMP1,
            (_) => { _registers.Poke(RhRf69.REG_4E_TEMP1, 0x04); });
        _registers.DoAfterRead(RhRf69.REG_4E_TEMP1,
            (count) =>
            {
                if (count >= 3)
                    _registers.Poke(RhRf69.REG_4E_TEMP1, 0x00);
            });
        _registers.Poke(RhRf69.REG_4F_TEMP2, 165);

        // ACT:
        var actual = _radio.TemperatureRead();

        // ASSERT:
        Assert.That(_registers.WriteCount(RhRf69.REG_4E_TEMP1), Is.EqualTo(1));
        Assert.That(_registers.ReadCount(RhRf69.REG_4E_TEMP1), Is.GreaterThanOrEqualTo(1));
        Assert.That(_registers.ReadCount(RhRf69.REG_4F_TEMP2), Is.EqualTo(1));
        Assert.That(actual, Is.EqualTo(1));
    }


    // GIVEN: an instance of RhRf69Tests who's idle-mode has not been set
    // WHEN: SetModeIdle() is called
    // THEN: the OpMode register's mode bits should be set to standby
    [Test]
    public void SetModeIdleDefault()
    {
        // ARRANGE:
        _registers.Poke(RhRf69.REG_01_OPMODE, 0xFF);

        // ACT:
        _radio.SetModeIdle();

        // ASSERT:
        Assert.That(_registers.ReadCount(RhRf69.REG_01_OPMODE), Is.EqualTo(1));
        Assert.That(_registers.Peek(RhRf69.REG_01_OPMODE) & 0b00011100, Is.EqualTo(RhRf69.OPMODE_MODE_STDBY));
        Assert.That(_registers.ReadCount(RhRf69.REG_27_IRQFLAGS1), Is.GreaterThan(0));
    }

    // GIVEN: an instance of RhRf69Tests who's idle-mode has been set
    // WHEN: SetModeIdle() is called
    // THEN: the OpMode register's mode bits should be set according to the idle-mode
    [TestCase(RhRf69.OPMODE_MODE_SLEEP)]
    [TestCase(RhRf69.OPMODE_MODE_STDBY)]
    public void SetModeIdle(byte idleMode)
    {
        // ARRANGE:
        _registers.Poke(RhRf69.REG_01_OPMODE, 0xFF);
        _radio.SetIdleMode(idleMode);

        // ACT:
        _radio.SetModeIdle();

        // ASSERT:
        Assert.That(_registers.ReadCount(RhRf69.REG_01_OPMODE), Is.EqualTo(1));
        Assert.That(_registers.Peek(RhRf69.REG_01_OPMODE) & 0b00011100, Is.EqualTo(idleMode));
        Assert.That(_registers.ReadCount(RhRf69.REG_27_IRQFLAGS1), Is.GreaterThan(0));
    }

    // GIVEN: an instance of RhRf69Tests
    // WHEN: SetModeRx() is called
    // THEN: the OpMode register's mode bits should be set correctly
    [Test]
    public void SetModeRx()
    {
        // ARRANGE:
        _registers.Poke(RhRf69.REG_01_OPMODE, 0xFF);

        // ACT:
        _radio.SetModeRx();

        // ASSERT:
        Assert.That(_registers.ReadCount(RhRf69.REG_01_OPMODE), Is.EqualTo(1));
        Assert.That(_registers.Peek(RhRf69.REG_01_OPMODE) & 0b00011100, Is.EqualTo(RhRf69.OPMODE_MODE_RX));

        Assert.That(_registers.ReadCount(RhRf69.REG_27_IRQFLAGS1), Is.GreaterThan(0));

        Assert.That(_registers.WriteCount(RhRf69.REG_25_DIOMAPPING1), Is.EqualTo(1));
        Assert.That(_registers.Peek(RhRf69.REG_25_DIOMAPPING1), Is.EqualTo(RhRf69.DIOMAPPING1_DIO0MAPPING_01));
    }

    // GIVEN: an instance of RhRf69Tests
    // WHEN: SetModeTx() is called
    // THEN: the OpMode register's mode bits should be set correctly
    [Test]
    public void SetModeTx()
    {
        // ARRANGE:
        _registers.Poke(RhRf69.REG_01_OPMODE, 0xFF);

        // ACT:
        _radio.SetModeTx();

        // ASSERT:
        Assert.That(_registers.ReadCount(RhRf69.REG_01_OPMODE), Is.EqualTo(1));
        Assert.That(_registers.Peek(RhRf69.REG_01_OPMODE) & 0b00011100, Is.EqualTo(RhRf69.OPMODE_MODE_TX));
        Assert.That(_registers.ReadCount(RhRf69.REG_27_IRQFLAGS1), Is.GreaterThan(0));
    }

    // GIVEN: an instance of RhRf69Tests
    // WHEN: SetFrequency() is called
    // THEN: the 3 RF Carrier Frequency registers should be set correctly
    [Test]
    public void SetFrequency()
    {
        // ARRANGE:
        _registers.Poke(RhRf69.REG_07_FRFMSB, 0xFF);
        _registers.Poke(RhRf69.REG_08_FRFMID, 0xFF);
        _registers.Poke(RhRf69.REG_09_FRFLSB, 0xFF);

        // ACT:
        _radio.SetFrequency(915.0f);

        // ASSERT:
        Assert.That(_registers.WriteCount(RhRf69.REG_07_FRFMSB), Is.EqualTo(1));
        Assert.That(_registers.Peek(RhRf69.REG_07_FRFMSB), Is.EqualTo(0xE4));

        Assert.That(_registers.WriteCount(RhRf69.REG_08_FRFMID), Is.EqualTo(1));
        Assert.That(_registers.Peek(RhRf69.REG_08_FRFMID), Is.EqualTo(0xC0));

        Assert.That(_registers.WriteCount(RhRf69.REG_09_FRFLSB), Is.EqualTo(1));
        Assert.That(_registers.Peek(RhRf69.REG_09_FRFLSB), Is.EqualTo(0x00));
    }

    // GIVEN: an instance of RhRf69Tests
    // WHEN: SetTxPower() is called with the high-powered flag set to true
    // THEN: the Tx Power register should be set correctly
    //
    // NOTE: the ranges are -2 to 13, 14 to 17, 18 to 20
    [TestCase(-3, 0b0_1_0_10000)] // adjusted to -2
    [TestCase(-2, 0b0_1_0_10000)]
    [TestCase(13, 0b0_1_0_11111)]
    [TestCase(14, 0b0_1_1_11100)]
    [TestCase(17, 0b0_1_1_11111)]
    [TestCase(18, 0b0_1_1_11101)]
    [TestCase(20, 0b0_1_1_11111)]
    [TestCase(21, 0b0_1_1_11111)] // adjusted to 20
    public void SetTxPowerForHighPowered(sbyte powerLevel, byte expected)
    {
        // ARRANGE:
        _registers.Poke(RhRf69.REG_11_PALEVEL, 0xFF);

        // ACT:
        _radio.SetTxPower(powerLevel, true);

        // ASSERT:
        Assert.That(_registers.WriteCount(RhRf69.REG_11_PALEVEL), Is.EqualTo(1));
        Assert.That(_registers.Peek(RhRf69.REG_11_PALEVEL), Is.EqualTo(expected));
    }

    // GIVEN: an instance of RhRf69Tests
    // WHEN: SetTxPower() is called with the high-powered flag set to false
    // THEN: the Tx Power register should be set correctly
    //
    // NOTE: the range is -18 to 13
    [TestCase(-19, 0b1_0_0_00000)] // adjusted to -18
    [TestCase(-18, 0b1_0_0_00000)]
    [TestCase(+13, 0b1_0_0_11111)]
    [TestCase(+14, 0b1_0_0_11111)] // adjust to 13
    public void SetTxPowerForNormalPower(sbyte powerLevel, byte expected)
    {
        // ARRANGE:
        _registers.Poke(RhRf69.REG_11_PALEVEL, 0xFF);

        // ACT:
        _radio.SetTxPower(powerLevel, false);

        // ASSERT:
        Assert.That(_registers.WriteCount(RhRf69.REG_11_PALEVEL), Is.EqualTo(1));
        Assert.That(_registers.Peek(RhRf69.REG_11_PALEVEL), Is.EqualTo(expected));
    }

}
