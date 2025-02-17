using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using RadioHeadIot.Examples.Shared;
using System.Device.Gpio;
using RadioHead.RhRf69;

namespace Rf69Client;

internal static class Program
{
    internal static void Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        var host = builder
            .AddConfigurationOptions()
            .ConfigureDependencyInjection()
            .ConfigureApplication()
            .Build();

        host.Services.GetRequiredService<Application>().Run();
    }
}

internal static class ApplicationExtension
{
    public static HostApplicationBuilder ConfigureApplication(this HostApplicationBuilder builder)
    {
        builder.Services.AddSingleton<Application>(provider =>
        {
            var resetPin = provider.GetRequiredKeyedService<GpioPin>("ResetPin");
            var radio = provider.GetRequiredService<Rf69>();

            var radioConfigOptions = provider.GetRequiredService<IOptions<RadioConfiguration>>();

            var app = new Application(resetPin, radio, radioConfigOptions);
            return app;
        });

        return builder;
    }
}