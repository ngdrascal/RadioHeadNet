// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Local
// ReSharper disable UnusedMember.Global
// ReSharper disable IdentifierTypo

namespace RadioHeadNet;

public partial class RhRf69
{
    // These are indexed by the values of ModemConfigChoice
    // Stored in flash (program) memory to save SRAM
    // It is important to keep the modulation index for FSK between 0.5 and 10
    // modulation index = 2 * Fdev / BR
    // Note that I have not had much success with FSK with Fd > ~5
    // You have to construct these by hand, using the data from the RF69 Datasheet :-(
    // or use the SX1231 starter kit software (Ctl-Alt-N to use that without a connected radio)
    private const byte CONFIG_FSK = RhRf69.DATAMODUL_DATAMODE_PACKET | RhRf69.DATAMODUL_MODULATIONTYPE_FSK |
                                    RhRf69.DATAMODUL_MODULATIONSHAPING_FSK_NONE;

    private const byte CONFIG_GFSK = RhRf69.DATAMODUL_DATAMODE_PACKET | RhRf69.DATAMODUL_MODULATIONTYPE_FSK |
                                     RhRf69.DATAMODUL_MODULATIONSHAPING_FSK_BT1_0;

    private const byte CONFIG_OOK = RhRf69.DATAMODUL_DATAMODE_PACKET | RhRf69.DATAMODUL_MODULATIONTYPE_OOK |
                                    RhRf69.DATAMODUL_MODULATIONSHAPING_OOK_NONE;

    // Choices for RH_RF69_REG_37_PACKETCONFIG1:
    private const byte CONFIG_NOWHITE = RhRf69.PACKETCONFIG1_PACKETFORMAT_VARIABLE |
                                        RhRf69.PACKETCONFIG1_DCFREE_NONE | RhRf69.PACKETCONFIG1_CRC_ON |
                                        RhRf69.PACKETCONFIG1_ADDRESSFILTERING_NONE;

    private const byte CONFIG_WHITE = RhRf69.PACKETCONFIG1_PACKETFORMAT_VARIABLE |
                                      RhRf69.PACKETCONFIG1_DCFREE_WHITENING | RhRf69.PACKETCONFIG1_CRC_ON |
                                      RhRf69.PACKETCONFIG1_ADDRESSFILTERING_NONE;

    private const byte CONFIG_MANCHESTER = RhRf69.PACKETCONFIG1_PACKETFORMAT_VARIABLE |
                                           RhRf69.PACKETCONFIG1_DCFREE_MANCHESTER |
                                           RhRf69.PACKETCONFIG1_CRC_ON |
                                           RhRf69.PACKETCONFIG1_ADDRESSFILTERING_NONE;


    /// Defines register values for a set of modem configuration registers
    /// that can be passed to setModemRegisters() if none of the choices in
    /// ModemConfigChoice suit your need setModemRegisters() writes the
    /// register values from this structure to the appropriate RF69 registers
    /// to set the desired modulation type, data rate and deviation/bandwidth.
    public struct ModemConfig
    {
        public byte reg_02;   // Value for register RH_RF69_REG_02_DATAMODUL
        public byte reg_03;   // Value for register RH_RF69_REG_03_BITRATEMSB
        public byte reg_04;   // Value for register RH_RF69_REG_04_BITRATELSB
        public byte reg_05;   // Value for register RH_RF69_REG_05_FDEVMSB
        public byte reg_06;   // Value for register RH_RF69_REG_06_FDEVLSB
        public byte reg_19;   // Value for register RH_RF69_REG_19_RXBW
        public byte reg_1a;   // Value for register RH_RF69_REG_1A_AFCBW
        public byte reg_37;   // Value for register RH_RF69_REG_37_PACKETCONFIG1
    }

    /// Choices for setModemConfig() for a selected subset of common
    /// modulation types, and data rates. If you need another configuration,
    /// use the register calculator.  and call setModemRegisters() with your
    /// desired settings.  
    /// These are indexes into MODEM_CONFIG_TABLE. We strongly recommend you use these symbolic
    /// definitions and not their integer equivalents: its possible that new values will be
    /// introduced in later versions (though we will try to avoid it).
    /// CAUTION: some of these configurations do not work corectly and are marked as such.
    public enum ModemConfigChoice : byte
    {
        FSK_Rb2Fd5 = 0,    // FSK, Whitening, Rb = 2kbs,    Fd = 5kHz
        FSK_Rb2_4Fd4_8,    // FSK, Whitening, Rb = 2.4kbs,  Fd = 4.8kHz 
        FSK_Rb4_8Fd9_6,    // FSK, Whitening, Rb = 4.8kbs,  Fd = 9.6kHz 
        FSK_Rb9_6Fd19_2,   // FSK, Whitening, Rb = 9.6kbs,  Fd = 19.2kHz
        FSK_Rb19_2Fd38_4,  // FSK, Whitening, Rb = 19.2kbs, Fd = 38.4kHz
        FSK_Rb38_4Fd76_8,  // FSK, Whitening, Rb = 38.4kbs, Fd = 76.8kHz
        FSK_Rb57_6Fd120,   // FSK, Whitening, Rb = 57.6kbs, Fd = 120kHz
        FSK_Rb125Fd125,    // FSK, Whitening, Rb = 125kbs,  Fd = 125kHz
        FSK_Rb250Fd250,    // FSK, Whitening, Rb = 250kbs,  Fd = 250kHz
        FSK_Rb55555Fd50,   // FSK, Whitening, Rb = 55555kbs,Fd = 50kHz for RFM69 lib compatibility

        GFSK_Rb2Fd5,        // GFSK, Whitening, Rb = 2kbs,    Fd = 5kHz
        GFSK_Rb2_4Fd4_8,    // GFSK, Whitening, Rb = 2.4kbs,  Fd = 4.8kHz
        GFSK_Rb4_8Fd9_6,    // GFSK, Whitening, Rb = 4.8kbs,  Fd = 9.6kHz
        GFSK_Rb9_6Fd19_2,   // GFSK, Whitening, Rb = 9.6kbs,  Fd = 19.2kHz
        GFSK_Rb19_2Fd38_4,  // GFSK, Whitening, Rb = 19.2kbs, Fd = 38.4kHz
        GFSK_Rb38_4Fd76_8,  // GFSK, Whitening, Rb = 38.4kbs, Fd = 76.8kHz
        GFSK_Rb57_6Fd120,   // GFSK, Whitening, Rb = 57.6kbs, Fd = 120kHz
        GFSK_Rb125Fd125,    // GFSK, Whitening, Rb = 125kbs,  Fd = 125kHz
        GFSK_Rb250Fd250,    // GFSK, Whitening, Rb = 250kbs,  Fd = 250kHz
        GFSK_Rb55555Fd50,   // GFSK, Whitening, Rb = 55555kbs,Fd = 50kHz

        OOK_Rb1Bw1,         // OOK, Whitening, Rb = 1kbs,    Rx Bandwidth = 1kHz. 
        OOK_Rb1_2Bw75,      // OOK, Whitening, Rb = 1.2kbs,  Rx Bandwidth = 75kHz. 
        OOK_Rb2_4Bw4_8,     // OOK, Whitening, Rb = 2.4kbs,  Rx Bandwidth = 4.8kHz. 
        OOK_Rb4_8Bw9_6,     // OOK, Whitening, Rb = 4.8kbs,  Rx Bandwidth = 9.6kHz. 
        OOK_Rb9_6Bw19_2,    // OOK, Whitening, Rb = 9.6kbs,  Rx Bandwidth = 19.2kHz. 
        OOK_Rb19_2Bw38_4,   // OOK, Whitening, Rb = 19.2kbs, Rx Bandwidth = 38.4kHz. 
        OOK_Rb32Bw64,       // OOK, Whitening, Rb = 32kbs,   Rx Bandwidth = 64kHz. 

    };


    private ModemConfig[] MODEM_CONFIG_TABLE =
    {
    // FSK, No Manchester, no shaping, whitening, CRC, no address filtering
    // AFC BW == RX BW == 2 x bit rate
    new() { reg_02 = CONFIG_FSK,  reg_03 = 0x3e, reg_04 = 0x80, reg_05 = 0x00, reg_06 = 0x52, reg_19 = 0xf4, reg_1a = 0xf4, reg_37 = CONFIG_WHITE }, // FSK_Rb2Fd5      
    new() { reg_02 = CONFIG_FSK,  reg_03 = 0x34, reg_04 = 0x15, reg_05 = 0x00, reg_06 = 0x4f, reg_19 = 0xf4, reg_1a = 0xf4, reg_37 = CONFIG_WHITE}, // FSK_Rb2_4Fd4_8
    new() { reg_02 = CONFIG_FSK,  reg_03 = 0x1a, reg_04 = 0x0b, reg_05 = 0x00, reg_06 = 0x9d, reg_19 = 0xf4, reg_1a = 0xf4, reg_37 = CONFIG_WHITE}, // FSK_Rb4_8Fd9_6

    new() { reg_02 = CONFIG_FSK,  reg_03 = 0x0d, reg_04 = 0x05, reg_05 = 0x01, reg_06 = 0x3b, reg_19 = 0xf4, reg_1a = 0xf4, reg_37 = CONFIG_WHITE}, // FSK_Rb9_6Fd19_2
    new() { reg_02 = CONFIG_FSK,  reg_03 = 0x06, reg_04 = 0x83, reg_05 = 0x02, reg_06 = 0x75, reg_19 = 0xf3, reg_1a = 0xf3, reg_37 = CONFIG_WHITE}, // FSK_Rb19_2Fd38_4
    new() { reg_02 = CONFIG_FSK,  reg_03 = 0x03, reg_04 = 0x41, reg_05 = 0x04, reg_06 = 0xea, reg_19 = 0xf2, reg_1a = 0xf2, reg_37 = CONFIG_WHITE}, // FSK_Rb38_4Fd76_8

    new() { reg_02 = CONFIG_FSK,  reg_03 = 0x02, reg_04 = 0x2c, reg_05 = 0x07, reg_06 = 0xae, reg_19 = 0xe2, reg_1a = 0xe2, reg_37 = CONFIG_WHITE}, // FSK_Rb57_6Fd120
    new() { reg_02 = CONFIG_FSK,  reg_03 = 0x01, reg_04 = 0x00, reg_05 = 0x08, reg_06 = 0x00, reg_19 = 0xe1, reg_1a = 0xe1, reg_37 = CONFIG_WHITE}, // FSK_Rb125Fd125
    new() { reg_02 = CONFIG_FSK,  reg_03 = 0x00, reg_04 = 0x80, reg_05 = 0x10, reg_06 = 0x00, reg_19 = 0xe0, reg_1a = 0xe0, reg_37 = CONFIG_WHITE}, // FSK_Rb250Fd250
    new() { reg_02 = CONFIG_FSK,  reg_03 = 0x02, reg_04 = 0x40, reg_05 = 0x03, reg_06 = 0x33, reg_19 = 0x42, reg_1a = 0x42, reg_37 = CONFIG_WHITE}, // FSK_Rb55555Fd50 

    //  02,        03,   04,   05,   06,   19,   1a,  37
    // GFSK (BT=1.0), No Manchester, whitening, CRC, no address filtering
    // AFC BW == RX BW == 2 x bit rate
    new() { reg_02 = CONFIG_GFSK, reg_03 = 0x3e, reg_04 = 0x80, reg_05 = 0x00, reg_06 = 0x52, reg_19 = 0xf4, reg_1a = 0xf5, reg_37 = CONFIG_WHITE}, // GFSK_Rb2Fd5
    new() { reg_02 = CONFIG_GFSK, reg_03 = 0x34, reg_04 = 0x15, reg_05 = 0x00, reg_06 = 0x4f, reg_19 = 0xf4, reg_1a = 0xf4, reg_37 = CONFIG_WHITE}, // GFSK_Rb2_4Fd4_8
    new() { reg_02 = CONFIG_GFSK, reg_03 = 0x1a, reg_04 = 0x0b, reg_05 = 0x00, reg_06 = 0x9d, reg_19 = 0xf4, reg_1a = 0xf4, reg_37 = CONFIG_WHITE}, // GFSK_Rb4_8Fd9_6

    new() { reg_02 = CONFIG_GFSK, reg_03 = 0x0d, reg_04 = 0x05, reg_05 = 0x01, reg_06 = 0x3b, reg_19 = 0xf4, reg_1a = 0xf4, reg_37 = CONFIG_WHITE}, // GFSK_Rb9_6Fd19_2
    new() { reg_02 = CONFIG_GFSK, reg_03 = 0x06, reg_04 = 0x83, reg_05 = 0x02, reg_06 = 0x75, reg_19 = 0xf3, reg_1a = 0xf3, reg_37 = CONFIG_WHITE}, // GFSK_Rb19_2Fd38_4
    new() { reg_02 = CONFIG_GFSK, reg_03 = 0x03, reg_04 = 0x41, reg_05 = 0x04, reg_06 = 0xea, reg_19 = 0xf2, reg_1a = 0xf2, reg_37 = CONFIG_WHITE}, // GFSK_Rb38_4Fd76_8

    new() { reg_02 = CONFIG_GFSK, reg_03 = 0x02, reg_04 = 0x2c, reg_05 = 0x07, reg_06 = 0xae, reg_19 = 0xe2, reg_1a = 0xe2, reg_37 = CONFIG_WHITE}, // GFSK_Rb57_6Fd120
    new() { reg_02 = CONFIG_GFSK, reg_03 = 0x01, reg_04 = 0x00, reg_05 = 0x08, reg_06 = 0x00, reg_19 = 0xe1, reg_1a = 0xe1, reg_37 = CONFIG_WHITE}, // GFSK_Rb125Fd125
    new() { reg_02 = CONFIG_GFSK, reg_03 = 0x00, reg_04 = 0x80, reg_05 = 0x10, reg_06 = 0x00, reg_19 = 0xe0, reg_1a = 0xe0, reg_37 = CONFIG_WHITE}, // GFSK_Rb250Fd250
    new() { reg_02 = CONFIG_GFSK, reg_03 = 0x02, reg_04 = 0x40, reg_05 = 0x03, reg_06 = 0x33, reg_19 = 0x42, reg_1a = 0x42, reg_37 = CONFIG_WHITE}, // GFSK_Rb55555Fd50 

    //  02,        03,   04,   05,   06,   19,   1a,  37
    // OOK, No Manchester, no shaping, whitening, CRC, no address filtering
    // with the help of the SX1231 configuration program
    // AFC BW == RX BW
    // All OOK configs have the default:
    // Threshold Type: Peak
    // Peak Threshold Step: 0.5dB
    // Peak threshiold dec: ONce per chip
    // Fixed threshold: 6dB
    new() { reg_02 = CONFIG_OOK,  reg_03 = 0x7d, reg_04 = 0x00, reg_05 = 0x00, reg_06 = 0x10, reg_19 = 0x88, reg_1a = 0x88, reg_37 = CONFIG_WHITE}, // OOK_Rb1Bw1
    new() { reg_02 = CONFIG_OOK,  reg_03 = 0x68, reg_04 = 0x2b, reg_05 = 0x00, reg_06 = 0x10, reg_19 = 0xf1, reg_1a = 0xf1, reg_37 = CONFIG_WHITE}, // OOK_Rb1_2Bw75
    new() { reg_02 = CONFIG_OOK,  reg_03 = 0x34, reg_04 = 0x15, reg_05 = 0x00, reg_06 = 0x10, reg_19 = 0xf5, reg_1a = 0xf5, reg_37 = CONFIG_WHITE}, // OOK_Rb2_4Bw4_8
    new() { reg_02 = CONFIG_OOK,  reg_03 = 0x1a, reg_04 = 0x0b, reg_05 = 0x00, reg_06 = 0x10, reg_19 = 0xf4, reg_1a = 0xf4, reg_37 = CONFIG_WHITE}, // OOK_Rb4_8Bw9_6
    new() { reg_02 = CONFIG_OOK,  reg_03 = 0x0d, reg_04 = 0x05, reg_05 = 0x00, reg_06 = 0x10, reg_19 = 0xf3, reg_1a = 0xf3, reg_37 = CONFIG_WHITE}, // OOK_Rb9_6Bw19_2
    new() { reg_02 = CONFIG_OOK,  reg_03 = 0x06, reg_04 = 0x83, reg_05 = 0x00, reg_06 = 0x10, reg_19 = 0xf2, reg_1a = 0xf2, reg_37 = CONFIG_WHITE}, // OOK_Rb19_2Bw38_4
    new() { reg_02 = CONFIG_OOK,  reg_03 = 0x03, reg_04 = 0xe8, reg_05 = 0x00, reg_06 = 0x10, reg_19 = 0xe2, reg_1a = 0xe2, reg_37 = CONFIG_WHITE}  // OOK_Rb32Bw64
    };
}
