using System;
using System.Device;
using System.Device.Gpio;
using System.Diagnostics;
using RadioHead.RhRf69;

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
            Debug.WriteLine("Resetting radio ...");
            ResetRadio();

            Debug.WriteLine("Configuring radio ...");
            if (!ConfigureRadio())
            {
                Debug.WriteLine("Configuring radio failed!");
                return;
            }

            while (true)
            {
                Debug.WriteLine("Sending data ...");
                SendData(12.3f, 4.56f, 7.8f);

                Debug.WriteLine("Polling for data ...");
                if (_radio.PollAvailable(5000))
                {
                    _radio.Receive(out var receivedData);
                    var temp = BitConverter.ToSingle(receivedData, 0);
                    var hum = BitConverter.ToSingle(receivedData, 4);
                    var vol = BitConverter.ToSingle(receivedData, 8);
                    Debug.WriteLine($"{temp} C, {hum} %RH, {vol} V");
                }
                else
                    Debug.WriteLine("Timed out waiting for data!");

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

        private bool ConfigureRadio()
        {
            if (!_radio.Init())
            {
                Debug.WriteLine("RadioHead init failed");
                return false;
            }

            _radio.SetTxPower(_power, true);


            if (_radio.SetFrequency(_frequency))
                return true;

            Debug.WriteLine("set frequency failed");
            return false;

        }

        private void ResetRadio()
        {
            _resetPin.Write(PinValue.Low);
            DelayHelper.DelayMilliseconds(5, false);

            _resetPin.Write(PinValue.High);
            DelayHelper.DelayMicroseconds(100, false);

            _resetPin.Write(PinValue.Low);
            DelayHelper.DelayMilliseconds(5, false);
        }
    }
}
