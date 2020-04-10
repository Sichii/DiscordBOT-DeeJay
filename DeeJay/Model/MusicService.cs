using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DeeJay.Definitions;
using Discord;
using Discord.Audio;
using Discord.WebSocket;
using NLog;

namespace DeeJay.Model
{
    /// <summary>
    ///     A service that represents music playback for an individual discord server.
    /// </summary>
    public class MusicService : IServiceProvider
    {
        private readonly Logger Log;
        internal CancellationTokenSource CancellationTokenSource { get; set; }
        internal Task PlayingTask { get; set; }
        internal IAudioClient AudioClient { get; set; }
        internal ulong VoiceChannelId { get; set; }
        internal ulong DesignatedChannelId { get; set; }
        internal ulong GuildId { get; }

        internal SocketTextChannel DesignatedChannel => DesignatedChannelId != 0 ? Guild?.GetTextChannel(DesignatedChannelId) : default;
        internal SocketVoiceChannel VoiceChannel => VoiceChannelId != 0 ? Guild?.GetVoiceChannel(VoiceChannelId) : default;
        internal SocketGuild Guild => GuildId != 0 ? Client.SocketClient.GetGuild(GuildId) : default;
        internal bool InVoice => VoiceChannelId != 0;

        internal ConcurrentQueue<Song> SongQueue { get; }
        internal bool Playing => PlayingTask?.Status == TaskStatus.WaitingForActivation;

        internal MusicService(ulong guildId)
        {
            GuildId = guildId;
            CancellationTokenSource = new CancellationTokenSource();
            SongQueue = new ConcurrentQueue<Song>();
            Log = LogManager.GetLogger($"MscServ-{guildId}");
        }

        /// <summary>
        ///     Pauses audio playback and joins a voice channel.
        /// </summary>
        /// <param name="channel"></param>
        internal async Task JoinVoiceAsync(IVoiceChannel channel)
        {
            if (VoiceChannelId != channel.Id)
            {
                await PauseSongAsync(out _);
                VoiceChannelId = channel.Id;
                AudioClient = await channel.ConnectAsync();
            }
        }

        /// <summary>
        ///     Pauses audio playback and leaves the current voice channel.
        /// </summary>
        internal async Task LeaveVoiceAsync()
        {
            await PauseSongAsync(out _);
            await (VoiceChannel?.DisconnectAsync() ?? Task.CompletedTask);
            AudioClient?.Dispose();

            VoiceChannelId = 0;
            AudioClient = null;
        }

        /// <summary>
        ///     An asynchronous loop that plays songs from the queue until it's empty.
        /// </summary>
        private async Task AsyncPlay()
        {
            //play through the queue until otherwise told
            while (true)
                //if we're not playing a song and there's one available...
                if (!SongQueue.IsEmpty)
                    try
                    {
                        //preload the first 3 songs
                        foreach (var song in SongQueue.Take(3))
                            song.TrySetData();

                        await PlayNextSongAsync();
                    } catch (OperationCanceledException)
                    {
                        await AudioClient.SetSpeakingAsync(false);
                        return;
                    }
                else //otherwise exit this method
                {
                    await AudioClient.SetSpeakingAsync(false);
                    return;
                }
        }

        /// <summary>
        ///     Begins the playback loop if it wasn't already running.
        /// </summary>
        internal Task PlayAsync()
        {
            if (!Playing)
                PlayingTask = AsyncPlay();

            return Task.CompletedTask;
        }

        /// <summary>
        ///     Plays a song in the current voice channel.
        /// </summary>
        internal async Task PlayNextSongAsync()
        {
            if (SongQueue.TryPeek(out var song))
            {
                var token = CancellationTokenSource.Token;
                var dataStream = await song.DataTask;

                await using var audioStream = AudioClient.CreatePCMStream(AudioApplication.Music, (int) BitRate.b128k);

                //seek if we paused
                if (song.Progress.Elapsed > TimeSpan.Zero)
                    await song.AutoSeekAsync();

                try
                {
                    song.Progress.Start();

                    await (DesignatedChannel?.SendMessageAsync($"Now playing {song.ToString(false)}") ?? Task.CompletedTask);

                    await dataStream.CopyToAsync(audioStream, CancellationTokenSource.Token);
                } finally
                {
                    if (!token.IsCancellationRequested)
                    {
                        await audioStream.FlushAsync(CancellationTokenSource.Token);
                        song.Progress.Stop();

                        if (song.Progress.Elapsed < song.Duration.Subtract(TimeSpan.FromSeconds(10)))
                            Log.Error($"Playback failure. {song.ToString(true)}");
                        else
                            await song.DisposeAsync();

                        SongQueue.TryDequeue(out _);
                    }
                }
            }
        }

        /// <summary>
        ///     Pauses playback.
        /// </summary>
        /// <param name="song">The song that was paused.</param>
        internal Task<bool> PauseSongAsync(out Song song)
        {
            song = default;

            //if the audioclient isnt null
            if (Playing && SongQueue.TryPeek(out song))
            {
                song.Progress.Stop();
                CancellationTokenSource.Cancel();
                CancellationTokenSource.Dispose();
                CancellationTokenSource = new CancellationTokenSource();

                Log.Info("Playback paused.");
                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }

        /// <summary>
        ///     Pauses the currently playing song and removes it from the queue.
        /// </summary>
        /// <param name="songTask">The song that was skipped</param>
        internal Task<bool> SkipSongAsync(out ValueTask<Song> songTask)
        {
            var source = new TaskCompletionSource<Song>();
            songTask = new ValueTask<Song>(source.Task);

            async Task<bool> InnerSkipSongAsync()
            {
                if (await PauseSongAsync(out _) && SongQueue.TryDequeue(out var outSong))
                {
                    await PlayAsync();
                    await outSong.DisposeAsync();

                    source.SetResult(outSong);
                    return true;
                }

                source.SetResult(default);
                return false;
            }

            return InnerSkipSongAsync();
        }

        /// <summary>
        ///     Removes a song from the queue at a given index.
        /// </summary>
        /// <param name="index">The index in the queue from which the song should be removed. (1-based)</param>
        internal async Task<Song> RemoveSongAsync(int index)
        {
            Song result;

            if (index < 1)
                return null;

            if (index == 1)
            {
                await SkipSongAsync(out var songTask);
                result = await songTask;
            } else
            {
                result = SongQueue.RemoveAt(index);
                await result.DisposeAsync();
            }

            return result;
        }

        /// <inheritdoc />
        public object GetService(Type serviceType) => this;
    }
}