using System.Text;

namespace RadioHeadIot.Configuration
{
    public static class ConfigurationDebugExtensions
    {
        public static string Dump(this GpioConfiguration config)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"DeviceSelectPin: {config.DeviceSelectPin}");
            sb.AppendLine($"ResetPin: {config.ResetPin}");
            sb.AppendLine($"InterruptPin: {config.InterruptPin}");
            return sb.ToString();
        }

        public static string Dump(this HostDeviceConfiguration config)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"HostDevice: {config.HostDevice}");
            return sb.ToString();
        }

        public static string Dump(this RadioConfiguration config)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Frequency: {config.Frequency}");
            sb.AppendLine($"IsHighPowered: {config.IsHighPowered}");
            sb.AppendLine($"PowerLevel: {config.PowerLevel}");
            sb.AppendLine($"ChangeDetectionMode: {config.ChangeDetectionMode}");

            var hexString = BitConverter.ToString(config.EncryptionKey).Replace("-", " ");
            sb.AppendLine($"EncryptionKey: {hexString}");
            return sb.ToString();
        }

        public static string Dump(this SpiConfiguration config)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"BusId: {config.BusId}");
            sb.AppendLine($"ClockFrequency: {config.ClockFrequency}");
            sb.AppendLine($"DataBitLength: {config.DataBitLength}");
            sb.AppendLine($"DataFlow: {config.DataFlow}");
            sb.AppendLine($"ChipSelectLineActiveState: {config.ChipSelectLineActiveState}");
            sb.AppendLine($"Mode: {config.Mode}");
            return sb.ToString();
        }
    }
}
