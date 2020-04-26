using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DeeJay.Definitions;
using DeeJay.Discord;
using DeeJay.Services;
using Discord;
using Discord.WebSocket;
using NLog;

namespace DeeJay.Model
{
    /// <summary>
    ///     The bot's discord client. Used for interacting with discord.
    /// </summary>
    internal static class Client
    {
        private static readonly string Token;
        private static readonly Logger Log;
        private static readonly DiscordSocketClient SocketClient;
        internal static ulong Id => SocketClient.CurrentUser.Id;
        internal static bool Connected => SocketClient.ConnectionState == ConnectionState.Connected;
        internal static ConcurrentDictionary<ulong, ServiceProvider> Providers { get; }

        static Client()
        {
            Token = File.ReadAllText(CONSTANTS.TOKEN_PATH);
            Providers = new ConcurrentDictionary<ulong, ServiceProvider>();
            SocketClient = new DiscordSocketClient(new DiscordSocketConfig { LogLevel = LogSeverity.Info });
            Log = LogManager.GetLogger("Client");

            //set up the discord client to log things and act on messages people send
            SocketClient.Log += msg => LogMessage(msg.Severity, msg.Message);
            SocketClient.MessageReceived += msg => CommandHandler.TryHandleAsync(SocketClient, msg);
            SocketClient.Disconnected += ex =>
            {
                Log.Error($"Disconnecting because \"{ex.Message}\"");
                foreach (var service in Providers.SelectMany(kvp => kvp.Value.ConnectedServices))
                    service.DisconnectAsync(true);

                return Task.CompletedTask;
            };
            SocketClient.Connected += () =>
            {
                foreach (var service in Providers.SelectMany(kvp => kvp.Value.ConnectedServices))
                    service.ConnectAsync();

                return Task.CompletedTask;
            };

            SocketClient.Ready += () =>
                SocketClient.SetActivityAsync(new Activity("hard to get (!help)", ActivityType.Playing, ActivityProperties.None,
                    string.Empty));
        }

        /// <summary>
        ///     Attempts to retreive a guild object for a given guild id. Retries if it's unavailable.
        /// </summary>
        /// <param name="guildId">A guild unique identifier.</param>
        internal static async ValueTask<SocketGuild> GetGuildAsync(ulong guildId)
        {
            var now = DateTime.UtcNow;
            var result = SocketClient.GetGuild(guildId);

            while (DateTime.UtcNow.Subtract(now)
                       .TotalSeconds < 1.5 && result == null)
            {
                await Task.Delay(250);
                result = SocketClient.GetGuild(guildId);
            }

            return result;
        }

        /// <summary>
        ///     Attempts to retreive a text channel for a given guild.
        /// </summary>
        /// <param name="guildId">A guild unique identifier.</param>
        /// <param name="textChannelId">A text channel unique identifier.</param>
        internal static async ValueTask<SocketTextChannel> GetTextChannelAsync(ulong guildId, ulong textChannelId)
        {
            var guild = await GetGuildAsync(guildId);
            return guild?.GetTextChannel(textChannelId);
        }

        /// <summary>
        ///     Attempts to retreive a voice channel for a given guild.
        /// </summary>
        /// <param name="guildId">A guild unique identifier.</param>
        /// <param name="voiceChannelId">A voice channel unique identifier.</param>
        internal static async ValueTask<SocketVoiceChannel> GetVoiceChannelAsync(ulong guildId, ulong voiceChannelId)
        {
            var guild = await GetGuildAsync(guildId);
            return guild?.GetVoiceChannel(voiceChannelId);
        }

        /// <summary>
        ///     Logs the bot into discord.
        /// </summary>
        internal static async Task Login()
        {
            await SocketClient.LoginAsync(TokenType.Bot, Token);
            await SocketClient.StartAsync();
        }

        /// <summary>
        ///     Interceptor for logging messages from Discord.NET
        /// </summary>
        /// <param name="severity">Specifies the severity of the log message.</param>
        /// <param name="message">The message to be logged.</param>
        /// <returns></returns>
        internal static Task LogMessage(LogSeverity severity, string message)
        {
            switch (severity)
            {
                case LogSeverity.Critical:
                case LogSeverity.Error:
                    Log.Error(message);
                    break;
                case LogSeverity.Warning:
                    Log.Warn(message);
                    break;
                case LogSeverity.Info:
                    Log.Info(message);
                    break;
                case LogSeverity.Verbose:
                    Log.Trace(message);
                    break;
                case LogSeverity.Debug:
                    Log.Debug(message);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(severity), severity, null);
            }

            return Task.CompletedTask;
        }
    }
}