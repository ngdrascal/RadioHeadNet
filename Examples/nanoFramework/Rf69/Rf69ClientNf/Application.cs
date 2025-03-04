using System;
using System.Diagnostics;
using System.Threading;
using Rf69.Examples.Rf69SharedNf;

namespace Rf69.Examples.Rf69ClientNf
{
    internal class Application : ApplicationBase
    {
        public override void Run(CancellationToken cancellationToken)
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
                if (Radio.PollAvailable(5000))
                {
                    Radio.Receive(out var receivedData);
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

            Radio.Send(message);
            Radio.SetModeIdle();
        }

    }
}
