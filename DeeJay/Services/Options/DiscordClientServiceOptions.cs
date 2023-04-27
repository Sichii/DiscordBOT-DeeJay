namespace DeeJay.Services.Options;

/// <summary>
///    The options for the <see cref="DiscordClientService"/>
/// </summary>
public sealed class DiscordClientServiceOptions
{
    /// <summary>
    ///    The token to use for the discord client
    /// </summary>
    public string TokenPath { get; init; } = null!;

    /// <summary>
    /// The value of the token (either read from the path or entered in the config directly)
    /// </summary>
    public string TokenValue { get; set; } = null!;
}