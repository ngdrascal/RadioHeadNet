﻿using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace RadioHeadIot.Configuration.Tests;

[TestFixture, ExcludeFromCodeCoverage]
public class GpioConfigurationTests
{
    [TestCase("RPI", 1, 2, 3)]
    [TestCase("rPi", 1, 2, 3)]
    [TestCase("FTX232H", 1, 2, -1)]
    [TestCase("fTx232h", 1, 2, -1)]
    public void Test01(string hostDevice, int selectPin, int resetPin, int intrPin)
    {
        // ARRANGE:
        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddInMemoryCollection(new Dictionary<string, string>
        {
            { $"HostDevice", hostDevice },
            { $"{GpioConfiguration.SectionName}:DeviceSelectPin", selectPin.ToString() },
            { $"{GpioConfiguration.SectionName}:ResetPin", resetPin.ToString() },
            { $"{GpioConfiguration.SectionName}:InterruptPin", intrPin.ToString() }
        }!);

        var configRoot = configBuilder.Build();

        var services = new ServiceCollection();
        services.AddOptions<GpioConfiguration>()
            .Bind(configRoot.GetSection(GpioConfiguration.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        var provider = services.BuildServiceProvider();

        // ACT:
        var configuration = provider.GetRequiredService<IOptions<GpioConfiguration>>().Value;

        // ASSERT:
        Assert.That(configRoot["HostDevice"], Is.EqualTo(hostDevice));
        Assert.That(configuration.DeviceSelectPin, Is.EqualTo(selectPin));
        Assert.That(configuration.ResetPin, Is.EqualTo(resetPin));
        Assert.That(configuration.InterruptPin, Is.EqualTo(intrPin));
    }

    [TestCase("RPI", -1, 1, 2)]
    [TestCase("RPI", 0, -2, 2)]
    [TestCase("RPI", 0, 1, -2)]
    public void Test02(string hostDevice, int selectPin, int resetPin, int intrPin)
    {
        // ARRANGE:
        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddInMemoryCollection(new Dictionary<string, string>
        {
            { "HostDevice", hostDevice },
            { $"{GpioConfiguration.SectionName}:DeviceSelectPin", selectPin.ToString() },
            { $"{GpioConfiguration.SectionName}:ResetPin", resetPin.ToString() },
            { $"{GpioConfiguration.SectionName}:InterruptPin", intrPin.ToString() }
        }!);
        var configRoot = configBuilder.Build();

        var services = new ServiceCollection();
        services.AddOptions<GpioConfiguration>()
            .Bind(configRoot.GetSection(GpioConfiguration.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        var provider = services.BuildServiceProvider();

        // ACT:

        // ASSERT:
        Assert.Throws<OptionsValidationException>(() =>
        {
            _ = provider.GetRequiredService<IOptions<GpioConfiguration>>().Value;
        });
    }
}
