using Discord;

namespace DeeJay.Abstractions;

/// <summary>
/// Defines the methods required to create a streaming service
/// </summary>
public interface IStreamingServiceFactory
{
    /// <summary>
    /// Creates a streaming service for the specified guild
    /// </summary>
    IStreamingService Create(IGuild guild);
}