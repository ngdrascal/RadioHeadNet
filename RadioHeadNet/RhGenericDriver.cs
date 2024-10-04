// ReSharper disable InconsistentNaming

using System.Diagnostics;

namespace RadioHeadNet;

public abstract class RhGenericDriver
{

    // Defines bits of the FLAGS header reserved for use by the RadioHead library and 
    // the flags Available for use by applications
    protected const byte RH_FLAGS_RESERVED = 0xf0;
    protected const byte RH_FLAGS_APPLICATION_SPECIFIC = 0x0F;
    protected const byte RH_FLAGS_NONE = 0;

    // Default timeout for WaitCAD() in ms
    protected const int RH_CAD_DEFAULT_TIMEOUT = 10000;

    /////////////////////////////////////////////////////////////////////
    /// <summary>
    /// Abstract base class for a RadioHead driver.
    ///
    /// This class defines the functions that must be provided by any RadioHead driver.
    /// Different types of driver will implement all the abstract functions, and will perhaps override 
    /// other functions in this subclass, or perhaps add new functions specifically required by that driver.
    /// Do not directly instantiate this class: it is only to be subclassed by driver classes.
    ///
    /// Subclasses are expected to implement a half-duplex, unreliable, error checked, unaddressed packet transport.
    /// They are expected to carry a message payload with an appropriate maximum length for the transport hardware
    /// and to also carry unaltered 4 message headers: TO, FROM, ID, FLAGS
    /// <para>Headers</para>
    ///
    /// Each message sent and received by a RadioHead driver includes 4 headers:
    /// -TO The node address that the message is being sent to (broadcast RH_BROADCAST_ADDRESS (255) is permitted)
    /// -FROM The node address of the sending node
    /// -ID A message ID, distinct (over short time scales) for each message sent by a particular node
    /// -FLAGS A bitmask of flags. The most significant 4 bits are reserved for use by RadioHead. The least
    /// significant 4 bits are reserved for applications.
    /// </summary>

    /// Constructor
    protected RhGenericDriver()
    {
        Mode = Rh69Modes.Initialising;
        _thisAddress = RadioHead.RH_BROADCAST_ADDRESS;
        _txHeaderTo = RadioHead.RH_BROADCAST_ADDRESS;
        _txHeaderFrom = RadioHead.RH_BROADCAST_ADDRESS;
        _txHeaderId = 0;
        _txHeaderFlags = 0;
        _rxBad = 0;
        _rxGood = 0;
        _txGood = 0;
        _cad_timeout = 0;
    }

    /// Initialise the Driver transport hardware and software.
    /// Make sure the Driver is properly configured before calling Init().
    /// \return true if initialisation succeeded.
    public virtual bool Init()
    {
        return true;
    }

    /// Tests whether a new message is Available
    /// from the Driver. 
    /// On most drivers, if there is an uncollected received message, and there is no message
    /// currently bing transmitted, this will also put the Driver into Rh69Modes.Rx Mode until
    /// a message is actually received by the transport, when it will be returned to Rh69Modes.Idle.
    /// This can be called multiple times in a timeout loop.
    /// \return true if a new, complete, error-free uncollected message is Available to be
    /// retrieved by Receive().
    public abstract bool Available();

    /// Turns the receiver on if it not already on.
    /// If there is a valid message Available, copy it to buffer and return true
    /// else return false.
    /// If a message is copied, *len is set to the length (Caution, 0 length messages are permitted).
    /// You should be sure to call this function frequently enough to not miss any messages
    /// It is recommended that you call it in your main loop.
    /// \param[in] buffer Location to copy the received message
    /// \param[in,out] len Pointer to Available space in buffer. Set to the actual number of octets copied.
    /// \return true if a valid message was copied to buffer
    public abstract bool Receive(out byte[] buffer);

    /// Waits until any previous transmit packet is finished being transmitted with WaitPacketSent().
    /// Then optionally waits for Channel Activity Detection (CAD) 
    /// to show the channel is clear (if the radio supports CAD) by calling WaitCAD().
    /// Then loads a message into the transmitter and starts the transmitter. Note that a message length
    /// of 0 is NOT permitted. If the message is too long for the underlying radio technology, Send() will
    /// return false and will not Send the message.
    /// \param[in] data Array of data to be sent
    /// \param[in] len Number of bytes of data to Send (> 0)
    /// specify the maximum time in ms to wait. If 0 (the default) do not wait for CAD before transmitting.
    /// \return true if the message length was valid, and it was correctly queued for transmit. Return false
    /// if CAD was requested and the CAD timeout timed out before clear channel was detected.
    public abstract bool Send(byte[] data);

    /// Returns the maximum message length 
    /// Available in this Driver.
    /// \return The maximum legal message length
    public abstract byte maxMessageLength();

    /// Starts the receiver and blocks until a valid received 
    /// message is Available.
    /// Default implementation calls Available() repeatedly until it returns true;
    /// \param[in] pollDelay Time between polling Available() in milliseconds. This can be useful
    /// in multitaking environment like Linux to prevent WaitAvailableTimeout
    /// using all the CPU while polling for receiver activity
    public virtual void WaitAvailable(ushort pollDelay = 0)
    {
        while (!Available())
        {
            Thread.Yield();
            if (pollDelay != 0)
                Thread.Sleep(pollDelay);
        }
    }

    /// Blocks until the transmitter 
    /// is no longer transmitting.
    public virtual bool WaitPacketSent()
    {
        while (Mode == Rh69Modes.Tx)
            Thread.Yield(); // Wait for any previous transmit to finish
        return true;
    }

    /// Blocks until the transmitter is no longer transmitting.
    /// or until the timeout occuers, whichever happens first
    /// \param[in] timeout Maximum time to wait in milliseconds.
    /// \return true if the radio completed transmission within the timeout period. False if it timed out.
    public virtual bool WaitPacketSent(ushort timeout)
    {
        while (Mode == Rh69Modes.Tx)
            Thread.Yield(); // Wait for any previous transmit to finish
        return true;
    }

    /// Starts the receiver and blocks until a received message is Available or a timeout.
    /// Default implementation calls Available() repeatedly until it returns true;
    /// \param[in] timeout Maximum time to wait in milliseconds.
    /// \param[in] polldelay Time between polling Available() in milliseconds. This can be useful
    /// in multitasking environment like Linux to prevent WaitAvailableTimeout
    /// using all the CPU while polling for receiver activity
    /// \return true if a message is Available
    public virtual bool WaitAvailableTimeout(ushort timeout, ushort polldelay = 0)
    {
        var stopwatch = new Stopwatch();
        stopwatch.Start();
        while (stopwatch.ElapsedMilliseconds < timeout)
        {
            if (Available())
            {
                return true;
            }
            Thread.Yield();
            if (polldelay != 0)
                Thread.Sleep(polldelay);
        }
        return false;
    }

    // Bent G Christensen (bentor@gmail.com), 08/15/2016
    /// <summary>
    /// Channel Activity Detection (CAD).
    /// Blocks until channel activity is finished or CAD timeout occurs.  Uses the
    /// radio's CAD function (if supported) to detect channel activity.  Implements
    /// random delays of 100 to 1000ms while activity is detected and until timeout.
    /// Caution: the random() function is not seeded. If you want non-deterministic
    /// behaviour, consider using something like randomSeed(analogRead(A0)); in your
    /// sketch.
    /// Permits the implementation of listen-before-talk mechanism (Collision Avoidance).
    /// Calls the IsChannelActive() member function for the radio (if supported) to
    /// determine if the channel is active. If the radio does not support
    /// IsChannelActive(), always returns true immediately.
    /// </summary>
    /// <returns>true if the radio-specific CAD (as returned by IsChannelActive()) shows
    /// the channel is clear within the timeout period (or the timeout period is 0), else
    /// returns false.
    /// </returns>
    public virtual bool WaitCAD()
    {
        if (_cad_timeout == 0)
            return true;

        // Wait for any channel activity to finish or timeout
        // Sophisticated DCF function...
        // DCF : BackoffTime = random() x aSlotTime
        // 100 - 1000 ms
        // 10 sec timeout
        var random = new Random();
        var stopwatch = new Stopwatch();
        stopwatch.Start();
        while (IsChannelActive())
        {
            if (stopwatch.ElapsedMilliseconds > _cad_timeout)
                return false;
            Thread.Sleep(random.Next(1, 10) * 100); // Should these values be configurable? Macros?
        }

        return true;
    }

    /// Sets the Channel Activity Detection timeout in milliseconds to be used by WaitCAD().
    /// The default is 0, which means do not wait for CAD detection.
    /// CAD detection depends on support for IsChannelActive() by your particular radio.
    public void SetCADTimeout(uint cad_timeout) { _cad_timeout = cad_timeout; }

    /// Determine if the currently selected radio channel is active.
    /// This is expected to be subclassed by specific radios to implement their Channel Activity Detection
    /// if supported. If the radio does not support CAD, returns true immediately. If a RadioHead radio 
    /// supports IsChannelActive() it will be documented in the radio specific documentation.
    /// This is called automatically by WaitCAD().
    /// \return true if the radio-specific CAD (as returned by override of IsChannelActive()) shows the
    /// current radio channel as active, else false. If there is no radio-specific CAD, returns false.
    ///
    /// subclasses are expected to override if CAD is Available for that radio
    public virtual bool IsChannelActive(){ return false; }

    /// Sets the address of this node. Defaults to 0xFF. Subclasses or the user may want to change this.
    /// This will be used to test the address in incoming messages. In non-promiscuous Mode,
    /// only messages with a TO header the same as thisAddress or the broadcast address (0xFF) will be accepted.
    /// In promiscuous Mode, all messages will be accepted regardless of the TO header.
    /// In a conventional multinode system, all nodes will have a unique address 
    /// (which you could store in EEPROM).
    /// You would normally set the header FROM address to be the same as thisAddress (though you dont have to, 
    /// allowing the possibility of address spoofing).
    /// \param[in] thisAddress The address of this node.
    public virtual void SetThisAddress(byte thisAddress) { _thisAddress = thisAddress; }

    /// Sets the TO header to be sent in all subsequent messages
    /// \param[in] to The new TO header value
    public virtual void SetHeaderTo(byte to) { _txHeaderTo = to; } // TODO: convert to property

    /// Sets the FROM header to be sent in all subsequent messages
    /// \param[in] from The new FROM header value
    public virtual void SetHeaderFrom(byte from) { _txHeaderFrom = from; } // TODO: convert to property

    /// Sets the ID header to be sent in all subsequent messages
    /// \param[in] id The new ID header value
    public virtual void SetHeaderId(byte id) { _txHeaderId = id; } // TODO: convert to property

    /// Sets and clears bits in the FLAGS header to be sent in all subsequent messages
    /// First it clears he FLAGS according to the clear argument, then sets the flags according to the 
    /// set argument. The default for clear always clears the application specific flags.
    /// \param[in] set bitmask of bits to be set. Flags are cleared with the clear mask before being set.
    /// \param[in] clear bitmask of flags to clear. Defaults to RH_FLAGS_APPLICATION_SPECIFIC
    ///            which clears the application specific flags, resulting in new application specific flags
    ///            identical to the set.
    public virtual void SetHeaderFlags(byte set, byte clear = RH_FLAGS_APPLICATION_SPECIFIC) // TODO: convert to property
    {
        _txHeaderFlags &= (byte)~clear;
        _txHeaderFlags |= set;
    }

    /// Tells the receiver to accept messages with any TO address, not just messages
    /// addressed to thisAddress or the broadcast address
    /// \param[in] promiscuous true if you wish to receive messages with any TO address
    public virtual void SetPromiscuous(bool promiscuous) { _promiscuous = promiscuous; } // TODO: convert to property

    /// Returns the TO header of the last received message
    /// \return The TO header
    public virtual byte HeaderTo() { return _rxHeaderTo; } // TODO: convert to property

    /// Returns the FROM header of the last received message
    /// \return The FROM header
    public virtual byte HeaderFrom() { return _rxHeaderFrom; } // TODO: convert to property

    /// Returns the ID header of the last received message
    /// \return The ID header
    public virtual byte headerId() { return _rxHeaderId; } // TODO: convert to property

    /// Returns the FLAGS header of the last received message
    /// \return The FLAGS header
    public virtual byte HeaderFlags() { return _rxHeaderFlags; } // TODO: convert to property

    /// Returns the most recent RSSI (Receiver Signal Strength Indicator).
    /// Usually it is the RSSI of the last received message, which is measured when the preamble is received.
    /// If you called readRssi() more recently, it will return that more recent value.
    /// \return The most recent RSSI measurement in dBm.
    public virtual short LastRssi() { return _lastRssi; }

    // protected Rh69Modes _mode;


    /// <summary>
    /// The current transport operating Mode
    /// </summary>
    public Rh69Modes Mode { get; set; }

    /// Sets the transport hardware into low-power Sleep Mode
    /// (if supported). May be overridden by specific drivers to initiate Sleep Mode.
    /// If successful, the transport will stay in Sleep Mode until woken by 
    /// changing Mode it idle, transmit or receive (eg by calling Send(), Receive(), Available() etc.)
    /// \return true if Sleep Mode is supported by transport hardware and the RadioHead driver, and if Sleep Mode
    ///         was successfully entered. If Sleep Mode is not supported, return false.
    public virtual bool Sleep() { return false; }

    /// Prints a data buffer in HEX.
    /// For diagnostic use
    /// \param[in] prompt string to preface the print
    /// \param[in] buf Location of the buffer to print
    /// \param[in] len Length of the buffer in octets.
    public static void PrintBuffer(string prompt, byte[] buf) { }

    /// Returns the count of the number of bad received packets (ie packets with bad lengths, checksum etc)
    /// which were rejected and not delivered to the application.
    /// Caution: not all drivers can correctly report this count. Some underlying hardware only report
    /// good packets.
    /// \return The number of bad packets received.
    public virtual ushort RxBad() { return _rxBad; }

    /// Returns the count of the number of 
    /// good received packets
    /// \return The number of good packets received.
    public virtual ushort RxGood() { return _rxGood; }

    /// Returns the count of the number of 
    /// packets successfully transmitted (though not necessarily received by the destination)
    /// \return The number of packets successfully transmitted
    public virtual ushort TxGood() { return _txGood; }

    /// This node id
    protected byte _thisAddress;

    /// Whether the transport is in promiscuous Mode
    protected bool _promiscuous;

    /// TO header in the last received message
    protected byte _rxHeaderTo;

    /// FROM header in the last received message
    protected byte _rxHeaderFrom;

    /// ID header in the last received message
    protected byte _rxHeaderId;

    /// FLAGS header in the last received message
    protected byte _rxHeaderFlags;

    /// TO header to Send in all messages
    protected byte _txHeaderTo;

    /// FROM header to Send in all messages
    protected byte _txHeaderFrom;

    /// ID header to Send in all messages
    protected byte _txHeaderId;

    /// FLAGS header to Send in all messages
    protected byte _txHeaderFlags;

    /// The value of the last received RSSI value, in some transport specific units
    protected short _lastRssi;

    /// Count of the number of bad messages (e.g. - bad checksum etc.) received
    protected ushort _rxBad;

    /// Count of the number of successfully transmitted messaged
    protected ushort _rxGood;

    /// Count of the number of bad messages (correct checksum etc.) received
    protected ushort _txGood;

    /// Channel activity detected
    protected bool _cad;

    /// Channel activity timeout in ms
    protected uint _cad_timeout;
}
