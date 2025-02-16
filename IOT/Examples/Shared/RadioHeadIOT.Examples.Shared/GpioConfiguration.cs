namespace RadioHeadIot.Examples.Shared;

public class GpioConfiguration
{
    public const string SectionName = "Gpio";

    public string HostDevice { get; set; } = string.Empty;
    public int DeviceSelectPin { get; set; }
    public int ResetPin { get; set; }
    public int InterruptPin { get; set; }
}