using Discord.Audio;

namespace DeeJay.Abstractions;

/// <summary>
/// Defines the methods to control a stream
/// </summary>
public interface IStreamPlayer
{
    /// <summary>
    /// Whether or not the stream has ended
    /// </summary>
    bool EoS { get; }
    
    /// <summary>
    /// Plays the stream
    /// </summary>
    Task PlayAsync(IAudioClient audioClient);

    /// <summary>
    /// Stops the stream
    /// </summary>
    Task StopAsync();
}