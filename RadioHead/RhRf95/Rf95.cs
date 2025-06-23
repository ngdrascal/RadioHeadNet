#pragma warning disable CA1825
// ReSharper disable RedundantNameQualifier
// ReSharper disable UseArrayEmptyMethod
// ReSharper disable UseCollectionExpression
// ReSharper disable ArrangeObjectCreationWhenTypeEvident
// ReSharper disable ChangeFieldTypeToSystemThreadingLock
// ReSharper disable MergeIntoPattern
// ReSharper disable RedundantExplicitArrayCreation

// ReSharper disable RedundantUsingDirective
using System;
using System.ComponentModel;
using System.Device.Gpio;
using System.Device.Spi;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace RadioHead.RhRf95
{
    public partial class Rf95 : RhGenericDriver
    {
        private static readonly object CriticalSection = new object();

        private readonly RhSpiDriver _spi;
        private ChangeDetectionMode _changeDetectionMode;
        private readonly ILogger _logger;

        // Number of octets in the buffer
        private byte _bufLen;

        // The receiver/transmitter buffer
        private byte[] _buf = new byte[MAX_PAYLOAD_LEN];

        private readonly byte[] _emptyBuffer = new byte[0];

        // True when there is a valid message in the buffer
        private bool _rxBufValid;

        // True if we are using the HF port (779.0 MHz and above)
        private bool _usingHfPort;

        // Last measured SNR, dB
        // private byte _lastSNR;

        // If true, sends CRCs in every packet and requires a valid CRC in every received packet
        private bool _enableCrc;

        // device ID
        private byte _deviceVersion;

        /// <summary>
        /// Constructor. You can have multiple instances, but each instance must have its own
        /// interrupt and slave select pin. After constructing, you must call init() to initialise the interface
        /// and the radio module. A maximum of 3 instances can co-exist on one processor, provided there are sufficient
        /// distinct interrupt lines, one for each instance.
        /// </summary>
        /// <param name="spi"> spi Pointer to the SPI interface object to use. 
        /// Defaults to the standard Arduino hardware SPI interface
        /// </param>
        /// <param name="changeDetectionMode"></param>
        /// <param name="logger"></param>
        public Rf95(RhSpiDriver spi, ChangeDetectionMode changeDetectionMode, ILogger logger)
        {
            _spi = spi;
            _changeDetectionMode = changeDetectionMode;
            _logger = logger;
            _enableCrc = true;
            _rxBufValid = false;
        }

        /// <summary>
        /// Initialise the Driver transport hardware and software.
        /// Leaves the radio in idle mode,
        /// with default configuration of: 434.0MHz, 13dBm, Bw = 125 kHz, Cr = 4/5, Sf = 128chips/symbol, CRC on
        /// </summary>
        /// <return>true if initialisation succeeded.</return>
        public override bool Init()
        {
            _logger.LogTrace("---> {0}()", nameof(Init));

            if (!base.Init())
                return false;

            // No way to check the device type :-(

            // Set sleep mode, so we can also set LORA mode:
            _spi.WriteTo(REG_01_OP_MODE, MODE_SLEEP | LONG_RANGE_MODE);

            // Wait for sleep mode to take over from say, CAD
            Thread.Sleep(10);

            // Check we are in sleep mode, with LORA set
            if (_spi.ReadFrom(REG_01_OP_MODE) != (MODE_SLEEP | LONG_RANGE_MODE))
            {
                //	Serial.println(spiRead(REG_01_OP_MODE), HEX);
                return false; // No device present?
            }

            // Set up FIFO
            // We configure so that we can use the entire 256 byte FIFO for either receive
            // or transmit, but not both at the same time
            _spi.WriteTo(REG_0E_FIFO_TX_BASE_ADDR, 0);
            _spi.WriteTo(REG_0F_FIFO_RX_BASE_ADDR, 0);

            // Packet format is preamble + explicit-header + payload + crc
            // Explicit Header Mode
            // payload is TO + FROM + ID + FLAGS + message data
            // RX mode is implemented with RXCONTINUOUS
            // max message data length is 255 - 4 = 251 octets

            SetModeIdle();

            // Set up default configuration
            // No Sync Words in LORA mode. ACTUALLY that's not correct, and for the RF95,
            // the default LoRaSync Word is 0x12 (i.e. - a private network) and it can be
            // changed at REG_39_SYNC_WORD
            SetModemConfig(ModemConfigChoice.Bw125Cr45Sf128); // Radio default

            // setModemConfig(Bw125Cr48Sf4096); // slow and reliable?

            SetPreambleLength(8); // Default is 8

            // An innocuous ISM frequency, same as RF22's
            SetFrequency(434.0f);

            // Lowish power
            SetTxPower(13);

            _logger.LogTrace("<--- {0}()", nameof(Init));

            return true;
        }

        /// <summary>
        /// Sets all the registers required to configure the data modem in the radio, including the bandwidth, 
        /// spreading factor etc. You can use this to configure the modem with custom configurations if none of the 
        /// canned configurations in ModemConfigChoice suit you.
        /// </summary>
        /// <param name="configuration">A ModemConfiguration structure containing values for the modem configuration registers.
        /// </param>
        public void SetModemRegisters(ModemConfiguration configuration)
        {
            _spi.WriteTo(REG_1D_MODEM_CONFIG1, configuration.Reg1D);
            _spi.WriteTo(REG_1E_MODEM_CONFIG2, configuration.Reg1E);
            _spi.WriteTo(REG_26_MODEM_CONFIG3, configuration.Reg26);
        }

        /// <summary>
        /// Select one of the predefined modem configurations. If you need a modem configuration not provided 
        /// here, use SetModemRegisters() with your own ModemConfiguration.
        /// Caution: the slowest protocols may require a radio module with TCXO temperature controlled oscillator
        /// for reliable operation.
        /// </summary>
        /// <param name="index">The configuration choice.</param>
        /// <returns>true if index is a valid choice.</returns>
        public bool SetModemConfig(ModemConfigChoice index)
        {
            if ((byte)index >= ModemConfigTable.Length)
                return false;

            SetModemRegisters(ModemConfigTable[(byte)index]);
            return true;
        }

        /// <summary>
        /// Tests whether a new message is available from the Driver. 
        /// On most drivers, this will also put the Driver into RhModes.Rx mode until
        /// a message is actually received by the transport, when it will be returned to RHModeIdle.
        /// This can be called multiple times in a timeout loop
        /// </summary>
        /// <returns>true if a new, complete, error-free uncollected message is available to be retrieved by Receive()
        /// </returns>
        // public override bool Available()
        // {
        //     // Multithreading support
        //     lock (CriticalSection)
        //     {
        //
        //         if (Mode == RhModes.Tx)
        //         {
        //             return false;
        //         }
        //
        //         SetModeRx();
        //         return _rxBufValid;
        //     } // Will be set by the interrupt handler when a good message is received
        // }
        public override bool Available()
        {
            if (Mode == RhModes.Tx)
                return false;

            // Make sure we are receiving
            SetModeRx();

            if (_changeDetectionMode == ChangeDetectionMode.Polling)
            {
                HandleInterrupt(this, new PinValueChangedEventArgs(PinEventTypes.Rising, -1));
            }

            return _rxBufValid;
        }

        /// <summary>
        /// Turns the receiver on if it not already on.
        /// If there is a valid message available, copy it to buffer and return true
        /// else return false.
        /// You should be sure to call this function frequently enough to not miss any messages.
        /// It is recommended that you call it in your main loop.
        /// </summary>
        /// <param name="buffer"></param>Location to copy the received message
        /// <returns>true if a valid message was copied to buffer</returns>
        public override bool Receive(out byte[] buffer)
        {
            if (!Available())
            {
                buffer = _emptyBuffer;
                return false;
            }

            // buffer = new byte[MAX_PAYLOAD_LEN];
            buffer = new byte[_bufLen - HEADER_LEN];

            lock (CriticalSection)
            {
                Array.Copy(_buf, HEADER_LEN, buffer, 0, _bufLen - HEADER_LEN);

                ClearRxBuf(); // This message accepted and cleared
            }

            return true;
        }

        /// <summary>
        /// Waits until any previous transmit packet is finished being transmitted with waitPacketSent().
        /// Then optionally waits for Channel Activity Detection (CAD) 
        /// to show the channel is clear (if the radio supports CAD) by calling waitCAD().
        /// Then loads a message into the transmitter and starts the transmitter. Note that a message length
        /// of 0 is permitted.
        /// </summary>
        /// <param name="data">Array of data to be sent</param>
        /// specify the maximum time in ms to wait. If 0 (the default) do not wait for CAD before transmitting.
        /// <returns>true if the message length was valid and it was correctly queued for transmit. Return false
        /// if CAD was requested and the CAD timeout timed out before clear channel was detected.
        /// </returns>
        public override bool Send(byte[] data)
        {
            if (data.Length > MAX_MESSAGE_LEN)
                return false;

            // Make sure we don't interrupt an outgoing message
            WaitPacketSent();

            SetModeIdle();

            if (!WaitCAD())
                return false; // Check channel activity

            // Position at the beginning of the FIFO
            _spi.WriteTo(REG_0D_FIFO_ADDR_PTR, 0);
            // The headers
            _spi.WriteTo(REG_00_FIFO, TxHeaderTo);
            _spi.WriteTo(REG_00_FIFO, TxHeaderFrom);
            _spi.WriteTo(REG_00_FIFO, TxHeaderId);
            _spi.WriteTo(REG_00_FIFO, TxHeaderFlags);
            // The message data
            _spi.BurstWriteTo(REG_00_FIFO, data);
            _spi.WriteTo(REG_22_PAYLOAD_LENGTH, (byte)(data.Length + HEADER_LEN));

            lock (CriticalSection)
            {
                SetModeTx(); // Start the transmitter
            }

            // when Tx is done, interruptHandler will fire and radio mode will return to STANDBY
            return true;
        }

        /// <summary>
        /// Determines how the driver detects when the radio completed sending a packet.
        /// If set to ChangeDetectionMode.Interrupt, the driver will wait for an interrupt to signal
        /// the send operation is complete. If set to ChangeDetectionMode.Poll, the driver will
        /// poll the device to determine if the send operation is complete.
        /// </summary>
        /// <param name="mode"></param>
        public void SetChangeDetectionMode(ChangeDetectionMode mode)
        {
            _changeDetectionMode = mode;
        }

        public override bool WaitPacketSent()
        {
            return _changeDetectionMode == ChangeDetectionMode.Interrupt ?
                base.WaitPacketSent() : PollPacketSent();
        }

        private bool PollPacketSent()
        {
            var args = new PinValueChangedEventArgs(PinEventTypes.None, -1);
            HandleInterrupt(this, args);
            while (Mode == RhModes.Tx)
            {
                RadioHead.Yield();
                HandleInterrupt(this, args);
            }
            return true;
        }

        /// <summary>
        /// Sets the length of the preamble in bytes. 
        /// Caution: this should be set to the same value on all nodes in your network. Default is 8.
        /// </summary>
        /// <param name="value">Preamble length in bytes.</param>
        public void SetPreambleLength(ushort value)
        {
            _spi.WriteTo(REG_20_PREAMBLE_MSB, (byte)(value >> 8));
            _spi.WriteTo(REG_21_PREAMBLE_LSB, (byte)(value & 0xFF));
        }

        /// <summary>
        /// The maximum message length supported by this driver
        /// </summary>
        public override byte MaxMessageLength => MAX_MESSAGE_LEN;

        /// <summary>
        /// Sets the transmitter and receiver center frequency.
        /// </summary>
        /// <param name="center">Frequency in MHz. 137.0 to 1020.0. Caution: RFM95/96/97/98 comes in several</param>
        /// different frequency ranges, and setting a frequency outside that range of your radio will probably not work
        /// <returns>true if the selected frequency centre is within range</returns>
        public bool SetFrequency(float center)
        {
            // Frf = FRF / FSTEP
            var frf = (uint)((center * 1000000.0) / FSTEP);
            _spi.WriteTo(REG_06_FRF_MSB, (byte)((frf >> 16) & 0xFF));
            _spi.WriteTo(REG_07_FRF_MID, (byte)((frf >> 8) & 0xFF));
            _spi.WriteTo(REG_08_FRF_LSB, (byte)(frf & 0xff));
            _usingHfPort = (center >= 779.0);

            return true;
        }

        /// <summary>
        /// If current mode is Rx or Tx changes it to Idle. If the transmitter or receiver is running, 
        /// disables them.
        /// </summary>
        public void SetModeIdle()
        {
            if (Mode != RhModes.Idle)
            {
                ModeWillChange(RhModes.Idle);
                _spi.WriteTo(REG_01_OP_MODE, MODE_STDBY);
                Mode = RhModes.Idle;
            }
        }

        /// <summary>
        /// If current mode is Tx or Idle, changes it to Rx. 
        /// Starts the receiver in the RF95/96/97/98.
        /// </summary>
        public void SetModeRx()
        {
            if (Mode != RhModes.Rx)
            {
                ModeWillChange(RhModes.Rx);
                _spi.WriteTo(REG_01_OP_MODE, MODE_RXCONTINUOUS);
                _spi.WriteTo(REG_40_DIO_MAPPING1, 0x00); // Interrupt on RxDone
                Mode = RhModes.Rx;
            }
        }

        /// <summary>
        /// If current mode is Rx or Idle, changes it to Rx. F
        /// Starts the transmitter in the RF95/96/97/98.
        /// </summary>
        public void SetModeTx()
        {
            if (Mode != RhModes.Tx)
            {
                ModeWillChange(RhModes.Tx);
                _spi.WriteTo(REG_01_OP_MODE, MODE_TX);
                _spi.WriteTo(REG_40_DIO_MAPPING1, 0x40); // Interrupt on TxDone
                Mode = RhModes.Tx;
            }
        }

        /// <summary>
        /// Sets the transmitter power output level, and configures the transmitter pin.
        /// Be a good neighbour and set the lowest power level you need.
        /// Some SX1276/77/78/79 and compatible modules (such as RFM95/96/97/98) 
        /// use the PA_BOOST transmitter pin for high power output (and optionally the PA_DAC)
        /// while some (such as the Modtronix inAir4 and inAir9) 
        /// use the RFO transmitter pin for lower power but higher efficiency.
        /// You must set the appropriate power level and useRFO argument for your module.
        /// Check with your module manufacturer which transmitter pin is used on your module
        /// to ensure you are setting useRFO correctly. 
        /// Failure to do so will result in very low 
        /// transmitter power output.
        /// Caution: legal power limits may apply in certain countries.
        /// After init(), the power will be set to 13dBm, with useRFO false (ie PA_BOOST enabled).
        /// </summary>
        /// <param name="power">Transmitter power level in dBm. For RFM95/96/97/98 LORA with useRFO false,
        /// valid values are from +2 to +20. For 18, 19 and 20, PA_DAC is enabled, 
        /// For Modtronix inAir4 and inAir9 with useRFO true (ie RFO pins in use), 
        /// valid values are from 0 to 15.
        /// </param>
        /// <param name="useRfo">If true, enables the use of the RFO transmitter pins instead of
        /// the PA_BOOST pin (false). Choose the correct setting for your module.
        /// </param>
        public void SetTxPower(sbyte power, bool useRfo = false)
        {
            // Sigh, different behaviours depending on whether the module use PA_BOOST or the RFO pin
            // for the transmitter output
            if (useRfo)
            {
                if (power > 15)
                    power = 15;
                if (power < 0)
                    power = 0;

                // Set the MaxPower register to 0x07 => MaxPower = 10.8 + 0.6 * 7 = 15dBm
                // So Pout = Pmax - (15 - power) = 15 - 15 + power
                _spi.WriteTo(REG_09_PA_CONFIG, (byte)(MAX_POWER | power));
                _spi.WriteTo(REG_4D_PA_DAC, PA_DAC_DISABLE);
            }
            else
            {
                if (power > 20)
                    power = 20;
                if (power < 2)
                    power = 2;

                // For PA_DAC_ENABLE, manual says '+20dBm on PA_BOOST when OutputPower=0x0F'
                // PA_DAC_ENABLE actually adds about 3dBm to all power levels. We will use it
                // for 18, 19 and 20dBm
                if (power > 17)
                {
                    _spi.WriteTo(REG_4D_PA_DAC, PA_DAC_ENABLE);
                    power -= 3;
                }
                else
                {
                    _spi.WriteTo(REG_4D_PA_DAC, PA_DAC_DISABLE);
                }

                // RFM95/96/97/98 does not have RFO pins connected to anything. Only PA_BOOST
                // pin is connected, so must use PA_BOOST
                // Pout = 2 + OutputPower (+3dBm if DAC enabled)
                _spi.WriteTo(REG_09_PA_CONFIG, (byte)(PA_SELECT | (power - 2)));
            }
        }

        /// <summary>
        /// Sets the radio into low-power sleep mode.
        /// If successful, the transport will stay in sleep mode until woken by 
        /// changing the mode to idle, transmit or receive (e.g. by calling Send(), Receive(), Available() etc.)
        /// <remarks> Caution: there is a time penalty as the radio takes a finite time to wake from sleep mode.
        /// </remarks>
        /// </summary>
        /// <returns>true if sleep mode was successfully entered.</returns>
        public override bool Sleep()
        {
            if (Mode != RhModes.Sleep)
            {
                ModeWillChange(RhModes.Sleep);
                _spi.WriteTo(REG_01_OP_MODE, MODE_SLEEP);
                Mode = RhModes.Sleep;
            }

            return true;
        }

        /// <summary>
        /// Bent G Christensen (bentor@gmail.com), 08/15/2016
        /// Use the radio's Channel Activity Detect (CAD) function to detect channel activity.
        /// Sets the RF95 radio into CAD mode and waits until CAD detection is complete.
        /// To be used in a listen-before-talk mechanism (Collision Avoidance)
        /// with a reasonable time backoff algorithm.
        /// This is called automatically by waitCAD().
        /// </summary>
        /// <returns>true if channel is in use.</returns>
        public override bool IsChannelActive()
        {
            // Set mode RhModes.Cad
            if (Mode != RhModes.Cad)
            {
                ModeWillChange(RhModes.Cad);
                _spi.WriteTo(REG_01_OP_MODE, MODE_CAD);
                _spi.WriteTo(REG_40_DIO_MAPPING1, 0x80); // Interrupt on CadDone
                Mode = RhModes.Cad;
            }

            while (Mode == RhModes.Cad)
                RadioHead.Yield();

            return Cad;
        }

        /// <summary>
        /// Enable TCXO mode
        /// Call this immediately after Init(), to force your radio to use an external
        /// frequency source, such as a Temperature Compensated Crystal Oscillator (TCXO), if available.
        /// See the comments in the main documentation about the sensitivity of this radio to
        /// clock frequency especially when using narrow bandwidths.
        /// Leaves the module in sleep mode.
        /// </summary>
        /// <remarks>
        /// Caution: the TCXO model radios are not low power when in sleep (consuming
        /// about ~600 uA, reported by Phang Moh Lim).
        /// </remarks>
        /// <remarks>
        /// Caution: if you enable TCXO and there is no external TCXO signal connected to the radio
        /// or if the external TCXO is not
        /// powered up, the radio will not work
        /// </remarks>
        /// <param name="on">true (the default) enables the radio to use the external TCXO.
        /// </param>
        public void EnableTcxo(bool on = true)
        {
            if (on)
            {
                while ((_spi.ReadFrom(REG_4B_TCXO) & TCXO_TCXO_INPUT_ON) != TCXO_TCXO_INPUT_ON)
                {
                    Sleep();
                    _spi.WriteTo(REG_4B_TCXO, (byte)(_spi.ReadFrom(REG_4B_TCXO) | TCXO_TCXO_INPUT_ON));
                }
            }
            else
            {
                while ((_spi.ReadFrom(REG_4B_TCXO) & TCXO_TCXO_INPUT_ON) != 0)
                {
                    Sleep();
                    _spi.WriteTo(REG_4B_TCXO, (byte)(_spi.ReadFrom(REG_4B_TCXO) & ~TCXO_TCXO_INPUT_ON));
                }
            }
        }

        /// <summary>
        /// Returns the last measured frequency error.
        /// The LoRa receiver estimates the frequency offset between the receiver centre frequency
        /// and that of the received LoRa signal. This function returns the estimates offset (in Hz) 
        /// of the last received message. Caution: this measurement is not absolute, but is measured 
        /// relative to the local receiver's oscillator.
        /// Apparent errors may be due to the transmitter, the receiver or both.
        /// </summary>
        /// <returns>The estimated centre frequency offset in Hz of the last received message. 
        /// If the modem bandwidth selector in 
        /// register REG_1D_MODEM_CONFIG1 is invalid, returns 0.
        /// </returns>
        public int FrequencyError()
        {
            // From section 4.1.5 of SX1276/77/78/79
            // Ferror = FreqError * 2**24 * BW / Fxtal / 500

            // Convert 2.5 bytes (5 nibbles, 20 bits) to a 32-bit signed int
            // Caution: some C compilers make errors with eg:
            // freqError = spiRead(REG_28_FEI_MSB) << 16
            // so we go more carefully.
            int freqError = _spi.ReadFrom(REG_28_FEI_MSB);
            freqError <<= 8;
            freqError |= _spi.ReadFrom(REG_29_FEI_MID);
            freqError <<= 8;
            freqError |= _spi.ReadFrom(REG_2A_FEI_LSB);

            // Sign extension into top 3 nibbles
            if ((freqError & 0x80000) > 0)
                freqError = (int)(freqError | 0xFFF00000);

            var error = 0; // In hertz
            var bwTable = new float[] { 7.8f, 10.4f, 15.6f, 20.8f, 31.25f, 41.7f, 62.5f, 125, 250, 500 };
            var bwIndex = (byte)(_spi.ReadFrom(REG_1D_MODEM_CONFIG1) >> 4);

            if (bwIndex < bwTable.Length)
                error = (int)(freqError * bwTable[bwIndex] * ((1L << 24) / FXOSC / 500.0f));
            // else not defined

            return error;
        }

        /// <summary>
        /// Returns the Signal-to-noise ratio (SNR) of the last received message, as measured
        /// by the receiver.
        /// </summary>
        /// <returns>SNR of the last received message in dB</returns>
        public sbyte LastSnr { get; private set; }

        /// <summary>
        /// brian.n.norman@gmail.com 9th Nov 2018
        /// Sets the radio spreading factor.
        /// valid values are 6 through 12.
        /// Out of range values below 6 are clamped to 6
        /// Out of range values above 12 are clamped to 12
        /// See Semtech DS SX1276/77/78/79 page 27 regarding SF6 configuration.
        /// </summary>
        /// <param name="sf">spreading factor 6..12</param>
        public void SetSpreadingFactor(byte sf)
        {
            if (sf <= 6)
                sf = SPREADING_FACTOR_64CPS;
            else if (sf == 7)
                sf = SPREADING_FACTOR_128CPS;
            else if (sf == 8)
                sf = SPREADING_FACTOR_256CPS;
            else if (sf == 9)
                sf = SPREADING_FACTOR_512CPS;
            else if (sf == 10)
                sf = SPREADING_FACTOR_1024CPS;
            else if (sf == 11)
                sf = SPREADING_FACTOR_2048CPS;
            else
                sf = SPREADING_FACTOR_4096CPS;

            // set the new spreading factor
            _spi.WriteTo(REG_1E_MODEM_CONFIG2, (byte)((_spi.ReadFrom(REG_1E_MODEM_CONFIG2) & ~SPREADING_FACTOR) | sf));
            // check if Low data Rate bit should be set or cleared
            SetLowDataRate();
        }

        /// <summary>
        /// brian.n.norman@gmail.com 9th Nov 2018
        /// Sets the radio signal bandwidth
        /// sbw ranges and resultant settings are as follows:-
        /// sbw range    actual bw (kHz)
        /// 0-7800       7.8
        /// 7801-10400   10.4
        /// 10401-15600  15.6
        /// 15601-20800  20.8
        /// 20801-31250  31.25
        /// 31251-41700	 41.7
        /// 41701-62500	 62.5
        /// 62501-12500  125.0
        /// 12501-250000 250.0
        /// >250000      500.0
        /// NOTE caution Earlier - Semtech do not recommend BW below 62.5 although, in testing
        /// I managed 31.25 with two devices in close proximity.
        /// </summary>
        /// <param name="sbw">signal bandwidth e.g. 125000</param>
        public void SetSignalBandwidth(long sbw)
        {
            byte bw; //register bit pattern

            if (sbw <= 7800)
                bw = BW_7_8KHZ;
            else if (sbw <= 10400)
                bw = BW_10_4KHZ;
            else if (sbw <= 15600)
                bw = BW_15_6KHZ;
            else if (sbw <= 20800)
                bw = BW_20_8KHZ;
            else if (sbw <= 31250)
                bw = BW_31_25KHZ;
            else if (sbw <= 41700)
                bw = BW_41_7KHZ;
            else if (sbw <= 62500)
                bw = BW_62_5KHZ;
            else if (sbw <= 125000)
                bw = BW_125KHZ;
            else if (sbw <= 250000)
                bw = BW_250KHZ;
            else
                bw = BW_500KHZ;

            // top 4 bits of reg 1D control bandwidth
            _spi.WriteTo(REG_1D_MODEM_CONFIG1, (byte)((_spi.ReadFrom(REG_1D_MODEM_CONFIG1) & ~BW) | bw));

            // check if low data rate bit should be set or cleared
            SetLowDataRate();
        }

        /// <summary>
        /// brian.n.norman@gmail.com 9th Nov 2018
        /// Sets the coding rate to 4/5, 4/6, 4/7 or 4/8.
        /// Valid denominator values are 5, 6, 7 or 8. A value of 5 sets the coding rate to 4/5 etc.
        /// Values below 5 are clamped at 5
        /// values above 8 are clamped at 8.
        /// Default for all standard modem config options is 4/5.
        /// </summary>
        /// <param name="denominator">denominator byte range 5..8</param>
        public void SetCodingRate4(byte denominator)
        {
            int cr = CODING_RATE_4_5;

            // if (denominator <= 5)
            //     cr = CODING_RATE_4_5;

            if (denominator == 6)
                cr = CODING_RATE_4_6;
            else if (denominator == 7)
                cr = CODING_RATE_4_7;
            else if (denominator >= 8)
                cr = CODING_RATE_4_8;

            // CR is bits 3..1 of REG_1D_MODEM_CONFIG1
            _spi.WriteTo(REG_1D_MODEM_CONFIG1, (byte)((_spi.ReadFrom(REG_1D_MODEM_CONFIG1) & ~CODING_RATE) | cr));
        }

        /// <summary>
        /// brian.n.norman@gmail.com 9th Nov 2018
        /// sets the low data rate flag if symbol time exceeds 16ms
        /// ref: https://www.thethingsnetwork.org/forum/t/a-point-to-note-lora-low-data-rate-optimisation-flag/12007
        /// called by SetBandwidth() and SetSpreadingFactor() since these affect the symbol time.
        /// </summary>
        public void SetLowDataRate()
        {
            // called after changing bandwidth and/or spreading factor
            //  Semtech modem design guide AN1200.13 says 
            // "To avoid issues surrounding  drift  of  the  crystal  reference  oscillator  due  to  either  temperature  change  
            // or  motion,the  low  data  rate optimization  bit  is  used. Specifically for 125  kHz  bandwidth  and  SF  =  11  and  12,  
            // this  adds  a  small  overhead  to increase robustness to reference frequency variations over the timescale of the LoRa packet."

            // read current value for BW and SF
            var bw = (byte)(_spi.ReadFrom(REG_1D_MODEM_CONFIG1) >> 4);    // bw is in bits 7..4
            var sf = (byte)(_spi.ReadFrom(REG_1E_MODEM_CONFIG2) >> 4);    // sf is in bits 7..4

            // calculate symbol time (see Semtech AN1200.22 section 4)
            var bwTab = new float[] { 7800, 10400, 15600, 20800, 31250, 41700, 62500, 125000, 250000, 500000 };

            var bandwidth = bwTab[bw];

            // var symbolTime = 1000.0f * (float)Math.Pow(2, sf) / bandwidth; // ms
            var symbolTime = 1000.0f * (2 << sf) / bandwidth; // ms

            // the symbolTime for SF 11 BW 125 is 16.384ms. 
            // and, according to this :- 
            // https://www.thethingsnetwork.org/forum/t/a-point-to-note-lora-low-data-rate-optimisation-flag/12007
            // the LDR bit should be set if the Symbol Time is > 16ms
            // So the threshold used here is 16.0ms

            // the LDR is bit 3 of REG_26_MODEM_CONFIG3
            var current = (byte)(_spi.ReadFrom(REG_26_MODEM_CONFIG3) & ~LOW_DATA_RATE_OPTIMIZE); // mask off the LDR bit
            if (symbolTime > 16.0)
                _spi.WriteTo(REG_26_MODEM_CONFIG3, (byte)(current | LOW_DATA_RATE_OPTIMIZE));
            else
                _spi.WriteTo(REG_26_MODEM_CONFIG3, current);
        }

        /// <summary>
        /// brian.n.norman@gmail.com 9th Nov 2018
        /// Allows the CRC to be turned on/off. Default is true (enabled)
        /// When true, RH_RF95 sends a CRC in outgoing packets and requires a valid CRC to be
        /// present and correct on incoming packets.
        /// When false, does not send CRC in outgoing packets and does not require a CRC to be
        /// present on incoming packets. However, if a CRC is present, it must be correct.
        /// Normally this should be left on (the default)
        /// so that packets with a bad CRC are rejected. If turned off you will be much more likely to receive
        /// false noise packets.
        /// </summary>
        /// <param name="on">true enables CRCs in incoming and outgoing packets, false disables them</param>
        public void SetPayloadCrc(bool on)
        {
            // Payload CRC is bit 2 of register 1E
            var current = (byte)(_spi.ReadFrom(REG_1E_MODEM_CONFIG2) & ~PAYLOAD_CRC_ON); // mask off the CRC

            if (on)
                _spi.WriteTo(REG_1E_MODEM_CONFIG2, (byte)(current | PAYLOAD_CRC_ON));
            else
                _spi.WriteTo(REG_1E_MODEM_CONFIG2, current);
            _enableCrc = on;
        }

        /// <summary>
        /// tilman_1@gloetzner.net
        /// Returns device version from register 42
        /// </summary>
        /// <returns>The version of the device</returns> 
        public byte GetDeviceVersion()
        {
            _deviceVersion = _spi.ReadFrom(REG_42_VERSION);
            return _deviceVersion;
        }

        /// <summary>
        /// This is a low level function to handle the interrupts for one instance of RH_RF95.
        /// Called automatically by isr*()
        /// Should not need to be called by user code.
        /// </summary>
        public void HandleInterrupt(object sender, PinValueChangedEventArgs arg)
        {
            lock (CriticalSection)
            {
                // we need the RF95 IRQ to be level triggered, or we ……have slim chance of missing events
                // https://github.com/geeksville/Meshtastic-esp32/commit/78470ed3f59f5c84fbd1325bcff1fd95b2b20183

                // Read the interrupt register
                var irqFlags = _spi.ReadFrom(REG_12_IRQ_FLAGS);
                if (irqFlags == 0)
                {
                    return;
                }

                // Read the RegHopChannel register to check if CRC presence is signalled
                // in the header. If not it might be a stray (noise) packet.*
                var hopChannel = _spi.ReadFrom(REG_1C_HOP_CHANNEL);

                // ack all interrupts, 
                // Sigh: on some processors, for some unknown reason, doing this only once does not actually
                // clear the radio's interrupt flag. So we do it twice. Why? (kevinh - I think the root cause we want level
                // triggered interrupts here - not edge.  Because edge allows us to miss handling second interrupts that occurred
                // while this ISR was running.  Better to instead, configure the interrupts as level triggered and clear pending
                // at the _beginning_ of the ISR.  If any interrupts occur while handling the ISR, the signal will remain asserted and
                // our ISR will be re-invoked to handle that case)
                // kevinh: turn this off until root cause is known, because it can cause missed interrupts!
                // WriteTo(REG_12_IRQ_FLAGS, 0xFF); // Clear all IRQ flags
                _spi.WriteTo(REG_12_IRQ_FLAGS, 0xFF); // Clear all IRQ flags

                // error if:
                //    - timeout
                //    - bad CRC
                //    - CRC is required, but it is not present
                if (Mode == RhModes.Rx &&
                    (((irqFlags & (RX_TIMEOUT | PAYLOAD_CRC_ERROR)) != 0) ||
                     (_enableCrc && (hopChannel & RX_PAYLOAD_CRC_IS_ON) == 0)))
                {
                    RxBad++;
                    ClearRxBuf();
                }

                // It is possible to get RX_DONE and CRC_ERROR and VALID_HEADER all at once
                // so this must be an else
                else if (Mode == RhModes.Rx && ((irqFlags & RX_DONE) != 0))
                {
                    // Packet received, no CRC error
                    //	Serial.println("R");
                    // Have received a packet
                    var len = _spi.ReadFrom(REG_13_RX_NB_BYTES);

                    // Reset the fifo read ptr to the beginning of the packet
                    _spi.WriteTo(REG_0D_FIFO_ADDR_PTR, _spi.ReadFrom(REG_10_FIFO_RX_CURRENT_ADDR));
                    _spi.BurstReadFrom(REG_00_FIFO, out _buf, len);
                    _bufLen = len;

                    // Remember the last signal-to-noise ratio, LORA mode
                    // Per page 111, SX1276/77/78/79 datasheet
                    LastSnr = (sbyte)(_spi.ReadFrom(REG_19_PKT_SNR_VALUE) / 4);

                    // Remember the RSSI of this packet, LORA mode
                    // this is according to the doc, but is it really correct?
                    // weakest receivable signals are reported RSSI at about -66
                    LastRssi = _spi.ReadFrom(REG_1A_PKT_RSSI_VALUE);

                    // Adjust the RSSI, datasheet page 87
                    if (LastSnr < 0)
                        LastRssi = (short)(LastRssi + LastSnr);
                    else
                        LastRssi = (short)(LastRssi * 16 / 15);
                    if (_usingHfPort)
                        LastRssi -= 157;
                    else
                        LastRssi -= 164;

                    // We have received a message.
                    ValidateRxBuf();
                    if (_rxBufValid)
                        SetModeIdle(); // Got one 
                }
                else if (Mode == RhModes.Tx && ((irqFlags & TX_DONE) != 0))
                {
                    //	Serial.println("T");
                    TxGood++;
                    SetModeIdle();
                }
                else if (Mode == RhModes.Cad && ((irqFlags & CAD_DONE) != 0))
                {
                    //	Serial.println("C");
                    Cad = (irqFlags & CAD_DETECTED) != 0;
                    SetModeIdle();
                }
                else
                {
                    //	Serial.println("?");
                }

                // Sigh: on some processors, for some unknown reason, doing this only once does not actually
                // clear the radio's interrupt flag. So we do it twice. Why?
                _spi.WriteTo(REG_12_IRQ_FLAGS, 0xff); // Clear all IRQ flags
                _spi.WriteTo(REG_12_IRQ_FLAGS, 0xff); // Clear all IRQ flags
            }
        }

        /// Examine the receive buffer to determine whether the message is for this node
        // Check whether the latest received message is complete and uncorrupted
        protected void ValidateRxBuf()
        {
            if (_bufLen < 4)
                return; // Too short to be a real message

            // Extract the 4 headers
            RxHeaderTo = _buf[0];
            RxHeaderFrom = _buf[1];
            RxHeaderId = _buf[2];
            RxHeaderFlags = _buf[3];
            if (Promiscuous || RxHeaderTo == ThisAddress || RxHeaderTo == RadioHead.BroadcastAddress)
            {
                RxGood++;
                _rxBufValid = true;
            }
        }

        /// Clear our local receive buffer
        private void ClearRxBuf()
        {
            _rxBufValid = false;
            _bufLen = 0;
        }

        /// Called by RH_RF95 when the radio mode is about to change to a new setting.
        /// Can be used by subclasses to implement antenna switching etc.
        /// \param[in] mode RHMode the new mode about to take effect
        /// \return true if the subclasses changes successful
        protected bool ModeWillChange(RhModes mode)
        {
            return true;
        }
    };
}
