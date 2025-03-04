using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RadioHeadIot.Configuration;

namespace RadioHeadIot.Examples.Rf69Server;

internal static class Program
{
    internal static void Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        var host = builder
            .AddConfigurationSources()
            .AddConfigurationOptions()
            .AddServices<Application>()
            .Build();

        var ct = CancellationToken.None;

        try
        {
            host.Services.GetRequiredService<Application>().Run(ct);
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }
    }
}
