using System.Diagnostics;
using DeeJay.Abstractions;
using Discord;

namespace DeeJay.Models;

/// <summary>
/// Represents a streamable song
/// </summary>
public sealed class Song : ISong
{
    /// <summary>
    /// The progress of the song
    /// </summary>
    public Stopwatch Progress { get; }
    /// <summary>
    /// The user who requested the song
    /// </summary>
    public IGuildUser RequestedBy { get; }
    /// <summary>
    /// The search result the song came from
    /// </summary>
    public ISearchResult SearchOrigin { get; }
    /// <summary>
    /// The URI of the song
    /// </summary>
    public Uri Uri => SearchOrigin.Uri!;
    /// <summary>
    /// The title of the song
    /// </summary>
    public string Title => SearchOrigin.Title!;
    /// <summary>
    /// The duration of the song
    /// </summary>
    public TimeSpan Duration => SearchOrigin.Duration!.Value;
    /// <summary>
    /// Whether or not the song is currently playing
    /// </summary>
    public bool Playing => Progress.IsRunning;
    /// <summary>
    /// Whether or not the stream is a live stream
    /// </summary>
    public bool IsLive { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Song"/> class.
    /// </summary>
    /// <param name="requestedBy">Who the song was requested by</param>
    /// <param name="origin">The search result the song came from</param>
    /// <param name="isLive">Whether or not the stream is a live stream</param>
    public Song(IGuildUser requestedBy, ISearchResult origin, bool isLive)
    {
        RequestedBy = requestedBy;
        SearchOrigin = origin;
        IsLive = isLive;
        Progress = new Stopwatch();
    }

    /// <summary>
    /// Starts the song
    /// </summary>
    public void Start() => Progress.Start();

    /// <summary>
    /// Stops the song
    /// </summary>
    public void Stop() => Progress.Stop();

    /// <summary>
    /// The current elapsed progress of the song
    /// </summary>
    public TimeSpan Elapsed => Progress.Elapsed;
}