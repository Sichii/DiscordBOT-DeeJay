using System.Diagnostics;
using DeeJay.Models;
using Discord;

namespace DeeJay.Abstractions;

/// <summary>
/// Defines the methods and properties of a streamable uri
/// </summary>
public interface ISong
{
    /// <summary>
    /// The progress of the song
    /// </summary>
    Stopwatch Progress { get; }
    /// <summary>
    /// The user who requested the song
    /// </summary>
    IGuildUser RequestedBy { get; }
    /// <summary>
    /// The search result the song came from
    /// </summary>
    ISearchResult SearchOrigin { get; }
    /// <summary>
    /// The URI of the song
    /// </summary>
    Uri Uri { get; }
    /// <summary>
    /// The title of the song
    /// </summary>
    string Title { get; }
    /// <summary>
    /// The duration of the song
    /// </summary>
    TimeSpan Duration { get; }
    /// <summary>
    /// Whether or not the song is currently playing
    /// </summary>
    bool Playing { get; }
    /// <summary>
    /// Whether or not the stream is a live stream
    /// </summary>
    bool IsLive { get; }
    /// <summary>
    /// The current elapsed progress of the song
    /// </summary>
    TimeSpan Elapsed { get; }

    /// <summary>
    /// Starts the song
    /// </summary>
    void Start();

    /// <summary>
    /// Stops the song
    /// </summary>
    void Stop();
}