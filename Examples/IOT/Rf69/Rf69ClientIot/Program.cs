using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RadioHeadIot.Configuration;

namespace RadioHead.Examples.Rf69ClientIot;

internal static class Program
{
    internal static void Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        var host = builder
            .AddConfigurationSources()
            .AddConfigurationOptions()
            .AddLogging()
            .AddIotServices()
            .AddRf69Services()
            .AddApplicationServices()
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
