using System;
using System.Threading;
using System.Device.Gpio;
using RadioHeadNF.RhRf69;

namespace RadioHeadNF.TestDriver
{
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
            ResetRadio();
            ConfigureRadio();

            SendData(12.3f, 4.56f, 7.8f);

            if (_radio.PollAvailable(5000))
            {
                _radio.Receive(out var receivedData);
                var temp = BitConverter.ToSingle(receivedData, 0);
                var hum = BitConverter.ToSingle(receivedData, 4);
                var vol = BitConverter.ToSingle(receivedData, 8);
                Console.WriteLine($"{temp} C, {hum} %RH, {vol} V");
            }
        }

        private void SendData(float temp, float humidity, float voltage)
        {
            var tempBuf = BitConverter.GetBytes(temp);
            var humBuf = BitConverter.GetBytes(humidity);
            var volBuf = BitConverter.GetBytes(voltage);

            var message = new byte[tempBuf.Length + humBuf.Length + volBuf.Length];
            tempBuf.CopyTo(message, 0);
            humBuf.CopyTo(message, tempBuf.Length);
            volBuf.CopyTo(message, tempBuf.Length + humBuf.Length);

            _radio.Send(message);
            _radio.SetModeIdle();
        }

        private void ConfigureRadio()
        {
            _radio.Init();
            _radio.SetTxPower(_power, true);
            _radio.SetFrequency(_frequency);
        }

        private void ResetRadio()
        {
            _resetPin.Write(PinValue.Low);

            _resetPin.Write(PinValue.High);
            Thread.Sleep(TimeSpan.FromMilliseconds(1));

            _resetPin.Write(PinValue.Low);
            Thread.Sleep(TimeSpan.FromMilliseconds(5));
        }
    }
}
