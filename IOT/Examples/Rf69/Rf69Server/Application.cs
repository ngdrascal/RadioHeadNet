using System.Text;
using Microsoft.Extensions.Options;
using RadioHead.RhRf69;
using RadioHeadIot.Examples.Shared;
using RadioHeadIOT.Examples.Shared;

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

        // // The encryption key has to be the same as the one in the server
        byte[] key = [0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08,
                      0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08];
        radio.SetEncryptionKey(key);

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
