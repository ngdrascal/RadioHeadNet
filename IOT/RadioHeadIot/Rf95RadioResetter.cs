using System.Device.Gpio;

namespace RadioHeadIot;

public class Rf95RadioResetter(GpioPin resetPin)
{
    /// <summary>
    /// Encapsulates timing needed to reset the radio.
    /// </summary>
    /// <remarks>
    /// A manual reset of the RFM95W/96W/98W is possible even for applications
    /// in which VDD cannot be physically disconnected. Pin  7 should be pulled
    /// low for a hundred microseconds, and then released. The user should then
    /// wait for 5 ms before using the chip.
    /// </remarks>
    public void ResetRadio()
    {
        resetPin.Write(PinValue.High);

        resetPin.Write(PinValue.Low);
        Thread.Sleep(TimeSpan.FromMicroseconds(100));

        resetPin.Write(PinValue.High);
        Thread.Sleep(TimeSpan.FromMilliseconds(5));
    }
}
