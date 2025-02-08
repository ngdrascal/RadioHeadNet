using System.Device.Gpio;
using System.Device.Spi;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using nanoFramework.Hardware.Esp32;
using nanoFramework.Logging.Debug;
using RadioHead.RhRf69;

namespace RadioHeadNF.TestDriver
{
    public class Program
    {
        private const int DeviceSelectPinNum = Gpio.IO06;
        private const int InterruptPinNum = Gpio.IO05;
        private const int ResetPinNum = Gpio.IO09;

        public static void Main()
        {
            Debug.WriteLine("Starting RF69 test driver program ...");

            var services = new ServiceCollection();
            ConfigureServices(services);
            var serviceProvider = services.BuildServiceProvider();

            var app = (Application)serviceProvider.GetRequiredService(typeof(Application));
            app.Run();

            Debug.WriteLine("... stopping RF69 test driver program.");
        }

        private static void ConfigureServices(ServiceCollection services)
        {
            services.AddSingleton(typeof(ILoggerFactory), typeof(DebugLoggerFactory));

            services.AddSingleton(typeof(GpioController), typeof(GpioController));

            services.AddSingleton(typeof(SpiDevice), _ =>
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

            services.AddSingleton(typeof(Rf69), provider =>
            {
                var gpioController = (GpioController)provider.GetRequiredService(typeof(GpioController));
                var deviceSelectPin = gpioController.OpenPin(DeviceSelectPinNum, PinMode.Output);
                var spiDevice = (SpiDevice)provider.GetRequiredService(typeof(SpiDevice));
                var loggerFactory = (ILoggerFactory)provider.GetRequiredService(typeof(ILoggerFactory));
                var radio = new Rf69(deviceSelectPin, spiDevice, loggerFactory);
                var interruptPin = gpioController.OpenPin(InterruptPinNum, PinMode.InputPullUp);
                interruptPin.ValueChanged += radio.HandleInterrupt;
                return radio;
            });

            services.AddSingleton(typeof(Application), provider =>
            {
                var gpioController = (GpioController)provider.GetRequiredService(typeof(GpioController));
                var resetPin = gpioController.OpenPin(ResetPinNum, PinMode.Output);
                var radio = (Rf69)provider.GetRequiredService(typeof(Rf69));
                var app = new Application(resetPin, radio, 915.0f, 13);
                return app;
            });
        }
    }
}
