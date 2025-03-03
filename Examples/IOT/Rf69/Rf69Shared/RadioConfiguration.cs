using System.ComponentModel.DataAnnotations;

namespace RadioHeadIot.Examples.Rf69Shared;

public class RadioConfiguration
{
    public const string SectionName = "Radio";

    [Required]
    public float Frequency { get; set; } = 0.0f;

    [Required]
    public bool IsHighPowered { get; set; } = false;

    [Required]
    public sbyte PowerLevel { get; set; } = 0;

    [Required]
    [RegularExpression("(?i)^(interrupt|polling)",
        ErrorMessage = "Supported modes are 'interrupt' and 'polling'.")]
    public string SentDetectionMode { get; set; } = "poll";

    [MaxLength(16)]
    public byte[] EncryptionKey { get; set; } = Array.Empty<byte>();
}
