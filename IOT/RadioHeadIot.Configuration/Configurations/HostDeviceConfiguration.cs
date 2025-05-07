using System.ComponentModel.DataAnnotations;

namespace RadioHeadIot.Configuration;

public class HostDeviceConfiguration
{
    [Required]
    [RegularExpression("(?i)^(FTX232H|RPi)",
    ErrorMessage = "Supported boards are FTX232H and Raspberry Pi.")]
    public HostDevices HostDevice { get; set; } = HostDevices.Unknown;
}