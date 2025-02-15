namespace RadioHead
{
    /// <summary>
    /// These are the different values that can be adopted by the _mode variable and 
    /// returned by the Mode() function.
    /// /// </summary>
    public enum RhModes : byte
    {
        /// <summary>
        /// Transport is initialising. Initial default value until Init() is called.
        /// </summary>
        Initialising = 0,

        /// <summary>
        /// Transport hardware is in low power Sleep Mode (if supported)
        /// </summary>
        Sleep,

        /// <summary>
        /// Transport is idle.
        /// </summary>
        Idle,

        /// <summary>
        /// Transport is in the process of transmitting a message.
        /// </summary>
        Tx,

        /// <summary>
        /// Transport is in the process of receiving a message.
        /// </summary>
        Rx,

        /// <summary>
        /// Transport is in the process of detecting channel activity (if supported)
        /// </summary>
        Cad
    }
}
