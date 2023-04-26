namespace DeeJay.Services.Options;

/// <summary>
///    The options for the <see cref="DiscordClientService"/>
/// </summary>
public sealed class DiscordClientServiceOptions
{
    /// <summary>
    ///    The token to use for the discord client
    /// </summary>
    public string Token { get; init; } = null!;
}