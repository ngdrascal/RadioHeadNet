using System.Device.Gpio;
using System.Device.Spi;
using Iot.Device.Board;
using Iot.Device.FtCommon;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RadioHead.RhRf69;
using RadioHeadIOT.Examples.Shared;

namespace RadioHeadIot.Examples.Shared;

public static class HostExtensions
{
    public static HostApplicationBuilder ConfigureConfigurations(this HostApplicationBuilder builder)
    {
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddJsonFile("appSettings.json", false);

        return builder;
    }

    public static HostApplicationBuilder AddConfigurationOptions(this HostApplicationBuilder builder)
    {
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddJsonFile("appSettings.json", false);

        builder
            .Services
            .Configure<GpioConfiguration>(
                builder.Configuration.GetSection(GpioConfiguration.SectionName));

        builder
            .Services
            .Configure<RadioConfiguration>(
                builder.Configuration.GetSection(RadioConfiguration.SectionName));

        return builder;
    }

    public static HostApplicationBuilder ConfigureDependencyInjection<TApp>(this HostApplicationBuilder builder)
       where TApp : class
    {
        var gpioConfig = builder
            .Configuration
            .GetSection(GpioConfiguration.SectionName)
            .Get<GpioConfiguration>();

        if (gpioConfig is null || !gpioConfig.IsValid())
            throw new ArgumentException("Invalid GPIO configuration.");

        builder.Services.AddLogging(loggingBuilder => loggingBuilder.AddConsole());

        builder.Services.AddKeyedSingleton<Board>(SupportedBoards.Ftx232H.ToString(), (_, _) =>
        {
            var allFtx232H = Ftx232HDevice.GetFtx232H();
            if (allFtx232H.Count == 0)
                throw new ApplicationException("No FT232 device found.");

            var hostBoard = allFtx232H[0];
            hostBoard.Reset();
            return hostBoard;
        });

        builder.Services.AddKeyedSingleton<Board>(SupportedBoards.RPi.ToString(), (_, _) =>
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

        builder.Services.AddSingleton<Rf69RadioResetter>(provider =>
        {
            var resetPin = provider.GetRequiredKeyedService<GpioPin>("ResetPin");
            return new Rf69RadioResetter(resetPin);
        });

        builder.Services.AddSingleton<TApp>();

        return builder;
    }

    private static bool IsValid(this GpioConfiguration configuration)
    {
        return !string.IsNullOrEmpty(configuration.HostDevice) &&
               configuration is { DeviceSelectPin: >= 0, ResetPin: >= 0, InterruptPin: >= 0 };
    }

    public static bool IsValid(this RadioConfiguration settings)
    {
        return settings is { Frequency: > 0, PowerLevel: >= 0 };
    }
}
