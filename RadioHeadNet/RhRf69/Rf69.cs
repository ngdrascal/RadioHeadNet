using System.Device.Gpio;
using System.Device.Spi;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace RadioHeadNet.RhRf69;

public partial class Rf69 : RhSpiDriver
{
    private readonly ILogger _logger;
    private static readonly object CriticalSection = new();

    /// The radio OP Mode to use when Mode is RHMode.Idle
    private byte _idleMode;

    /// The reported device type
    private byte _deviceType;

    /// The selected output power in dBm
    private sbyte _power;

    /// The message length in _rxBuf
    private byte _rxBufLen;

    /// Array of octets of the last received message or the next to transmit message
    private readonly byte[] _rxBuf = new byte[MAX_MESSAGE_LEN];

    /// True when there is a valid message in the Rx buffer
    private bool _rxBufValid;

    /// <summary>
    /// Time in millis since the last preamble was received (and the last time the RSSI
    /// was measured)
    /// </summary>
    public long LastPreambleTime { get; private set; }

    /// <summary>
    /// Constructor. You can have multiple instances, but each instance must have its own
    /// interrupt and slave select pin. After constructing, you must call Init() to
    /// initialise the interface and the radio module. A maximum of 3 instances can
    /// co-exist on one processor, provided there are sufficient distinct interrupt lines,
    /// one for each instance.
    /// </summary>
    /// <param name="deviceSelectPin"></param>
    /// <param name="spi"></param>
    /// <param name="loggerFactory"></param>
    public Rf69(GpioPin deviceSelectPin, SpiDevice spi, ILoggerFactory loggerFactory)
        : base(deviceSelectPin, spi)
    {
        _logger = loggerFactory.CreateLogger<Rf69>();
        _idleMode = OPMODE_MODE_STDBY;
    }

    /// <summary>
    /// Initialises this instance and the radio module connected to it.
    /// The following steps are taken:
    /// - Initialise the slave select pin and the SPI interface library
    /// - Checks the connected RF69 module can be communicated
    /// - Attaches an interrupt handler
    /// - Configures the RF69 module
    /// - Sets the frequency to 434.0 MHz
    /// - Sets the modem data rate to FSK_Rb2Fd5
    /// </summary>
    /// <returns>true if everything was successful</returns>
    public override bool Init()
    {
        _logger.LogTrace("---> {0}()", nameof(Init));

        if (!base.Init())
            return false;

        // Get the device type and check it. This also tests whether we are really
        // connected to a device.  My test devices return 0x24.
        _deviceType = ReadFrom(REG_10_Version);
        if (_deviceType is 0x00 or 0xFF)
            return false;

        SetModeIdle();

        // Configure important RF69 registers
        // Here we set up the standard packet format for use by the RH_RF69 library:
        //    4 bytes preamble
        //    2 SYNC words 2d, d4
        //    2 CRC CCITT octets computed on the header, length and data (this in the
        //      modem config data)
        //    0 to 60 bytes data
        //    RSSI Threshold -114dBm
        // We don't use the RH_RF69s address filtering: instead we prepend our own
        // headers to the beginning of the RH_RF69 payload

        // thresh 15 is default
        WriteTo(REG_3C_FifoThresh, FIFOTHRESH_TXSTARTCONDITION_NOTEMPTY | 0x0F);

        // RSSITHRESH is default
        // WriteTo(REG_29_RssiThresh, 220); // -110 dbM

        // SYNCCONFIG is default. SyncSize is set later by setSyncWords()
        // WriteTo(REG_2E_SyncConfig, SYNCCONFIG_SYNCON); // auto, tolerance 0

        // PAYLOADLENGTH is default
        // WriteTo(REG_38_PayloadLength, RH_RF69_FIFO_SIZE); // max size only for RX

        // PACKETCONFIG 2 is default
        WriteTo(REG_6F_TestDagc, TESTDAGC_CONTINUOUSDAGC_IMPROVED_LOWBETAOFF);

        // If high power boost set previously, disable it
        WriteTo(REG_5A_TestPa1, TESTPA1_NORMAL);
        WriteTo(REG_5C_TestPa2, TESTPA2_NORMAL);

        // The following can be changed later by the user if necessary.
        // Set up default configuration
        byte[] syncWords = [0x2d, 0xd4];
        SetSyncWords(syncWords); // Same as RF22's

        // Reasonably fast and reliable default speed and modulation
        SetModemConfig(ModemConfigChoice.GFSK_Rb250Fd250);

        // 3 would be sufficient, but this is the same as RF22's
        SetPreambleLength(4);

        // An innocuous ISM frequency, same as RF22's
        SetFrequency(434.0f);

        // No encryption
        SetEncryptionKey([]);

        // +13dBm, same as power-on default
        SetTxPower(13, false);

        _logger.LogTrace("<--- {0}()", nameof(Init));

        return true;
    }

    /// <summary>
    /// Interrupt handler for this instance.  RH_RF69 is unusual in that it has several
    /// interrupt lines, and not a single, combined one.  We use the single interrupt
    /// line to get PACKETSENT and PAYLOADREADY interrupts.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="arg"></param>
    public void HandleInterrupt(object sender, PinValueChangedEventArgs arg)
    {
        // Get the interrupt cause
        var irqFlags2 = ReadFrom(REG_28_IrqFlags2);

        _logger.LogTrace("-->{0}(): mode={1}, IrqFlags2={2}", nameof(HandleInterrupt), Mode.ToString(), irqFlags2.ToString());

        if (Mode == Rh69Modes.Tx && (irqFlags2 & IRQFLAGS2_PACKETSENT) != 0)
        {
            // A transmitter message has been fully sent
            SetModeIdle(); // Clears FIFO
            TxGood++;
        }

        // Must look for PAYLOADREADY, not CRCOK, since only PAYLOADREADY occurs _after_
        // AES decryption has been done
        if (Mode == Rh69Modes.Rx && (irqFlags2 & IRQFLAGS2_PAYLOADREADY) != 0)
        {
            // A complete message has been received with good CRC
            // Absolute value of the RSSI in dBm, 0.5dB steps.  RSSI = -RssiValue/2 [dBm]
            LastRssi = (short)-(ReadFrom(REG_24_RssiValue) >> 1);

            LastPreambleTime = DateTime.Now.Ticks;

            SetModeIdle();

            // save it in our buffer
            ReadFifo();
        }

        _logger.LogTrace("<--{0}()", nameof(HandleInterrupt));
    }

    // Low level function reads the FIFO and checks the address
    // Caution: since we put our headers in what the RH_RF69 considers to be the payload, if encryption is enabled
    // we have to suffer the cost of decryption before we can determine whether the address is acceptable.
    // Performance issue?
    private void ReadFifo()
    {
        lock (CriticalSection)
        {
            SelectDevice();
            WriteByte(REG_00_Fifo); // Send the start address with the write mask off
            var payloadLen = ReadByte(); // First byte is payload len (counting the headers)
            if (payloadLen is <= MAX_ENCRYPTABLE_PAYLOAD_LEN and >= HEADER_LEN)
            {
                RxHeaderTo = ReadByte();
                // check the address
                if (Promiscuous ||
                    RxHeaderTo == ThisAddress ||
                    RxHeaderTo == RadioHead.BroadcastAddress)
                {
                    // get the rest of the headers
                    RxHeaderFrom = ReadByte();
                    RxHeaderId = ReadByte();
                    RxHeaderFlags = ReadByte();

                    // and now the real payload
                    for (_rxBufLen = 0; _rxBufLen < (payloadLen - HEADER_LEN); _rxBufLen++)
                        _rxBuf[_rxBufLen] = ReadByte();

                    RxGood++;
                    _rxBufValid = true;
                }
            }

            DeselectDevice();
        }

        // NOTE: Any junk remaining in the FIFO will be cleared next time we go to
        //       receive Mode.
    }

    /// <summary>
    /// Reads the on-chip temperature sensor.
    /// The RF69 must be in Idle Mode (= RF69 Standby) to measure temperature.
    /// The measurement is uncalibrated and without calibration, you can expect it to be
    /// far from correct.
    /// </summary>
    /// <returns>The measured temperature, in degrees C from -40 to 85 (uncalibrated)</returns>
    public sbyte TemperatureRead()
    {
        // Caution: must be ins standby.
        // setModeIdle();
        WriteTo(REG_4E_Temp1, TEMP1_TEMPMEASSTART); // Start the measurement

        // Wait for the measurement to complete
        var reg4E = ReadFrom(REG_4E_Temp1);
        while ((reg4E & TEMP1_TEMPMEASRUNNING) != 0)  // wait for bit 4 to equal 0
        {
            reg4E = ReadFrom(REG_4E_Temp1);
        }

        return (sbyte)(166 - ReadFrom(REG_4F_Temp2)); // Very approximate, based on observation
    }

    /// <summary>
    /// Sets the transmitter and receiver center frequency
    /// </summary>
    /// <param name="center">center Frequency in MHz. 240.0 to 960.0. Caution, RF69 comes in several
    /// different frequency ranges, and setting a frequency outside that range of your radio will probably not work
    /// </param>
    /// <returns>true if the selected frequency center is within range</returns>
    public bool SetFrequency(float center)
    {
        // FRF = FRF / FSTEP
        var frf = (uint)((center * 1000000.0) / FSTEP);
        WriteTo(REG_07_FrfMsb, (byte)((frf >> 16) & 0xFF));
        WriteTo(REG_08_FrfMid, (byte)((frf >> 8) & 0xFF));
        WriteTo(REG_09_FrfLsb, (byte)(frf & 0xFF));

        return true;
    }

    /// <summary>
    /// Reads and returns the current RSSI value. 
    /// Causes the current signal strength to be measured and returned.
    /// If you want to find the RSSI of the last received message, use LastRssi() instead.
    /// </summary>
    /// <returns>The current RSSI value on units of 0.5dB.</returns>
    public sbyte RssiRead()
    {
        // Force a new value to be measured
        // Hmmm, this hangs forever!
        // spiWrite(REG_23_RssiConfig, RSSICONFIG_RSSISTART);
        // while (!(spiRead(REG_23_RssiConfig) & RSSICONFIG_RSSIDONE)) {}

        return (sbyte)-(ReadFrom(REG_24_RssiValue) >> 1);
    }

    /// <summary>
    /// Sets the parameters for the RF69 OPMODE.
    /// This is a low level device access function and should not normally need to be used by user code. 
    /// Instead, can use stModeRx(), setModeTx(), setModeIdle()
    /// </summary>
    /// <param name="mode">Mode RF69 OPMODE to set, one of OPMODEMode_*.</param>
    private void SetOpMode(byte mode)
    {
        var clrMask = InvertByte(OPMODE_MODE);

        var curValue = ReadFrom(REG_01_OpMode);
        var newValue = (byte)(curValue & clrMask);
        newValue |= (byte)(mode & OPMODE_MODE);
        WriteTo(REG_01_OpMode, newValue);

        // Wait for Mode to change.
        while ((ReadFrom(REG_27_IrqFlags1) & IRQFLAGS1_MODEREADY) == 0) { }
    }

    // Do this in a method instead of inline because the compiler doesn't like casting a
    // negative constant to a byte.
    private static byte InvertByte(byte input)
    {
        return (byte)~input;
    }

    /// <summary>
    /// If current Mode is Rx or Tx changes it to Idle. If the transmitter or receiver is running, 
    /// disables them.
    /// </summary>
    public void SetModeIdle()
    {
        if (Mode != Rh69Modes.Idle)
        {
            if (_power >= 18)
            {
                // If high power boost, return power amp to receive Mode
                WriteTo(REG_5A_TestPa1, TESTPA1_NORMAL);
                WriteTo(REG_5C_TestPa2, TESTPA2_NORMAL);
            }

            SetOpMode(_idleMode);
            Mode = Rh69Modes.Idle;
        }
    }

    /// <summary>
    /// If current Mode is Tx or Idle, changes it to Rx.  Starts the receiver.
    /// </summary>
    public void SetModeRx()
    {
        if (Mode != Rh69Modes.Rx)
        {
            if (_power >= 18)
            {
                // If high power boost, return power amp to receive Mode
                WriteTo(REG_5A_TestPa1, TESTPA1_NORMAL);
                WriteTo(REG_5C_TestPa2, TESTPA2_NORMAL);
            }

            // Set interrupt line 0 PAYLOADREADY
            WriteTo(REG_25_DioMapping1, DIOMAPPING1_DIO0MAPPING_01);

            SetOpMode(OPMODE_MODE_RX); // Clears FIFO
            Mode = Rh69Modes.Rx;
        }
    }

    /// <summary>
    /// If current Mode is Rx or Idle, changes it to Tx.  Starts the transmitter.
    /// </summary>
    public void SetModeTx()
    {
        if (Mode != Rh69Modes.Tx)
        {
            if (_power >= 18)
            {
                // Set high power boost Mode
                // Note that OCP defaults to ON so no need to change that.
                WriteTo(REG_5A_TestPa1, TESTPA1_BOOST);
                WriteTo(REG_5C_TestPa2, TESTPA2_BOOST);
            }

            // Set interrupt line 0 PACKETSENT
            WriteTo(REG_25_DioMapping1, DIOMAPPING1_DIO0MAPPING_00);
            
            SetOpMode(OPMODE_MODE_TX); // Clears FIFO
            Mode = Rh69Modes.Tx;
        }
    }

    /// <summary>
    /// Sets the transmitter power output level.
    /// Be a good neighbour and set the lowest power level you need.
    /// Caution: legal power limits may apply in certain countries.
    /// After Init(), the power will be set to 13dBm for a low power module.
    /// If you are using a high power module such as an RFM69HW, you MUST set the power level
    /// with the isHighPowerModule flag set to true. Else you wil get no measurable power output.
    /// Similarly, if you are not using a high power module, you must NOT set the isHighPowerModule
    /// (which is the default)
    /// </summary>
    /// <param name="power">power Transmitter power level in dBm. For RF69W (isHighPowerModule = false),
    /// valid values are from -18 to +13.; Values outside this range are trimmed.
    /// For RF69HW (isHighPowerModule = true), valid values are from -2 to +20.
    /// Caution: at +20dBm, duty cycle is limited to 1% and a 
    /// maximum VSWR of 3:1 at the antenna port.</param>
    /// <param name="isHighPowerModule">Set to true if the connected module is a high
    /// power module RFM69HW</param>
    public void SetTxPower(sbyte power, bool isHighPowerModule)
    {
        _power = power;
        byte paLevel;
        if (isHighPowerModule)
        {
            if (_power < -2)
                _power = -2; // RFM69HW only works down to -2.
            else if (_power > 20)
                _power = 20;

            if (_power <= 13)
            {
                // -2dBm to +13dBm
                // Need PA1 exclusively on RFM69HW
                paLevel = (byte)(PALEVEL_PA1ON | ((_power + 18) & PALEVEL_OUTPUTPOWER));
            }
            else if (_power >= 18)
            {
                // +18dBm to +20dBm
                // Need PA1+PA2
                // Also need PA boost settings change when tx is turned on and off, see setModeTx()
                paLevel = (byte)(PALEVEL_PA1ON | PALEVEL_PA2ON | ((_power + 11) & PALEVEL_OUTPUTPOWER));
            }
            else
            {
                // +14dBm to +17dBm
                // Need PA1+PA2
                paLevel = (byte)(PALEVEL_PA1ON | PALEVEL_PA2ON | ((_power + 14) & PALEVEL_OUTPUTPOWER));
            }
        }
        else
        {
            if (_power < -18)
                _power = -18;
            if (_power > 13)
                _power = 13; // limit for RFM69W

            paLevel = (byte)(PALEVEL_PA0ON | ((_power + 18) & PALEVEL_OUTPUTPOWER));
        }

        WriteTo(REG_11_PaLevel, paLevel);
    }

    /// <summary>
    /// Sets all the registers required to configure the data modem in the RF69, including the data rate, 
    /// bandwidths etc. You can use this to configure the modem with custom configurations if none of the 
    /// canned configurations in ModemConfigChoice suit you.
    /// </summary>
    /// <param name="config">config A ModemConfig structure containing values for the
    /// modem configuration registers.</param>
    public void SetModemRegisters(ModemConfig config)
    {
        BurstWriteTo(REG_02_DataModul,
            [config.Reg02, config.Reg03, config.Reg04, config.Reg05, config.Reg06]);

        BurstWriteTo(REG_19_RxBw, [config.Reg19, config.Reg1A]);

        WriteTo(REG_37_PacketConfig1, config.Reg37);
    }

    /// <summary>
    /// Select one of the predefined modem configurations. If you need a modem configuration not provided 
    /// here, use setModemRegisters() with your own ModemConfig. The default after Init() is
    /// RH_RF69::GFSK_Rb250Fd250.
    /// </summary>
    /// <param name="choice">index The configuration choice.</param>
    /// <returns>\return true if index is a valid choice.</returns>
    public bool SetModemConfig(ModemConfigChoice choice)
    {
        var idx = (byte)choice;
        if (idx > MODEM_CONFIG_TABLE.Length - 1)
            return false;

        SetModemRegisters(MODEM_CONFIG_TABLE[idx]);

        return true;
    }

    /// <summary>
    /// Starts the receiver and checks whether a received message is Available.
    /// This can be called multiple times in a timeout loop
    ///  </summary>
    /// <returns>true if a complete, valid message has been received and the message is
    /// retrievable by Receive()
    /// </returns>
    public override bool Available()
    {
        if (Mode == Rh69Modes.Tx)
            return false;

        // Make sure we are receiving
        SetModeRx();

        return _rxBufValid;
    }

    /// <summary>
    /// Turns the receiver on if it not already on.  If there is a valid message
    /// available, copy it to buf and return true  else return false.
    /// If a message is copied,  (Caution, 0 length messages are permitted).
    /// You should be sure to call this function frequently enough to not miss any messages
    /// It is recommended that you call it in your main loop.
    /// </summary>
    /// <param name="buffer">buffer to copy the received message</param>
    /// <returns>true if a valid message was copied to buffer</returns>
    /// <exception cref="IndexOutOfRangeException"></exception>
    public override bool Receive(out byte[] buffer)
    {
        if (!Available())
        {
            buffer = [];
            return false;
        }

        buffer = new byte[_rxBufLen];
        lock (CriticalSection)
        {
            Array.Copy(_rxBuf, buffer, _rxBufLen);
            _rxBufValid = false; // Got the most recent message
        }

        return true;
    }

    /// <summary>
    /// For use with devices that don't support interrupts. Polls the device to determine
    /// if a packet is available.
    /// </summary>
    /// <param name="timeout">milliseconds to wait before a packet before return false</param>
    /// <returns></returns>
    public bool PollAvailable(int timeout)
    {
        if (Mode == Rh69Modes.Tx)
            return false;

        SetModeRx();

        var stopwatch = new Stopwatch();
        stopwatch.Start();

        var irqFlags2 = ReadFrom(REG_28_IrqFlags2);
        while ((irqFlags2 & IRQFLAGS2_PAYLOADREADY) == 0 && stopwatch.ElapsedMilliseconds < timeout)
        {
            irqFlags2 = ReadFrom(REG_28_IrqFlags2);
        }

        stopwatch.Stop();

        if ((irqFlags2 & IRQFLAGS2_PAYLOADREADY) == 0)
            return false;

        HandleInterrupt(this, new PinValueChangedEventArgs(PinEventTypes.Rising, 1));
        return true;
    }

    /// <summary>
    /// Waits until any previous transmit packet is finished being transmitted with
    /// WaitPacketSent().  Then loads a message into the transmitter and starts the
    /// transmitter. Note that a message length of 0 is NOT permitted. 
    /// </summary>
    /// <param name="data">array of byte data to be sent</param>
    /// <returns>true if the message length was valid and it was correctly queued for
    /// transmit</returns>
    public override bool Send(byte[] data)
    {
        if (data.Length > MaxMessageLength)
            return false;

        WaitPacketSent();  // Make sure we don't interrupt an outgoing message
        SetModeIdle();     // Prevent RX while filling the fifo

        if (!WaitCAD())
            return false; // Check channel activity

        lock (CriticalSection)
        {
            SelectDevice();

            // Send the start address with the write mask on
            WriteByte(REG_00_Fifo | SPI_WRITE_MASK);

            // Include length of headers
            WriteByte((byte)(data.Length + HEADER_LEN));

            // First the 4 headers
            WriteByte(TxHeaderTo);
            WriteByte(TxHeaderFrom);
            WriteByte(TxHeaderId);
            WriteByte(TxHeaderFlags);

            // Now the payload
            foreach (var d in data)
                WriteByte(d);

            DeselectDevice();
        }

        SetModeTx(); // Start the transmitter
        return true;
    }

    /// <summary>
    /// Sets the length of the preamble in bytes. 
    /// Caution: this should be set to the same value on all nodes in your network.
    /// Default is 4.
    /// </summary>
    /// <param name="length">Preamble length in bytes</param>
    public void SetPreambleLength(ushort length)
    {
        WriteTo(REG_2C_PreambleMsb, (byte)(length >> 8));
        WriteTo(REG_2D_PreambleLsb, (byte)(length & 0xFF));
    }

    /// <summary>
    /// Sets the sync words for transmit and receive 
    /// Caution: SyncWords should be set to the same value on all nodes in your network.
    /// Nodes with different SyncWords set will never receive each others messages, so
    /// different SyncWords can be used to isolate different networks from each other.
    /// Default is { 0x2d, 0xd4 }.
    /// Caution: tests here show that with a single sync word (ie where len == 1), RFM69
    /// reception can be unreliable.
    /// To disable sync word generation and detection, call with a zero length array:
    /// setSyncWords([]);
    /// </summary>
    /// <param name="syncWords">Byte array of sync words, 1 to 4 octets long. 0 length
    /// if no sync words to be used.</param>
    public void SetSyncWords(byte[] syncWords)
    {
        if (syncWords.Length > 4)
            throw new ArgumentException($"{nameof(SetSyncWords)}: syncWords must be 1 to 4 octets long.");

        var syncConfig = ReadFrom(REG_2E_SyncConfig);
        if (syncWords.Length is >= 1 and <= 4)
        {
            BurstWriteTo(REG_2F_SyncValue1, syncWords);
            syncConfig |= SYNCCONFIG_SYNCON;
        }
        else
            syncConfig &= InvertByte(SYNCCONFIG_SYNCON);

        syncConfig &= InvertByte(SYNCCONFIG_SYNCSIZE);
        syncConfig |= (byte)((syncWords.Length - 1) << 3);
        WriteTo(REG_2E_SyncConfig, syncConfig);
    }

    /// <summary>
    /// Enables AES encryption and sets the AES encryption key, used to encrypt and
    /// decrypt all messages. The default is disabled.
    /// </summary>
    /// <param name="key">The key to use. Must be 16 bytes long. The same key must be
    /// installed in other instances of RF69, otherwise communications will not work
    /// correctly. If key is NULL, encryption is disabled, which is the default.</param>
    public void SetEncryptionKey(byte[] key)
    {
        if (key.Length == 16)
        {
            BurstWriteTo(REG_3E_AesKey1, key);

            WriteTo(REG_3D_PacketConfig2, (byte)(ReadFrom(REG_3D_PacketConfig2) | PACKETCONFIG2_AESON));
        }
        else if (key.Length == 0)
        {
            WriteTo(REG_3D_PacketConfig2, (byte)(ReadFrom(REG_3D_PacketConfig2) & ~PACKETCONFIG2_AESON));
        }
        else
        {
            throw new ArgumentException($"{nameof(SetEncryptionKey)}: key must be 16 bytes long.");
        }
    }

    /// <summary>
    /// The maximum message length supported by this driver
    /// </summary>
    public override byte MaxMessageLength => MAX_MESSAGE_LEN;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="idleMode"></param>
    /// <exception cref="ArgumentException"></exception>
    public void SetIdleMode(byte idleMode)
    {
        if (idleMode is OPMODE_MODE_SLEEP or OPMODE_MODE_STDBY)
            _idleMode = idleMode;
        else
            throw new ArgumentException("Only SLEEP and STANDBY modes allowed", nameof(idleMode));
    }

    /// <summary>
    /// Puts the radio into low-power Sleep Mode.  If successful, the transport will stay
    /// in Sleep Mode until woken by changing Mode to idle, transmit or receive
    /// (e.g. - by calling Send(), Receive(), Available(),  etc.)
    /// Caution: there is a time penalty as the radio takes time to wake from Sleep Mode.
    /// </summary>
    /// <returns>true if Sleep Mode was successfully entered</returns>
    public override bool Sleep()
    {
        if (Mode != Rh69Modes.Sleep)
        {
            WriteTo(REG_01_OpMode, OPMODE_MODE_SLEEP);
            Mode = Rh69Modes.Sleep;
        }

        return true;
    }

    /// <summary>
    /// Return the integer value of the device type as read from the device in from
    /// REG_10_Version.  Expect 0x24, depending on the type of device actually connected.
    /// </summary>
    public byte DeviceType => _deviceType;
}
