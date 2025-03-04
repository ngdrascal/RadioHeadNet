using System.ComponentModel.DataAnnotations;
using RadioHead;

namespace RadioHeadIot.Configuration;

public class RadioConfiguration
{
    public const string SectionName = "Radio";

    [Required]
    public float Frequency { get; set; } = 0.0f;

    [Required]
    public bool IsHighPowered { get; set; } = false;

    [Required]
    public sbyte PowerLevel { get; set; } = 0;

    [Required] public ChangeDetectionMode ChangeDetectionMode { get; set; } = ChangeDetectionMode.Polling;

    [MaxLength(16)]
    public byte[] EncryptionKey { get; set; } = [];
}
