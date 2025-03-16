using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace RadioHead.Examples.Rf69ClientIot;

public static class HostExtensions
{
    public static HostApplicationBuilder AddApplicationServices(this HostApplicationBuilder builder)
    {
        builder.Services.AddSingleton<Application>();

        return builder;
    }
}