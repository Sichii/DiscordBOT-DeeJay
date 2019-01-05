using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Discord.Audio;

namespace DeeJay
{
    internal static class Client
    {
        private readonly static CommandHandler Commands;
        private static string Token;

        internal static Tuple<IVoiceChannel, IAudioClient> AudioClient { get; set; }
        internal static DiscordSocketClient SocketClient { get; }

        static Client()
        {
            Commands = new CommandHandler();
            AudioClient = new Tuple<IVoiceChannel, IAudioClient>(default, default);
            SocketClient = new DiscordSocketClient(new DiscordSocketConfig
            {
                LogLevel = LogSeverity.Debug
            });
        }

        internal static async Task Initialize()
        {
            Task init = Commands.Initialize();
            Token = (await File.ReadAllTextAsync(CONSTANTS.TOKEN_PATH)).Trim();

            SocketClient.Log += (msg) => Task.Run(() => { Console.WriteLine(msg.Message); });
            SocketClient.MessageReceived += SocketReceive;
            SocketClient.Ready += SocketReady;
            await init;
            await SocketClient.LoginAsync(TokenType.Bot, Token);
            await SocketClient.StartAsync();
        }

        private static async Task SocketReceive(SocketMessage msg) => await Commands.TryHandle(msg);
        private static async Task SocketReady() => await SocketClient.SetGameAsync("hard to get", "https://www.youtube.com/", ActivityType.Playing);

        internal static async Task JoinVoice(IVoiceChannel channel)
        {
            if (AudioClient.Item1?.Id == channel.Id)
                await LeaveVoice();

            AudioClient = new Tuple<IVoiceChannel, IAudioClient>(channel, await channel.ConnectAsync());
        }

        internal static async Task LeaveVoice()
        {
            await StopAudio();
            await AudioClient.Item1.DisconnectAsync();
        }

        internal static async Task PlayAudio(string URL)
        {
            using (var ffmpeg = Process.Start(new ProcessStartInfo
            {
                FileName = CONSTANTS.FFMPEG_PATH,
                Arguments = $"-hide_banner -loglevel quiet -i \"{URL}\" -ac 2 -f s16le -ar 48000 pipe:1",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true
            }))
            using(AudioOutStream audioStream = AudioClient.Item2.CreatePCMStream(AudioApplication.Music))
            {
                try
                {
                    await ffmpeg.StandardOutput.BaseStream.CopyToAsync(audioStream);
                }
                finally { await audioStream.FlushAsync(); }
            }
        }

        internal static async Task StopAudio()
        {
            Task t1 = AudioClient.Item2?.StopAsync() ?? Task.CompletedTask;
            await t1;
            AudioClient.Item2?.Dispose();
        }
    }
}
