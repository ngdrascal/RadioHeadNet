// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Local
// ReSharper disable IdentifierTypo
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedMember.Global

#define RFM69_HW

namespace RadioHeadNet.RhRf69;

public partial class Rf69
{
    // The crystal oscillator frequency of the RF69 module
    private const float RH_RF69_FXOSC = 32000000.0f;

    // The Frequency Synthesizer step = RH_RF69_FXOSC / 2^^19
    private const float RH_RF69_FSTEP = (RH_RF69_FXOSC / 524288);

    // This is the maximum number of interrupts the driver can support
    private const byte RH_RF69_NUM_INTERRUPTS = 3;

    // This is the bit in the SPI address that marks it as a write operation
    private const byte RH_RF69_SPI_WRITE_MASK = 0x80;

    // Max number of octets the RH_RF69 Rx and Tx FIFOs can hold
    private const byte RH_RF69_FIFO_SIZE = 66;

    // Maximum encrypt-able payload length the RF69 can support
    internal const byte RH_RF69_MAX_ENCRYPTABLE_PAYLOAD_LEN = 64;

    // The length of the headers we add.
    // The headers are inside the RF69's payload and are therefore encrypted if encryption is enabled
    internal const byte RH_RF69_HEADER_LEN = 4;

    // This is the maximum message length that can be supported by this driver. Limited by
    // the size of the FIFO, since we are unable to support on-the-fly filling and emptying 
    // of the FIFO.
    // Can be pre-defined to a smaller size (to save SRAM) prior to including this header
    // Here we allow for 4 bytes of address and header and payload to be included in the 64 byte encryption limit.
    // the one byte payload length is not encrypted
    private const byte RH_RF69_MAX_MESSAGE_LEN = RH_RF69_MAX_ENCRYPTABLE_PAYLOAD_LEN - RH_RF69_HEADER_LEN;

    // Keep track of the Mode the RF69 is in
    public const byte RH_RF69_MODE_IDLE = 0;
    public const byte RH_RF69_MODE_RX = 1;
    public const byte RH_RF69_MODE_TX = 2;

    // This is the default node address,
    private const byte RH_RF69_DEFAULT_NODE_ADDRESS = 0;

    // You can define the following macro (either by editing here or by passing it as a
    // compiler definition// to change the default value of the isHighPowerModule argument
    // to setTxPower to true

#if RFM69_HW
    private const bool RH_RF69_DEFAULT_HIGHPOWER = true;
#else
    private const bool RH_RF69_DEFAULT_HIGHPOWER = false;
#endif

    // Register names
    internal const byte Reg00Fifo = 0x00;
    internal const byte Reg01OpMode = 0x01;
    internal const byte Reg02DataModul = 0x02;
    internal const byte Reg03BitRateMsb = 0x03;
    internal const byte Reg04BitRateLsb = 0x04;
    internal const byte Reg05FDevMsb = 0x05;
    internal const byte Reg06FDevLsb = 0x06;
    internal const byte Reg07FrfMsb = 0x07;
    internal const byte Reg08FrfMid = 0x08;
    internal const byte Reg09FrfLsb = 0x09;
    internal const byte Reg0aOsc1 = 0x0A;
    internal const byte Reg0bAfcCtrl = 0x0B;
    // internal const byte Reg0CReserved = 0x0C;
    internal const byte Reg0dListen1 = 0x0D;
    internal const byte Reg0eListen2 = 0x0E;
    internal const byte Reg0fListen3 = 0x0F;
    internal const byte Reg10Version = 0x10;
    internal const byte Reg11PaLevel = 0x11;
    internal const byte Reg12PaRamp = 0x12;
    internal const byte Reg13Ocp = 0x13;
    // internal const byte Reg14Reserved = 0x14;
    // internal const byte Reg15Reserved = 0x15;
    // internal const byte Reg16Reserved = 0x16;
    // internal const byte Reg17Reserved = 0x17;
    internal const byte Reg18Lna = 0x18;
    internal const byte Reg19RxBw = 0x19;
    internal const byte Reg1aAfcBw = 0x1A;
    internal const byte Reg1bOokPeak = 0x1B;
    internal const byte Reg1cOokAvg = 0x1C;
    internal const byte Reg1dOokFix = 0x1D;
    internal const byte Reg1eAfcFei = 0x1E;
    internal const byte Reg1fAfcMsb = 0x1F;
    internal const byte Reg20AfcLsb = 0x20;
    internal const byte Reg21FeiMsb = 0x21;
    internal const byte Reg22FeiLsb = 0x22;
    internal const byte Reg23RssiConfig = 0x23;
    internal const byte Reg24RssiValue = 0x24;
    internal const byte Reg25DioMapping1 = 0x25;
    internal const byte Reg26DioMapping2 = 0x26;
    internal const byte Reg27IrqFlags1 = 0x27;
    internal const byte Reg28IrqFlags2 = 0x28;
    internal const byte Reg29RssiThresh = 0x29;
    internal const byte Reg2aRxTimeout1 = 0x2A;
    internal const byte Reg2bRxTimeout2 = 0x2B;
    internal const byte Reg2cPreambleMsb = 0x2C;
    internal const byte Reg2dPreambleLsb = 0x2D;
    internal const byte Reg2eSyncConfig = 0x2E;
    internal const byte Reg2fSyncValue1 = 0x2F;
    // another 7 sync word bytes follow, 30 through 36 inclusive
    internal const byte Reg37PacketConfig1 = 0x37;
    internal const byte Reg38PayloadLength = 0x38;
    internal const byte Reg39NodeAdrs = 0x39;
    internal const byte Reg3aBroadcastAdrs = 0x3A;
    internal const byte Reg3bAutoModes = 0x3B;
    internal const byte Reg3cFifoThresh = 0x3C;
    internal const byte Reg3dPacketConfig2 = 0x3D;
    internal const byte Reg3eAesKey1 = 0x3E;
    // Another 15 AES key bytes follow
    internal const byte Reg4eTemp1 = 0x4E;
    internal const byte Reg4fTemp2 = 0x4F;
    internal const byte Reg58TestLna = 0x58;
    internal const byte Reg5aTestPa1 = 0x5A;
    internal const byte Reg5cTestPa2 = 0x5C;
    internal const byte Reg6fTestDagc = 0x6F;
    internal const byte Reg71TestAfc = 0x71;

    // These register masks etc. are named wherever possible
    // corresponding to the bit and field names in the RFM69 Manual

    // Reg01OpMode
    private const byte OPMODE_SEQUENCEROFF = 0x80;
    private const byte OPMODE_LISTENON = 0x40;
    private const byte OPMODE_LISTENABORT = 0x20;
    private const byte OPMODE_MODE = 0x1C;
    internal const byte OPMODE_MODE_SLEEP = 0x00;
    internal const byte OPMODE_MODE_STDBY = 0x04;
    internal const byte OPMODE_MODE_FS = 0x08;
    internal const byte OPMODE_MODE_TX = 0x0C;
    internal const byte OPMODE_MODE_RX = 0x10;

    // Reg02DataModul                                            
    private const byte DATAMODUL_DATAMODE = 0x60;
    private const byte DATAMODUL_DATAMODE_PACKET = 0x00;
    private const byte DATAMODUL_DATAMODE_CONT_WITH_SYNC = 0x40;
    private const byte DATAMODUL_DATAMODE_CONT_WITHOUT_SYNC = 0x60;
    private const byte DATAMODUL_MODULATIONTYPE = 0x18;
    private const byte DATAMODUL_MODULATIONTYPE_FSK = 0x00;
    private const byte DATAMODUL_MODULATIONTYPE_OOK = 0x08;
    private const byte DATAMODUL_MODULATIONSHAPING = 0x03;
    private const byte DATAMODUL_MODULATIONSHAPING_FSK_NONE = 0x00;
    private const byte DATAMODUL_MODULATIONSHAPING_FSK_BT1_0 = 0x01;
    private const byte DATAMODUL_MODULATIONSHAPING_FSK_BT0_5 = 0x02;
    private const byte DATAMODUL_MODULATIONSHAPING_FSK_BT0_3 = 0x03;
    private const byte DATAMODUL_MODULATIONSHAPING_OOK_NONE = 0x00;
    private const byte DATAMODUL_MODULATIONSHAPING_OOK_BR = 0x01;
    private const byte DATAMODUL_MODULATIONSHAPING_OOK_2BR = 0x02;

    // Reg11PaLevel                                              
    private const byte PALEVEL_PA0ON = 0x80;
    private const byte PALEVEL_PA1ON = 0x40;
    private const byte PALEVEL_PA2ON = 0x20;
    private const byte PALEVEL_OUTPUTPOWER = 0x1F;

    // Reg23RssiConfig                                           
    private const byte RH_RF69_RSSICONFIG_RSSIDONE = 0x02;
    private const byte RH_RF69_RSSICONFIG_RSSISTART = 0x01;

    // Reg25DioMapping1                                          
    private const byte DIOMAPPING1_DIO0MAPPING = 0xC0;
    private const byte DIOMAPPING1_DIO0MAPPING_00 = 0x00;
    internal const byte DIOMAPPING1_DIO0MAPPING_01 = 0x40;
    private const byte DIOMAPPING1_DIO0MAPPING_10 = 0x80;
    private const byte DIOMAPPING1_DIO0MAPPING_11 = 0xc0;

    private const byte DIOMAPPING1_DIO1MAPPING = 0x30;
    private const byte DIOMAPPING1_DIO1MAPPING_00 = 0x00;
    private const byte DIOMAPPING1_DIO1MAPPING_01 = 0x10;
    private const byte DIOMAPPING1_DIO1MAPPING_10 = 0x20;
    private const byte DIOMAPPING1_DIO1MAPPING_11 = 0x30;

    private const byte DIOMAPPING1_DIO2MAPPING = 0x0C;
    private const byte DIOMAPPING1_DIO2MAPPING_00 = 0x00;
    private const byte DIOMAPPING1_DIO2MAPPING_01 = 0x04;
    private const byte DIOMAPPING1_DIO2MAPPING_10 = 0x08;
    private const byte DIOMAPPING1_DIO2MAPPING_11 = 0x0c;

    private const byte DIOMAPPING1_DIO3MAPPING = 0x03;
    private const byte DIOMAPPING1_DIO3MAPPING_00 = 0x00;
    private const byte DIOMAPPING1_DIO3MAPPING_01 = 0x01;
    private const byte DIOMAPPING1_DIO3MAPPING_10 = 0x02;
    private const byte DIOMAPPING1_DIO3MAPPING_11 = 0x03;

    // Reg26DioMapping2                                          
    private const byte DIOMAPPING2_DIO4MAPPING = 0xC0;
    private const byte DIOMAPPING2_DIO4MAPPING_00 = 0x00;
    private const byte DIOMAPPING2_DIO4MAPPING_01 = 0x40;
    private const byte DIOMAPPING2_DIO4MAPPING_10 = 0x80;
    private const byte DIOMAPPING2_DIO4MAPPING_11 = 0xc0;

    private const byte DIOMAPPING2_DIO5MAPPING = 0x30;
    private const byte DIOMAPPING2_DIO5MAPPING_00 = 0x00;
    private const byte DIOMAPPING2_DIO5MAPPING_01 = 0x10;
    private const byte DIOMAPPING2_DIO5MAPPING_10 = 0x20;
    private const byte DIOMAPPING2_DIO5MAPPING_11 = 0x30;

    private const byte DIOMAPPING2_CLKOUT = 0x07;
    private const byte DIOMAPPING2_CLKOUT_FXOSC_ = 0x00;
    private const byte DIOMAPPING2_CLKOUT_FXOSC_2 = 0x01;
    private const byte DIOMAPPING2_CLKOUT_FXOSC_4 = 0x02;
    private const byte DIOMAPPING2_CLKOUT_FXOSC_8 = 0x03;
    private const byte DIOMAPPING2_CLKOUT_FXOSC_16 = 0x04;
    private const byte DIOMAPPING2_CLKOUT_FXOSC_32 = 0x05;
    private const byte DIOMAPPING2_CLKOUT_FXOSC_RC = 0x06;
    private const byte DIOMAPPING2_CLKOUT_FXOSC_OFF = 0x07;

    // Reg27IrqFlags1                                            
    private const byte IRQFLAGS1_MODEREADY = 0x80;
    private const byte IRQFLAGS1_RXREADY = 0x40;
    private const byte IRQFLAGS1_TXREADY = 0x20;
    private const byte IRQFLAGS1_PLLLOCK = 0x10;
    private const byte IRQFLAGS1_RSSI = 0x08;
    private const byte IRQFLAGS1_TIMEOUT = 0x04;
    private const byte IRQFLAGS1_AUTOMODE = 0x02;
    private const byte IRQFLAGS1_SYNADDRESSMATCH = 0x01;

    // Reg28IrqFlags2                                            
    internal const byte IRQFLAGS2_FIFOFULL = 0x80;
    internal const byte IRQFLAGS2_FIFONOTEMPTY = 0x40;
    internal const byte IRQFLAGS2_FIFOLEVEL = 0x20;
    internal const byte IRQFLAGS2_FIFOOVERRUN = 0x10;
    internal const byte IRQFLAGS2_PACKETSENT = 0x08;
    internal const byte IRQFLAGS2_PAYLOADREADY = 0x04;
    internal const byte IRQFLAGS2_CRCOK = 0x02;

    // Reg2eSyncConfig                                           
    private const byte SYNCCONFIG_SYNCON = 0x80;
    private const byte SYNCCONFIG_FIFOFILLCONDITION_MANUAL = 0x40;
    private const byte SYNCCONFIG_SYNCSIZE = 0x38;
    private const byte SYNCCONFIG_SYNCSIZE_1 = 0x00;
    private const byte SYNCCONFIG_SYNCSIZE_2 = 0x08;
    private const byte SYNCCONFIG_SYNCSIZE_3 = 0x10;
    private const byte SYNCCONFIG_SYNCSIZE_4 = 0x18;
    private const byte SYNCCONFIG_SYNCSIZE_5 = 0x20;
    private const byte SYNCCONFIG_SYNCSIZE_6 = 0x28;
    private const byte SYNCCONFIG_SYNCSIZE_7 = 0x30;
    private const byte SYNCCONFIG_SYNCSIZE_8 = 0x38;
    private const byte SYNCCONFIG_SYNCSIZE_SYNCTOL = 0x07;

    // Reg37PacketConfig1                                        
    private const byte PACKETCONFIG1_PACKETFORMAT_VARIABLE = 0x80;
    private const byte PACKETCONFIG1_DCFREE = 0x60;
    private const byte PACKETCONFIG1_DCFREE_NONE = 0x00;
    private const byte PACKETCONFIG1_DCFREE_MANCHESTER = 0x20;
    private const byte PACKETCONFIG1_DCFREE_WHITENING = 0x40;
    private const byte PACKETCONFIG1_DCFREE_RESERVED = 0x60;
    private const byte PACKETCONFIG1_CRC_ON = 0x10;
    private const byte PACKETCONFIG1_CRCAUTOCLEAROFF = 0x08;
    private const byte PACKETCONFIG1_ADDRESSFILTERING = 0x06;
    private const byte PACKETCONFIG1_ADDRESSFILTERING_NONE = 0x00;
    private const byte PACKETCONFIG1_ADDRESSFILTERING_NODE = 0x02;
    private const byte PACKETCONFIG1_ADDRESSFILTERING_NODE_BC = 0x04;
    private const byte PACKETCONFIG1_ADDRESSFILTERING_RESERVED = 0x06;

    // Reg3bAutoModes                                            
    private const byte AUTOMODE_ENTER_COND_NONE = 0x00;
    private const byte AUTOMODE_ENTER_COND_FIFO_NOT_EMPTY = 0x20;
    private const byte AUTOMODE_ENTER_COND_FIFO_LEVEL = 0x40;
    private const byte AUTOMODE_ENTER_COND_CRC_OK = 0x60;
    private const byte AUTOMODE_ENTER_COND_PAYLOAD_READY = 0x80;
    private const byte AUTOMODE_ENTER_COND_SYNC_ADDRESS = 0xA0;
    private const byte AUTOMODE_ENTER_COND_PACKET_SENT = 0xC0;
    private const byte AUTOMODE_ENTER_COND_FIFO_EMPTY = 0xE0;

    private const byte AUTOMODE_EXIT_COND_NONE = 0x00;
    private const byte AUTOMODE_EXIT_COND_FIFO_EMPTY = 0x04;
    private const byte AUTOMODE_EXIT_COND_FIFO_LEVEL = 0x08;
    private const byte AUTOMODE_EXIT_COND_CRC_OK = 0x0C;
    private const byte AUTOMODE_EXIT_COND_PAYLOAD_READY = 0x10;
    private const byte AUTOMODE_EXIT_COND_SYNC_ADDRESS = 0x14;
    private const byte AUTOMODE_EXIT_COND_PACKET_SENT = 0x18;
    private const byte AUTOMODE_EXIT_COND_TIMEOUT = 0x1C;

    private const byte AUTOMODE_INTERMEDIATE_MODE_SLEEP = 0x00;
    private const byte AUTOMODE_INTERMEDIATE_MODE_STDBY = 0x01;
    private const byte AUTOMODE_INTERMEDIATE_MODE_RX = 0x02;
    private const byte AUTOMODE_INTERMEDIATE_MODE_TX = 0x03;

    // Reg3cFifoThresh                                           
    private const byte FIFOTHRESH_TXSTARTCONDITION_NOTEMPTY = 0x80;
    private const byte FIFOTHRESH_FIFOTHRESHOLD = 0x7F;

    // Reg3dPacketConfig2                                        
    private const byte PACKETCONFIG2_INTERPACKETRXDELAY = 0xF0;
    private const byte PACKETCONFIG2_RESTARTRX = 0x04;
    private const byte PACKETCONFIG2_AUTORXRESTARTON = 0x02;
    private const byte PACKETCONFIG2_AESON = 0x01;

    // Reg4eTemp1                                                
    private const byte TEMP1_TEMPMEASSTART = 0x08;
    private const byte TEMP1_TEMPMEASRUNNING = 0x04;

    // Reg5aTestPa1                                              
    private const byte TESTPA1_NORMAL = 0x55;
    private const byte TESTPA1_BOOST = 0x5D;

    // Reg5cTestPa2                                              
    private const byte TESTPA2_NORMAL = 0x70;
    private const byte TESTPA2_BOOST = 0x7C;

    // Reg6fTestDagc                                             
    private const byte TESTDAGC_CONTINUOUSDAGC_NORMAL = 0x00;
    private const byte TESTDAGC_CONTINUOUSDAGC_IMPROVED_LOWBETAON = 0x20;
    private const byte TESTDAGC_CONTINUOUSDAGC_IMPROVED_LOWBETAOFF = 0x30;
}
