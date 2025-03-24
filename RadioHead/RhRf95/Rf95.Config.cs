namespace RadioHead.RhRf95
{
    public partial class Rf95
    {
        /// \brief Defines register values for a set of modem configuration registers
        ///
        /// Defines register values for a set of modem configuration registers
        /// that can be passed to setModemRegisters() if none of the choices in
        /// ModemConfigChoice suit your need setModemRegisters() writes the
        /// register values from this structure to the appropriate registers
        /// to set the desired spreading factor, coding rate and bandwidth
        public struct ModemConfiguration
        {
            public byte Reg1D;   // Value for register REG_1D_MODEM_CONFIG1
            public byte Reg1E;   // Value for register REG_1E_MODEM_CONFIG2
            public byte Reg26;   // Value for register REG_26_MODEM_CONFIG3
        }


        /// Choices for setModemConfig() for a selected subset of common
        /// data rates. If you need another configuration,
        /// determine the necessary settings and call setModemRegisters() with your
        /// desired settings. It might be helpful to use the LoRa calculator mentioned in 
        /// http://www.semtech.com/images/datasheet/LoraDesignGuide_STD.pdf
        /// These are indexes into MODEM_CONFIG_TABLE. We strongly recommend you use these symbolic
        /// definitions and not their integer equivalents: its possible that new values will be
        /// introduced in later versions (though we will try to avoid it).
        /// Caution: if you are using slow packet rates and long packets with RHReliableDatagram or subclasses
        /// you may need to change the RHReliableDatagram timeout for reliable operations.
        /// Caution: for some slow rates nad with ReliableDatagrams you may need to increase the reply timeout 
        /// with manager.setTimeout() to
        /// deal with the long transmission times.
        /// Caution: SX1276 family errata suggests alternate settings for some LoRa registers when 500kHz bandwidth
        /// is in use. See the Semtech SX1276/77/78 Errata Note. These are not implemented by RH_RF95.
        public enum ModemConfigChoice : byte
        {
            Bw125Cr45Sf128 = 0,    // Bw = 125 kHz, Cr = 4/5, Sf = 128chips/symbol, CRC on. Default medium range
            Bw500Cr45Sf128,        // Bw = 500 kHz, Cr = 4/5, Sf = 128chips/symbol, CRC on. Fast+short range
            Bw3125Cr48Sf512,       // Bw = 31.25 kHz, Cr = 4/8, Sf = 512chips/symbol, CRC on. Slow+long range
            Bw125Cr48Sf4096,       // Bw = 125 kHz, Cr = 4/8, Sf = 4096chips/symbol, low data rate, CRC on. Slow+long range
            Bw125Cr45Sf2048,       // Bw = 125 kHz, Cr = 4/5, Sf = 2048chips/symbol, CRC on. Slow+long range
        }

        // These are indexed by the values of ModemConfigChoice
        // Stored in flash (program) memory to save SRAM
        internal readonly ModemConfiguration[] ModemConfigTable =
        {
            new ModemConfiguration
                { Reg1D = 0x72, Reg1E = 0x74, Reg26 = 0x04 }, // Bw125Cr45Sf128 (the chip default), AGC enabled
            new ModemConfiguration
                { Reg1D = 0x92, Reg1E = 0x74, Reg26 = 0x04 }, // Bw500Cr45Sf128, AGC enabled
            new ModemConfiguration
                { Reg1D = 0x48, Reg1E = 0x94, Reg26 = 0x04 }, // Bw31_25Cr48Sf512, AGC enabled
            new ModemConfiguration
                { Reg1D = 0x78, Reg1E = 0xc4, Reg26 = 0x0c }, // Bw125Cr48Sf4096, AGC enabled
            new ModemConfiguration
                { Reg1D = 0x72, Reg1E = 0xb4, Reg26 = 0x04 }, // Bw125Cr45Sf2048, AGC enabled
        };
    }
}
