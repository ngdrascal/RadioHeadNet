using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using nanoFramework.Hosting;
using RadioHeadNf.Examples.Rf69Shared;

namespace RadioHeadNf.Examples.Rf69Client
{
    public static class Program
    {
        public static void Main()
        {
            var hostBuilder = Host.CreateDefaultBuilder();
            hostBuilder.AddServices<Application>();
            var host = hostBuilder.Build();

            var app = (Application)host.Services.GetRequiredService(typeof(Application));

            app.Run(CancellationToken.None);
        }
    }
}
