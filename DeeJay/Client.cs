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
        private static readonly CommandHandler Commands; //this isnt static because commands need context
        private static string Token;
        

        internal static ConcurrentQueue<Song> SongQueue { get; }
        internal static IVoiceChannel VoiceChannel { get; set; }
        internal static IAudioClient AudioClient { get; set; }
        internal static DiscordSocketClient SocketClient { get; }
        private static Task PlayingTask;
        internal static bool Playing => PlayingTask?.Status == TaskStatus.Running;
        
        static Client()
        {
            CancellationTokenSource = new CancellationTokenSource();
            SongQueue = new ConcurrentQueue<Song>();
            Commands = new CommandHandler();
            SocketClient = new DiscordSocketClient(new DiscordSocketConfig
            {
                LogLevel = LogSeverity.Info
            });
            PlayingTask = Play();
        }

        /// <summary>
        /// Initialize non-static variables, and set event handlers
        /// </summary>
        internal static async Task Initialize()
        {
            //initialize non-static membere (commands, token)
            await Commands.Initialize();
            Token = await File.ReadAllTextAsync(CONSTANTS.TOKEN_PATH);

            //set up the discord client to log things and act on messages people send
            SocketClient.Log += msg => Task.Run(() => Console.WriteLine(msg.Message));
            SocketClient.MessageReceived += msg => Commands.TryHandleAsync(msg);
            SocketClient.Ready += () => SocketClient.SetActivityAsync(new DiscordActivity("hard to get (!help)", ActivityType.Playing));

            await SocketClient.LoginAsync(TokenType.Bot, Token);
            await SocketClient.StartAsync();
        }

        /// <summary>
        /// Joins a voice channel.
        /// </summary>
        /// <param name="channel"></param>
        internal static async Task JoinVoiceAsync(IVoiceChannel channel)
        {
            if (VoiceChannel != channel)
            {
                await StopSongAsync();
                VoiceChannel = channel;
                AudioClient = await channel.ConnectAsync();
            }
        }

        /// <summary>
        /// Pauses audio playback and leaves the current voice channel.
        /// </summary>
        internal static async Task LeaveVoiceAsync()
        {
            await StopSongAsync();
            await VoiceChannel.DisconnectAsync();
            AudioClient.Dispose();

            VoiceChannel = null;
            AudioClient = null;
        }

        private static async Task PlayTask()
        {
            //play through the queue until otherwise told
            while (true)
                //if we're not playing a song and there's one available...
                if (!SongQueue.IsEmpty)
                    try
                    {
                        await PlayNextSongAsync();
                    } catch (OperationCanceledException)
                    {
                        //thread abort exceptions from pausing/leaving will propogate to here, reset the token just incase it was something else
                        CancellationTokenSource = new CancellationTokenSource();
                        Console.WriteLine("COMMAND - Playback paused.");
                        return;
                    }
                else //otherwise exit this method
                    return;
        }

        internal static Task Play()
        {
            if (!Playing)
                PlayingTask = PlayTask();

            return Task.CompletedTask;
        }

        /// <summary>
        /// Plays a song in the current voice channel.
        /// </summary>
        internal static async Task PlayNextSongAsync()
        {
            if (SongQueue.TryPeek(out var song))
            {
                await SocketClient.SetActivityAsync(new DiscordActivity($"Playing {song.Title}!", ActivityType.Listening));

                using var ffmpeg = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = CONSTANTS.FFMPEG_PATH,
                        //no text, seek to previously elapsed if necessary, 2 channel, 75% volume, pcm s16le stream format, 48000hz, pipe 1
                        Arguments =
                            $"-hide_banner -loglevel panic {(song.Progress.ElapsedTicks > 0 ? $"-ss {song.Progress.Elapsed:c}" : string.Empty)} -i \"{song.DirectLink}\" -ac 2 -af volume=0.75 -f s16le -ar 48000 pipe:1",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true
                    }
                };

                ffmpeg.Start();

                await using var audioStream = AudioClient.CreatePCMStream(AudioApplication.Music);
                try
                {
                    await AudioClient.SetSpeakingAsync(true);
                    song.Progress.Start();
                    await ffmpeg.StandardOutput.BaseStream.CopyToAsync(audioStream, CancellationTokenSource.Token);
                } finally
                {
                    await audioStream.FlushAsync(CancellationTokenSource.Token);
                    song.Progress.Stop();

                    if (song.Progress.Elapsed > song.Duration.Subtract(TimeSpan.FromSeconds(5)))
                        SongQueue.TryDequeue(out _);
                    else
                        Console.WriteLine(
                            $"Reached end of stream before playback finished. Song: {song.Title} [{song.Duration.ToReadableString()} / {song.Progress.Elapsed.ToReadableString()}]");
                }
            }
        }

        /// <summary>
        /// Stops playback of the current audio stream, if there is one.
        /// </summary>
        /// <returns></returns>
        internal static Task<bool> StopSongAsync()
        {
            //if the audioclient isnt null
            if (Playing && SongQueue.TryPeek(out var song))
            {
                song.Progress.Stop();
                CancellationTokenSource.Cancel();
                CancellationTokenSource = new CancellationTokenSource();

                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }

        internal static async Task<bool> SkipSongAsync()
        {
            if (await StopSongAsync() && SongQueue.TryDequeue(out _))
            {
                await Play();
                return true;
            }

            return false;
        }

        internal static async Task<bool> RemoveSongAsync(int index)
        {
            if (index < 1)
                return false;

            switch (index)
            {
                case 1:
                    return await SkipSongAsync();
                default:
                    SongQueue.RemoveAt(index);
                    return true;
            }
        }
    }
}
