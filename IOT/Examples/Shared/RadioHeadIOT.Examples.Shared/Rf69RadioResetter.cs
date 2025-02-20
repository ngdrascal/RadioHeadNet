
using System.Device.Gpio;

namespace RadioHeadIOT.Examples.Shared;

public class Rf69RadioResetter(GpioPin resetPin)
{
    /// <summary>
    /// Encapsulates timing needed to reset the radio.
    /// </summary>
    public void ResetRadio()
    {
        resetPin.Write(PinValue.Low);

        resetPin.Write(PinValue.High);
        Thread.Sleep(TimeSpan.FromMicroseconds(100));

        resetPin.Write(PinValue.Low);
        Thread.Sleep(TimeSpan.FromMilliseconds(5));
    }
}
