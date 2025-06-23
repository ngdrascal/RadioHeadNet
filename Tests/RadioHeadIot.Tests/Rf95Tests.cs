using System.Device.Gpio;
using System.Device.Spi;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using RadioHead;
using RadioHead.RhRf95;
using UnitsNet;
using UnitTestLogger;

namespace RadioHeadIot.Tests;

[TestFixture, ExcludeFromCodeCoverage]
public class Rf95Tests
{
    private readonly byte[] _initialRegValues =
    [
        //         0     1     2     3     4     5     6     7     8     9     A     B     C     D     E     F
        /* 0 */ 0x00, 0x09, 0x00, 0x00, 0x00, 0x00, 0x6c, 0x80, 0x00, 0x2F, 0x09, 0x2B, 0x20, 0x00, 0x80, 0x00,
        /* 1 */ 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x10, 0x00, 0x00, 0x00, 0x00, 0x72, 0x70, 0x64,
        /* 2 */ 0x00, 0x08, 0x01, 0xFF, 0x00, 0x00, 0x00, 0x80, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x20
    ];

    private ILoggerFactory _loggerFactory;
    private RfRegistersFake _registers;
    private Rf95 _radio;
    private GpioPin _interruptPin;

    private void MockReceiveData(byte[] dataPacket)
    {
        var pkgIdx = 0;
        _registers.DoOnRead(Rf95.REG_00_FIFO, _ => { _registers.Poke(Rf95.REG_00_FIFO, dataPacket[pkgIdx++]); });

        // simulate the receiver updating the IRQFLAGS2 register to indicate a data
        // packed was received
        _registers.Poke(Rf95.REG_12_IRQ_FLAGS, Rf95.RX_DONE_MASK);

        _registers.Poke(Rf95.REG_1C_HOP_CHANNEL, Rf95.RX_PAYLOAD_CRC_IS_ON);

        _registers.Poke(Rf95.REG_13_RX_NB_BYTES, (byte)dataPacket.Length);

        _registers.Poke(Rf95.REG_1A_PKT_RSSI_VALUE, 0x26);

        _registers.Poke(Rf95.REG_1A_PKT_RSSI_VALUE, 0x3C);
    }

    private static byte[] BuildPacket(byte[] data,
        byte toAddress = RadioHead.RadioHead.BroadcastAddress,
        byte fromAddress = RadioHead.RadioHead.BroadcastAddress,
        byte headerId = 0x00,
        byte headerFlags = 0x00)
    {
        var packet = new List<byte>()
        {
            toAddress,
            fromAddress,
            headerId,
            headerFlags
        };
        packet.AddRange(data);

        return packet.ToArray();
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

    // GIVEN: an initialized instance of the Rf95 class
    // WHEN: SetModemRegisters() is called
    // THEN: the 3 modem configuration registers are set correctly
    [Test]
    public void SetModemRegisters()
    {
        // ARRANGE:
        _registers.Poke(Rf95.REG_1D_MODEM_CONFIG1, 0xFF);
        _registers.Poke(Rf95.REG_1E_MODEM_CONFIG2, 0xFF);
        _registers.Poke(Rf95.REG_26_MODEM_CONFIG3, 0xFF);
        var config = new Rf95.ModemConfiguration { Reg1D = 0x12, Reg1E = 0x34, Reg26 = 0x56 };

        // ACT:
        _radio.SetModemRegisters(config);

        // ASSERT:
        Assert.That(_registers.WriteCount(Rf95.REG_1D_MODEM_CONFIG1), Is.EqualTo(1));
        Assert.That(_registers.Peek(Rf95.REG_1D_MODEM_CONFIG1), Is.EqualTo(config.Reg1D));

        Assert.That(_registers.WriteCount(Rf95.REG_1E_MODEM_CONFIG2), Is.EqualTo(1));
        Assert.That(_registers.Peek(Rf95.REG_1E_MODEM_CONFIG2), Is.EqualTo(config.Reg1E));

        Assert.That(_registers.WriteCount(Rf95.REG_26_MODEM_CONFIG3), Is.EqualTo(1));
        Assert.That(_registers.Peek(Rf95.REG_26_MODEM_CONFIG3), Is.EqualTo(config.Reg26));
    }

    // GIVEN: an instance of the Rf95 class
    // WHEN: SetModemConfig() is called with a valid config index
    // THEN: the 3 modem configuration registers are set correctly
    [TestCase(Rf95.ModemConfigChoice.Bw125Cr45Sf128)]
    [TestCase(Rf95.ModemConfigChoice.Bw500Cr45Sf128)]
    [TestCase(Rf95.ModemConfigChoice.Bw3125Cr48Sf512)]
    [TestCase(Rf95.ModemConfigChoice.Bw125Cr48Sf4096)]
    [TestCase(Rf95.ModemConfigChoice.Bw125Cr45Sf2048)]
    public void SetModemConfig(Rf95.ModemConfigChoice choice)
    {
        // ARRANGE:
        _registers.Poke(Rf95.REG_1D_MODEM_CONFIG1, 0xFF);
        _registers.Poke(Rf95.REG_1E_MODEM_CONFIG2, 0xFF);
        _registers.Poke(Rf95.REG_26_MODEM_CONFIG3, 0xFF);
        var config = _radio.ModemConfigTable[(byte)choice];

        // ACT:
        _radio.SetModemConfig(choice);

        // ASSERT:
        Assert.That(_registers.WriteCount(Rf95.REG_1D_MODEM_CONFIG1), Is.EqualTo(1));
        Assert.That(_registers.Peek(Rf95.REG_1D_MODEM_CONFIG1), Is.EqualTo(config.Reg1D));

        Assert.That(_registers.WriteCount(Rf95.REG_1E_MODEM_CONFIG2), Is.EqualTo(1));
        Assert.That(_registers.Peek(Rf95.REG_1E_MODEM_CONFIG2), Is.EqualTo(config.Reg1E));

        Assert.That(_registers.WriteCount(Rf95.REG_26_MODEM_CONFIG3), Is.EqualTo(1));
        Assert.That(_registers.Peek(Rf95.REG_26_MODEM_CONFIG3), Is.EqualTo(config.Reg26));
    }

    // GIVEN: an initialized instance of the Rf95 class
    //        AND the radio is not in Tx mode
    //        AND no data was received
    // WHEN: Available() is called
    // THEN: false is returned
    [Test]
    public void AvailableNoDataNotTxMode()
    {
        // ARRANGE:
        _radio.Init();
        _radio.SetModeIdle();

        // ACT:
        var actual = _radio.Available();

        // ASSERT:
        Assert.That(actual, Is.False);
    }

    // GIVEN: an initialized instance of the Rf95 class
    //        AND the radio is in Tx mode
    //        AND no data was received
    // WHEN: Available() is called
    // THEN: false is returned
    [Test]
    public void AvailableNoDataTxMode()
    {
        // ARRANGE:
        _radio.Init();
        _radio.SetModeTx();

        // ACT:
        var actual = _radio.Available();

        // ASSERT:
        Assert.That(actual, Is.False);
    }

    // GIVEN: an initialized instance of the Rf95 class
    //        AND the radio is configured for interrupts
    //        AND the radio is in Rx mode
    //        AND data was received
    // WHEN: Available() is called
    // THEN: true is returned
    [Test]
    public void AvailableHasDataInterrupt()
    {
        // ARRANGE:
        _radio.Init();
        _radio.SetChangeDetectionMode(ChangeDetectionMode.Interrupt);

        byte[] expected = [1, 2, 3, 4];
        var packet = BuildPacket(expected);
        MockReceiveData(packet);

        _radio.SetModeRx();

        // simulate the receiver notifying the MPU a received data packet is ready by
        // activating the interrupt pin
        _interruptPin.Write(PinValue.Low);

        // ACT:
        var actual = _radio.Available();

        // ASSERT:
        Assert.That(actual, Is.True);
    }

    // GIVEN: an initialized instance of the Rf95 class
    //        AND the radio is configured for polling
    //        AND data was received
    // WHEN: Available() is called
    // THEN: true is returned
    [Test]
    public void AvailableHasDataPolling()
    {
        // ARRANGE:
        _radio.Init();
        _radio.SetChangeDetectionMode(ChangeDetectionMode.Polling);

        byte[] expected = [1, 2, 3, 4];
        var packet = BuildPacket(expected);
        MockReceiveData(packet);

        // ACT:
        var actual = _radio.Available();

        // ASSERT:
        Assert.That(actual, Is.True);
    }

    // GIVEN: an initialized instance of the Rf95 class
    //        AND data was received
    // WHEN: Receive() is called
    // THEN: true is returned
    //       AND the data is in the buffer
    [Test]
    public void ReceiveHasData()
    {
        // ARRANGE:
        _radio.Init();
        _radio.SetChangeDetectionMode(ChangeDetectionMode.Interrupt);

        byte[] expected = [1, 2, 3, 4];
        var packet = BuildPacket(expected);
        MockReceiveData(packet);

        _radio.SetModeRx();

        // simulate the receiver notifying the MPU a received data packet is ready by
        // activating the interrupt pin
        _interruptPin.Write(PinValue.Low);

        // ACT:
        var actual = _radio.Receive(out var buffer);

        // ASSERT:
        Assert.That(actual, Is.True);
        Assert.That(buffer, Is.EquivalentTo(expected));
    }

    // GIVEN: an initialized instance of the Rf95 class
    //        AND data was NOT received
    // WHEN: Receive() is called
    // THEN: false is returned
    //       AND the buffer is empty
    [Test]
    public void ReceiveNoData()
    {
        // ARRANGE:
        _radio.Init();
        _radio.SetChangeDetectionMode(ChangeDetectionMode.Interrupt);
        _radio.SetModeRx();

        // ACT:
        var actual = _radio.Receive(out var buffer);

        // ASSERT:
        Assert.That(actual, Is.False);
        Assert.That(buffer.Length, Is.EqualTo(0));
    }

    // GIVEN: an initialized instance of the Rf95 class
    //        AND the radio is in Tx mode
    // WHEN: Send() is called with a valid data packet
    // THEN: true is returned
    [Test]
    public void SendValidDataPacket()
    {
        // ARRANGE:
        _radio.Init();

        var data = new byte[] { 1, 2, 3, 4 };
        var packet = BuildPacket(data);

        // ACT:
        var actual = _radio.Send(packet);

        // ASSERT:
        Assert.That(actual, Is.True);
    }

    // GIVEN: an initialized instance of the Rf95 class
    //        AND the radio is in Tx mode
    // WHEN: Send() is called with an invalid data packet
    // THEN: false is returned
    [Test]
    public void SendInvalidDataPacket()
    {
        // ARRANGE:
        _radio.Init();

        var data = new byte[_radio.MaxMessageLength + 1];
        var packet = BuildPacket(data);

        // ACT:
        var actual = _radio.Send(packet);

        // ASSERT:
        Assert.That(actual, Is.False);
    }

    // GIVEN: an initialized instance of the Rf95 class
    // WHEN: WaitPacketSent() is called
    // THEN: returns true
    [TestCase(ChangeDetectionMode.Polling)]
    [TestCase(ChangeDetectionMode.Interrupt)]
    public void WaitPacketSent(ChangeDetectionMode mode)
    {
        // ARRANGE:
        _radio.Init();
        _radio.SetChangeDetectionMode(mode);

        // ACT:
        var actual = _radio.WaitPacketSent();

        // ASSERT:
        Assert.That(actual, Is.True);
    }

    // GIVEN: an initialized instance of the Rf95 class
    // WHEN: SetTxPower() is called
    // THEN: returns true
    [TestCase(true, 16, 0x7F, Rf95.PA_DAC_DISABLE)]
    [TestCase(true, 15, 0x7F, Rf95.PA_DAC_DISABLE)]
    [TestCase(true, 0, 0x70, Rf95.PA_DAC_DISABLE)]
    [TestCase(true, -1, 0x70, Rf95.PA_DAC_DISABLE)]

    [TestCase(false, 21, 0x8F, Rf95.PA_DAC_ENABLE)]
    [TestCase(false, 20, 0x8F, Rf95.PA_DAC_ENABLE)]
    [TestCase(false, 18, 0x8D, Rf95.PA_DAC_ENABLE)]
    [TestCase(false, 2,  0x80, Rf95.PA_DAC_DISABLE)]
    [TestCase(false, 1,  0x80, Rf95.PA_DAC_DISABLE)]

    public void SetTxPower(bool useRfo, sbyte powerLevel, byte expectedConfig, byte expectedDac)
    {
        // ARRANGE:
        _radio.Init();
        _registers.Poke(Rf95.REG_09_PA_CONFIG, 0xFF); // reset PA config register
        _registers.Poke(Rf95.REG_4D_PA_DAC, Rf95.PA_DAC_DISABLE); // reset PA DAC register

        // ACT:
        _radio.SetTxPower(powerLevel, useRfo);

        // ASSERT:
        Assert.That(_registers.Peek(Rf95.REG_09_PA_CONFIG), Is.EqualTo(expectedConfig));
        Assert.That(_registers.Peek(Rf95.REG_4D_PA_DAC), Is.EqualTo(expectedDac));
    }
}
