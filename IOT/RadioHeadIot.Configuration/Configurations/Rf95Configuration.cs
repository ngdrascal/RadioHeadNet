using System.ComponentModel.DataAnnotations;
using RadioHead;

namespace RadioHeadIot.Configuration;

public class Rf95Configuration
{
    public const string SectionName = "Radio";

    [Required]
    public float Frequency { get; set; } = 0.0f;

    [Required]
    public bool IsHighPowered { get; set; } = false;

    [Required]
    public sbyte PowerLevel { get; set; } = 0;

    [Required]
    public ChangeDetectionMode ChangeDetectionMode { get; set; } = ChangeDetectionMode.Polling;
}