namespace RadioHeadIot.Configuration;

public static class StringExtensions
{
    public static HostDevices AsHostDevice(this string value)
    {
        return value?.ToLower() switch
        {
            "ftx232h" => HostDevices.Ftx232H,
            "rpi" => HostDevices.RPi,
            _ => HostDevices.Unknown
        };
    }
}
