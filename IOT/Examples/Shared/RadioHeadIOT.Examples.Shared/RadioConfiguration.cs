namespace RadioHeadIot.Examples.Shared;

public class RadioConfiguration
{
    public const string SectionName = "Radio";

    public float Frequency { get; set; }
    public bool IsHighPowered { get; set; }
    public sbyte PowerLevel { get; set; }
}