using Iot.Device.Board;
using Iot.Device.FtCommon;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RadioHead.RhRf69;
using System.Device.Gpio;
using System.Device.Spi;
using Microsoft.Extensions.Hosting;
using RadioHeadIot.Configuration;

namespace RadioHeadIot.TestDriver;

internal static class HostExtensions
{
    public static HostApplicationBuilder AddDefaultConfiguration(this HostApplicationBuilder builder)
    {
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Gpio:HostDevice"] = HostDevices.Ftx232H.ToString(),
            ["Gpio:DeviceSelectPin"] = "5",
            ["Gpio:ResetPin"] = "6",
            ["Gpio:InterruptPin"] = "7",
            ["Radio:Frequency"] = "915.0",
            ["Radio:PowerLevel"] = "20",
        });
        return builder;
    }

    public static HostApplicationBuilder ConfigureLogging(this HostApplicationBuilder builder)
    {
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();
        return builder;
    }

    public static HostApplicationBuilder ConfigureDependencyInjection(this HostApplicationBuilder builder)
    {
        var gpioConfig = new GpioConfiguration();
        builder.Configuration.Bind("Gpio", gpioConfig);
        if (!gpioConfig.IsValid())
            throw new ApplicationException("Invalid GPIO configuration.");

        var radioConfig = new RadioConfiguration();
        builder.Configuration.Bind("Radio", radioConfig);
        if (!radioConfig.IsValid())
            throw new ApplicationException("Invalid Radio configuration.");

        builder.Services.AddLogging(loggingBuilder => loggingBuilder.AddConsole());

        builder.Services.AddKeyedSingleton<Board>(HostDevices.Ftx232H.ToString(), (_, _) =>
        {
            var allFtx232H = Ftx232HDevice.GetFtx232H();
            if (allFtx232H.Count == 0)
                throw new ApplicationException("No FT232 device found.");

            var hostBoard = allFtx232H[0];
            hostBoard.Reset();
            return hostBoard;
        });

        builder.Services.AddKeyedSingleton<Board>(HostDevices.RPi.ToString(), (_, _) =>
            new RaspberryPiBoard());

        builder.Services.AddSingleton<GpioController>(provider =>
        {
            var hostBoard = provider.GetRequiredKeyedService<Board>(gpioConfig.HostDevice);
            var gpioController = hostBoard.CreateGpioController();
            return gpioController;
        });

        builder.Services.AddKeyedSingleton<GpioPin>("DeviceSelectPin", (provider, _) =>
        {
            var pinNumber = gpioConfig.DeviceSelectPin;
            var gpioController = provider.GetRequiredService<GpioController>();
            var pin = gpioController.OpenPin(pinNumber, PinMode.Output);
            return pin;
        });

        builder.Services.AddKeyedSingleton<GpioPin>("ResetPin", (provider, _) =>
        {
            var pinNumber = gpioConfig.ResetPin;
            var gpioController = provider.GetRequiredService<GpioController>();
            var pin = gpioController.OpenPin(pinNumber, PinMode.Output);
            return pin;
        });

        builder.Services.AddSingleton<SpiDevice>(provider =>
        {
            var spiSettings = new SpiConnectionSettings(0, 3)
            {
                ClockFrequency = 1_000_000,
                DataBitLength = 8,
                ChipSelectLineActiveState = PinValue.Low,
                Mode = SpiMode.Mode0
            };

            var hostBoard = provider.GetRequiredKeyedService<Board>(gpioConfig.HostDevice);
            var spiDevice = hostBoard.CreateSpiDevice(spiSettings);
            return spiDevice;
        });

        builder.Services.AddSingleton<Rf69>(provider =>
        {
            var deviceSelectPin = provider.GetRequiredKeyedService<GpioPin>("DeviceSelectPin");
            var spiDevice = provider.GetRequiredService<SpiDevice>();
            var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger<Rf69>();
            var radio = new Rf69(deviceSelectPin, spiDevice, logger);
            return radio;
        });

        builder.Services.AddSingleton<Application>(provider =>
        {
            var resetPin = provider.GetRequiredKeyedService<GpioPin>("ResetPin");
            var radio = provider.GetRequiredService<Rf69>();
            var frequency = radioConfig.Frequency;
            var power = radioConfig.PowerLevel;
            var app = new Application(resetPin, radio, frequency, power);
            return app;
        });

        return builder;
    }

    private static bool IsValid(this GpioConfiguration configuration)
    {
        return !string.IsNullOrEmpty(configuration.HostDevice) &&
               configuration is { DeviceSelectPin: >= 0, ResetPin: >= 0, InterruptPin: >= 0 };
    }

    private static bool IsValid(this RadioConfiguration settings)
    {
        return settings is { Frequency: > 0, PowerLevel: >= 0 };
    }
}
