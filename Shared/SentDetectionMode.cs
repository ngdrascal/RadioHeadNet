namespace RadioHead
{
    /// <summary>
    /// The used to detect when a send operation completed.
    /// </summary>
    public enum SentDetectionMode
    {
        /// <summary>
        /// Completed send operations are detected using interrupts.
        /// </summary>
        Interrupt,

        /// <summary>
        /// Completed send operations are detected by polling the device.
        /// </summary>
        Polling
    }
}
