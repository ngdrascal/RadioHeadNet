using System.Device.Gpio;
using System.Diagnostics.CodeAnalysis;
using RadioHeadNet;
using Rf69 = RadioHeadNet.RhRf69.Rf69;

namespace RF69TestDriver;

[ExcludeFromCodeCoverage]
internal class Application
{
    private readonly GpioPin _resetPin;
    private readonly Rf69 _radio;
    private readonly float _frequency;
    private readonly sbyte _power;

    public Application(GpioPin resetPin, Rf69 radio, float frequency, sbyte power)
    {
        _resetPin = resetPin;
        _radio = radio;
        _frequency = frequency;
        _power = power;
    }

    public void Run()
    {
        InitializeRadio();

        var tempBuf = BitConverter.GetBytes(12.3f);
        var humBuf = BitConverter.GetBytes(45.6f);
        var volBuf = BitConverter.GetBytes(3.7f);
        var message = tempBuf.Concat(humBuf).Concat(volBuf).ToArray();

        _radio.Send(message);
        _radio.SetModeIdle();

        if (_radio.PoleReceiver(5000))
        {
            _radio.Receive(out var receivedData);
            var temp = BitConverter.ToSingle(receivedData, 0);
            var hum = BitConverter.ToSingle(receivedData, 4);
            var vol = BitConverter.ToSingle(receivedData, 8);
            Console.WriteLine($"{temp} C, {hum} %RH, {vol} V");
        }
    }

    private void InitializeRadio()
    {
        ResetRadio();
        _radio.Init();
        _radio.SetTxPower(_power, true);
        _radio.SetFrequency(_frequency);
    }

    private void ResetRadio()
    {
        _resetPin.Write(PinValue.Low);

        _resetPin.Write(PinValue.High);
        Thread.Sleep(TimeSpan.FromMicroseconds(100));

        _resetPin.Write(PinValue.Low);
        Thread.Sleep(TimeSpan.FromMilliseconds(5));
    }
}
