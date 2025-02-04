// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Local
// ReSharper disable UnusedMember.Global
// ReSharper disable IdentifierTypo

namespace RadioHeadIot.RhRf69;

public partial class Rf69
{
    // These are indexed by the values of ModemConfigChoice
    // Stored in flash (program) memory to save SRAM
    // It is important to keep the modulation index for FSK between 0.5 and 10
    // modulation index = 2 * Fdev / BR
    // Note that I have not had much success with FSK with Fd > ~5
    // You have to construct these by hand, using the data from the RF69 Datasheet :-(
    // or use the SX1231 starter kit software (Ctl-Alt-N to use that without a connected radio)
    private const byte CONFIG_FSK = Rf69.DATAMODUL_DATAMODE_PACKET | Rf69.DATAMODUL_MODULATIONTYPE_FSK |
                                    Rf69.DATAMODUL_MODULATIONSHAPING_FSK_NONE;

    private const byte CONFIG_GFSK = Rf69.DATAMODUL_DATAMODE_PACKET | Rf69.DATAMODUL_MODULATIONTYPE_FSK |
                                     Rf69.DATAMODUL_MODULATIONSHAPING_FSK_BT1_0;

    private const byte CONFIG_OOK = Rf69.DATAMODUL_DATAMODE_PACKET | Rf69.DATAMODUL_MODULATIONTYPE_OOK |
                                    Rf69.DATAMODUL_MODULATIONSHAPING_OOK_NONE;

    // Choices for Reg37_PacketConfig1:
    private const byte CONFIG_NOWHITE = Rf69.PACKETCONFIG1_PACKETFORMAT_VARIABLE |
                                        Rf69.PACKETCONFIG1_DCFREE_NONE | Rf69.PACKETCONFIG1_CRC_ON |
                                        Rf69.PACKETCONFIG1_ADDRESSFILTERING_NONE;

    private const byte CONFIG_WHITE = Rf69.PACKETCONFIG1_PACKETFORMAT_VARIABLE |
                                      Rf69.PACKETCONFIG1_DCFREE_WHITENING | Rf69.PACKETCONFIG1_CRC_ON |
                                      Rf69.PACKETCONFIG1_ADDRESSFILTERING_NONE;

    private const byte CONFIG_MANCHESTER = Rf69.PACKETCONFIG1_PACKETFORMAT_VARIABLE |
                                           Rf69.PACKETCONFIG1_DCFREE_MANCHESTER |
                                           Rf69.PACKETCONFIG1_CRC_ON |
                                           Rf69.PACKETCONFIG1_ADDRESSFILTERING_NONE;


    /// Defines register values for a set of modem configuration registers
    /// that can be passed to setModemRegisters() if none of the choices in
    /// ModemConfigChoice suit your need setModemRegisters() writes the
    /// register values from this structure to the appropriate RF69 registers
    /// to set the desired modulation type, data rate and deviation/bandwidth.
    public struct ModemConfig
    {
        public byte Reg02;   // Value for register Reg02_DataModul
        public byte Reg03;   // Value for register Reg03_BitRateMsb
        public byte Reg04;   // Value for register Reg04_BitRateLsb
        public byte Reg05;   // Value for register Reg05_FDevMsb
        public byte Reg06;   // Value for register Reg06_FDevLsb
        public byte Reg19;   // Value for register Reg19_RxBw
        public byte Reg1A;   // Value for register Reg1A_ArcBw
        public byte Reg37;   // Value for register Reg37_PacketConfig1
    }

    /// Choices for setModemConfig() for a selected subset of common
    /// modulation types, and data rates. If you need another configuration,
    /// use the register calculator.  and call setModemRegisters() with your
    /// desired settings.  
    /// These are indexes into MODEM_CONFIG_TABLE. We strongly recommend you use these symbolic
    /// definitions and not their integer equivalents: it's possible that new values will be
    /// introduced in later versions (though we will try to avoid it).
    /// CAUTION: some of these configurations do not work correctly and are marked as such.
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

    private readonly ModemConfig[] MODEM_CONFIG_TABLE =
    [
        // FSK, No Manchester, no shaping, whitening, CRC, no address filtering
        // AFC BW == RX BW == 2 x bit rate
        new() { Reg02 = CONFIG_FSK,  Reg03 = 0x3e, Reg04 = 0x80, Reg05 = 0x00, Reg06 = 0x52, Reg19 = 0xf4, Reg1A = 0xf4, Reg37 = CONFIG_WHITE }, // FSK_Rb2Fd5      
        new() { Reg02 = CONFIG_FSK,  Reg03 = 0x34, Reg04 = 0x15, Reg05 = 0x00, Reg06 = 0x4f, Reg19 = 0xf4, Reg1A = 0xf4, Reg37 = CONFIG_WHITE}, // FSK_Rb2_4Fd4_8
        new() { Reg02 = CONFIG_FSK,  Reg03 = 0x1a, Reg04 = 0x0b, Reg05 = 0x00, Reg06 = 0x9d, Reg19 = 0xf4, Reg1A = 0xf4, Reg37 = CONFIG_WHITE}, // FSK_Rb4_8Fd9_6

        new() { Reg02 = CONFIG_FSK,  Reg03 = 0x0d, Reg04 = 0x05, Reg05 = 0x01, Reg06 = 0x3b, Reg19 = 0xf4, Reg1A = 0xf4, Reg37 = CONFIG_WHITE}, // FSK_Rb9_6Fd19_2
        new() { Reg02 = CONFIG_FSK,  Reg03 = 0x06, Reg04 = 0x83, Reg05 = 0x02, Reg06 = 0x75, Reg19 = 0xf3, Reg1A = 0xf3, Reg37 = CONFIG_WHITE}, // FSK_Rb19_2Fd38_4
        new() { Reg02 = CONFIG_FSK,  Reg03 = 0x03, Reg04 = 0x41, Reg05 = 0x04, Reg06 = 0xea, Reg19 = 0xf2, Reg1A = 0xf2, Reg37 = CONFIG_WHITE}, // FSK_Rb38_4Fd76_8

        new() { Reg02 = CONFIG_FSK,  Reg03 = 0x02, Reg04 = 0x2c, Reg05 = 0x07, Reg06 = 0xae, Reg19 = 0xe2, Reg1A = 0xe2, Reg37 = CONFIG_WHITE}, // FSK_Rb57_6Fd120
        new() { Reg02 = CONFIG_FSK,  Reg03 = 0x01, Reg04 = 0x00, Reg05 = 0x08, Reg06 = 0x00, Reg19 = 0xe1, Reg1A = 0xe1, Reg37 = CONFIG_WHITE}, // FSK_Rb125Fd125
        new() { Reg02 = CONFIG_FSK,  Reg03 = 0x00, Reg04 = 0x80, Reg05 = 0x10, Reg06 = 0x00, Reg19 = 0xe0, Reg1A = 0xe0, Reg37 = CONFIG_WHITE}, // FSK_Rb250Fd250
        new() { Reg02 = CONFIG_FSK,  Reg03 = 0x02, Reg04 = 0x40, Reg05 = 0x03, Reg06 = 0x33, Reg19 = 0x42, Reg1A = 0x42, Reg37 = CONFIG_WHITE}, // FSK_Rb55555Fd50 

        //  02,        03,   04,   05,   06,   19,   1a,  37
        // GFSK (BT=1.0), No Manchester, whitening, CRC, no address filtering
        // AFC BW == RX BW == 2 x bit rate
        new() { Reg02 = CONFIG_GFSK, Reg03 = 0x3e, Reg04 = 0x80, Reg05 = 0x00, Reg06 = 0x52, Reg19 = 0xf4, Reg1A = 0xf5, Reg37 = CONFIG_WHITE}, // GFSK_Rb2Fd5
        new() { Reg02 = CONFIG_GFSK, Reg03 = 0x34, Reg04 = 0x15, Reg05 = 0x00, Reg06 = 0x4f, Reg19 = 0xf4, Reg1A = 0xf4, Reg37 = CONFIG_WHITE}, // GFSK_Rb2_4Fd4_8
        new() { Reg02 = CONFIG_GFSK, Reg03 = 0x1a, Reg04 = 0x0b, Reg05 = 0x00, Reg06 = 0x9d, Reg19 = 0xf4, Reg1A = 0xf4, Reg37 = CONFIG_WHITE}, // GFSK_Rb4_8Fd9_6

        new() { Reg02 = CONFIG_GFSK, Reg03 = 0x0d, Reg04 = 0x05, Reg05 = 0x01, Reg06 = 0x3b, Reg19 = 0xf4, Reg1A = 0xf4, Reg37 = CONFIG_WHITE}, // GFSK_Rb9_6Fd19_2
        new() { Reg02 = CONFIG_GFSK, Reg03 = 0x06, Reg04 = 0x83, Reg05 = 0x02, Reg06 = 0x75, Reg19 = 0xf3, Reg1A = 0xf3, Reg37 = CONFIG_WHITE}, // GFSK_Rb19_2Fd38_4
        new() { Reg02 = CONFIG_GFSK, Reg03 = 0x03, Reg04 = 0x41, Reg05 = 0x04, Reg06 = 0xea, Reg19 = 0xf2, Reg1A = 0xf2, Reg37 = CONFIG_WHITE}, // GFSK_Rb38_4Fd76_8

        new() { Reg02 = CONFIG_GFSK, Reg03 = 0x02, Reg04 = 0x2c, Reg05 = 0x07, Reg06 = 0xae, Reg19 = 0xe2, Reg1A = 0xe2, Reg37 = CONFIG_WHITE}, // GFSK_Rb57_6Fd120
        new() { Reg02 = CONFIG_GFSK, Reg03 = 0x01, Reg04 = 0x00, Reg05 = 0x08, Reg06 = 0x00, Reg19 = 0xe1, Reg1A = 0xe1, Reg37 = CONFIG_WHITE}, // GFSK_Rb125Fd125
        new() { Reg02 = CONFIG_GFSK, Reg03 = 0x00, Reg04 = 0x80, Reg05 = 0x10, Reg06 = 0x00, Reg19 = 0xe0, Reg1A = 0xe0, Reg37 = CONFIG_WHITE}, // GFSK_Rb250Fd250
        new() { Reg02 = CONFIG_GFSK, Reg03 = 0x02, Reg04 = 0x40, Reg05 = 0x03, Reg06 = 0x33, Reg19 = 0x42, Reg1A = 0x42, Reg37 = CONFIG_WHITE}, // GFSK_Rb55555Fd50 

        // 02,  03,            04,         05,        06,  19,   1a,  37
        // OOK, No Manchester, no shaping, whitening, CRC, no address filtering
        // with the help of the SX1231 configuration program
        // AFC BW == RX BW
        // All OOK configs have the default:
        // Threshold Type: Peak
        // Peak Threshold Step: 0.5dB
        // Peak threshold dec: ONce per chip
        // Fixed threshold: 6dB
        new() { Reg02 = CONFIG_OOK,  Reg03 = 0x7d, Reg04 = 0x00, Reg05 = 0x00, Reg06 = 0x10, Reg19 = 0x88, Reg1A = 0x88, Reg37 = CONFIG_WHITE}, // OOK_Rb1Bw1
        new() { Reg02 = CONFIG_OOK,  Reg03 = 0x68, Reg04 = 0x2b, Reg05 = 0x00, Reg06 = 0x10, Reg19 = 0xf1, Reg1A = 0xf1, Reg37 = CONFIG_WHITE}, // OOK_Rb1_2Bw75
        new() { Reg02 = CONFIG_OOK,  Reg03 = 0x34, Reg04 = 0x15, Reg05 = 0x00, Reg06 = 0x10, Reg19 = 0xf5, Reg1A = 0xf5, Reg37 = CONFIG_WHITE}, // OOK_Rb2_4Bw4_8
        new() { Reg02 = CONFIG_OOK,  Reg03 = 0x1a, Reg04 = 0x0b, Reg05 = 0x00, Reg06 = 0x10, Reg19 = 0xf4, Reg1A = 0xf4, Reg37 = CONFIG_WHITE}, // OOK_Rb4_8Bw9_6
        new() { Reg02 = CONFIG_OOK,  Reg03 = 0x0d, Reg04 = 0x05, Reg05 = 0x00, Reg06 = 0x10, Reg19 = 0xf3, Reg1A = 0xf3, Reg37 = CONFIG_WHITE}, // OOK_Rb9_6Bw19_2
        new() { Reg02 = CONFIG_OOK,  Reg03 = 0x06, Reg04 = 0x83, Reg05 = 0x00, Reg06 = 0x10, Reg19 = 0xf2, Reg1A = 0xf2, Reg37 = CONFIG_WHITE}, // OOK_Rb19_2Bw38_4
        new() { Reg02 = CONFIG_OOK,  Reg03 = 0x03, Reg04 = 0xe8, Reg05 = 0x00, Reg06 = 0x10, Reg19 = 0xe2, Reg1A = 0xe2, Reg37 = CONFIG_WHITE}  // OOK_Rb32Bw64
    ];
}
