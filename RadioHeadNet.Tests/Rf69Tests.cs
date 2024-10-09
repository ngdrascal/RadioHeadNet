using System.Device.Gpio;
using System.Device.Spi;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using RadioHeadNet.RhRf69;
using UnitTestLogger;

namespace RadioHeadNet.Tests;

[TestFixture, ExcludeFromCodeCoverage]
public class Rf69Tests
{
    private ILoggerFactory _loggerFactory;
    private Rf69RegistersFake _registers;
    private Rf69 _radio;
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
                builder.AddFilter(nameof(Rf69RegistersFake) + ".states", LogLevel.None);
            });

        var driver = new GpioDriverFake();
        var controller = new GpioController(PinNumberingScheme.Board, driver);
        var deviceSelectPin = controller.OpenPin(5);
        _registers = new Rf69RegistersFake(deviceSelectPin, _loggerFactory);
        var spiConnSetting = new SpiConnectionSettings(0);
        var spiDevice = new SpiDeviceFake(spiConnSetting, controller, _registers, _loggerFactory);
        _radio = new Rf69(deviceSelectPin, spiDevice, _loggerFactory);

        _interruptPin = controller.OpenPin(6);
        _interruptPin.ValueChanged += _radio.HandleInterrupt;
    }

    [TearDown]
    public void TearDown()
    {
        _loggerFactory.Dispose();
    }

    // GIVEN: an instance of the Rf69 class
    //        AND the radio is not initialized
    //        AND the radio's device type is not supported
    // WHEN: Init() is call
    // THEN: return false
    [Test]
    public void Init()
    {
        // ARRANGE:
        const byte deviceType = 0xFF;
        _registers.Poke(Rf69.REG_10_Version, deviceType);

        // ACT:
        var actual = _radio.Init();

        // ASSERT:
        Assert.That(actual, Is.False);
    }

    // GIVEN: an instance of the Rf69 class
    // WHEN: the DeviceType property is read
    // THEN: the correct device type is returned
    [Test]
    public void DeviceType()
    {
        // ARRANGE:
        const byte expected = 0xAA;
        _registers.Poke(Rf69.REG_10_Version, expected);
        _ = _radio.Init();

        // ACT:
        var actual = _radio.DeviceType;

        // ASSERT:
        Assert.That(actual, Is.EqualTo(expected));
    }

    // GIVEN: an instance of the Rf69 class
    // WHEN: TemperatureRead() is called
    // THEN: the temperature registers are read
    [Test]
    public void TemperatureRead()
    {
        // ARRANGE:
        _registers.Poke(Rf69.REG_4E_Temp1, 0xFF);
        _registers.DoAfterWrite(Rf69.REG_4E_Temp1,
            _ => { _registers.Poke(Rf69.REG_4E_Temp1, 0x04); });
        _registers.DoAfterRead(Rf69.REG_4E_Temp1,
            (count) =>
            {
                if (count >= 3)
                    _registers.Poke(Rf69.REG_4E_Temp1, 0x00);
            });
        _registers.Poke(Rf69.REG_4F_Temp2, 165);

        // ACT:
        var actual = _radio.TemperatureRead();

        // ASSERT:
        Assert.That(_registers.WriteCount(Rf69.REG_4E_Temp1), Is.EqualTo(1));
        Assert.That(_registers.ReadCount(Rf69.REG_4E_Temp1), Is.GreaterThanOrEqualTo(1));
        Assert.That(_registers.ReadCount(Rf69.REG_4F_Temp2), Is.EqualTo(1));
        Assert.That(actual, Is.EqualTo(1));
    }

    // GIVEN: an instance of the Rf69 class
    //        AND SetIdleMode() has not been called
    // WHEN: SetModeIdle() is called
    // THEN: the OpMode register's mode bits equal STANDBY
    //       AND the power mode is set to normal
    [Test]
    public void SetModeIdleDefault()
    {
        // ARRANGE:
        _registers.Poke(Rf69.REG_01_OpMode, 0xFF);
        _radio.SetTxPower(20, true);

        // ACT:
        _radio.SetModeIdle();

        // ASSERT:
        Assert.That(_registers.Peek(Rf69.REG_01_OpMode) & 0b00011100, Is.EqualTo(Rf69.OPMODE_MODE_STDBY));
        Assert.That(_registers.Peek(Rf69.REG_5A_TestPa1), Is.EqualTo(Rf69.TESTPA1_NORMAL));
        Assert.That(_registers.Peek(Rf69.REG_5C_TestPa2), Is.EqualTo(Rf69.TESTPA2_NORMAL));
    }

    // GIVEN: an instance of the Rf69 class
    //        AND SetIdleMode() was called
    // WHEN: SetModeIdle() is called
    // THEN: the OpMode register's mode bits should be set correctly
    //       AND the power mode is set to normal
    [TestCase(Rf69.OPMODE_MODE_SLEEP)]
    [TestCase(Rf69.OPMODE_MODE_STDBY)]
    public void SetModeIdle(byte idleMode)
    {
        // ARRANGE:
        _registers.Poke(Rf69.REG_01_OpMode, InvertByte(Rf69.IRQFLAGS1_MODEREADY));
        _registers.Poke(Rf69.REG_5A_TestPa1, Rf69.TESTPA1_BOOST);
        _registers.Poke(Rf69.REG_5C_TestPa2, Rf69.TESTPA2_BOOST);

        _radio.SetTxPower(20, true);

        _registers.DoOnRead(Rf69.REG_27_IrqFlags1, count =>
        {
            // return the ModeReady flag set after the 3rd read
            _registers.Poke(Rf69.REG_27_IrqFlags1, count <= 3 ?
                (byte)0 : Rf69.IRQFLAGS1_MODEREADY);
        });

        _radio.SetIdleMode(idleMode);

        // ACT:
        _radio.SetModeIdle();

        // ASSERT:
        Assert.That(_registers.Peek(Rf69.REG_01_OpMode) & 0b00011100, Is.EqualTo(idleMode));
        Assert.That(_registers.Peek(Rf69.REG_5A_TestPa1), Is.EqualTo(Rf69.TESTPA1_NORMAL));
        Assert.That(_registers.Peek(Rf69.REG_5C_TestPa2), Is.EqualTo(Rf69.TESTPA2_NORMAL));
    }

    // GIVEN: an instance of the Rf69 class
    // WHEN: SetModeRx() is called
    // THEN: the OpMode register's mode bits equal RX
    //       AND the power mode is set to normal
    //       AND interrupt line 0 is set to PAYLOADREADY
    [Test]
    public void SetModeRx()
    {
        // ARRANGE:
        _registers.Poke(Rf69.REG_01_OpMode, 0xFF);
        _registers.Poke(Rf69.REG_5A_TestPa1, Rf69.TESTPA1_BOOST);
        _registers.Poke(Rf69.REG_5C_TestPa2, Rf69.TESTPA2_BOOST);

        _radio.SetTxPower(20, true);

        // ACT:
        _radio.SetModeRx();

        // ASSERT:
        Assert.That(_registers.Peek(Rf69.REG_01_OpMode) & 0b00011100, Is.EqualTo(Rf69.OPMODE_MODE_RX));
        Assert.That(_registers.Peek(Rf69.REG_5A_TestPa1), Is.EqualTo(Rf69.TESTPA1_NORMAL));
        Assert.That(_registers.Peek(Rf69.REG_5C_TestPa2), Is.EqualTo(Rf69.TESTPA2_NORMAL));
        Assert.That(_registers.Peek(Rf69.REG_25_DioMapping1), Is.EqualTo(Rf69.DIOMAPPING1_DIO0MAPPING_01));
    }

    // GIVEN: an instance of the Rf69 class
    // WHEN: SetModeTx() is called
    // THEN: the OpMode register's mode bits equal TX
    //       AND the power mode is set to BOOST
    //       AND interrupt line 0 is set to PACKETSENT
    [Test]
    public void SetModeTx()
    {
        // ARRANGE:
        _registers.Poke(Rf69.REG_01_OpMode, 0xFF);
        _registers.Poke(Rf69.REG_5A_TestPa1, Rf69.TESTPA1_NORMAL);
        _registers.Poke(Rf69.REG_5C_TestPa2, Rf69.TESTPA2_NORMAL);

        _radio.SetTxPower(20, true);

        // ACT:
        _radio.SetModeTx();

        // ASSERT:
        Assert.That(_registers.Peek(Rf69.REG_01_OpMode) & 0b00011100, Is.EqualTo(Rf69.OPMODE_MODE_TX));
        Assert.That(_registers.Peek(Rf69.REG_5A_TestPa1), Is.EqualTo(Rf69.TESTPA1_BOOST));
        Assert.That(_registers.Peek(Rf69.REG_5C_TestPa2), Is.EqualTo(Rf69.TESTPA2_BOOST));
        Assert.That(_registers.Peek(Rf69.REG_25_DioMapping1), Is.EqualTo(Rf69.DIOMAPPING1_DIO0MAPPING_00));
    }

    // GIVEN: an instance of the Rf69 class
    // WHEN: Sleep() is called
    // THEN: the OpMode register's mode bits should be set correctly
    [Test]
    public void Sleep()
    {
        // ARRANGE:
        _registers.Poke(Rf69.REG_01_OpMode, 0xFF);

        // ACT:
        _radio.Sleep();

        // ASSERT:
        Assert.That(_registers.Peek(Rf69.REG_01_OpMode) & 0b00011100, Is.EqualTo(Rf69.OPMODE_MODE_SLEEP));
    }

    // GIVEN: an instance of the Rf69 class
    // WHEN: SetFrequency() is called
    // THEN: the 3 RF Carrier Frequency registers should be set correctly
    [Test]
    public void SetFrequency()
    {
        // ARRANGE:
        _registers.Poke(Rf69.REG_07_FrfMsb, 0xFF);
        _registers.Poke(Rf69.REG_08_FrfMid, 0xFF);
        _registers.Poke(Rf69.REG_09_FrfLsb, 0xFF);

        // ACT:
        _ = _radio.SetFrequency(915.0f);

        // ASSERT:
        Assert.That(_registers.WriteCount(Rf69.REG_07_FrfMsb), Is.EqualTo(1));
        Assert.That(_registers.Peek(Rf69.REG_07_FrfMsb), Is.EqualTo(0xE4));

        Assert.That(_registers.WriteCount(Rf69.REG_08_FrfMid), Is.EqualTo(1));
        Assert.That(_registers.Peek(Rf69.REG_08_FrfMid), Is.EqualTo(0xC0));

        Assert.That(_registers.WriteCount(Rf69.REG_09_FrfLsb), Is.EqualTo(1));
        Assert.That(_registers.Peek(Rf69.REG_09_FrfLsb), Is.EqualTo(0x00));
    }

    // GIVEN: an instance of the Rf69 class
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
        _registers.Poke(Rf69.REG_11_PaLevel, 0xFF);

        // ACT:
        _radio.SetTxPower(powerLevel, true);

        // ASSERT:
        Assert.That(_registers.WriteCount(Rf69.REG_11_PaLevel), Is.EqualTo(1));
        Assert.That(_registers.Peek(Rf69.REG_11_PaLevel), Is.EqualTo(expected));
    }

    // GIVEN: an instance of the Rf69 class
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
        _registers.Poke(Rf69.REG_11_PaLevel, 0xFF);

        // ACT:
        _radio.SetTxPower(powerLevel, false);

        // ASSERT:
        Assert.That(_registers.WriteCount(Rf69.REG_11_PaLevel), Is.EqualTo(1));
        Assert.That(_registers.Peek(Rf69.REG_11_PaLevel), Is.EqualTo(expected));
    }

    // GIVEN: an instance of the Rf69 class
    // WHEN: SetSyncWords() is called with a 1 to 4 byte long sync word
    // THEN: the SyncOn bit is set
    //       AND the SyncSize bits are set
    //       AND the SyncValue bits are set
    [TestCase(1, null, null, null, 0b1_0_000_000)]
    [TestCase(1, 2, null, null, 0b1_0_001_000)]
    [TestCase(1, 2, 3, null, 0b1_0_010_000)]
    [TestCase(1, 2, 3, 4, 0b1_0_011_000)]
    public void SetSyncWordsLength1To4(byte byte1, byte? byte2, byte? byte3, byte? byte4, byte expected)
    {
        // ARRANGE:
        _registers.Poke(Rf69.REG_2E_SyncConfig, 0x00);
        _registers.Poke(Rf69.REG_2F_SyncValue1 + 0, 0xFF);
        _registers.Poke(Rf69.REG_2F_SyncValue1 + 1, 0xFF);
        _registers.Poke(Rf69.REG_2F_SyncValue1 + 2, 0xFF);
        _registers.Poke(Rf69.REG_2F_SyncValue1 + 3, 0xFF);

        var syncWordList = new List<byte> { byte1 };
        if (byte2 != null) syncWordList.Add(byte2.Value);
        if (byte3 != null) syncWordList.Add(byte3.Value);
        if (byte4 != null) syncWordList.Add(byte4.Value);
        var syncWords = syncWordList.ToArray();

        // ACT:
        _radio.SetSyncWords(syncWords);

        // ASSERT:
        Assert.That(_registers.WriteCount(Rf69.REG_2E_SyncConfig), Is.EqualTo(1));
        Assert.That(_registers.Peek(Rf69.REG_2E_SyncConfig), Is.EqualTo(expected));

        for (var i = 0; i < syncWords.Length; i++)
        {
            Assert.That(_registers.WriteCount((byte)(Rf69.REG_2F_SyncValue1 + i)), Is.EqualTo(1));
            Assert.That(_registers.Peek((byte)(Rf69.REG_2F_SyncValue1 + i)), Is.EqualTo(syncWords[i]));
        }
    }

    // GIVEN: an instance of the Rf69 class
    // WHEN: SetSyncWords() is called with a 1 to 4 byte long sync word
    // THEN: an ArgumentException is thrown
    [Test]
    public void SetSyncWordsLength5()
    {
        // ARRANGE:
        byte[] syncWords = [0x01, 0x02, 0x03, 0x04, 0x05];

        // ACT:
        void Lambda() => _radio.SetSyncWords(syncWords);

        // ASSERT:
        Assert.Throws<ArgumentException>(Lambda);
    }

    // GIVEN: an instance of the Rf69 class
    // WHEN: SetSyncWords() is called with a 0 byte long sync word
    // THEN: sync word generation is turned off
    [Test]
    public void SetSyncWordsLength0()
    {
        // ARRANGE:
        byte[] syncWords = [];

        // ACT:
        _radio.SetSyncWords(syncWords);

        // ASSERT:
        Assert.That(_registers.Peek((Rf69.REG_2E_SyncConfig & 0x80)) >> 7, Is.EqualTo(0));
    }

    // GIVEN: an instance of the Rf69 class
    // WHEN: SetPreambleLength() is called
    // THEN: the Preamble Length registers should be set correctly
    [Test]
    public void SetPreambleLength()
    {
        // ARRANGE:
        _registers.Poke(Rf69.REG_2C_PreambleMsb, 0xFF);
        _registers.Poke(Rf69.REG_2D_PreambleLsb, 0xFF);

        const ushort length = 257;

        // ACT:
        _radio.SetPreambleLength(length);

        // ASSERT:
        Assert.That(_registers.Peek(Rf69.REG_2C_PreambleMsb), Is.EqualTo((byte)(length >> 8)));
        Assert.That(_registers.Peek(Rf69.REG_2D_PreambleLsb), Is.EqualTo((byte)(length & 0xFF)));
    }

    // GIVEN: an instance of the Rf69 class
    // WHEN: SetEncryptionKey() is called with a 16 byte key
    // THEN: the AES-On flag is set
    //       AND the Encryption Key registers should be set correctly
    [Test]
    public void SetEncryptionKeyLength16()
    {
        // ARRANGE:
        _registers.Poke(Rf69.REG_3D_PacketConfig2, 0x00);
        for (var i = 0; i < 16; i++)
            _registers.Poke((byte)(Rf69.REG_3E_AesKey1 + i), 0xFF);

        byte[] key = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16];

        // ACT:
        _radio.SetEncryptionKey(key);

        // ASSERT:
        Assert.That(_registers.Peek(Rf69.REG_3D_PacketConfig2) & 0x01, Is.EqualTo(1));
        for (var i = 0; i < 16; i++)
            Assert.That(_registers.Peek((byte)(Rf69.REG_3E_AesKey1 + i)), Is.EqualTo(key[i]));
    }

    // GIVEN: an instance of the Rf69 class
    // WHEN: SetEncryptionKey() is called with a 0 byte key
    // THEN: the AES-On flag is cleared
    [Test]
    public void SetEncryptionKeyLength0()
    {
        // ARRANGE:
        _registers.Poke(Rf69.REG_3D_PacketConfig2, 0xFF);

        byte[] key = [];

        // ACT:
        _radio.SetEncryptionKey(key);

        // ASSERT:
        Assert.That(_registers.Peek(Rf69.REG_3D_PacketConfig2) & 0x01, Is.EqualTo(0));
    }

    // GIVEN: an instance of the Rf69 class
    // WHEN: SetEncryptionKey() is called with a key whose length is not 0 or 16
    // THEN: an ArgumentException is thrown
    [Test]
    public void SetEncryptionKeyLengthNot0Or16()
    {
        // ARRANGE:
        byte[] key = [1, 2, 3, 4];

        // ACT:
        void Lambda() => _radio.SetEncryptionKey(key);

        // ASSERT:
        Assert.Throws<ArgumentException>(Lambda);
    }

    // GIVEN: an instance of the Rf69 class
    //        AND using the default settings
    //        AND the radio is not in Tx mode
    // WHEN: Send() is called
    // THEN: the sent packet has the correct data
    //       AND the sent packet has the correct header
    //       AND the of packets transmitted is 1
    [Test]
    public void Send()
    {
        // ARRANGE:
        _radio.Init();

        byte[] data = [1, 2, 3, 4];
        var expected = BuildPacket([1, 2, 3, 4]);

        var actual = new List<byte>();

        _registers.DoAfterWrite(Rf69.REG_00_Fifo, _ =>
            {
                // record each byte written to the FIFO register
                actual.Add(_registers.Peek(Rf69.REG_00_Fifo));
            });

        _registers.DoOnRead(Rf69.REG_28_IrqFlags2, _ =>
            {
                // simulate the packet sent flag being set after the 3rd the flag is read
                _registers.Poke(Rf69.REG_28_IrqFlags2, Rf69.IRQFLAGS2_PACKETSENT);
            });

        // ACT:
        var result = _radio.Send(data);

        // simulate the transmitter notifying the MPU the data packet was sent by
        // activating the interrupt pin
        _interruptPin.Write(PinValue.Low);

        // ASSERT:
        Assert.That(result, Is.True);
        Assert.That(actual, Is.EqualTo(expected));
        Assert.That(_radio.TxGood, Is.EqualTo(1));
        Assert.That(_registers.Peek(Rf69.REG_01_OpMode), Is.Not.EqualTo(Rf69.OPMODE_MODE_TX));
    }

    // GIVEN: an instance of the Rf69 class
    //        AND the radio is not in Rx mode
    //        AND no data was received
    // WHEN: Available() is called
    // THEN: false is returned
    [Test]
    public void AvailableNoData()
    {
        // ARRANGE:
        _radio.Init();

        // ACT:
        var result = _radio.Available();

        // ASSERT:
        Assert.That(result, Is.False);
    }

    // GIVEN: an instance of the Rf69 class
    //        AND the radio is not in Rx mode
    //        AND data was received
    // WHEN: Available() is called
    // THEN: true is returned
    [Test]
    public void AvailableHasData()
    {
        // ARRANGE:
        byte[] expected = [1, 2, 3, 4];

        _radio.Init();
        var packet = BuildPacket(expected);
        MockReceiveData(packet);

        _radio.SetModeRx();
        _radio.HandleInterrupt(this, new PinValueChangedEventArgs(PinEventTypes.Falling, 1));

        // ACT:
        var result = _radio.Available();

        // ASSERT:
        Assert.That(result, Is.True);
    }

    // GIVEN: an instance of the Rf69 class
    //        AND IRQFLAGS2.PAYLOADREADY flag is set
    // WHEN: Receive() is called
    // THEN: the data received matches the data sent
    //       AND the headers received match the headers sent
    //       AND the RSSI value is correct
    //       AND the LastPreambleTime is set
    //       AND the count of good packets is 1
    //       AND the count of bad packets is 0
    [Test]
    public void Receive()
    {
        // ARRANGE:
        const byte expectedTo = 0xAA;
        const byte expectedFrom = 0xBB;
        const byte expectedId = 0xCC;
        const byte expectedFlags = 0xDD;

        // RSSI value is -(170/2) = -85 dBm
        _registers.Poke(Rf69.REG_24_RssiValue, 170);

        byte[] expectedData = [1, 2, 3, 4];

        _radio.Init();
        _radio.ThisAddress = expectedTo;

        var packet = BuildPacket(expectedData, expectedTo, expectedFrom, expectedId, expectedFlags);
        MockReceiveData(packet);

        _radio.SetModeRx();

        // simulate the receiver notifying the MPU a received data packet is ready by
        // activating the interrupt pin
        _interruptPin.Write(PinValue.Low);

        // ACT:
        var result = _radio.Receive(out var actualData);

        // ASSERT:
        Assert.That(result, Is.True);
        Assert.That(actualData, Is.EqualTo(expectedData));
        Assert.That(_radio.RxHeaderTo, Is.EqualTo(expectedTo));
        Assert.That(_radio.RxHeaderFrom, Is.EqualTo(expectedFrom));
        Assert.That(_radio.RxHeaderId, Is.EqualTo(expectedId));
        Assert.That(_radio.RxHeaderFlags, Is.EqualTo(expectedFlags));
        Assert.That(_radio.LastRssi, Is.EqualTo(-85));
        Assert.That(_radio.LastPreambleTime, Is.Not.EqualTo(0));
        Assert.That(_radio.RxGood, Is.EqualTo(1));
        Assert.That(_radio.RxBad, Is.EqualTo(0));
    }

    // GIVEN: an instance of the Rf69 class
    // WHEN: Receive() is called
    //       AND the incoming TO address does not match the radio's address
    // THEN: the data received is accepted or discarded based on the Promiscuous flag
    [TestCase(true)]
    [TestCase(false)]
    public void Promiscuous(bool promiscuous)
    {
        // ARRANGE:
        byte[] expected = [1, 2, 3, 4];
        byte toAddress = 0xBB;

        _radio.Init();

        _radio.Promiscuous = promiscuous;
        var packet = BuildPacket(expected, toAddress);
        MockReceiveData(packet);

        _radio.SetModeRx();

        // simulate the receiver notifying the MPU a received data packet is ready by
        // activating the interrupt pin
        _interruptPin.Write(PinValue.Low);

        // ACT:
        var result = _radio.Receive(out _);

        // ASSERT:
        Assert.That(result, Is.EqualTo(promiscuous));
    }

    // GIVEN: an instance of the Rf69 class
    //        AND the radio received a packet
    // WHEN: PollAvailable() is call
    // THEN: return true
    [Test]
    public void PollAvailablePacketReceived()
    {
        // ARRANGE:
        _registers.Poke(Rf69.REG_28_IrqFlags2, Rf69.IRQFLAGS2_PAYLOADREADY);

        _radio.Init();

        // ACT:
        var actual = _radio.PollAvailable(100);

        // ASSERT:
        Assert.That(actual, Is.True);
    }

    // GIVEN: an instance of the Rf69 class
    //        AND the radio received a packet
    // WHEN: PollAvailable() is call
    // THEN: return true
    [Test]
    public void PollAvailableNoPackedReceived()
    {
        // ARRANGE:
        _registers.Poke(Rf69.REG_28_IrqFlags2, 0x00);

        _radio.Init();

        // ACT:
        var actual = _radio.PollAvailable(100);

        // ASSERT:
        Assert.That(actual, Is.False);
    }

    private void MockReceiveData(byte[] dataPacket)
    {
        var pkgIdx = 0;
        _registers.DoOnRead(Rf69.REG_00_Fifo, _ =>
        {
            _registers.Poke(Rf69.REG_00_Fifo, dataPacket[pkgIdx++]);
        });

        // simulate the receiver updating the IRQFLAGS2 register to indicate a data
        // packed was received
        _registers.Poke(Rf69.REG_28_IrqFlags2, Rf69.IRQFLAGS2_PAYLOADREADY);
    }

    private byte[] BuildPacket(byte[] data,
        byte toAddress = RadioHead.BROADCAST_ADDRESS,
        byte fromAddress = RadioHead.BROADCAST_ADDRESS,
        byte headerId = 0x00,
        byte headerFlags = 0x00)
    {
        var packet = new List<byte>()
        {
            (byte)(data.Length + Rf69.HEADER_LEN),
            toAddress,
            fromAddress,
            headerId,
            headerFlags
        };
        packet.AddRange(data);

        return packet.ToArray();
    }

    // GIVEN: an instance of the Rf69 class
    // WHEN: WaitAvailable() is called
    // THEN: the method returns when the interrupt signals a packet has been received
    [TestCase((ushort)0)]
    [TestCase((ushort)10)]
    public void WaitAvailable(ushort pollDelay)
    {
        // ARRANGE:
        byte[] expected = [1, 2, 3, 4];

        _radio.Init();

        var packet = BuildPacket(expected);
        MockReceiveData(packet);

        _radio.SetModeRx();

        // simulate the receiver notifying the MPU a received data packet is ready by
        // activating the interrupt pin
        _interruptPin.Write(PinValue.Low);

        // ACT:
        _radio.WaitAvailable(pollDelay);

        // ASSERT:
        Assert.Pass();
    }

    // GIVEN: an instance of the Rf69 class
    // WHEN: WaitAvailableTimeout() is called
    //       AND a packet is received before the timeout expires
    // THEN: the method returns true
    [TestCase((ushort)100, (ushort)0)]
    [TestCase((ushort)100, (ushort)10)]
    public void WaitAvailableTimeout(ushort timeout, ushort pollDelay)
    {
        // ARRANGE:
        byte[] data = [1, 2, 3, 4];

        _radio.Init();

        var packet = BuildPacket(data);
        MockReceiveData(packet);

        _radio.SetModeRx();

        // simulate the receiver notifying the MPU a received data packet is ready by
        // activating the interrupt pin
        _interruptPin.Write(PinValue.Low);

        // ACT:
        var result = _radio.WaitAvailableTimeout(timeout, pollDelay);

        // ASSERT:
        Assert.That(result, Is.True);
    }

    // GIVEN: an instance of the Rf69 class
    // WHEN: WaitAvailableTimeout() is called
    //       AND a packet is not received before the timeout expires
    // THEN: the method returns false
    [TestCase((ushort)100, (ushort)0)]
    [TestCase((ushort)100, (ushort)10)]
    public void WaitAvailableTimeoutNoPacket(ushort timeout, ushort pollDelay)
    {
        // ARRANGE:
        byte[] data = [1, 2, 3, 4];

        _radio.Init();

        var packet = BuildPacket(data);
        MockReceiveData(packet);

        _radio.SetModeRx();

        // ACT:
        var result = _radio.WaitAvailableTimeout(timeout, pollDelay);

        // ASSERT:
        Assert.That(result, Is.False);
    }

    // GIVEN: an instance of the Rf69 class
    //        AND the radio is not in Tx mode
    // WHEN: WaitPacketSent() is called
    // THEN: return true
    [Test]
    public void WaitPacketSend()
    {
        // ARRANGE:
        _radio.Init();

        // ACT:
        var actual = _radio.WaitPacketSent(100);

        // ASSERT:
        Assert.That(actual, Is.True);
    }
}
