namespace RadioHeadIot.TestDriver
{
    internal class GpioConfiguration
    {
        public string HostDevice { get; set; } = string.Empty;
        public int DeviceSelectPin { get; set; }
        public int ResetPin { get; set; }
        public int InterruptPin { get; set; }
    }
}
