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
using System.Collections.Concurrent;
using System.Threading;

namespace DeeJay
{
    internal static class Client
    {
        internal static CancellationTokenSource CancellationTokenSource;
        private readonly static CommandHandler Commands;
        private static string Token;

        internal static ConcurrentQueue<Song> SongQueue { get; }
        internal static Tuple<IVoiceChannel, IAudioClient> AudioClient { get; set; }
        internal static DiscordSocketClient SocketClient { get; }

        static Client()
        {
            CancellationTokenSource = new CancellationTokenSource();
            SongQueue = new ConcurrentQueue<Song>();
            Commands = new CommandHandler();
            AudioClient = new Tuple<IVoiceChannel, IAudioClient>(default, default);
            SocketClient = new DiscordSocketClient(new DiscordSocketConfig
            {
                LogLevel = LogSeverity.Info
            });
        }

        internal static async Task Initialize()
        {
            Task init = Commands.Initialize();
            Token = (await File.ReadAllTextAsync(CONSTANTS.TOKEN_PATH)).Trim();

            SocketClient.Log += (msg) => Task.Run(() => { Console.WriteLine(msg.Message); });
            SocketClient.MessageReceived += SocketReceiveAsync;
            SocketClient.Ready += SocketReadyAsync;
            await init;
            await SocketClient.LoginAsync(TokenType.Bot, Token);
            await SocketClient.StartAsync();
        }

        private static Task SocketReceiveAsync(SocketMessage msg) => Commands.TryHandleAsync(msg);
        private static Task SocketReadyAsync() => SocketClient.SetGameAsync("hard to get", "https://www.youtube.com/watch?v=", ActivityType.Playing);

        internal static async Task JoinVoiceAsync(IVoiceChannel channel) => AudioClient = new Tuple<IVoiceChannel, IAudioClient>(channel, await channel.ConnectAsync());

        internal static async Task LeaveVoiceAsync() => await AudioClient.Item1.DisconnectAsync();

        internal static async Task PlayAudioAsync(Song song, bool seek = false)
        {
            if (!seek || (seek && Song.PlayTime.Elapsed < song.Duration))
                using (var ffmpeg = Process.Start(new ProcessStartInfo
                {
                    FileName = CONSTANTS.FFMPEG_PATH,
                    Arguments = $"-hide_banner -loglevel quiet {(seek ? $"-ss {Song.PlayTime.Elapsed.ToString("c")}" : string.Empty)} -i \"{song.DirectLink}\" -ac 2 -af volume=0.75 -f s16le -ar 48000 pipe:1",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true
                }))
                using (AudioOutStream audioStream = AudioClient.Item2.CreatePCMStream(AudioApplication.Music))
                {
                    Song.PlayTime.Start();
                    try
                    {
                        await ffmpeg.StandardOutput.BaseStream.CopyToAsync(audioStream, CancellationTokenSource.Token);
                    }
                    catch (OperationCanceledException ex) { throw ex; }
                    catch (Exception ex) { throw ex; }
                    finally
                    {
                        await audioStream.FlushAsync();
                        ffmpeg.Kill();
                    }
                }

            Song.PlayTime.Reset();
            SongQueue.TryDequeue(out Song s);
        }

        internal static Task StopAudioAsync()
        {
            if (AudioClient.Item2 != null)
            {
                CancellationTokenSource.Cancel();
                CancellationTokenSource = new CancellationTokenSource();
            }

            return Task.CompletedTask;
        }
    }
}
