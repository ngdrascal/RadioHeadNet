using System.ComponentModel.DataAnnotations;

namespace RadioHeadIot.Examples.Shared;

public class RadioConfiguration
{
    public const string SectionName = "Radio";

    [Required]
    public float Frequency { get; set; }

    [Required]
    public bool IsHighPowered { get; set; }

    [Required]
    public sbyte PowerLevel { get; set; }
}