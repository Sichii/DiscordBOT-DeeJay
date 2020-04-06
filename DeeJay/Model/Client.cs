using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;
using DeeJay.Command;
using DeeJay.Definitions;
using Discord;
using Discord.WebSocket;
using NLog;

namespace DeeJay.Model
{
    internal static class Client
    {
        internal static ConcurrentDictionary<ulong, GuildMusicService> Services { get; }
        private static readonly string Token;
        private static readonly DiscordSocketClient SocketClient;
        private static readonly Logger Log;


        static Client()
        {
            Token = File.ReadAllText(CONSTANTS.TOKEN_PATH);
            Services = new ConcurrentDictionary<ulong, GuildMusicService>();
            SocketClient = new DiscordSocketClient(new DiscordSocketConfig { LogLevel = LogSeverity.Info });
            Log = LogManager.GetLogger("Client");

            //set up the discord client to log things and act on messages people send
            SocketClient.Log += msg => LogMessage(msg.Severity, msg.Message);
            SocketClient.MessageReceived += msg => CommandHandler.TryHandleAsync(SocketClient, msg);
            SocketClient.Ready += () => SocketClient.SetActivityAsync(new DiscordActivity("hard to get (!help)", ActivityType.Playing));
        }

        /// <summary>
        ///     Initialize non-static variables, and set event handlers
        /// </summary>
        internal static async Task Initialize()
        {
            await SocketClient.LoginAsync(TokenType.Bot, Token);
            await SocketClient.StartAsync();
        }

        internal static Task LogMessage(LogSeverity severity, string message)
        {
            switch(severity)
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