using System.Text;
using Microsoft.Extensions.Options;
using RadioHead;
using RadioHead.RhRf69;
using RadioHeadIot.Examples.Shared;

namespace Rf69Client;

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
        Console.WriteLine("Sending to rf69_server");

        // Send a message to rf69_server
        var data = Encoding.UTF8.GetBytes("Hello World!");
        radio.Send(data);

        radio.WaitPacketSent();

        // Now wait for a reply
        if (radio.WaitAvailableTimeout(500))
        {
            // Should be a reply message for us now   

            Console.WriteLine(radio.Receive(out var buf) ? $"Got reply: {buf}" :
                                                                   "Received failed");
        }
        else
        {
            Console.WriteLine("No reply, is Rf69Server running?");
        }

        Thread.Sleep(3000);
    }
}
