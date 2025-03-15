// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Local
// ReSharper disable UnusedMember.Global
// ReSharper disable IdentifierTypo
// ReSharper disable MemberCanBePrivate.Global

namespace RadioHead.RhRf95
{
    public partial class Rf95
    {
        // Max number of octets the LORA Rx/Tx FIFO can hold
        private const byte FIFO_SIZE = 255;

        // This is the maximum number of bytes that can be carried by the LORA.
        // We use some for headers, keeping fewer for RadioHead messages
        public const byte MAX_PAYLOAD_LEN = FIFO_SIZE;

        // The length of the headers we add.
        // The headers are inside the LORA's payload
        public const byte HEADER_LEN = 4;

        // This is the maximum message length that can be supported by this driver. 
        // Can be pre-defined to a smaller size (to save SRAM) prior to including this header
        // Here we allow for 1 byte message length, 4 bytes headers, user data and 2 bytes of FCS
        private const byte MAX_MESSAGE_LEN = MAX_PAYLOAD_LEN - HEADER_LEN;

        // The crystal oscillator frequency of the module
        private const float FXOSC = 32000000.0f;

        // The Frequency Synthesizer step = RH_RF95_FXOSC / 2^^19
        private const float FSTEP = FXOSC / 524288;


        // Register names (LoRa Mode, from table 85)
        internal const byte REG_00_FIFO = 0x00;
        internal const byte REG_01_OP_MODE = 0x01;
        internal const byte REG_02_RESERVED = 0x02;
        internal const byte REG_03_RESERVED = 0x03;
        internal const byte REG_04_RESERVED = 0x04;
        internal const byte REG_05_RESERVED = 0x05;
        internal const byte REG_06_FRF_MSB = 0x06;
        internal const byte REG_07_FRF_MID = 0x07;
        internal const byte REG_08_FRF_LSB = 0x08;
        internal const byte REG_09_PA_CONFIG = 0x09;
        internal const byte REG_0A_PA_RAMP = 0x0a;
        internal const byte REG_0B_OCP = 0x0b;
        internal const byte REG_0C_LNA = 0x0c;
        internal const byte REG_0D_FIFO_ADDR_PTR = 0x0d;
        internal const byte REG_0E_FIFO_TX_BASE_ADDR = 0x0e;
        internal const byte REG_0F_FIFO_RX_BASE_ADDR = 0x0f;
        internal const byte REG_10_FIFO_RX_CURRENT_ADDR = 0x10;
        internal const byte REG_11_IRQ_FLAGS_MASK = 0x11;
        internal const byte REG_12_IRQ_FLAGS = 0x12;
        internal const byte REG_13_RX_NB_BYTES = 0x13;
        internal const byte REG_14_RX_HEADER_CNT_VALUE_MSB = 0x14;
        internal const byte REG_15_RX_HEADER_CNT_VALUE_LSB = 0x15;
        internal const byte REG_16_RX_PACKET_CNT_VALUE_MSB = 0x16;
        internal const byte REG_17_RX_PACKET_CNT_VALUE_LSB = 0x17;
        internal const byte REG_18_MODEM_STAT = 0x18;
        internal const byte REG_19_PKT_SNR_VALUE = 0x19;
        internal const byte REG_1A_PKT_RSSI_VALUE = 0x1a;
        internal const byte REG_1B_RSSI_VALUE = 0x1b;
        internal const byte REG_1C_HOP_CHANNEL = 0x1c;
        internal const byte REG_1D_MODEM_CONFIG1 = 0x1d;
        internal const byte REG_1E_MODEM_CONFIG2 = 0x1e;
        internal const byte REG_1F_SYMB_TIMEOUT_LSB = 0x1f;
        internal const byte REG_20_PREAMBLE_MSB = 0x20;
        internal const byte REG_21_PREAMBLE_LSB = 0x21;
        internal const byte REG_22_PAYLOAD_LENGTH = 0x22;
        internal const byte REG_23_MAX_PAYLOAD_LENGTH = 0x23;
        internal const byte REG_24_HOP_PERIOD = 0x24;
        internal const byte REG_25_FIFO_RX_BYTE_ADDR = 0x25;
        internal const byte REG_26_MODEM_CONFIG3 = 0x26;
        internal const byte REG_27_PPM_CORRECTION = 0x27;
        internal const byte REG_28_FEI_MSB = 0x28;
        internal const byte REG_29_FEI_MID = 0x29;
        internal const byte REG_2A_FEI_LSB = 0x2a;
        internal const byte REG_2C_RSSI_WIDEBAND = 0x2c;
        internal const byte REG_31_DETECT_OPTIMIZE = 0x31;
        internal const byte REG_33_INVERT_IQ = 0x33;
        internal const byte REG_37_DETECTION_THRESHOLD = 0x37;
        internal const byte REG_39_SYNC_WORD = 0x39;
        internal const byte REG_40_DIO_MAPPING1 = 0x40;
        internal const byte REG_41_DIO_MAPPING2 = 0x41;
        internal const byte REG_42_VERSION = 0x42;
        internal const byte REG_4B_TCXO = 0x4b;
        internal const byte REG_4D_PA_DAC = 0x4d;
        internal const byte REG_5B_FORMER_TEMP = 0x5b;
        internal const byte REG_61_AGC_REF = 0x61;
        internal const byte REG_62_AGC_THRESH1 = 0x62;
        internal const byte REG_63_AGC_THRESH2 = 0x63;
        internal const byte REG_64_AGC_THRESH3 = 0x64;

        // REG_01_OP_MODE
        internal const byte LONG_RANGE_MODE = 0x80;
        internal const byte ACCESS_SHARED_REG = 0x40;
        internal const byte LOW_FREQUENCY_MODE = 0x08;
        internal const byte MODE = 0x07;
        internal const byte MODE_SLEEP = 0x00;
        internal const byte MODE_STDBY = 0x01;
        internal const byte MODE_FSTX = 0x02;
        internal const byte MODE_TX = 0x03;
        internal const byte MODE_FSRX = 0x04;
        internal const byte MODE_RXCONTINUOUS = 0x05;
        internal const byte MODE_RXSINGLE = 0x06;
        internal const byte MODE_CAD = 0x07;

        // REG_09_PA_CONFIG
        internal const byte PA_SELECT = 0x80;
        internal const byte MAX_POWER = 0x70;
        internal const byte OUTPUT_POWER = 0x0f;

        // REG_0A_PA_RAMP
        internal const byte LOW_PN_TX_PLL_OFF = 0x10;
        internal const byte PA_RAMP = 0x0f;
        internal const byte PA_RAMP_3_4MS = 0x00;
        internal const byte PA_RAMP_2MS = 0x01;
        internal const byte PA_RAMP_1MS = 0x02;
        internal const byte PA_RAMP_500US = 0x03;
        internal const byte PA_RAMP_250US = 0x04;
        internal const byte PA_RAMP_125US = 0x05;
        internal const byte PA_RAMP_100US = 0x06;
        internal const byte PA_RAMP_62US = 0x07;
        internal const byte PA_RAMP_50US = 0x08;
        internal const byte PA_RAMP_40US = 0x09;
        internal const byte PA_RAMP_31US = 0x0a;
        internal const byte PA_RAMP_25US = 0x0b;
        internal const byte PA_RAMP_20US = 0x0c;
        internal const byte PA_RAMP_15US = 0x0d;
        internal const byte PA_RAMP_12US = 0x0e;
        internal const byte PA_RAMP_10US = 0x0f;

        // REG_0B_OCP
        internal const byte OCP_ON = 0x20;
        internal const byte OCP_TRIM = 0x1f;

        // REG_0C_LNA
        internal const byte LNA_GAIN = 0xe0;
        internal const byte LNA_GAIN_G1 = 0x20;
        internal const byte LNA_GAIN_G2 = 0x40;
        internal const byte LNA_GAIN_G3 = 0x60;
        internal const byte LNA_GAIN_G4 = 0x80;
        internal const byte LNA_GAIN_G5 = 0xa0;
        internal const byte LNA_GAIN_G6 = 0xc0;
        internal const byte LNA_BOOST_LF = 0x18;
        internal const byte LNA_BOOST_LF_DEFAULT = 0x00;
        internal const byte LNA_BOOST_HF = 0x03;
        internal const byte LNA_BOOST_HF_DEFAULT = 0x00;
        internal const byte LNA_BOOST_HF_150PC = 0x03;

        // REG_11_IRQ_FLAGS_MASK
        internal const byte RX_TIMEOUT_MASK = 0x80;
        internal const byte RX_DONE_MASK = 0x40;
        internal const byte PAYLOAD_CRC_ERROR_MASK = 0x20;
        internal const byte VALID_HEADER_MASK = 0x10;
        internal const byte TX_DONE_MASK = 0x08;
        internal const byte CAD_DONE_MASK = 0x04;
        internal const byte FHSS_CHANGE_CHANNEL_MASK = 0x02;
        internal const byte CAD_DETECTED_MASK = 0x01;

        // REG_12_IRQ_FLAGS
        internal const byte RX_TIMEOUT = 0x80;
        internal const byte RX_DONE = 0x40;
        internal const byte PAYLOAD_CRC_ERROR = 0x20;
        internal const byte VALID_HEADER = 0x10;
        internal const byte TX_DONE = 0x08;
        internal const byte CAD_DONE = 0x04;
        internal const byte FHSS_CHANGE_CHANNEL = 0x02;
        internal const byte CAD_DETECTED = 0x01;

        // REG_18_MODEM_STAT
        internal const byte RX_CODING_RATE = 0xe0;
        internal const byte MODEM_STATUS_CLEAR = 0x10;
        internal const byte MODEM_STATUS_HEADER_INFO_VALID = 0x08;
        internal const byte MODEM_STATUS_RX_ONGOING = 0x04;
        internal const byte MODEM_STATUS_SIGNAL_SYNCHRONIZED = 0x02;
        internal const byte MODEM_STATUS_SIGNAL_DETECTED = 0x01;

        // REG_1C_HOP_CHANNEL
        internal const byte PLL_TIMEOUT = 0x80;
        internal const byte RX_PAYLOAD_CRC_IS_ON = 0x40;
        internal const byte FHSS_PRESENT_CHANNEL = 0x3f;

        // REG_1D_MODEM_CONFIG1
        internal const byte BW = 0xf0;

        internal const byte BW_7_8KHZ = 0x00;
        internal const byte BW_10_4KHZ = 0x10;
        internal const byte BW_15_6KHZ = 0x20;
        internal const byte BW_20_8KHZ = 0x30;
        internal const byte BW_31_25KHZ = 0x40;
        internal const byte BW_41_7KHZ = 0x50;
        internal const byte BW_62_5KHZ = 0x60;
        internal const byte BW_125KHZ = 0x70;
        internal const byte BW_250KHZ = 0x80;
        internal const byte BW_500KHZ = 0x90;
        internal const byte CODING_RATE = 0x0e;
        internal const byte CODING_RATE_4_5 = 0x02;
        internal const byte CODING_RATE_4_6 = 0x04;
        internal const byte CODING_RATE_4_7 = 0x06;
        internal const byte CODING_RATE_4_8 = 0x08;
        internal const byte IMPLICIT_HEADER_MODE_ON = 0x01;

        // REG_1E_MODEM_CONFIG2
        internal const byte SPREADING_FACTOR = 0xf0;
        internal const byte SPREADING_FACTOR_64CPS = 0x60;
        internal const byte SPREADING_FACTOR_128CPS = 0x70;
        internal const byte SPREADING_FACTOR_256CPS = 0x80;
        internal const byte SPREADING_FACTOR_512CPS = 0x90;
        internal const byte SPREADING_FACTOR_1024CPS = 0xa0;
        internal const byte SPREADING_FACTOR_2048CPS = 0xb0;
        internal const byte SPREADING_FACTOR_4096CPS = 0xc0;
        internal const byte TX_CONTINUOUS_MODE = 0x08;

        internal const byte PAYLOAD_CRC_ON = 0x04;
        internal const byte SYM_TIMEOUT_MSB = 0x03;

        // REG_26_MODEM_CONFIG3
        internal const byte MOBILE_NODE = 0x08; // HopeRF term
        internal const byte LOW_DATA_RATE_OPTIMIZE = 0x08; // Semtechs term
        internal const byte AGC_AUTO_ON = 0x04;

        // REG_4B_TCXO
        internal const byte TCXO_TCXO_INPUT_ON = 0x10;

        // REG_4D_PA_DAC
        internal const byte PA_DAC_DISABLE = 0x04;
        internal const byte PA_DAC_ENABLE = 0x07;
    }
}
