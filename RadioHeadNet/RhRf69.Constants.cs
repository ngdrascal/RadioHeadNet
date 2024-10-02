﻿// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Local
// ReSharper disable IdentifierTypo
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedMember.Global

#define RFM69_HW

namespace RadioHeadNet;

public partial class RhRf69
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
    internal const byte REG_00_FIFO = 0x00;
    internal const byte REG_01_OPMODE = 0x01;
    internal const byte REG_02_DATAMODUL = 0x02;
    internal const byte REG_03_BITRATEMSB = 0x03;
    internal const byte REG_04_BITRATELSB = 0x04;
    internal const byte REG_05_FDEVMSB = 0x05;
    internal const byte REG_06_FDEVLSB = 0x06;
    internal const byte REG_07_FRFMSB = 0x07;
    internal const byte REG_08_FRFMID = 0x08;
    internal const byte REG_09_FRFLSB = 0x09;
    internal const byte REG_0A_OSC1 = 0x0A;
    internal const byte REG_0B_AFCCTRL = 0x0B;
    internal const byte REG_0C_RESERVED = 0x0C;
    internal const byte REG_0D_LISTEN1 = 0x0D;
    internal const byte REG_0E_LISTEN2 = 0x0E;
    internal const byte REG_0F_LISTEN3 = 0x0F;
    internal const byte REG_10_VERSION = 0x10;
    internal const byte REG_11_PALEVEL = 0x11;
    internal const byte REG_12_PARAMP = 0x12;
    internal const byte REG_13_OCP = 0x13;
    internal const byte REG_14_RESERVED = 0x14;
    internal const byte REG_15_RESERVED = 0x15;
    internal const byte REG_16_RESERVED = 0x16;
    internal const byte REG_17_RESERVED = 0x17;
    internal const byte REG_18_LNA = 0x18;
    internal const byte REG_19_RXBW = 0x19;
    internal const byte REG_1A_AFCBW = 0x1A;
    internal const byte REG_1B_OOKPEAK = 0x1B;
    internal const byte REG_1C_OOKAVG = 0x1C;
    internal const byte REG_1D_OOKFIX = 0x1D;
    internal const byte REG_1E_AFCFEI = 0x1E;
    internal const byte REG_1F_AFCMSB = 0x1F;
    internal const byte REG_20_AFCLSB = 0x20;
    internal const byte REG_21_FEIMSB = 0x21;
    internal const byte REG_22_FEILSB = 0x22;
    internal const byte REG_23_RSSICONFIG = 0x23;
    internal const byte REG_24_RSSIVALUE = 0x24;
    internal const byte REG_25_DIOMAPPING1 = 0x25;
    internal const byte REG_26_DIOMAPPING2 = 0x26;
    internal const byte REG_27_IRQFLAGS1 = 0x27;
    internal const byte REG_28_IRQFLAGS2 = 0x28;
    internal const byte REG_29_RSSITHRESH = 0x29;
    internal const byte REG_2A_RXTIMEOUT1 = 0x2A;
    internal const byte REG_2B_RXTIMEOUT2 = 0x2B;
    internal const byte REG_2C_PREAMBLEMSB = 0x2C;
    internal const byte REG_2D_PREAMBLELSB = 0x2D;
    internal const byte REG_2E_SYNCCONFIG = 0x2E;
    internal const byte REG_2F_SYNCVALUE1 = 0x2F;
    // another 7 sync word bytes follow, 30 through 36 inclusive
    internal const byte REG_37_PACKETCONFIG1 = 0x37;
    internal const byte REG_38_PAYLOADLENGTH = 0x38;
    internal const byte REG_39_NODEADRS = 0x39;
    internal const byte REG_3A_BROADCASTADRS = 0x3A;
    internal const byte REG_3B_AUTOMODES = 0x3B;
    internal const byte REG_3C_FIFOTHRESH = 0x3C;
    internal const byte REG_3D_PACKETCONFIG2 = 0x3D;
    internal const byte REG_3E_AESKEY1 = 0x3E;
    // Another 15 AES key bytes follow
    internal const byte REG_4E_TEMP1 = 0x4E;
    internal const byte REG_4F_TEMP2 = 0x4F;
    internal const byte REG_58_TESTLNA = 0x58;
    internal const byte REG_5A_TESTPA1 = 0x5A;
    internal const byte REG_5C_TESTPA2 = 0x5C;
    internal const byte REG_6F_TESTDAGC = 0x6F;
    internal const byte REG_71_TESTAFC = 0x71;

    // These register masks etc. are named wherever possible
    // corresponding to the bit and field names in the RFM69 Manual

    // REG_01_OPMODE
    private const byte OPMODE_SEQUENCEROFF = 0x80;
    private const byte OPMODE_LISTENON = 0x40;
    private const byte OPMODE_LISTENABORT = 0x20;
    private const byte OPMODE_MODE = 0x1C;
    internal const byte OPMODE_MODE_SLEEP = 0x00;
    internal const byte OPMODE_MODE_STDBY = 0x04;
    internal const byte OPMODE_MODE_FS = 0x08;
    internal const byte OPMODE_MODE_TX = 0x0C;
    internal const byte OPMODE_MODE_RX = 0x10;

    // REG_02_DATAMODUL                                            
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

    // REG_11_PALEVEL                                              
    private const byte PALEVEL_PA0ON = 0x80;
    private const byte PALEVEL_PA1ON = 0x40;
    private const byte PALEVEL_PA2ON = 0x20;
    private const byte PALEVEL_OUTPUTPOWER = 0x1F;

    // REG_23_RSSICONFIG                                           
    private const byte RH_RF69_RSSICONFIG_RSSIDONE = 0x02;
    private const byte RH_RF69_RSSICONFIG_RSSISTART = 0x01;

    // REG_25_DIOMAPPING1                                          
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

    // REG_26_DIOMAPPING2                                          
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

    // REG_27_IRQFLAGS1                                            
    private const byte IRQFLAGS1_MODEREADY = 0x80;
    private const byte IRQFLAGS1_RXREADY = 0x40;
    private const byte IRQFLAGS1_TXREADY = 0x20;
    private const byte IRQFLAGS1_PLLLOCK = 0x10;
    private const byte IRQFLAGS1_RSSI = 0x08;
    private const byte IRQFLAGS1_TIMEOUT = 0x04;
    private const byte IRQFLAGS1_AUTOMODE = 0x02;
    private const byte IRQFLAGS1_SYNADDRESSMATCH = 0x01;

    // REG_28_IRQFLAGS2                                            
    internal const byte IRQFLAGS2_FIFOFULL = 0x80;
    internal const byte IRQFLAGS2_FIFONOTEMPTY = 0x40;
    internal const byte IRQFLAGS2_FIFOLEVEL = 0x20;
    internal const byte IRQFLAGS2_FIFOOVERRUN = 0x10;
    internal const byte IRQFLAGS2_PACKETSENT = 0x08;
    internal const byte IRQFLAGS2_PAYLOADREADY = 0x04;
    internal const byte IRQFLAGS2_CRCOK = 0x02;

    // REG_2E_SYNCCONFIG                                           
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

    // REG_37_PACKETCONFIG1                                        
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

    // REG_3B_AUTOMODES                                            
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

    // REG_3C_FIFOTHRESH                                           
    private const byte FIFOTHRESH_TXSTARTCONDITION_NOTEMPTY = 0x80;
    private const byte FIFOTHRESH_FIFOTHRESHOLD = 0x7F;

    // REG_3D_PACKETCONFIG2                                        
    private const byte PACKETCONFIG2_INTERPACKETRXDELAY = 0xF0;
    private const byte PACKETCONFIG2_RESTARTRX = 0x04;
    private const byte PACKETCONFIG2_AUTORXRESTARTON = 0x02;
    private const byte PACKETCONFIG2_AESON = 0x01;

    // REG_4E_TEMP1                                                
    private const byte TEMP1_TEMPMEASSTART = 0x08;
    private const byte TEMP1_TEMPMEASRUNNING = 0x04;

    // REG_5A_TESTPA1                                              
    private const byte TESTPA1_NORMAL = 0x55;
    private const byte TESTPA1_BOOST = 0x5D;

    // REG_5C_TESTPA2                                              
    private const byte TESTPA2_NORMAL = 0x70;
    private const byte TESTPA2_BOOST = 0x7C;

    // REG_6F_TESTDAGC                                             
    private const byte TESTDAGC_CONTINUOUSDAGC_NORMAL = 0x00;
    private const byte TESTDAGC_CONTINUOUSDAGC_IMPROVED_LOWBETAON = 0x20;
    private const byte TESTDAGC_CONTINUOUSDAGC_IMPROVED_LOWBETAOFF = 0x30;
}
