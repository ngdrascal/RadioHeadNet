using System.ComponentModel.DataAnnotations;
using System.Device.Spi;

namespace RadioHeadIot.Configuration
{
    public class SpiConfiguration
    {
        public const string SectionName = "Spi";

        [Range(0, int.MaxValue, ErrorMessage = "BusId number must be >= 0.")]
        public int BusId { get; set; } = 0;

        [Range(0, int.MaxValue, ErrorMessage = "ClockFrequency number must be >= 0.")]
        public int ClockFrequency { get; set; } = 1000000;

        [Range(6, 10, ErrorMessage = "DataBitLength number must be between 6 and 10.")]
        public int DataBitLength { get; set; } = 8;

        public DataFlow DataFlow { get; set; } = DataFlow.MsbFirst;

        [Range(0, 1, ErrorMessage = "ChipSelectLineActiveState must be 0 for LOW and 1 for HIGH.")]
        public int ChipSelectLineActiveState { get; set; } = 0;

        public SpiMode Mode { get; set; } = SpiMode.Mode0;
    }
}
