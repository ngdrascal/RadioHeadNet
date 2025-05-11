using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RadioHead.RhRf95;
using RadioHeadIot;
using RadioHeadIot.Configuration;
using Rf95SharedIot;

namespace RadioHead.Examples.Rf95ServerIot;

internal class Application : ExampleApplicationBase
{
    public Application(Rf95 radio, IOptions<RadioConfiguration> radioConfig,
        Rf95RadioResetter resetter, ILogger<Rf95> logger) : base(radio, radioConfig, resetter, logger)
    {
    }

    protected override Task LoopAsync()
    {
        if (Radio.WaitAvailableTimeout(5000))
        {
            if (Radio.Receive(out var inBuffer))
            {
                var dataOnly = new byte[inBuffer.Length - 4];
                Array.Copy(inBuffer, 4, dataOnly, 0, inBuffer.Length - 4);
                var dataStr = Encoding.ASCII.GetString(dataOnly);
                Console.WriteLine($"Server: received: {dataStr}");
                Console.WriteLine($"Server: RSSI: {Radio.LastRssi}");

                Thread.Sleep(500); // Wait a little bit to avoid collisions

                // Send a reply
                string outStr;
                if (TimeOnly.TryParse(dataStr, out var inTime))
                {
                    // add 12 hours
                    var outTime = inTime.Add(new TimeSpan(12, 0, 0));
                    outStr = outTime.ToString("HH:mm:ss");
                }
                else
                {
                    outStr = $"Invalid time: {dataStr}";
                }
                var outBuffer = Encoding.UTF8.GetBytes(outStr);
                Radio.Send(outBuffer);
                Radio.WaitPacketSent();
                Console.WriteLine($"Server: sent: {outStr}");
            }
            else
            {
                Console.WriteLine("Server: receive failed.");
            }
        }
        else
        {
            Console.WriteLine("Server: timed out waiting for request.");
        }

        return Task.CompletedTask;
    }
}
