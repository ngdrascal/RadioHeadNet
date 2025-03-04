// ReSharper disable RedundantUsingDirective

using System.Device;

namespace RadioHead
{
    internal static class RadioHead
    {
        public const byte BroadcastAddress = 0xFF;

#if NET5_0_OR_GREATER
        public static void Yield() => Task.Yield();
#else
        public static void Yield() => DelayHelper.DelayMicroseconds(0, true);
#endif
    }
}
