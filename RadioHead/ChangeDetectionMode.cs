namespace RadioHead
{
    /// <summary>
    /// The used to detect when a radio operation completed.
    /// </summary>
    public enum ChangeDetectionMode
    {
        /// <summary>
        /// Completed radio operations are detected using interrupts.
        /// </summary>
        Interrupt,

        /// <summary>
        /// Completed radio operations are detected by polling the device.
        /// </summary>
        Polling
    }
}
