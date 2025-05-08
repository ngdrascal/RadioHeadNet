using System.Device.Gpio;
using System.Device.Spi;
using Iot.Device.Board;
using Iot.Device.FtCommon;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RadioHead;
using RadioHead.RhRf69;
using RadioHead.RhRf95;

namespace RadioHeadIot.Configuration;

public static class HostExtensions
{
    public static HostApplicationBuilder AddConfigurationSources(this HostApplicationBuilder builder)
    {
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddJsonFile("appsettings.json", false);

        var hostDevice = builder.Configuration["HostDevice"]?.ToLower();
        if (string.IsNullOrEmpty(hostDevice))
            throw new ApplicationException("HostDevice must be specified in appsettings.json.");

        if (!hostDevice.Equals(nameof(HostDevices.Ftx232H), StringComparison.CurrentCultureIgnoreCase) &&
            !hostDevice.Equals(nameof(HostDevices.RPi), StringComparison.InvariantCultureIgnoreCase))
        {
            throw new ApplicationException("HostDevice must be either 'FTX232H' or 'RPi'.");
        }

        builder.Configuration.AddJsonFile($"appsettings.{hostDevice}.json", false);

        return builder;
    }

    public static HostApplicationBuilder AddConfigurationOptions(this HostApplicationBuilder builder)
    {
        builder.Services
            .AddOptions<HostDeviceConfiguration>()
            .Bind(builder.Configuration)
            .ValidateDataAnnotations();

        builder.Services
            .AddOptions<SpiConfiguration>()
            .Bind(builder.Configuration.GetSection(SpiConfiguration.SectionName))
            .ValidateDataAnnotations();

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

    public static HostApplicationBuilder AddIotServices(this HostApplicationBuilder builder)
    {
        builder.Services.AddKeyedSingleton<Board>(HostDevices.Ftx232H, (_, _) =>
        {
            var allFtx232H = Ftx232HDevice.GetFtx232H();
            if (allFtx232H.Count == 0)
                throw new ApplicationException("No FT232 device found.");

            var fxt232H = allFtx232H[0];
            fxt232H.Reset();
            return fxt232H;
        });

        builder.Services.AddKeyedSingleton<Board>(HostDevices.RPi, (_, _) =>
            new RaspberryPiBoard());

        builder.Services.AddSingleton<GpioController>(provider =>
        {
            var deviceConfig = provider.GetRequiredService<IOptions<HostDeviceConfiguration>>().Value;

            GpioController controller;
            if (deviceConfig.HostDevice == HostDevices.RPi)
            {
                controller = new GpioController(PinNumberingScheme.Logical);
            }
            else
            {
                var board = provider.GetRequiredKeyedService<Board>(HostDevices.Ftx232H);
                controller = board.CreateGpioController();
            }

            return controller;
        });

        builder.Services.AddKeyedSingleton<GpioPin>("DeviceSelectPin", (provider, _) =>
        {
            var spiConfig = provider.GetRequiredService<IOptions<SpiConfiguration>>().Value;
            var gpioConfig = provider.GetRequiredService<IOptions<GpioConfiguration>>().Value;

            var gpioController = provider.GetRequiredService<GpioController>();
            var deviceSelectPin = spiConfig.ChipSelectLine == RPiChipSelectLines.Disabled ? 
                gpioConfig.DeviceSelectPin : 27;

            var pin = gpioController.OpenPin(deviceSelectPin, PinMode.Output, PinValue.High);

            return pin;
        });

        builder.Services.AddKeyedSingleton<GpioPin>("ResetPin", (provider, _) =>
        {
            var gpioConfig = provider.GetRequiredService<IOptions<GpioConfiguration>>().Value;
            var gpioController = provider.GetRequiredService<GpioController>();
            var pin = gpioController.OpenPin(gpioConfig.ResetPin, PinMode.Output, PinValue.Low);
            return pin;
        });

        builder.Services.AddKeyedSingleton<GpioPin>("InterruptPin", (provider, _) =>
        {
            var gpioConfig = provider.GetRequiredService<IOptions<GpioConfiguration>>().Value;
            var gpioController = provider.GetRequiredService<GpioController>();
            var pin = gpioController.OpenPin(gpioConfig.InterruptPin, PinMode.Input);
            return pin;
        });

        builder.Services.AddSingleton<SpiDevice>(provider =>
        {
            var hostConfig = provider.GetRequiredService<IOptions<HostDeviceConfiguration>>().Value;
            var spiConfig = provider.GetRequiredService<IOptions<SpiConfiguration>>().Value;

            var chipSelectLine = hostConfig.HostDevice switch
            {
                HostDevices.Ftx232H => 3,
                HostDevices.RPi => spiConfig.ChipSelectLine == RPiChipSelectLines.Disabled ?
                     (int)RPiChipSelectLines.Ce0 : (int)spiConfig.ChipSelectLine,
                _ => -1

            };

            var spiSettings = new SpiConnectionSettings(spiConfig.BusId)
            {
                ClockFrequency = spiConfig.ClockFrequency,
                DataBitLength = spiConfig.DataBitLength,
                DataFlow = spiConfig.DataFlow,
                ChipSelectLine = chipSelectLine,
                ChipSelectLineActiveState = PinValue.Low,
                Mode = spiConfig.Mode
            };

            SpiDevice spiDevice;
            if (hostConfig.HostDevice == HostDevices.RPi)
            {
                spiDevice = SpiDevice.Create(spiSettings);
            }
            else
            {
                var board = provider.GetRequiredKeyedService<Board>(HostDevices.Ftx232H);
                spiDevice = board.CreateSpiDevice(spiSettings);
            }

            return spiDevice;
        });

        return builder;
    }

    public static HostApplicationBuilder AddLogging(this HostApplicationBuilder builder)
    {
        builder.Services.AddLogging(loggingBuilder =>
        {
            loggingBuilder.ClearProviders();
            loggingBuilder.AddConsole();
        });

        return builder;
    }

    public static HostApplicationBuilder AddRf69Services(this HostApplicationBuilder builder)
    {
        builder.Services.AddSingleton<Rf69>(provider =>
        {
            var radioConfig = provider.GetRequiredService<IOptions<RadioConfiguration>>().Value;

            var spiDevice = provider.GetRequiredService<SpiDevice>();
            var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger<Rf69>();

            var deviceSelectPin = provider.GetRequiredKeyedService<GpioPin>("DeviceSelectPin");
            var radio = new Rf69(deviceSelectPin, spiDevice, radioConfig.ChangeDetectionMode, logger);

            if (radioConfig.ChangeDetectionMode == ChangeDetectionMode.Polling)
                return radio;

            var interruptPin = provider.GetRequiredKeyedService<GpioPin>("InterruptPin");
            interruptPin.ValueChanged += radio.HandleInterrupt;
            return radio;
        });

        builder.Services.AddSingleton<Rf69RadioResetter>(provider =>
        {
            var resetPin = provider.GetRequiredKeyedService<GpioPin>("ResetPin");
            return new Rf69RadioResetter(resetPin);
        });

        return builder;
    }

    public static HostApplicationBuilder AddRf95Services(this HostApplicationBuilder builder)
    {
        builder.Services.AddSingleton<Rf95>(provider =>
        {
            var radioConfig = provider.GetRequiredService<IOptions<RadioConfiguration>>().Value;

            var spiDevice = provider.GetRequiredService<SpiDevice>();
            var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger<Rf95>();

            var deviceSelectPin = provider.GetRequiredKeyedService<GpioPin>("DeviceSelectPin");
            var radio = new Rf95(deviceSelectPin, spiDevice, radioConfig.ChangeDetectionMode, logger);

            if (radioConfig.ChangeDetectionMode == ChangeDetectionMode.Polling)
                return radio;

            var interruptPin = provider.GetRequiredKeyedService<GpioPin>("InterruptPin");
            interruptPin.ValueChanged += radio.HandleInterrupt;
            return radio;
        });

        builder.Services.AddSingleton<Rf95RadioResetter>(provider =>
        {
            var resetPin = provider.GetRequiredKeyedService<GpioPin>("ResetPin");
            resetPin.Write(PinValue.High);
            return new Rf95RadioResetter(resetPin);
        });

        return builder;
    }
}
