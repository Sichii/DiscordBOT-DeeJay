using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;
using DeeJay.Definitions;
using DeeJay.DiscordModel;
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
        internal static ConcurrentDictionary<ulong, MusicService> Services { get; }
        internal static DiscordSocketClient SocketClient { get; }

        static Client()
        {
            Token = File.ReadAllText(CONSTANTS.TOKEN_PATH);
            Services = new ConcurrentDictionary<ulong, MusicService>();
            SocketClient = new DiscordSocketClient(new DiscordSocketConfig { LogLevel = LogSeverity.Info });
            Log = LogManager.GetLogger("Client");

            //set up the discord client to log things and act on messages people send
            SocketClient.Log += msg => LogMessage(msg.Severity, msg.Message);
            SocketClient.MessageReceived += CommandHandler.TryHandleAsync;
            SocketClient.Connected += () =>
            {
                foreach (var kvp in Services)
                    Reconnect(kvp.Key);
                return Task.CompletedTask;
            };

            SocketClient.Ready += () => SocketClient.SetActivityAsync(new DiscordActivity("hard to get (!help)", ActivityType.Playing));
        }

        internal static async void Reconnect(ulong guildId)
        {
            if (Services.TryGetValue(guildId, out var musicService) && musicService.Playing)
            {
                await musicService.JoinVoiceAsync(musicService.VoiceChannel);
                await musicService.PlayAsync();
                Log.Error($"Reconnecting and resuming playback for {guildId}-{musicService.VoiceChannel.Name}");
            }
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