﻿using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RadioHead.RhRf95;
using RadioHeadIot;
using RadioHeadIot.Configuration;
using Rf95SharedIot;

namespace RadioHead.Examples.Rf95ClientIot;

internal class Application : ExampleApplicationBase
{
    private readonly Stopwatch _stopwatch;

    public Application(Rf95 radio, IOptions<RadioConfiguration> radioConfig,
        Rf95RadioResetter resetter, ILogger<Rf95> logger) : base(radio, radioConfig, resetter, logger)
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

            var data = Encoding.ASCII.GetBytes(outStr);
            Radio.Send(data);

            Radio.WaitPacketSent();

            _stopwatch.Restart();
        }

        if (Radio.Available())
        {
            if (Radio.Receive(out var inBuffer))
            {
                var dataOnly = new byte[inBuffer.Length - 4];
                Array.Copy(inBuffer, 4, dataOnly, 0, inBuffer.Length - 4);
                var dataStr = Encoding.ASCII.GetString(dataOnly);
                Console.WriteLine($"Client: received: {dataStr}");
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
