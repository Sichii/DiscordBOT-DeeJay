using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace DeeJay
{
    internal sealed class Client
    {
        internal static Task CurrentTask;
        private readonly DiscordSocketClient SocketClient;
        private readonly CommandHandler Commands;
        private string Token;

        internal Client()
        {
            SocketClient = new DiscordSocketClient(new DiscordSocketConfig
            {
                LogLevel = LogSeverity.Debug
            });
            Commands = new CommandHandler();
        }

        internal async Task Initialize()
        {
            Task init = Commands.Initialize();
            Token = await File.ReadAllTextAsync(CONSTANTS.TOKEN_PATH);

            SocketClient.Log += (msg) => Task.Run(() => { Console.WriteLine(msg.Message); });
            SocketClient.MessageReceived += SocketReceive;
            SocketClient.Ready += SocketReady;
            await init;
            await SocketClient.LoginAsync(TokenType.Bot, Token);
            await SocketClient.StartAsync();
        }

        private async Task SocketReceive(SocketMessage msg) => await Commands.TryHandle(SocketClient, msg);
        private async Task SocketReady() => await SocketClient.SetGameAsync("hard to get", "https://www.youtube.com/", ActivityType.Playing);
    }
}
