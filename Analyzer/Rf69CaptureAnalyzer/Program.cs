using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Rf69CaptureAnalyzer;

internal static class Program
{
    public static void Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.ConfigureServices();
        var host = builder.Build();

        var app = host.Services.GetRequiredService<Application>();

        var fileName = args[1];
        using FileStream fileStream = new(fileName, FileMode.Open, FileAccess.Read, FileShare.Read);

        var options = new ParseOptions
        {
            RecordTypeIndex = 1,
            StartIndex = 2,
            DurationIndex = 3,
            MosiIndex = 4,
            MisoIndex = 5
        };

        var fileNameOnly = Path.GetFileName(fileName);
        app.Run(fileNameOnly, fileStream, options);
    }
}

internal static class HostAppBuilderExtensions
{
    public static IHostApplicationBuilder ConfigureServices(this IHostApplicationBuilder builder)
    {
        builder.Services.AddSingleton<Application>();
        builder.Services.AddSingleton<Analyzer>();
        return builder;
    }
}
