using Iot.Device.FtCommon;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Device.Gpio;
using System.Device.Spi;
using Iot.Device.Board;
using Iot.Device.Common;
using Microsoft.Extensions.Configuration;
using RadioHeadNet;

namespace RF69TestDriver;

internal static class Program
{
    private enum SupportedBoards { Ftx232H, RPi }

    private static SupportedBoards _targetBoard;

    static void Main(string[] args)
    {
        _targetBoard = SupportedBoards.Ftx232H;

        var builder = Host.CreateApplicationBuilder(args);
        BuildDefaultConfiguration(builder.Configuration);
        ConfigureServices(builder.Services, builder.Configuration);
        var host = builder.Build();

        host.Services.GetRequiredService<Application>().Run();
        // host.Run();
    }

    private static void BuildDefaultConfiguration(ConfigurationManager configMgr)
    {
        configMgr.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["HostDevice"] = "Ftx232H",
            ["DeviceSelectPin"] = "5",
            ["ResetPin"] = "6",
            ["Frequency"] = "915.0",
            ["Power"] = "20",
        });
    }

    private static void ConfigureServices(IServiceCollection services, IConfiguration config)
    {
        services.AddKeyedSingleton<Board>(SupportedBoards.Ftx232H, (_, _) =>
        {
            var allFtx232H = Ftx232HDevice.GetFtx232H();
            if (allFtx232H.Count == 0)
                throw new ApplicationException("No FT232 device found.");

            var hostBoard = allFtx232H[0];
            hostBoard.Reset();
            return hostBoard;
        });

        services.AddKeyedSingleton<Board>(SupportedBoards.RPi, (_, _) => new RaspberryPiBoard());

        services.AddSingleton<GpioController>(provider =>
        {
            var hostBoard = provider.GetRequiredKeyedService<Board>(_targetBoard);
            var gpioController = hostBoard.CreateGpioController();
            return gpioController;
        });

        services.AddKeyedSingleton<GpioPin>("DeviceSelectPin", (provider, _) =>
        {
            var pinNumber = config.GetValue<int>("DeviceSelectPin");
            var gpioController = provider.GetRequiredService<GpioController>();
            var pin = gpioController.OpenPin(pinNumber, PinMode.Output);
            return pin;
        });

        services.AddKeyedSingleton<GpioPin>("ResetPin", (provider, _) =>
        {
            var pinNumber = config.GetValue<int>("ResetPin");
            var gpioController = provider.GetRequiredService<GpioController>();
            var pin = gpioController.OpenPin(pinNumber, PinMode.Output);
            return pin;
        });

        services.AddSingleton<SpiDevice>(provider =>
        {
            var spiSettings = new SpiConnectionSettings(0, 3)
            {
                ClockFrequency = 1_000_000,
                DataBitLength = 8,
                ChipSelectLineActiveState = PinValue.Low,
                Mode = SpiMode.Mode0
            };

            var hostBoard = provider.GetRequiredKeyedService<Board>(_targetBoard);
            var spiDevice = hostBoard.CreateSpiDevice(spiSettings);
            return spiDevice;
        });

        services.AddSingleton<RhRf69>(provider =>
        {
            var deviceSelectPin = provider.GetRequiredKeyedService<GpioPin>("DeviceSelectPin");
            var spiDevice = provider.GetRequiredService<SpiDevice>();
            var radio = new RhRf69(deviceSelectPin, spiDevice, new SimpleConsoleLoggerFactory());
            return radio;
        });

        services.AddSingleton<Application>(provider =>
        {
            var resetPin = provider.GetRequiredKeyedService<GpioPin>("ResetPin");
            var radio = provider.GetRequiredService<RhRf69>();
            var frequency = config.GetValue<float>("Frequency");
            var power = config.GetValue<sbyte>("Power");
            var app = new Application(resetPin, radio, frequency, power);
            return app;
        });
    }
}