using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using RadioHeadIot.Examples.Shared;

namespace Rf69Client;

internal static class Program
{
    internal static int Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        var host = builder
            .AddConfigurationSources()
            .AddConfigurationOptions()
            .AddServices<Application>()
            .Build();

        if (!host.Services.GetRequiredService<IOptions<GpioConfiguration>>().Value.IsValid())
        {
            Console.WriteLine("Invalid GPIO configuration.  Check 'appSettings.json'.");
            return 1;
        }

        if (!host.Services.GetRequiredService<IOptions<RadioConfiguration>>().Value.IsValid())
        {
            Console.WriteLine("Invalid Radio configuration.  Check 'appSettings.json'.");
            return 1;
        }

        var ct = CancellationToken.None;
        host.Services.GetRequiredService<Application>().Run(ct);

        return 0;
    }
}
