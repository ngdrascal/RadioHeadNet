using System.Device.Gpio;
using System.Device.Spi;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using UnitTestLogger;

namespace RadioHeadNet.Tests;

[TestFixture, ExcludeFromCodeCoverage]
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
                builder.AddFilter(nameof(Rf69RegistersFake) + ".states", LogLevel.None);
            });

        var driver = new GpioDriverFake();
        var controller = new GpioController(PinNumberingScheme.Board, driver);
        var deviceSelectPin = controller.OpenPin(0);
        _registers = new Rf69RegistersFake(deviceSelectPin, _loggerFactory);
        var spiConnSetting = new SpiConnectionSettings(0);
        var spiDevice = new SpiDeviceFake(spiConnSetting, controller, _registers, _loggerFactory);
        _radio = new RhRf69(deviceSelectPin, spiDevice, _loggerFactory);
    }

    [TearDown]
    public void TearDown()
    {
        _loggerFactory.Dispose();
    }

    // GIVEN: an instance of the RhRf69 class
    // WHEN: TemperatureRead() is called
    // THEN: the temperature registers are read
    [Test]
    public void TemperatureRead()
    {
        // ARRANGE:
        _registers.Poke(RhRf69.REG_4E_TEMP1, 0xFF);
        _registers.DoAfterWrite(RhRf69.REG_4E_TEMP1,
            _ => { _registers.Poke(RhRf69.REG_4E_TEMP1, 0x04); });
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


    // GIVEN: an instance of the RhRf69 class who's idle-mode has not been set
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

    // GIVEN: an instance of the RhRf69 class who's idle-mode has been set
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

    // GIVEN: an instance of the RhRf69 class
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

    // GIVEN: an instance of the RhRf69 class
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

    // GIVEN: an instance of the RhRf69 class
    // WHEN: Sleep() is called
    // THEN: the OpMode register's mode bits should be set correctly
    [Test]
    public void Sleep()
    {
        // ARRANGE:
        _registers.Poke(RhRf69.REG_01_OPMODE, 0xFF);

        // ACT:
        _radio.Sleep();

        // ASSERT:
        Assert.That(_registers.Peek(RhRf69.REG_01_OPMODE) & 0b00011100, Is.EqualTo(RhRf69.OPMODE_MODE_SLEEP));
    }

    // GIVEN: an instance of the RhRf69 class
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

    // GIVEN: an instance of the RhRf69 class
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

    // GIVEN: an instance of the RhRf69 class
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

    // GIVEN: an instance of the RhRf69 class
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
        _registers.Poke(RhRf69.REG_2E_SYNCCONFIG, 0x00);
        _registers.Poke(RhRf69.REG_2F_SYNCVALUE1 + 0, 0xFF);
        _registers.Poke(RhRf69.REG_2F_SYNCVALUE1 + 1, 0xFF);
        _registers.Poke(RhRf69.REG_2F_SYNCVALUE1 + 2, 0xFF);
        _registers.Poke(RhRf69.REG_2F_SYNCVALUE1 + 3, 0xFF);

        var syncWordList = new List<byte> { byte1 };
        if (byte2 != null) syncWordList.Add(byte2.Value);
        if (byte3 != null) syncWordList.Add(byte3.Value);
        if (byte4 != null) syncWordList.Add(byte4.Value);
        var syncWords = syncWordList.ToArray();

        // ACT:
        _radio.SetSyncWords(syncWords);

        // ASSERT:
        Assert.That(_registers.WriteCount(RhRf69.REG_2E_SYNCCONFIG), Is.EqualTo(1));
        Assert.That(_registers.Peek(RhRf69.REG_2E_SYNCCONFIG), Is.EqualTo(expected));

        for (var i = 0; i < syncWords.Length; i++)
        {
            Assert.That(_registers.WriteCount((byte)(RhRf69.REG_2F_SYNCVALUE1 + i)), Is.EqualTo(1));
            Assert.That(_registers.Peek((byte)(RhRf69.REG_2F_SYNCVALUE1 + i)), Is.EqualTo(syncWords[i]));
        }
    }

    // GIVEN: an instance of the RhRf69 class
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

    // GIVEN: an instance of the RhRf69 class
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
        Assert.That(_registers.Peek((RhRf69.REG_2E_SYNCCONFIG & 0x80)) >> 7, Is.EqualTo(0));
    }

    // GIVEN: an instance of the RhRf69 class
    // WHEN: SetPreambleLength() is called
    // THEN: the Preamble Length registers should be set correctly
    [Test]
    public void SetPreambleLength()
    {
        // ARRANGE:
        _registers.Poke(RhRf69.REG_2C_PREAMBLEMSB, 0xFF);
        _registers.Poke(RhRf69.REG_2D_PREAMBLELSB, 0xFF);

        const ushort length = 257;

        // ACT:
        _radio.SetPreambleLength(length);

        // ASSERT:
        Assert.That(_registers.Peek(RhRf69.REG_2C_PREAMBLEMSB), Is.EqualTo((byte)(length >> 8)));
        Assert.That(_registers.Peek(RhRf69.REG_2D_PREAMBLELSB), Is.EqualTo((byte)(length & 0xFF)));
    }

    // GIVEN: an instance of the RhRf69 class
    // WHEN: SetEncryptionKey() is called with a 16 byte key
    // THEN: the AES-On flag is set
    //       AND the Encryption Key registers should be set correctly
    [Test]
    public void SetEncryptionKeyLength16()
    {
        // ARRANGE:
        _registers.Poke(RhRf69.REG_3D_PACKETCONFIG2, 0x00);
        for (var i = 0; i < 16; i++)
            _registers.Poke((byte)(RhRf69.REG_3E_AESKEY1 + i), 0xFF);

        byte[] key = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16];

        // ACT:
        _radio.SetEncryptionKey(key);

        // ASSERT:
        Assert.That(_registers.Peek(RhRf69.REG_3D_PACKETCONFIG2) & 0x01, Is.EqualTo(1));
        for (var i = 0; i < 16; i++)
            Assert.That(_registers.Peek((byte)(RhRf69.REG_3E_AESKEY1 + i)), Is.EqualTo(key[i]));
    }

    // GIVEN: an instance of the RhRf69 class
    // WHEN: SetEncryptionKey() is called with a 0 byte key
    // THEN: the AES-On flag is cleared
    [Test]
    public void SetEncryptionKeyLength0()
    {
        // ARRANGE:
        _registers.Poke(RhRf69.REG_3D_PACKETCONFIG2, 0xFF);

        byte[] key = [];

        // ACT:
        _radio.SetEncryptionKey(key);

        // ASSERT:
        Assert.That(_registers.Peek(RhRf69.REG_3D_PACKETCONFIG2) & 0x01, Is.EqualTo(0));
    }

    // GIVEN: an instance of the RhRf69 class
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

    // GIVEN: an instance of the RhRf69 class
    //        AND using the default settings
    //        AND the radio is in Rx mode
    // WHEN: Send() is called
    // THEN: a data packet is constructed with the correct length preamble
    //       AND the correct sync words
    //       AND if encryption is enabled, is encrypted with the correct key
    //       AND is broadcast by the radio
    [Test]
    public void Send()
    {
        // ARRANGE:
        _radio.Init();

        byte[] data = [1, 2, 3, 4];
        var expected = BuildPacketWithDefaults([1, 2, 3, 4]);

        var actual = new List<byte>();

        _registers.DoAfterWrite(RhRf69.REG_00_FIFO,
            _ =>
            {
                actual.Add(_registers.Peek(RhRf69.REG_00_FIFO));
            });

        // ACT:
        var result = _radio.Send(data);

        // ASSERT:
        Assert.That(result, Is.True);
        Assert.That(actual, Is.EqualTo(expected));
        Assert.That(_registers.Peek(RhRf69.REG_01_OPMODE), Is.EqualTo(RhRf69.OPMODE_MODE_TX));
    }

    // GIVEN: an instance of the RhRf69 class
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

    // GIVEN: an instance of the RhRf69 class
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
        MockSendData(expected);
        _radio.SetModeRx();
        _radio.HandleInterrupt(this, new PinValueChangedEventArgs(PinEventTypes.Falling, 1));

        // ACT:
        var result = _radio.Available();

        // ASSERT:
        Assert.That(result, Is.True);
    }


    // GIVEN: an instance of the RhRf69 class
    //        AND IRQFLAGS2.PAYLOADREADY flag is set
    // WHEN: Receive() is called
    // THEN: the data received matches the data sent
    //       AND the RSSI value is correct
    [Test]
    public void Receive()
    {
        // ARRANGE:
        byte[] expected = [1, 2, 3, 4];

        _radio.Init();
        MockSendData(expected);
        _radio.SetModeRx();
        _radio.HandleInterrupt(this, new PinValueChangedEventArgs(PinEventTypes.Falling, 1));
        
        // ACT:
        var result = _radio.Receive(out var actual);

        // ASSERT:
        Assert.That(result, Is.True);
        Assert.That(actual, Is.EqualTo(expected));
    }

    private void MockSendData(byte[] data)
    {
        var dataPacket = BuildPacketWithDefaults(data);

        var pkgIdx = 0;
        _registers.DoOnRead(RhRf69.REG_00_FIFO, _ =>
        {
            _registers.Poke(RhRf69.REG_00_FIFO, dataPacket[pkgIdx++]);
        });

        _registers.Poke(RhRf69.REG_28_IRQFLAGS2, RhRf69.IRQFLAGS2_PAYLOADREADY);
    }


    private byte[] BuildPacketWithDefaults(byte[] data)
    {
        var result = new List<byte>()
        {
            (byte)(data.Length + RhRf69.RH_RF69_HEADER_LEN),
            RadioHead.RH_BROADCAST_ADDRESS, // _radio._txHeaderFrom,
            RadioHead.RH_BROADCAST_ADDRESS, // _radio.HeaderTo(), 
            _radio.headerId(),
            _radio.HeaderFlags()
        };
        result.AddRange(data);

        return result.ToArray();
    }
}
