using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Rf69Client;

internal static class Program
{
    static void Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        var host = builder
            .AddConfigurationOptions()
            .ConfigureDependencyInjection()
            .Build();

        host.Services.GetRequiredService<Application>().Run();
    }
}