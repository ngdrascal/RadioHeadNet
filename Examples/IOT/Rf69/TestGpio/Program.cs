using System.Device.Gpio;
using System.Device.Spi;
using Iot.Device.Board;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RadioHeadIot.Configuration;

namespace TestGpio;
internal class Program
{
    private static void Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        var host = builder
            .AddConfigurationSources()
            .AddConfigurationOptions()
            .AddServices<Application>()
            .Build();

        var gpioController = new GpioController(PinNumberingScheme.Logical);
        gpioController.OpenPin(17, PinMode.Output);
        // var board = new RaspberryPiBoard();
        var spiSettings = new SpiConnectionSettings(0)
        {
            ClockFrequency = 500000,
            DataBitLength = 8,
            DataFlow = DataFlow.MsbFirst,
            ChipSelectLine = -1,
            ChipSelectLineActiveState = PinValue.Low,
            Mode = SpiMode.Mode0
        };
        var spiDevice = SpiDevice.Create(spiSettings);
        // var spiDevice = host.Services.GetRequiredService<SpiDevice>();
        spiDevice.WriteByte(3);

        Console.WriteLine("Success!");
    }
}