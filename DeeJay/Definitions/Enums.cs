namespace DeeJay.Definitions
{
    /// <summary>
    ///     Bitrate of a stream expressed in bits per second. (1kb = 1024b)
    /// </summary>
    internal enum Enums
    {
        b64k = 65536,
        b128k = 131072
    }

    /// <summary>
    ///     Represents the current state of the music service.
    /// </summary>
    internal enum MusicServiceState
    {
        None = 0,
        Playing = 1,
        Paused = 2
    }

    /// <summary>
    ///     Alignment of text within a field of fixed width.
    /// </summary>
    internal enum TextAlignment
    {
        LeftAlign = 0,
        Center = 1,
        RightAlign = 2
    }

    /// <summary>
    ///     Required privilege level.
    /// </summary>
    internal enum PrivilegeLevel
    {
        /// <summary>
        ///     No privilege required.
        /// </summary>
        None = 0,

        /// <summary>
        ///     Checks ability to participate. (write messages AND join voice)
        /// </summary>
        Normal = 1,

        /// <summary>
        ///     Checks ability to moderate a server. (manage channels OR kick)
        /// </summary>
        Elevated = 2,

        /// <summary>
        ///     Checks server administrator privilege
        /// </summary>
        Administrator = 3
    }
}