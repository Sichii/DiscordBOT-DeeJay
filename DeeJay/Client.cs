using System;
using System.Diagnostics;
using System.IO;
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
        private readonly static CommandHandler Commands; //this isnt static because commands need context
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

        /// <summary>
        /// Initialize non-static variables, and set event handlers
        internal static async Task Initialize()
        {
            //initialize non-static membere (commands, token)
            Task init = Commands.Initialize();
            Token = (await File.ReadAllTextAsync(CONSTANTS.TOKEN_PATH)).Trim();

            //set up the discord client to log things and act on messages people send
            SocketClient.Log += (msg) => Task.Run(() => { Console.WriteLine(msg.Message); });
            SocketClient.MessageReceived += (msg) => Commands.TryHandleAsync(msg);
            SocketClient.Ready += () => SocketClient.SetGameAsync("hard to get", "https://www.youtube.com/watch?v=", ActivityType.Playing);

            await init;
            await SocketClient.LoginAsync(TokenType.Bot, Token);
            await SocketClient.StartAsync();
        }

        /// <summary>
        /// Joins a voice channel.
        /// </summary>
        /// <param name="channel"></param>
        internal static async Task JoinVoiceAsync(IVoiceChannel channel) => AudioClient = new Tuple<IVoiceChannel, IAudioClient>(channel, await channel.ConnectAsync());

        /// <summary>
        /// Pauses audio playback and leaves the current voice channel.
        /// </summary>
        internal static async Task LeaveVoiceAsync() => await AudioClient.Item1.DisconnectAsync();

        /// <summary>
        /// Plays a song in the current voice channel.
        /// </summary>
        /// <param name="song">The song to play.</param>
        /// <param name="seek">Whether or not to seek to a position in the song.</param>
        internal static async Task PlayAudioAsync(Song song, bool seek = false)
        {
            //if we're being told to seek, but we've elapsed beyond the song duration, skip audio playback
            if (!seek || (seek && Song.PlayTime.Elapsed < song.Duration))
                //use FFMPEG
                using (var ffmpeg = Process.Start(new ProcessStartInfo
                {
                    FileName = CONSTANTS.FFMPEG_PATH,
                    //no text, seek to previously elapsed if necessary, 2 channel, 75% volume, pcm s16le stream format, 48000hz, pipe 1
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
                    //propogate exceptions to outer try-catch
                    catch (OperationCanceledException ex) { throw ex; }
                    catch (Exception ex) { throw ex; }
                    finally
                    {
                        //in the cast of failure, empty the stream
                        await audioStream.FlushAsync();

                        //kill the ffmpeg process if its still running
                        if (!ffmpeg.HasExited)
                            ffmpeg.Kill();
                    }
                }

            //in the case where internet hiccups and the stream takes a dump, catch playback progress and allow outer retry
            if(Song.PlayTime.Elapsed < song.Duration)
            {
                Console.WriteLine($"Playback error, recovering...");
                Song.PlayTime.Stop();
                return;
            }

            //song successfully completed, reset playback timer and dequeue the song that completed
            Song.PlayTime.Reset();
            SongQueue.TryDequeue(out Song s);
        }

        /// <summary>
        /// Stops playback of the current audio stream, if there is one.
        /// </summary>
        /// <returns></returns>
        internal static Task StopAudioAsync()
        {
            //if the audioclient isnt null
            if (AudioClient.Item2 != null)
            {
                //cancel the current playback, reset the token
                CancellationTokenSource.Cancel();
                CancellationTokenSource = new CancellationTokenSource();
            }

            //otherwise we're gucci
            return Task.CompletedTask;
        }
    }
}
