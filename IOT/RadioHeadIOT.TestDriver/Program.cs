using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace RadioHeadIot.TestDriver;

internal static class Program
{
    private static void Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        var host = builder
            .AddDefaultConfiguration()
            .ConfigureLogging()
            .ConfigureDependencyInjection()
            .Build();

        host.Services.GetRequiredService<Application>().Run();
    }
}
