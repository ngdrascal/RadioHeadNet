// ReSharper disable RedundantUsingDirective

using System.Device;

namespace RadioHead
{
    internal static class RadioHead
    {
        public const byte BroadcastAddress = 0xFF;

#if NETSTANDARD2_1
        public static void Yield() => System.Threading.Tasks.Task.Yield();
#else
        public static void Yield() => DelayHelper.DelayMicroseconds(0, true);
#endif
    }
}
