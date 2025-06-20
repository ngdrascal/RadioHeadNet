using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RadioHead.Examples.Rf95SharedNf;

namespace RadioHead.Examples.Rf95ClientNf
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
