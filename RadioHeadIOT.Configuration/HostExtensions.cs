﻿using System.Device.Gpio;
using System.Device.Spi;
using Iot.Device.Board;
using Iot.Device.FtCommon;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
            .ValidateDataAnnotations()
            .Validate<IOptions<HostDeviceConfiguration>>((gpioConfig, hostConfig) =>
            {
                if (hostConfig.Value.HostDevice == HostDevices.RPi)
                    return gpioConfig.DeviceSelectPin is 7 or 8;

                return true;
            }, "Invalid DeviceSelectPin configuration. Valid values for RPi are 8 for (CD0) and 7 for (CE1)");

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
            var hostConfig = provider.GetRequiredService<IOptions<HostDeviceConfiguration>>().Value;
            var hostBoard = provider.GetRequiredKeyedService<Board>(hostConfig.HostDevice);
            var gpioController = hostBoard.CreateGpioController();
            return gpioController;
        });

        builder.Services.AddKeyedSingleton<GpioPin>("DeviceSelectPin", (provider, _) =>
        {
            var gpioConfig = provider.GetRequiredService<IOptions<GpioConfiguration>>().Value;
            var pinNumber = gpioConfig.DeviceSelectPin;
            var gpioController = provider.GetRequiredService<GpioController>();
            var pin = gpioController.OpenPin(pinNumber, PinMode.Output);
            return pin;
        });

        builder.Services.AddKeyedSingleton<GpioPin>("ResetPin", (provider, _) =>
        {
            var gpioConfig = provider.GetRequiredService<IOptions<GpioConfiguration>>().Value;
            var pinNumber = gpioConfig.ResetPin;
            var gpioController = provider.GetRequiredService<GpioController>();
            var pin = gpioController.OpenPin(pinNumber, PinMode.Output);
            return pin;
        });

        builder.Services.AddSingleton<SpiDevice>(provider =>
        {
            var hostConfig = provider.GetRequiredService<IOptions<HostDeviceConfiguration>>().Value;
            var gpioConfig = provider.GetRequiredService<IOptions<GpioConfiguration>>().Value;

            var chipSelectLine = hostConfig.HostDevice switch
            {
                HostDevices.Ftx232H => 3,
                HostDevices.RPi when gpioConfig.DeviceSelectPin == 8 => 0,
                HostDevices.RPi when gpioConfig.DeviceSelectPin == 7 => 1,
                _ => -1
            };

            var spiConfig = provider.GetRequiredService<IOptions<SpiConfiguration>>().Value;
            var spiSettings = new SpiConnectionSettings(spiConfig.BusId)
            {
                ClockFrequency = spiConfig.ClockFrequency,
                DataBitLength = spiConfig.DataBitLength,
                DataFlow = spiConfig.DataFlow,
                ChipSelectLine = chipSelectLine,
                ChipSelectLineActiveState = PinValue.Low,
                Mode = spiConfig.Mode
            };

            var hostBoard = provider.GetRequiredKeyedService<Board>(hostConfig.HostDevice);
            var spiDevice = hostBoard.CreateSpiDevice(spiSettings);
            return spiDevice;
        });

        builder.Services.AddSingleton<Rf69>(provider =>
        {
            var spiDevice = provider.GetRequiredService<SpiDevice>();
            var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger<Rf69>();

            Rf69 radio;
            var hostConfig = provider.GetRequiredService<IOptions<HostDeviceConfiguration>>().Value;
            if (hostConfig.HostDevice == HostDevices.RPi)
            {
                radio = new Rf69(spiDevice, logger);
            }
            else
            {
                var deviceSelectPin = provider.GetRequiredKeyedService<GpioPin>("DeviceSelectPin");
                radio = new Rf69(deviceSelectPin, spiDevice, logger);
            }
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
