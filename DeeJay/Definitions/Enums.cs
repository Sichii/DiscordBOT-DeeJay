namespace DeeJay.Definitions;

/// <summary>
///     Bitrate of a stream expressed in bits per second. (1kb = 1024b)
/// </summary>
public enum BitRate
{
    /// <summary>
    /// 64kbps
    /// </summary>
    b64k = 65536,
    /// <summary>
    /// 128kbps
    /// </summary>
    b128k = 131072
}

/// <summary>
///     Required privilege level.
/// </summary>
public enum Privilege
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

/// <summary>
///    The state of the music streaming service.
/// </summary>
public enum MusicStreamingServiceState
{
    /// <summary>
    ///    The service is idle.
    /// </summary>
    Idle,
    /// <summary>
    ///   The service is playing music from the queue.
    /// </summary>
    Playing,
    /// <summary>
    ///   The service is streaming a live stream.
    /// </summary>
    Streaming,
    /// <summary>
    ///  The service is paused.
    /// </summary>
    Paused
}
    
/// <summary>
/// An action to be performed on the state
/// </summary>
public enum StateAction
{
    /// <summary>
    /// Start playing music
    /// </summary>
    Play,
    /// <summary>
    /// Stop playing music
    /// </summary>
    Pause,
    /// <summary>
    /// Skip the current song
    /// </summary>
    Skip
}