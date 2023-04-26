using Discord;

namespace DeeJay.Abstractions;

/// <summary>
/// Defines the methods required to provide streaming services
/// </summary>
public interface IStreamingServiceProvider
{
    /// <summary>
    /// Gets the streaming service for the specified guild
    /// </summary>
    public IStreamingService GetStreamingService(IGuild guild);
}