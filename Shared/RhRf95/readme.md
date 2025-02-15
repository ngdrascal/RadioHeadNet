        /////////////////////////////////////////////////////////////////////
        /// \class RH_RF95 RH_RF95.h <RH_RF95.h>
        /// \brief Driver to send and receive unaddressed, unreliable datagrams via a LoRa 
        /// capable radio transceiver.
        ///
        /// For an excellent discussion of LoRa range and modulations, see
        /// https://medium.com/home-wireless/testing-lora-radios-with-the-limesdr-mini-part-2-37fa481217ff
        ///
        /// For Semtech SX1276/77/78/79 and HopeRF RF95/96/97/98 and other similar LoRa capable radios.
        /// Based on http://www.hoperf.com/upload/rf/RFM95_96_97_98W.pdf
        /// and http://www.hoperf.cn/upload/rfchip/RF96_97_98.pdf
        /// and http://www.semtech.com/images/datasheet/LoraDesignGuide_STD.pdf
        /// and http://www.semtech.com/images/datasheet/sx1276.pdf
        /// and http://www.semtech.com/images/datasheet/sx1276_77_78_79.pdf
        /// FSK/GFSK/OOK modes are not (yet) supported.
        ///
        /// Works with
        /// - the excellent MiniWirelessLoRa from Anarduino http://www.anarduino.com/miniwireless
        /// - The excellent Modtronix inAir4 http://modtronix.com/inair4.html 
        /// and inAir9 modules http://modtronix.com/inair9.html.
        /// - the excellent Rocket Scream Mini Ultra Pro with the RFM95W 
        ///   http://www.rocketscream.com/blog/product/mini-ultra-pro-with-radio/
        /// - Lora1276 module from NiceRF http://www.nicerf.com/product_view.aspx?id=99
        /// - Adafruit Feather M0 with RFM95
        /// - The very fine Talk2 Whisper Node LoRa boards https://wisen.com.au/store/products/whisper-node-lora
        ///   an Arduino compatible board, which include an on-board RFM95/96 LoRa Radio (Semtech SX1276), external antenna, 
        ///   run on 2xAAA batteries and support low power operations. RF95 examples work without modification.
        ///   Use Arduino Board Manager to install the Talk2 code support. Upload the code with an FTDI adapter set to 5V.
        /// - heltec / TTGO ESP32 LoRa OLED https://www.aliexpress.com/item/Internet-Development-Board-SX1278-ESP32-WIFI-chip-0-96-inch-OLED-Bluetooth-WIFI-Lora-Kit-32/32824535649.html
        ///
        /// \par Overview
        ///
        /// This class provides basic functions for sending and receiving unaddressed, 
        /// unreliable datagrams of arbitrary length to 251 octets per packet.
        ///
        /// Manager classes may use this class to implement reliable, addressed datagrams and streams, 
        /// mesh routers, repeaters, translators etc.
        ///
        /// Naturally, for any 2 radios to communicate that must be configured to use the same frequency and 
        /// modulation scheme.
        ///
        /// This Driver provides an object-oriented interface for sending and receiving data messages with Hope-RF
        /// RFM95/96/97/98(W), Semtech SX1276/77/78/79 and compatible radio modules in LoRa mode.
        ///
        /// The Hope-RF (http://www.hoperf.com) RFM95/96/97/98(W) and Semtech SX1276/77/78/79 is a low-cost ISM transceiver
        /// chip. It supports FSK, GFSK, OOK over a wide range of frequencies and
        /// programmable data rates, and it also supports the proprietary LoRA (Long Range) mode, which
        /// is the only mode supported in this RadioHead driver.
        ///
        /// This Driver provides functions for sending and receiving messages of up
        /// to 251 octets on any frequency supported by the radio, in a range of
        /// predefined Bandwidths, Spreading Factors and Coding Rates.  Frequency can be set with
        /// 61Hz precision to any frequency from 240.0MHz to 960.0MHz. Caution: most modules only support a more limited
        /// range of frequencies due to antenna tuning.
        ///
        /// Up to 2 modules can be connected to an Arduino (3 on a Mega),
        /// permitting the construction of translators and frequency changers, etc.
        ///
        /// Support for other features such as transmitter power control etc is
        /// also provided.
        ///
        /// Tested on MinWirelessLoRa with arduino-1.0.5
        /// on OpenSuSE 13.1. 
        /// Also tested with Teensy3.1, Modtronix inAir4 and Arduino 1.6.5 on OpenSuSE 13.1
        ///
        /// \par Packet Format
        ///
        /// All messages sent and received by this RH_RF95 Driver conform to this packet format, which is compatible with RH_SX126x:
        ///
        /// - LoRa mode:
        /// - 8 symbol PREAMBLE
        /// - Explicit header with header CRC (default CCITT, handled internally by the radio)
        /// - 4 octets HEADER: (TO, FROM, ID, FLAGS)
        /// - 0 to 251 octets DATA 
        /// - CRC (default CCITT, handled internally by the radio)
        ///
        /// \par Connecting RFM95/96/97/98 and Semtech SX1276/77/78/79 to Arduino
        ///
        /// We tested with Anarduino MiniWirelessLoRA, which is an Arduino Duemilanove compatible with a RFM96W
        /// module on-board. Therefore it needs no connections other than the USB
        /// programming connection and an antenna to make it work.
        ///
        /// If you have a bare RFM95/96/97/98 that you want to connect to an Arduino, you
        /// might use these connections (untested): CAUTION: you must use a 3.3V type
        /// Arduino, otherwise you will also need voltage level shifters between the
        /// Arduino and the RFM95.  CAUTION, you must also ensure you connect an
        /// antenna.
        /// 
        /// \code
        ///                 Arduino      RFM95/96/97/98
        ///                 GND----------GND   (ground in)
        ///                 3V3----------3.3V  (3.3V in)
        /// interrupt 0 pin D2-----------DIO0  (interrupt request out)
        ///          SS pin D10----------NSS   (CS chip select in)
        ///         SCK pin D13----------SCK   (SPI clock in)
        ///        MOSI pin D11----------MOSI  (SPI Data in)
        ///        MISO pin D12----------MISO  (SPI Data out)
        /// \endcode
        /// With these connections, you can then use the default constructor RH_RF95().
        /// You can override the default settings for the SS pin and the interrupt in
        /// the RH_RF95 constructor if you wish to connect the slave select SS to other
        /// than the normal one for your Arduino (D10 for Diecimila, Uno etc and D53
        /// for Mega) or the interrupt request to other than pin D2 (Caution,
        /// different processors have different constraints as to the pins available
        /// for interrupts).
        ///
        /// You can connect a Modtronix inAir4 or inAir9 directly to a 3.3V part such as a Teensy 3.1 like
        /// this (tested).
        /// \code
        ///                 Teensy      inAir4 inAir9
        ///                 GND----------0V   (ground in)
        ///                 3V3----------3.3V  (3.3V in)
        /// interrupt 0 pin D2-----------D0   (interrupt request out)
        ///          SS pin D10----------CS    (CS chip select in)
        ///         SCK pin D13----------CK    (SPI clock in)
        ///        MOSI pin D11----------SI    (SPI Data in)
        ///        MISO pin D12----------SO    (SPI Data out)
        /// \endcode
        /// With these connections, you can then use the default constructor RH_RF95().
        /// you must also set the transmitter power with useRFO:
        /// driver.setTxPower(13, true);
        ///
        /// Note that if you are using Modtronix inAir4 or inAir9,or any other module which uses the
        /// transmitter RFO pins and not the PA_BOOST pins
        /// that you must configure the power transmitter power for -1 to 14 dBm and with useRFO true. 
        /// Failure to do that will result in extremely low transmit powers.
        ///
        /// If you have an Arduino M0 Pro from arduino.org, 
        /// you should note that you cannot use Pin 2 for the interrupt line 
        /// (Pin 2 is for the NMI only). The same comments apply to Pin 4 on Arduino Zero from arduino.cc.
        /// Instead you can use any other pin (we use Pin 3) and initialise RH_RF69 like this:
        /// \code
        /// // Slave Select is pin 10, interrupt is Pin 3
        /// RH_RF95 driver(10, 3);
        /// \endcode
        /// You can use the same constructor for Arduino Due, and this pinout diagram may be useful:
        /// http://www.robgray.com/temp/Due-pinout-WEB.png
        ///
        /// If you have a Rocket Scream Mini Ultra Pro with the RFM95W:
        /// - Ensure you have Arduino SAMD board support 1.6.5 or later in Arduino IDE 1.6.8 or later.
        /// - The radio SS is hardwired to pin D5 and the DIO0 interrupt to pin D2, 
        /// so you need to initialise the radio like this:
        /// \code
        /// RH_RF95 driver(5, 2);
        /// \endcode
        /// - The name of the serial port on that board is 'SerialUSB', not 'Serial', so this may be helpful at the top of our
        ///   sample sketches:
        /// \code
        /// #define Serial SerialUSB
        /// \endcode
        /// - You also need this in setup before radio initialisation  
        /// \code
        /// // Ensure serial flash is not interfering with radio communication on SPI bus
        ///  pinMode(4, OUTPUT);
        ///  digitalWrite(4, HIGH);
        /// \endcode
        /// - and if you have a 915MHz part, you need this after driver/manager intitalisation:
        /// \code
        /// rf95.setFrequency(915.0);
        /// \endcode
        /// which adds up to modifying sample sketches something like:
        /// \code
        /// #include <SPI.h>
        /// #include <RH_RF95.h>
        /// RH_RF95 rf95(5, 2); // Rocket Scream Mini Ultra Pro with the RFM95W
        /// #define Serial SerialUSB
        /// 
        /// void setup() 
        /// {
        ///   // Ensure serial flash is not interfering with radio communication on SPI bus
        ///   pinMode(4, OUTPUT);
        ///   digitalWrite(4, HIGH);
        /// 
        ///   Serial.begin(9600);
        ///   while (!Serial) ; // Wait for serial port to be available
        ///   if (!rf95.init())
        ///     Serial.println("init failed");
        ///   rf95.setFrequency(915.0);
        /// }
        /// ...
        /// \endcode
        ///
        /// For Adafruit Feather M0 with RFM95, construct the driver like this:
        /// \code
        /// RH_RF95 rf95(8, 3);
        /// \endcode
        ///
        /// If you have a talk2 Whisper Node LoRa board with on-board RF95 radio, 
        /// the example rf95_* sketches work without modification. Initialise the radio like
        /// with the default constructor:
        /// \code
        ///  RH_RF95 driver;
        /// \endcode
        ///
        /// It is possible to have 2 or more radios connected to one Arduino, provided
        /// each radio has its own SS and interrupt line (SCK, SDI and SDO are common
        /// to all radios)
        ///
        /// Caution: on some Arduinos such as the Mega 2560, if you set the slave
        /// select pin to be other than the usual SS pin (D53 on Mega 2560), you may
        /// need to set the usual SS pin to be an output to force the Arduino into SPI
        /// master mode.
        ///
        /// Caution: Power supply requirements of the RFM module may be relevant in some circumstances: 
        /// RFM95/96/97/98 modules are capable of pulling 120mA+ at full power, where Arduino's 3.3V line can
        /// give 50mA. You may need to make provision for alternate power supply for
        /// the RFM module, especially if you wish to use full transmit power, and/or you have
        /// other shields demanding power. Inadequate power for the RFM is likely to cause symptoms such as:
        /// - reset's/bootups terminate with "init failed" messages
        /// - random termination of communication after 5-30 packets sent/received
        /// - "fake ok" state, where initialization passes fluently, but communication doesn't happen
        /// - shields hang Arduino boards, especially during the flashing
        ///
        /// \par Interrupts
        ///
        /// The RH_RF95 driver uses interrupts to react to events in the RFM module,
        /// such as the reception of a new packet, or the completion of transmission
        /// of a packet. The driver configures the radio so the required interrupt is generated by the radio's DIO0 pin.
        /// The RH_RF95 driver interrupt service routine reads status from
        /// and writes data to the the RFM module via the SPI interface. It is very
        /// important therefore, that if you are using the RH_RF95 driver with another
        /// SPI based deviced, that you disable interrupts while you transfer data to
        /// and from that other device.  Use cli() to disable interrupts and sei() to
        /// reenable them.
        ///
        /// \par Memory
        ///
        /// The RH_RF95 driver requires non-trivial amounts of memory. The sample
        /// programs all compile to about 8kbytes each, which will fit in the
        /// flash proram memory of most Arduinos. However, the RAM requirements are
        /// more critical. Therefore, you should be vary sparing with RAM use in
        /// programs that use the RH_RF95 driver.
        ///
        /// It is often hard to accurately identify when you are hitting RAM limits on Arduino. 
        /// The symptoms can include:
        /// - Mysterious crashes and restarts
        /// - Changes in behaviour when seemingly unrelated changes are made (such as adding print() statements)
        /// - Hanging
        /// - Output from Serial.print() not appearing
        ///
        /// \par Range
        ///
        /// We have made some simple range tests under the following conditions:
        /// - rf95_client base station connected to a VHF discone antenna at 8m height above ground
        /// - rf95_server mobile connected to 17.3cm 1/4 wavelength antenna at 1m height, no ground plane.
        /// - Both configured for 13dBm, 434MHz, Bw = 125 kHz, Cr = 4/8, Sf = 4096chips/symbol, CRC on. Slow+long range
        /// - Minimum reported RSSI seen for successful comms was about -91
        /// - Range over flat ground through heavy trees and vegetation approx 2km.
        /// - At 20dBm (100mW) otherwise identical conditions approx 3km.
        /// - At 20dBm, along salt water flat sandy beach, 3.2km.
        ///
        /// It should be noted that at this data rate, a 12 octet message takes 2 seconds to transmit.
        ///
        /// At 20dBm (100mW) with Bw = 125 kHz, Cr = 4/5, Sf = 128chips/symbol, CRC on. 
        /// (Default medium range) in the conditions described above.
        /// - Range over flat ground through heavy trees and vegetation approx 2km.
        ///
        /// Caution: the performance of this radio, especially with narrow bandwidths is strongly dependent on the
        /// accuracy and stability of the chip clock. HopeRF and Semtech do not appear to 
        /// recommend bandwidths of less than 62.5 kHz 
        /// unless you have the optional Temperature Compensated Crystal Oscillator (TCXO) installed and 
        /// enabled on your radio module. See the reference manual for more data.
        /// Also https://lowpowerlab.com/forum/rf-range-antennas-rfm69-library/lora-library-experiences-range/15/
        /// and http://www.semtech.com/images/datasheet/an120014-xo-guidance-lora-modulation.pdf
        /// 
        /// \par Transmitter Power
        ///
        /// You can control the transmitter power on the RF transceiver
        /// with the RH_RF95::setTxPower() function. The argument can be any of
        /// +2 to +20 (for modules that use PA_BOOST)
        /// 0 to +15 (for modules that use RFO transmitter pin)
        /// The default is 13. Eg:
        /// \code
        /// driver.setTxPower(10); // use PA_BOOST transmitter pin
        /// driver.setTxPower(10, true); // use PA_RFO pin transmitter pin instead of PA_BOOST
        /// \endcode
        ///
        /// We have made some actual power measurements against
        /// programmed power for Anarduino MiniWirelessLoRa (which has RFM96W-433Mhz installed, and which includes an RF power
        /// amp for addition 3dBm of power
        /// - MiniWirelessLoRa RFM96W-433Mhz, USB power
        /// - 30cm RG316 soldered direct to RFM96W module ANT and GND
        /// - SMA connector
        /// - 12db attenuator
        /// - SMA connector
        /// - MiniKits AD8307 HF/VHF Power Head (calibrated against Rohde&Schwartz 806.2020 test set)
        /// - Tektronix TDS220 scope to measure the Vout from power head
        /// \code
        /// Program power           Measured Power
        ///    dBm                         dBm
        ///      2                           5
        ///      4                           7
        ///      6                           8
        ///      8                          11
        ///     10                          13
        ///     12                          15
        ///     14                          16
        ///     16                          18
        ///     17                          20 
        ///     18                          21 
        ///     19                          22 
        ///     20                          23 
        /// \endcode
        ///
        /// We have also measured the actual power output from a Modtronix inAir4 http://modtronix.com/inair4.html
        /// connected to a Teensy 3.1:
        /// Teensy 3.1 this is a 3.3V part, connected directly to:
        /// Modtronix inAir4 with SMA antenna connector, connected as above:
        /// 10cm SMA-SMA cable
        /// - MiniKits AD8307 HF/VHF Power Head (calibrated against Rohde&Schwartz 806.2020 test set)
        /// - Tektronix TDS220 scope to measure the Vout from power head
        /// \code
        /// Program power           Measured Power
        ///    dBm                         dBm
        ///      0                         0
        ///      2                         2
        ///      3                         4
        ///      6                         7
        ///      8                         10
        ///      10                        13
        ///     12                         14.2
        ///     14                         15
        ///     15                         16
        /// \endcode
        /// (Caution: we don't claim laboratory accuracy for these power measurements)
        /// You would not expect to get anywhere near these powers to air with a simple 1/4 wavelength wire antenna.
