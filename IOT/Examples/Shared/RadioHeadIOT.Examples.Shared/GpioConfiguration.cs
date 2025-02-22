﻿using System.ComponentModel.DataAnnotations;

namespace RadioHeadIot.Examples.Shared;

public class GpioConfiguration
{
    public const string SectionName = "Gpio";

    [Required]
    [RegularExpression("^FTX232H|RPI$",
        ErrorMessage = "Supported boards are FTX232H and Raspberry Pi.")]
    public string HostDevice { get; set; } = string.Empty;

    [Required]
    [Range(0, 27, ErrorMessage = "Pin number must be between 0 and 27.")]
    public int DeviceSelectPin { get; set; } = -1;

    [Required]
    [Range(0, 27, ErrorMessage = "Pin number must be between 0 and 27.")]
    public int ResetPin { get; set; } = -1;

    [Required]
    [Range(-1, 27, ErrorMessage = "Pin number must be between -1 and 27.")]
    public int InterruptPin { get; set; } = -1;
}
