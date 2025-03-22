using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RadioHead.RhRf69;
using RadioHeadIot;
using RadioHeadIot.Configuration;

namespace Rf69SharedIot
{
    public abstract class ExampleApplicationBase
    {
        protected readonly Rf69 Radio;
        protected readonly RadioConfiguration RadioConfig;
        protected readonly Rf69RadioResetter Resetter;
        protected readonly ILogger<Rf69> Logger;

        protected ExampleApplicationBase(Rf69 radio, IOptions<RadioConfiguration> radioConfig,
            Rf69RadioResetter resetter, ILogger<Rf69> logger)
        {
            Radio = radio;
            RadioConfig = radioConfig.Value;
            Resetter = resetter;
            Logger = logger;
        }

        public async Task RunAsync(CancellationToken cancellationToken)
        {
            if (!await InitAsync())
                return;

            while (!cancellationToken.IsCancellationRequested)
                await LoopAsync();
        }

        protected virtual Task<bool> InitAsync()
        {
            Logger.LogDebug("Radio Configuration:");
            Logger.LogDebug(RadioConfig.Dump());

            Resetter.ResetRadio();

            if (ConfigureRadio())
            {
                Logger.LogDebug("Radio successfully configured.");
                return Task.FromResult(true);
            }
            else
            {
                Logger.LogDebug("Radio configuration failed.");
                return Task.FromResult(false);
            }
        }

        protected virtual bool ConfigureRadio()
        {
            if (!Radio.Init())
            {
                Logger.LogDebug("Radio initialization failed.");
                return false;
            }

            // Defaults after init are
            //    - frequency: 434.0MHz
            //    - modulation: GFSK_Rb250Fd250
            //    - power: +13dbM (for low power module)
            //    - encryption: none

            if (!Radio.SetFrequency(RadioConfig.Frequency))
            {
                Logger.LogDebug("SetFrequency failed");
                return false;
            }

            // If you are using a high power RF69 e.g. - RFM69HW, you *must* set a Tx power with the
            // isHighPowerModule flag set like this:
            Radio.SetTxPower(RadioConfig.PowerLevel, RadioConfig.IsHighPowered);

            // The encryption key has to be the same as the one in the server
            var key = RadioConfig.EncryptionKey;
            if (key.Length > 0)
                Radio.SetEncryptionKey(key);

            Radio.SetChangeDetectionMode(RadioConfig.ChangeDetectionMode);

            return true;
        }

        protected abstract Task LoopAsync();
    }
}
