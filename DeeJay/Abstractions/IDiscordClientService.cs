using Discord.WebSocket;

namespace DeeJay.Abstractions;

/// <summary>
///    The bot's discord client. Used for interacting with discord.
/// </summary>
public interface IDiscordClientService
{
    /// <summary>
    ///   The discord socket client
    /// </summary>
    DiscordSocketClient SocketClient { get; }
}