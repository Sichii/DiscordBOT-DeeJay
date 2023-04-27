using DeeJay.Abstractions;

namespace DeeJay.Models;

/// <summary>
/// Represents the options for a guild
/// </summary>
public sealed class GuildOptions : IGuildOptions
{
    /// <inheritdoc />
    public int? MaxSongsPerPerson { get; set; }
    /// <inheritdoc />
    public ulong? DesignatedTextChannelId { get; set; }
}