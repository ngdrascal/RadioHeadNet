using System.Device.Gpio;
using System.Device.Spi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using nanoFramework.Hardware.Esp32;
using nanoFramework.Logging.Debug;

namespace RadioHead.Examples.Rf69SharedNf
{
    public static class HostExtensions
    {
        private const int DeviceSelectPinNum = Gpio.IO06;
        private const int InterruptPinNum = Gpio.IO05;
        private const int ResetPinNum = Gpio.IO09;

        public static IHostBuilder AddServices<TApp>(this IHostBuilder builder) where TApp : ApplicationBase
        {
            builder.ConfigureServices(context =>
            {
                context.AddSingleton(typeof(ILoggerFactory), typeof(DebugLoggerFactory));

                context.AddSingleton(typeof(GpioController), typeof(GpioController));

                context.AddSingleton(typeof(SpiDevice), _ =>
                {
                    Configuration.SetPinFunction(Gpio.IO36, DeviceFunction.SPI2_CLOCK);
                    Configuration.SetPinFunction(Gpio.IO35, DeviceFunction.SPI2_MOSI);
                    Configuration.SetPinFunction(Gpio.IO37, DeviceFunction.SPI2_MISO);

                    var spiSettings = new SpiConnectionSettings(2)
                    {
                        ClockFrequency = 500_000,
                        DataBitLength = 8,
                        DataFlow = DataFlow.MsbFirst,
                        Mode = SpiMode.Mode0
                    };

                    var spiDevice = new SpiDevice(spiSettings);
                    return spiDevice;
                });

                context.AddSingleton(typeof(RhRf69.Rf69), provider =>
                {
                    var gpioController = (GpioController)provider.GetRequiredService(typeof(GpioController));
                    var deviceSelectPin = gpioController.OpenPin(DeviceSelectPinNum, PinMode.Output);
                    var spiDevice = (SpiDevice)provider.GetRequiredService(typeof(SpiDevice));
                    var loggerFactory = (ILoggerFactory)provider.GetRequiredService(typeof(ILoggerFactory));
                    var logger = loggerFactory.CreateLogger("Rf69");
                    var radio = new RhRf69.Rf69(deviceSelectPin, spiDevice, ChangeDetectionMode.Interrupt, logger);
                    var interruptPin = gpioController.OpenPin(InterruptPinNum, PinMode.InputPullUp);
                    interruptPin.ValueChanged += radio.HandleInterrupt;
                    return radio;
                });

                context.AddSingleton(typeof(TApp), provider =>
                {
                    var gpioController = (GpioController)provider.GetRequiredService(typeof(GpioController));
                    var resetPin = gpioController.OpenPin(ResetPinNum, PinMode.Output);
                    var radio = (RhRf69.Rf69)provider.GetRequiredService(typeof(RhRf69.Rf69));
                    var app = new ApplicationBase(resetPin, radio, 915.0f, 13);
                    return app as TApp;
                });
            });

            return builder;
        }
    }
}
