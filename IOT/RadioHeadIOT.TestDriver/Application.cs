using System.Device.Gpio;
using System.Diagnostics.CodeAnalysis;
using RadioHead.RhRf69;

namespace RadioHeadIot.TestDriver;

[ExcludeFromCodeCoverage]
internal class Application(GpioPin resetPin, Rf69 radio, float frequency, sbyte power)
{
    public void Run()
    {
        ResetRadio();
        ConfigureRadio();

        var tempBuf = BitConverter.GetBytes(12.3f);
        var humBuf = BitConverter.GetBytes(45.6f);
        var volBuf = BitConverter.GetBytes(3.7f);
        var message = tempBuf.Concat(humBuf).Concat(volBuf).ToArray();

        radio.Send(message);
        radio.SetModeIdle();

        if (radio.PollAvailable(5000))
        {
            radio.Receive(out var receivedData);
            var temp = BitConverter.ToSingle(receivedData, 0);
            var hum = BitConverter.ToSingle(receivedData, 4);
            var vol = BitConverter.ToSingle(receivedData, 8);
            Console.WriteLine($"{temp} C, {hum} %RH, {vol} V");
        }
    }

    private void ConfigureRadio()
    {
        radio.Init();
        radio.SetTxPower(power, true);
        radio.SetFrequency(frequency);
    }

    private void ResetRadio()
    {
        resetPin.Write(PinValue.Low);

        resetPin.Write(PinValue.High);
        Thread.Sleep(TimeSpan.FromMicroseconds(100));

        resetPin.Write(PinValue.Low);
        Thread.Sleep(TimeSpan.FromMilliseconds(5));
    }
}
