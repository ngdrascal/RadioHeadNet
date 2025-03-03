using System.Device.Gpio;
using System.Device.Spi;
using Iot.Device.Board;
using Iot.Device.FtCommon;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RadioHead.RhRf69;

namespace RadioHeadIot.Examples.Rf69Shared;

public static class HostExtensions
{
    public static HostApplicationBuilder AddConfigurationSources(this HostApplicationBuilder builder)
    {
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddJsonFile("appSettings.json", false);

        var hostDevice = builder.Configuration["HostDevice"]?.ToLower();
        if (string.IsNullOrEmpty(hostDevice))
            throw new ApplicationException("HostDevice must be specified in appSettings.json.");

        if (!hostDevice.Equals(SupportedBoards.Ftx232H.ToString(), StringComparison.CurrentCultureIgnoreCase) &&
            !hostDevice.Equals(SupportedBoards.RPi.ToString(), StringComparison.InvariantCultureIgnoreCase))
        {
            throw new ApplicationException("HostDevice must be either 'FTX232H' or 'RPi'.");
        }

        builder.Configuration.AddJsonFile($"appsettings.{hostDevice}.json", false);

        return builder;
    }

    public static HostApplicationBuilder AddConfigurationOptions(this HostApplicationBuilder builder)
    {
        builder.Services
            .AddOptions<GpioConfiguration>()
            .Bind(builder.Configuration.GetSection(GpioConfiguration.SectionName))
            .ValidateDataAnnotations();

        builder.Services
            .AddOptions<RadioConfiguration>()
            .Bind(builder.Configuration.GetSection(RadioConfiguration.SectionName))
            .ValidateDataAnnotations()
            .Validate(options =>
            {
                if (options.IsHighPowered)
                    return options.PowerLevel is >= -2 and <= 20;
                else
                    return options.PowerLevel is >= -18 and <= 13;
            }, "PowerLevel is out of range. High-Power: -2 to 20.  Low-Power: -18 to 13.");

        return builder;
    }

    public static HostApplicationBuilder AddServices<TApp>(this HostApplicationBuilder builder)
       where TApp : class
    {
        var gpioConfig = new GpioConfiguration();
        builder.Configuration
            .GetSection(GpioConfiguration.SectionName)
            .Bind(gpioConfig);

        builder.Services.AddLogging(loggingBuilder => loggingBuilder.AddConsole());

        builder.Services.AddKeyedSingleton<Board>(SupportedBoards.Ftx232H.ToString().ToLower(), (_, _) =>
        {
            var allFtx232H = Ftx232HDevice.GetFtx232H();
            if (allFtx232H.Count == 0)
                throw new ApplicationException("No FT232 device found.");

            var hostBoard = allFtx232H[0];
            hostBoard.Reset();
            return hostBoard;
        });

        builder.Services.AddKeyedSingleton<Board>(SupportedBoards.RPi.ToString().ToLower(), (_, _) =>
            new RaspberryPiBoard());

        builder.Services.AddSingleton<GpioController>(provider =>
        {
            var hostDevice = builder.Configuration["HostDevice"]?.ToLower();
            var hostBoard = provider.GetRequiredKeyedService<Board>(hostDevice);
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

            var hostDevice = builder.Configuration["HostDevice"]?.ToLower();
            var hostBoard = provider.GetRequiredKeyedService<Board>(hostDevice);
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
}
