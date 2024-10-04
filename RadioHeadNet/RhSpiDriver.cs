using System.Device.Gpio;
using System.Device.Spi;

namespace RadioHeadNet;

/// <summary>
/// Base class for RadioHead drivers that use the SPI bus to communicate with its
/// transport hardware.
///
/// This class can be subclassed by Drivers that require to use the SPI bus. It can be
/// configured to use either the RHHardwareSPI class (if there is one Available on the
/// platform) of the bit-banged RHSoftwareSPI class. The default behaviour is to use a
/// pre-instantiated built-in RHHardwareSPI  interface.
///
/// SPI bus access is protected by ATOMIC_BLOCK_START and ATOMIC_BLOCK_END, which will
/// ensure interrupts  are disabled during access.
/// 
/// The read and write routines implement commonly used SPI conventions: specifically
/// that the MSB of the first byte transmitted indicates that it is a write operation
/// and the remaining bits indicate the register to access.
/// 
/// This can be overriden  in subclasses if necessary or an alternative class,
/// RNRFSPIDriver can be used to access devices like Nordic NRF series radios, which
/// have different requirements.
///
/// Application developers are not expected to instantiate this class directly: 
/// it is for the use of Driver developers.
/// </summary>
public abstract class RhSpiDriver : RhGenericDriver
{
    private static readonly object CriticalSection = new();

    private readonly GpioPin _deviceSelectPin;
    protected readonly SpiDevice Spi;

    /// <summary>
    /// This is the bit in the SPI address that marks it as a write operation
    /// </summary>
    private const byte RhSpiWriteMask = 0x80;

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="deviceSelectPin"> The controller pin to use to select the desired SPI
    /// device. This pin will be driven LOW during SPI communications with the SPI device
    /// that is used by this Driver.
    /// </param>
    /// <param name="spi">Reference to the SPI interface to use. The default is to use a
    /// default built-in Hardware interface.
    /// </param>
    protected RhSpiDriver(GpioPin deviceSelectPin, SpiDevice spi)
    {
        _deviceSelectPin = deviceSelectPin;
        Spi = spi;
    }

    /// <summary>
    /// Initialise the Driver transport hardware and software.
    /// Make sure the Driver is properly configured before calling Init().
    /// </summary>
    /// <returns>true if initialisation succeeded.</returns>
    public override bool Init()
    {
        if (!base.Init())
            return false;

        DeselectDevice();

        return true;
    }

    /// <summary>
    /// Reads a single register from the SPI device
    /// </summary>
    /// <param name="reg">Register number</param>
    /// <returns>The value of the register</returns>
    protected byte SpiRead(byte reg)
    {
        byte val;
        lock (CriticalSection)
        {
            SelectDevice();
            // Send the address with the write mask off
            Spi.WriteByte((byte)(reg & ~RhSpiWriteMask));
            val = Spi.ReadByte(); // The written value is ignored, reg value is read
            DeselectDevice();
        }
        return val;
    }

    /// <summary>
    /// Writes a single byte to the SPI device
    /// </summary>
    /// <param name="reg">Register number</param>
    /// <param name="val">The value to write</param>
    /// <returns>Some devices return a status byte during the first data transfer. This
    /// byte is returned.  It may or may not be meaningful depending on the type of
    /// device being accessed.
    /// </returns>
    protected byte SpiWrite(byte reg, byte val)
    {
        const byte status = 0;
        lock (CriticalSection)
        {
            SelectDevice();
            // Send the address with the write mask on
            Spi.WriteByte((byte)(reg | RhSpiWriteMask));
            Spi.WriteByte(val); // New value follows
            DeselectDevice();
        }

        return status;
    }

    /// <summary>
    /// Reads a number of consecutive registers from the SPI device using burst read Mode
    /// </summary>
    /// <params name="reg">Register number of the first register</params> 
    /// <params name="dest">Array to write the register values to. Must be at least len
    /// bytes</params>
    /// <params name="len">Number of bytes to read</params>
    /// <returns>Some devices return a status byte during the first data transfer. This
    /// byte is returned.  It may or may not be meaningful depending on the type of
    /// device being accessed.
    /// </returns>
    public byte SpiBurstRead(byte reg, out byte[] dest, byte len)
    {
        const byte status = 0;
        dest = new byte[len];
        lock (CriticalSection)
        {
            SelectDevice();
            // send the start address with the write mask off
            Spi.WriteByte((byte)(reg & ~RhSpiWriteMask));
            Spi.Read(dest);
            DeselectDevice();
        }

        return status;
    }

    /// <summary>
    /// Write a number of consecutive registers using burst write Mode
    /// </summary>
    /// <param name="reg">Register number of the first register</param>
    /// <param name="buffer">Array of new register values to write.</param>
    /// <returns>Some devices return a status byte during the first data transfer. This
    /// byte is returned.  It may or may not be meaningful depending on the type of
    /// device being accessed.
    /// </returns>
    protected byte SpiBurstWrite(byte reg, byte[] buffer)
    {
        const byte status = 0;
        lock (CriticalSection)
        {
            SelectDevice();
            // Send the start address with the write mask on
            Spi.WriteByte((byte)(reg | RhSpiWriteMask));
            Spi.Write(buffer);
            DeselectDevice();
        }

        return status;
    }

    /// <summary>
    /// Set the SPI interrupt number.  If SPI transactions can occur within an interrupt,
    /// tell the low level SPI interface which interrupt is used.
    /// </summary>
    /// <param name="interruptNumber">the interrupt number</param>
    public void SpiUsingInterrupt(byte interruptNumber)
    {
        throw new NotImplementedException($"{nameof(SpiUsingInterrupt)}");
    }

    /// <summary>
    /// Override this if you need an unusual way of selecting the slave before SPI
    /// transactions.  The default uses digitalWrite(_slaveSelectPin, LOW) 
    /// </summary>
    protected void SelectDevice()
    {
        _deviceSelectPin.Write(PinValue.Low);
    }

    /// <summary>
    /// Override this if you need an unusual way of selecting the slave before SPI
    /// transactions. The default uses digitalWrite(_slaveSelectPin, HIGH)
    /// </summary>
    protected virtual void DeselectDevice()
    {
        _deviceSelectPin.Write(PinValue.High);
    }
}
