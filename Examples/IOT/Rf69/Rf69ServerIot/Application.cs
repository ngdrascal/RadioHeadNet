using System.Text;
using Microsoft.Extensions.Options;
using RadioHead.RhRf69;
using RadioHeadIot;
using RadioHeadIot.Configuration;

namespace RadioHead.Examples.Rf69ServerIot;

internal class Application
{
    private readonly Rf69 _radio;
    private readonly RadioConfiguration _radioConfig;
    private readonly Rf69RadioResetter _resetter;

    public Application(Rf69 radio, IOptions<RadioConfiguration> radioConfig,
        Rf69RadioResetter resetter)
    {
        _radio = radio;
        _resetter = resetter;
        _radioConfig = radioConfig.Value;
    }

    public void Run(CancellationToken cancellationToken)
    {
        if (!Init())
            return;

        while (!cancellationToken.IsCancellationRequested)
            Loop();
    }

    private bool Init()
    {
        Console.WriteLine("Radio Configuration:");
        Console.WriteLine(_radioConfig.Dump());

        _resetter.ResetRadio();

        if (ConfigureRadio())
        {
            Console.WriteLine("Server: radio successfully configured.");
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
        if (!_radio.Init())
            return false;

        // Defaults after init are
        //    - frequency: 434.0MHz
        //    - modulation: GFSK_Rb250Fd250
        //    - power: +13dbM (for low power module)
        //    - encryption: none

        if (!_radio.SetFrequency(_radioConfig.Frequency))
        {
            Console.WriteLine("SetFrequency failed");
            return false;
        }

        // If you are using a high power RF69 e.g. - RFM69HW, you *must* set a Tx power with the
        // isHighPowerModule flag set like this:
        _radio.SetTxPower(_radioConfig.PowerLevel, _radioConfig.IsHighPowered);

        // The encryption key has to be the same as the one in the client
        var key = _radioConfig.EncryptionKey;
        if (key.Length > 0)
            _radio.SetEncryptionKey(key);

        _radio.SetChangeDetectionMode(_radioConfig.ChangeDetectionMode);

        return true;
    }

    private void Loop()
    {
        if (_radio.WaitAvailableTimeout(5000))
        {
            if (_radio.Receive(out var inBuffer))
            {
                var inStr = Encoding.UTF8.GetString(inBuffer);
                Console.WriteLine($"Server received: {inStr}");
                Console.WriteLine($"Server RSSI: {_radio.LastRssi}");

                // Send a reply
                var outStr = inStr.ToUpper();
                var outBuffer = Encoding.UTF8.GetBytes(outStr);
                _radio.Send(outBuffer);
                _radio.WaitPacketSent();
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
