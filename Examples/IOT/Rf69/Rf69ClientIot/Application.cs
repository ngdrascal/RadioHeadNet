﻿using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RadioHead.RhRf69;
using RadioHeadIot;
using RadioHeadIot.Configuration;
using Rf69SharedIot;

namespace RadioHead.Examples.Rf69ClientIot;

internal class Application : ExampleApplicationBase
{
    private readonly Stopwatch _stopwatch;

    public Application(Rf69 radio, IOptions<RadioConfiguration> radioConfig,
        Rf69RadioResetter resetter, ILogger<Rf69> logger) : base(radio, radioConfig, resetter, logger)
    {
        _stopwatch = new Stopwatch();
        _stopwatch.Start();
    }

    protected override Task LoopAsync()
    {
        if (_stopwatch.ElapsedMilliseconds >= 3000)
        {
            var outStr = TimeOnly.FromDateTime(DateTime.Now).ToString("HH:mm:ss");
            Console.WriteLine($"Client: sending {outStr}");

            var data = Encoding.UTF8.GetBytes(outStr);
            Radio.Send(data);

            Radio.WaitPacketSent();

            _stopwatch.Restart();
        }

        if (Radio.Available())
        {
            if (Radio.Receive(out var inBuffer))
            {
                var inStr = Encoding.UTF8.GetString(inBuffer);
                Console.WriteLine($"Client: received: {inStr}");
            }
            else
            {
                Console.WriteLine("Client: receive failed.");
            }
        }
        else
        {
            // Console.WriteLine("Client: timed out waiting for response.");
        }

        return Task.CompletedTask;
    }
}
