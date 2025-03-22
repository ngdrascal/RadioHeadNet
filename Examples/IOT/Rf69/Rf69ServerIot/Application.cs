using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RadioHead.RhRf69;
using RadioHeadIot;
using RadioHeadIot.Configuration;
using Rf69SharedIot;

namespace RadioHead.Examples.Rf69ServerIot;

internal class Application : ApplicationBase
{
    public Application(Rf69 radio, IOptions<RadioConfiguration> radioConfig,
        Rf69RadioResetter resetter, ILogger<Rf69> logger) : base(radio, radioConfig, resetter, logger)
    {
    }

    protected override void Loop()
    {
        if (Radio.WaitAvailableTimeout(5000))
        {
            if (Radio.Receive(out var inBuffer))
            {
                var inStr = Encoding.UTF8.GetString(inBuffer);
                Console.WriteLine($"Server: received: {inStr}");
                Console.WriteLine($"Server: RSSI: {Radio.LastRssi}");

                Thread.Sleep(1000); // Wait a little bit to avoid collisions

                // Send a reply
                string outStr;
                if (TimeOnly.TryParse(inStr, out var inTime))
                {
                    // add 12 hours
                    var outTime = inTime.Add(new TimeSpan(12, 0, 0));
                    outStr = outTime.ToString("HH:mm:ss");
                }
                else
                {
                    outStr = $"Invalid time: {inStr}";
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
    }
}
