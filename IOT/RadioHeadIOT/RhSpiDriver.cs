using System.Device.Gpio;
using System.Device.Spi;

namespace RadioHeadIot;

/// <summary>
/// Base class for RadioHead drivers that use the SPI bus to communicate with its
/// transport hardware.
/// <para>
/// This class can be subclassed by Drivers that require to use the SPI bus. It can be
/// configured to use either the RHHardwareSPI class (if there is one Available on the
/// platform) of the bit-banged RHSoftwareSPI class. The default behaviour is to use a
/// pre-instantiated built-in RHHardwareSPI  interface.</para>
/// <para>
/// SPI bus access is protected by ATOMIC_BLOCK_START and ATOMIC_BLOCK_END, which will
/// ensure interrupts  are disabled during access.</para>
/// <para>
/// The read and write routines implement commonly used SPI conventions: specifically
/// that the MSB of the first byte transmitted indicates that it is a write operation
/// and the remaining bits indicate the register to access.</para>
/// <para>
/// This can be overriden  in subclasses if necessary or an alternative class,
/// RNRFSPIDriver can be used to access devices like Nordic NRF series radios, which
/// have different requirements.</para>
/// <para>
/// Application developers are not expected to instantiate this class directly: 
/// it is for the use of Driver developers.</para>
/// </summary>
public abstract class RhSpiDriver : RhGenericDriver
{
    private static readonly object CriticalSection = new();

    private readonly GpioPin _deviceSelectPin;
    private readonly SpiDevice _spi;

    /// <summary>
    /// This is the bit in the SPI address that marks it as a write operation
    /// </summary>
    private const byte RhSpiWriteMask = 0x80;

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="deviceSelectPin"> The controller pin to use to select the desired
    /// SPI device. This pin will be driven LOW during SPI communications with the SPI
    /// device that is used by this Driver.
    /// </param>
    /// <param name="spi">Reference to the SPI interface to use. The default is to use a
    /// default built-in Hardware interface.
    /// </param>
    protected RhSpiDriver(GpioPin deviceSelectPin, SpiDevice spi)
    {
        _deviceSelectPin = deviceSelectPin;
        _spi = spi;
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
    /// Reads a byte from the SPI device.
    /// </summary>
    /// <returns>A byte read from the SPI device.</returns>
    protected byte ReadByte()
    {
        byte value;
        lock (CriticalSection)
        {
            value = _spi.ReadByte();
        }

        return value;
    }

    /// <summary>
    /// Writes a byte to the SPI device.
    /// </summary>
    /// <param name="value">The byte to be written to the SPI device.</param>
    protected void WriteByte(byte value)
    {
        lock (CriticalSection)
        {
            _spi.WriteByte(value);
        }
    }

    /// <summary>
    /// Reads a single register from the SPI device
    /// </summary>
    /// <param name="reg">Register number</param>
    /// <returns>The value of the register</returns>
    protected byte ReadFrom(byte reg)
    {
        byte value;
        lock (CriticalSection)
        {
            SelectDevice();
            // Send the address with the write mask off
            _spi.WriteByte((byte)(reg & ~RhSpiWriteMask));
            value = _spi.ReadByte(); // The written value is ignored, reg value is read
            DeselectDevice();
        }
        return value;
    }

    /// <summary>
    /// Writes a single byte to the SPI device
    /// </summary>
    /// <param name="reg">Register number</param>
    /// <param name="value">The value to write</param>
    /// <returns>Some devices return a status byte during the first data transfer. This
    /// byte is returned.  It may or may not be meaningful depending on the type of
    /// device being accessed.
    /// </returns>
    protected void WriteTo(byte reg, byte value)
    {
        lock (CriticalSection)
        {
            SelectDevice();
            // Send the address with the write mask on
            _spi.WriteByte((byte)(reg | RhSpiWriteMask));
            _spi.WriteByte(value); // New value follows
            DeselectDevice();
        }
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
    protected byte BurstReadFrom(byte reg, out byte[] dest, byte len)
    {
        const byte status = 0;
        dest = new byte[len];
        lock (CriticalSection)
        {
            SelectDevice();
            // send the start address with the write mask off
            _spi.WriteByte((byte)(reg & ~RhSpiWriteMask));
            _spi.Read(dest);
            DeselectDevice();
        }

        return status;
    }

    /// <summary>
    /// Write a number of consecutive registers using burst write Mode
    /// </summary>
    /// <param name="reg">Register number of the first register</param>
    /// <param name="buffer">Array of new register values to write.</param>
    protected void BurstWriteTo(byte reg, byte[] buffer)
    {
        lock (CriticalSection)
        {
            SelectDevice();
            // Send the start address with the write mask on
            _spi.WriteByte((byte)(reg | RhSpiWriteMask));
            _spi.Write(buffer);
            DeselectDevice();
        }
    }

    /// <summary>
    /// Override this if you need an unusual way of selecting the slave before SPI
    /// transactions.  The default uses digitalWrite(_slaveSelectPin, LOW) 
    /// </summary>
    protected virtual void SelectDevice()
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
