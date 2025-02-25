using System.Text;
using Microsoft.Extensions.Options;
using RadioHead;
using RadioHead.RhRf69;
using RadioHeadIot.Examples.Shared;

namespace RadioHeadIot.Examples.Rf69Server;

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
        if (ConfigureRadio())
        {
            Console.WriteLine("Server: radio successfully configured.");
            Console.WriteLine("Server: waiting for incoming packet.");
            return true;
        }
        else
        {
            Console.WriteLine("Server: radio configuration failed.");
            return false;
        }
    }
    
    private bool ConfigureRadio()
    {
        if (!radio.Init())
            return false;

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

        // The encryption key has to be the same as the one in the client
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
        if (radio.Available())
        {
            // Should be a message for us now   
            if (radio.Receive(out var inBuffer))
            {
                var inStr = Encoding.UTF8.GetString(inBuffer);
                Console.WriteLine($"Server received: {inStr}");
                Console.WriteLine($"Server RSSI: {radio.LastRssi}");

                // Send a reply
                var outStr = inStr.ToUpper();
                var outBuffer = Encoding.UTF8.GetBytes(outStr);
                radio.Send(outBuffer);
                radio.WaitPacketSent();
                Console.WriteLine($"Sent: {outStr}");
            }
            else
            {
                Console.WriteLine("Server receive failed");
            }
        }
    }
}
