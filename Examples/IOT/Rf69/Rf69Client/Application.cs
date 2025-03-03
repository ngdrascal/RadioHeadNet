using System.Text;
using Microsoft.Extensions.Options;
using RadioHead;
using RadioHead.RhRf69;
using RadioHeadIot.Examples.Rf69Shared;

namespace RadioHeadIot.Examples.Rf69Client;

internal class Application(Rf69 radio, IOptions<RadioConfiguration> radioConfig,
    Rf69RadioResetter resetter)
{
    public void Run(CancellationToken cancellationToken)
    {
        if (!Init())
            return;

        while (!cancellationToken.IsCancellationRequested)
            Loop();
    }

    private bool Init()
    {
        resetter.ResetRadio();
        return ConfigureRadio();
    }

    private bool ConfigureRadio()
    {
        if (!radio.Init())
        {
            Console.WriteLine("Radio initialization failed.");
            return false;
        }

        // Defaults after init are
        //    - frequency: 434.0MHz
        //    - modulation: GFSK_Rb250Fd250
        //    - power: +13dbM (for low power module)
        //    - encryption: none

        if (!radio.SetFrequency(radioConfig.Value.Frequency))
        {
            Console.WriteLine("SetFrequency failed");
            return false;
        }

        // If you are using a high power RF69 eg RFM69HW, you *must* set a Tx power with the
        // isHighPowerModule flag set like this:
        radio.SetTxPower(radioConfig.Value.PowerLevel, radioConfig.Value.IsHighPowered);

        // The encryption key has to be the same as the one in the server
        var key = radioConfig.Value.EncryptionKey;
        if (key.Length > 0)
            radio.SetEncryptionKey(key);

        // read the ChangeDetectionMode from the configuration and convert it to an enum
        if (Enum.TryParse(radioConfig.Value.SentDetectionMode, true, out ChangeDetectionMode sentDetectionMode))
            radio.SetChangeDetectionMode(sentDetectionMode);

        return true;
    }

    private void Loop()
    {
        var outStr = "Hello World!";
        Console.WriteLine($"Client sending {outStr}");

        var data = Encoding.UTF8.GetBytes(outStr);
        radio.Send(data);

        radio.WaitPacketSent();

        // Now wait for a reply
        if (radio.WaitAvailableTimeout(1000))
        {
            if (radio.Receive(out var inBuffer))
            {
                var inStr = Encoding.UTF8.GetString(inBuffer);
                Console.WriteLine($"Client received: {inStr}");
            }
            else
            {
                Console.WriteLine("Client: receive failed.");
            }
        }
        else
        {
            Console.WriteLine("Client: timed out waiting for response.");
        }
    }
}
