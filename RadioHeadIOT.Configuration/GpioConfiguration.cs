using System.ComponentModel.DataAnnotations;

namespace RadioHeadIot.Configuration;

public class GpioConfiguration
{
    public const string SectionName = "Gpio";
    
    [Required]
    [Range(0, 27, ErrorMessage = "Pin number must be between 0 and 27.")]
    public int DeviceSelectPin { get; set; } = -1;

    [Required]
    [Range(0, 27, ErrorMessage = "Pin number must be between 0 and 27.")]
    public int ResetPin { get; set; } = -1;

    [Required]
    [Range(-1, 27, ErrorMessage = "Pin number must be between 0 and 27.")]
    public int InterruptPin { get; set; } = -1;
}
