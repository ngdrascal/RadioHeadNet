using System.Device;
using System.Device.Gpio;
using System.Diagnostics;
using System.Threading;

namespace RadioHead.Examples.Rf95SharedNf
{
    public class ApplicationBase
    {
        protected GpioPin ResetPin { get; }
        protected RhRf69.Rf69 Radio { get; }
        protected float Frequency { get; }
        protected sbyte Power { get; }

        protected ApplicationBase() { }

        public ApplicationBase(GpioPin resetPin, RhRf69.Rf69 radio, float frequency, sbyte power)
        {
            ResetPin = resetPin;
            Radio = radio;
            Frequency = frequency;
            Power = power;
        }

        protected virtual bool Init()
        {
            ResetRadio();
            return ConfigureRadio();
        }

        protected virtual void Loop() { }

        public virtual void Run(CancellationToken cancellationToken)
        {
            if (!Init())
                return;

            while (!cancellationToken.IsCancellationRequested)
                Loop();
        }

        protected bool ConfigureRadio()
        {
            if (!Radio.Init())
            {
                Debug.WriteLine("RadioHead init failed");
                return false;
            }

            Radio.SetTxPower(Power, true);


            if (Radio.SetFrequency(Frequency))
                return true;

            Debug.WriteLine("set frequency failed");
            return false;

        }

        protected void ResetRadio()
        {
            ResetPin.Write(PinValue.Low);
            DelayHelper.DelayMilliseconds(5, false);

            ResetPin.Write(PinValue.High);
            DelayHelper.DelayMicroseconds(100, false);

            ResetPin.Write(PinValue.Low);
            DelayHelper.DelayMilliseconds(5, false);
        }
    }
}
