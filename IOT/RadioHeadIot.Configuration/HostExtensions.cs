using System.Device.Gpio;
using System.Device.Gpio.Drivers;
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

namespace RadioHeadIot.Configuration;

public static class HostExtensions
{
    public static HostApplicationBuilder AddConfigurationSources(this HostApplicationBuilder builder)
    {
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddJsonFile("appSettings.json", false);

        var hostDevice = builder.Configuration["HostDevice"]?.ToLower();
        if (string.IsNullOrEmpty(hostDevice))
            throw new ApplicationException("HostDevice must be specified in appSettings.json.");

        if (!hostDevice.Equals(HostDevices.Ftx232H.ToString(), StringComparison.CurrentCultureIgnoreCase) &&
            !hostDevice.Equals(HostDevices.RPi.ToString(), StringComparison.InvariantCultureIgnoreCase))
        {
            throw new ApplicationException("HostDevice must be either 'FTX232H' or 'RPi'.");
        }

        builder.Configuration.AddJsonFile($"appSettings.{hostDevice}.json", false);

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
        // .Validate<IOptions<HostDeviceConfiguration>>((gpioConfig, hostConfig) =>
        // {
        //     if (hostConfig.Value.HostDevice == HostDevices.RPi)
        //         return gpioConfig.DeviceSelectPin is 7 or 8;

        //     return true;
        // }, "Invalid DeviceSelectPin configuration. Valid values for RPi are 8 for (CD0) and 7 for (CE1)");

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
        builder.Services.AddLogging(loggingBuilder => loggingBuilder.AddConsole());

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
#pragma warning disable SDGPIO0001
            var gpioController = new GpioController(PinNumberingScheme.Logical, new LibGpiodDriver(0));
#pragma warning restore SDGPIO0001
            return gpioController;
        });

        builder.Services.AddKeyedSingleton<GpioPin>("DeviceSelectPin", (provider, _) =>
        {
            var spiConfig = provider.GetRequiredService<IOptions<SpiConfiguration>>().Value;
            var gpioConfig = provider.GetRequiredService<IOptions<GpioConfiguration>>().Value;

            var gpioController = provider.GetRequiredService<GpioController>();
            GpioPin pin;
            if (spiConfig.ChipSelectLine == RPiChipSelectLines.Disabled)
                pin = gpioController.OpenPin(gpioConfig.DeviceSelectPin, PinMode.Output, PinValue.High);
            else
                pin = gpioController.OpenPin(27, PinMode.Output, PinValue.High);

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
                HostDevices.RPi => (int)spiConfig.ChipSelectLine,
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

            // var hostBoard = provider.GetRequiredKeyedService<Board>(hostConfig.HostDevice);
            // var spiDevice = hostBoard.CreateSpiDevice(spiSettings);
            var spiDevice = SpiDevice.Create(spiSettings);
            return spiDevice;
        });

        builder.Services.AddSingleton<Rf69>(provider =>
        {
            var spiDevice = provider.GetRequiredService<SpiDevice>();
            var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger<Rf69>();

            var deviceSelectPin = provider.GetRequiredKeyedService<GpioPin>("DeviceSelectPin");
            var radio = new Rf69(deviceSelectPin, spiDevice, logger);

            var radioConfig = provider.GetRequiredService<IOptions<RadioConfiguration>>().Value;
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

        builder.Services.AddSingleton<TApp>();

        return builder;
    }
}
