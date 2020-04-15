namespace DeeJay.Definitions
{
    /// <summary>
    ///     Bitrate of a stream expressed in bits per second. (1kb = 1024b)
    /// </summary>
    internal enum BitRate
    {
        b64k = 65536,
        b128k = 131072
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
}