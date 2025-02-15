// ReSharper disable RedundantUsingDirective
// ReSharper disable InconsistentNaming

using System;
using System.Device;
using System.Diagnostics;
using System.Threading;

namespace RadioHead
{
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
    /// -TO The node address that the message is being sent to (broadcast BroadcastAddress (255) is permitted)
    /// -FROM The node address of the sending node
    /// -ID A message ID, distinct (over short time scales) for each message sent by a particular node
    /// -FLAGS A bitmask of flags. The most significant 4 bits are reserved for use by RadioHead. The least
    /// significant 4 bits are reserved for applications.
    /// </summary>
    public abstract class RhGenericDriver
    {
        // Defines bits of the FLAGS header reserved for use by the RadioHead library and 
        // the flags Available for use by applications
        protected const byte RH_FLAGS_RESERVED = 0xf0;
        protected const byte RH_FLAGS_APPLICATION_SPECIFIC = 0x0F;
        protected const byte RH_FLAGS_NONE = 0;

        // Default timeout for WaitCAD() in ms
        protected const int RH_CAD_DEFAULT_TIMEOUT = 10000;

        protected RhGenericDriver()
        {
            Mode = RhModes.Initialising;
            ThisAddress = RadioHead.BroadcastAddress;
            TxHeaderTo = RadioHead.BroadcastAddress;
            TxHeaderFrom = RadioHead.BroadcastAddress;
            TxHeaderId = 0;
            TxHeaderFlags = 0;
            Promiscuous = false;
            RxBad = 0;
            RxGood = 0;
            RxGood = 0;
            CadTimeout = 0;
        }

        /// <summary>
        /// Initialise the Driver transport hardware and software.
        /// Make sure the Driver is properly configured before calling Init().
        /// <return>true if initialisation succeeded.</return>
        /// </summary>
        public virtual bool Init()
        {
            return true;
        }

        /// <summary>
        /// Tests whether a new message is Available
        /// from the Driver. 
        /// On most drivers, if there is an uncollected received message, and there is no message
        /// currently bing transmitted, this will also put the Driver into RhModes.Rx Mode until
        /// a message is actually received by the transport, when it will be returned to RhModes.Idle.
        /// This can be called multiple times in a timeout loop.
        /// </summary>
        /// <return>true if a new, complete, error-free uncollected message is Available to be
        /// retrieved by Receive().
        /// </return>
        public abstract bool Available();

        /// <summary>
        /// Turns the receiver on if it not already on.
        /// If there is a valid message Available, copy it to buffer and return true
        /// else return false.
        /// If a message is copied, *len is set to the length (Caution, 0 length messages are permitted).
        /// You should be sure to call this function frequently enough to not miss any messages
        /// It is recommended that you call it in your main loop.
        /// </summary>
        /// <param name="buffer">Location to copy the received message to</param>
        /// <return>true if a valid message was copied to buffer</return>
        public abstract bool Receive(out byte[] buffer);

        /// <summary>
        /// Waits until any previous transmit packet is finished being transmitted with WaitPacketSent().
        /// Then optionally waits for Channel Activity Detection (CAD) 
        /// to show the channel is clear (if the radio supports CAD) by calling WaitCAD().
        /// Then loads a message into the transmitter and starts the transmitter. Note that a message length
        /// of 0 is NOT permitted. If the message is too long for the underlying radio technology, Send() will
        /// return false and will not Send the message.
        /// </summary>
        /// <param name="data">Array of data to be sent</param>
        /// <return>true if the message length was valid, and it was correctly queued for transmit. Return false
        /// if CAD was requested and the CAD timeout timed out before clear channel was detected.
        /// </return>
        public abstract bool Send(byte[] data);

        /// <summary>
        /// Returns the maximum message length available in this driver.
        /// </summary>
        /// <returns>The maximum legal message length</returns>
        public abstract byte MaxMessageLength { get; }

        /// <summary>
        /// Starts the receiver and blocks until a valid received message is available.
        /// Default implementation calls Available() repeatedly until it returns true.
        /// </summary>
        /// <param name="pollDelay">Time between polling Available() in milliseconds. This
        /// can be useful in multitasking environment like Linux to prevent
        /// WaitAvailableTimeout using all the CPU while polling for receiver activity.
        /// </param>
        public virtual void WaitAvailable(ushort pollDelay = 0)
        {
            while (!Available())
            {
                RadioHead.Yield();
                if (pollDelay != 0)
                {
                    Thread.Sleep(pollDelay);
                }
            }
        }

        /// <summary>
        /// Blocks until the transmitter is no longer transmitting.
        /// </summary>
        public virtual bool WaitPacketSent()
        {
            while (Mode == RhModes.Tx)
                RadioHead.Yield();
            return true;
        }

        /// <summary>
        /// Blocks until the transmitter is no longer transmitting or until the timeout
        /// occurs, whichever happens first.
        /// </summary>
        /// <param name="timeout">Maximum time to wait in milliseconds.</param>
        /// <returns>true if the radio completed transmission within the timeout period.
        /// False if it timed out.
        /// </returns>
        public virtual bool WaitPacketSent(ushort timeout)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            while (Mode == RhModes.Tx && stopwatch.ElapsedMilliseconds < timeout)
            {
                RadioHead.Yield();
            }

            return Mode != RhModes.Tx;
        }
        /// <summary>
        /// Starts the receiver and blocks until a received message is Available or a timeout.
        /// Default implementation calls Available() repeatedly until it returns true;
        /// </summary>
        /// <param name="timeout">Maximum time to wait in milliseconds.</param>
        /// <param name="pollDelay">Time between polling Available() in milliseconds. This can be useful
        /// in multitasking environment like Linux to prevent WaitAvailableTimeout
        /// using all the CPU while polling for receiver activity.
        /// </param>
        /// <returns>true if a message is Available</returns>
        public virtual bool WaitAvailableTimeout(ushort timeout, ushort pollDelay = 0)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            while (stopwatch.ElapsedMilliseconds < timeout)
            {
                if (Available())
                {
                    return true;
                }

                RadioHead.Yield();

                if (pollDelay != 0)
                {
                    Thread.Sleep(pollDelay);
                }
            }
            return false;
        }

        /// <summary>
        /// Channel activity detected
        /// </summary>
        protected bool Cad { get;  set; }

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
        protected virtual bool WaitCAD()
        {
            if (CadTimeout == 0)
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
                if (stopwatch.ElapsedMilliseconds > CadTimeout)
                    return false;
                Thread.Sleep(random.Next(10) * 100);
            }

            return true;
        }

        /// <summary>
        /// Sets the Channel Activity Detection timeout in milliseconds to be used by
        /// WaitCAD().  The default is 0, which means do not wait for CAD detection.
        /// CAD detection depends on support for IsChannelActive() by your particular radio.
        /// </summary>
        public uint CadTimeout { get; set; }

        /// <summary>
        /// Determine if the currently selected radio channel is active.  This is expected
        /// to be subclassed by specific radios to implement their Channel Activity Detection
        /// if supported. If the radio does not support CAD, returns true immediately. If a
        /// RadioHead radio supports IsChannelActive() it will be documented in the radio
        /// specific documentation. This is called automatically by WaitCAD().
        /// </summary>
        /// <returns>true if the radio-specific CAD (as returned by override of
        /// IsChannelActive()) shows the current radio channel as active, else false. If
        /// there is no radio-specific CAD, returns false.
        /// </returns>
        public virtual bool IsChannelActive()
        {
            return false;
        }

        /// <summary>
        /// Sets the address of this node. Defaults to 0xFF. Subclasses or the user may want
        /// to change this.  This will be used to test the address in incoming messages. In
        /// non-promiscuous Mode, only messages with a TO header the same as thisAddress or
        /// the broadcast address (0xFF) will be accepted.  In promiscuous Mode, all messages
        /// will be accepted regardless of the TO header.  In a conventional multinode system,
        /// all nodes will have a unique address (which you could store in EEPROM).
        /// You would normally set the header FROM address to be the same as thisAddress
        /// (though you don't have to, allowing the possibility of address spoofing).
        /// </summary>
        public byte ThisAddress { get; set; }

        /// <summary>TO header sent in all messages</summary>
        public byte TxHeaderTo { get; set; }

        /// <summary>FROM header sent in all messages</summary>
        public byte TxHeaderFrom { get; set; }

        /// <summary>ID header sent in all messages</summary>
        public byte TxHeaderId { get; set; }

        /// <summary>FLAGS header sent in all messages</summary>
        public byte TxHeaderFlags { get; set; }

        /// <summary>Tells the receiver to accept messages with any TO address, not just
        /// messages addressed to thisAddress or the broadcast address.
        /// </summary>
        public bool Promiscuous { get; set; }

        /// <summary>TO header in the last received message</summary>
        public byte RxHeaderTo { get; protected set; }

        /// <summary>FROM header in the last received message</summary>
        public byte RxHeaderFrom { get; protected set; }

        /// <summary>ID header in the last received message</summary>
        public byte RxHeaderId { get; protected set; }

        /// <summary>FLAGS header in the last received message</summary>
        public byte RxHeaderFlags { get; protected set; }

        /// <summary>The RSSI of the last received message, which is measured when the
        /// preamble is received.
        /// </summary>
        public short LastRssi { get; protected set; }

        /// <summary>The current transport operating Mode</summary>
        protected RhModes Mode { get; set; }

        /// <summary>
        /// Sets the transport hardware into low-power Sleep Mode (if supported). May be
        /// overridden by specific drivers to initiate Sleep Mode.  If successful, the
        /// transport will stay in Sleep Mode until woken by changing Mode to idle,
        /// transmit or receive (e.g. - by calling Send(), Receive(), Available() etc.)
        /// </summary>
        /// <returns>true if Sleep Mode is supported by transport hardware and the RadioHead
        /// driver, and if Sleep Mode was successfully entered. If Sleep Mode is not
        /// supported, return false.
        /// </returns>
        public virtual bool Sleep()
        {
            return false;
        }

        /// <summary>
        /// The count of the number of bad received packets (ie packets with bad lengths,
        /// checksum etc.) which were rejected and not delivered to the application.
        /// Caution: not all drivers can correctly report this count. Some underlying
        /// hardware only report good packets.
        /// </summary>
        public ushort RxBad { get; protected set; }

        /// <summary>The count of the number of good received packets</summary>
        public ushort RxGood { get; protected set; }

        /// <summary>
        /// The count of the number of packets successfully transmitted (though not
        /// necessarily received by the destination)
        /// </summary>
        public ushort TxGood { get; protected set; }
    }
}
