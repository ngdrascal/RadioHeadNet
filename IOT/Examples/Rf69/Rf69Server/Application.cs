using System.Text;
using Microsoft.Extensions.Options;
using RadioHead;
using RadioHead.RhRf69;
using RadioHeadIot.Examples.Shared;

namespace Rf69Server;

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

        // read the SentDetectionMode from the configuration and convert it to an enum
        if (Enum.TryParse(radioConfig.Value.SentDetectionMode, true, out SentDetectionMode sentDetectionMode))
            radio.SetSentDetectionMode(sentDetectionMode);

        return true;
    }


    private void Loop()
    {
        if (radio.Available())
        {
            // Should be a message for us now   
            if (radio.Receive(out var inBuffer))
            {
                var str = Encoding.UTF8.GetString(inBuffer);
                Console.WriteLine($"got request: {str}");

                Console.WriteLine($"RSSI: {radio.LastRssi}");

                // Send a reply
                var outBuffer = Encoding.UTF8.GetBytes("And hello back to you");
                radio.Send(outBuffer);
                radio.WaitPacketSent();
                Console.WriteLine("Sent a reply");
            }
            else
            {
                Console.WriteLine("Receive failed");
            }
        }

        Thread.Sleep(3000);
    }
}
