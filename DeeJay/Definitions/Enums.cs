﻿namespace DeeJay.Definitions;

/// <summary>
///     Bitrate of a stream expressed in bits per second. (1kb = 1024b)
/// </summary>
internal enum BitRate
{
    b64k = 65536,
    b128k = 131072
}

/// <summary>
///     Required privilege level.
/// </summary>
internal enum Privilege
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

internal enum MusicStreamingServiceState
{
    Idle,
    Playing,
    Streaming,
    Paused
}
    
internal enum StateAction
{
    Play,
    Pause,
    Skip
}