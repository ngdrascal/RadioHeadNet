using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using nanoFramework.Hosting;
using Rf69.Examples.Rf69SharedNf;

namespace Rf69.Examples.Rf69ClientNf
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
