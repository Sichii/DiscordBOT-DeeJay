using Discord;

namespace DeeJay.Abstractions;

/// <summary>
/// Defines the methods required to stream media
/// </summary>
public interface IStreamingService
{
    /// <summary>
    /// Sets the designated text channel for the bot
    /// </summary>
    /// <param name="context">The context of the command</param>
    Task SetDesignatedChannelAsync(IInteractionContext context);
    
    /// <summary>
    /// Sets slow mode for the guild
    /// </summary>
    /// <param name="context">The context of the command</param>
    /// <param name="amountPerPerson">The maximum amount of songs allowed per person. -1 to remove slow mode.</param>
    Task SetSlowModeAsync(IInteractionContext context, int amountPerPerson);

    /// <summary>
    /// Queues a song for search and playback
    /// </summary>
    /// <param name="context">The context of the command</param>
    /// <param name="arg">The argument the context provided</param>
    /// <returns></returns>
    Task QueueSongAsync(IInteractionContext context, string arg);

    /// <summary>
    /// Plays the next song in the queue if the bot is in a voice channel and not already playing a song
    /// </summary>
    /// <param name="context">The context of the command</param>
    Task PlayAsync(IInteractionContext context);
    
    /// <summary>
    /// Pauses the current song if the bot is in a voice channel and playing a song
    /// </summary>
    /// <param name="context">The context of the command</param>
    Task PauseAsync(IInteractionContext context);

    /// <summary>
    /// Skips the current song if the bot is in a voice channel and currently playing a song
    /// </summary>
    /// <param name="context">The context of the command</param>
    Task SkipAsync(IInteractionContext context);

    /// <summary>
    /// Sets a live stream that will play when the queue is empty
    /// </summary>
    /// <param name="context">The context of the command</param>
    /// <param name="uri">The uri to the live stream</param>
    /// <returns></returns>
    Task SetLiveAsync(IInteractionContext context, Uri uri);
    
    /// <summary>
    /// Joins the voice channel of the context
    /// </summary>
    /// <param name="context">The context of the command</param>
    Task JoinVoiceAsync(IInteractionContext context);

    /// <summary>
    /// Leaves the voice channel of the context
    /// </summary>
    /// <param name="context">The context of the command</param>
    Task LeaveVoiceAsync(IInteractionContext context);
    
    /// <summary>
    /// Removes a song from the queue by index
    /// </summary>
    /// <param name="context">The context of the command</param>
    /// <param name="index">The 0-based index of the song to remove</param>
    /// <returns></returns>
    Task RemoveSongAsync(IInteractionContext context, int index);
    
    /// <summary>
    /// Clears the queue
    /// </summary>
    /// <param name="context">The context of the command</param>
    Task ClearQueueAsync(IInteractionContext context);
    
    /// <summary>
    /// Gets the song queue
    /// </summary>
    /// <param name="context">The context of the command</param>
    IAsyncEnumerable<ISong> GetQueueAsync(IInteractionContext context);
    
    /// <summary>
    /// Gets the currently playing song
    /// </summary>
    ISong? NowPlaying { get; }
}