namespace DeeJay.Abstractions;

/// <summary>
/// Defines the options for a guild
/// </summary>
public interface IGuildOptions
{
    /// <summary>
    /// The max number of songs allowed per person in slow mode
    /// </summary>
    int? MaxSongsPerPerson { get; set; }
    /// <summary>
    /// The id of the text channel to send messages to
    /// </summary>
    ulong? DesignatedTextChannelId { get; set; }
}