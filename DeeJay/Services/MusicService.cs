using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using DeeJay.Definitions;
using DeeJay.Interfaces;
using DeeJay.Model;
using DeeJay.Utility;
using DeeJay.YouTube;
using Discord;
using Discord.Audio;
using NLog;

namespace DeeJay.Services
{
    /// <summary>
    ///     A service that represents music playback for an individual discord server.
    /// </summary>
    public class MusicService : IConnectedService
    {
        internal Song StreamTarget;
        public bool Connected { get; set; }
        internal ValueTask PlayingTask { get; set; }
        internal IAudioClient AudioClient { get; set; }
        internal ulong VoiceChannelId { get; set; }
        internal ulong DesignatedChannelId { get; set; }
        internal MusicServiceState State { get; set; }
        internal byte SlowMode { get; set; }
        public Canceller SongCanceller { get; }
        internal bool InVoice => AudioClient?.ConnectionState == ConnectionState.Connected;
        internal bool Playing => State == MusicServiceState.Playing;
        internal bool Streaming => State == MusicServiceState.Streaming;
        internal Song NowPlaying => Playing ? SongQueue.FirstOrDefault() : null;

        internal ConcurrentQueue<Song> SongQueue { get; }

        internal MusicService(ulong guildId)
        {
            DisconnectCanceller = new Canceller();
            SongCanceller = new Canceller();
            GuildId = guildId;
            SongQueue = new ConcurrentQueue<Song>();
            Log = LogManager.GetLogger($"MscSvc-{guildId}");

            Connected = true;
        }

        /// <summary>
        ///     Determins whether or not a user can queue a song.
        /// </summary>
        /// <param name="userId">The id of the user.</param>
        internal bool CanQueue(ulong userId) => SlowMode == 0 || SlowMode >= SongQueue.Count(song => song.RequestedBy.Id == userId);

        /// <summary>
        ///     Pauses audio playback and joins a voice channel.
        /// </summary>
        /// <param name="channel"></param>
        internal async ValueTask JoinVoiceAsync(IVoiceChannel channel)
        {
            if (channel == null)
                return;

            if (channel.Id == VoiceChannelId && AudioClient?.ConnectionState == ConnectionState.Connected)
                return;

            if (AudioClient?.ConnectionState == ConnectionState.Connected)
                await LeaveVoiceAsync();

            VoiceChannelId = channel.Id;
            AudioClient = await channel.ConnectAsync();
        }

        /// <summary>
        ///     Pauses audio playback and leaves the current voice channel.
        /// </summary>
        internal async ValueTask LeaveVoiceAsync()
        {
            var voiceChannel = await Client.GetVoiceChannelAsync(GuildId, VoiceChannelId);

            if (voiceChannel == null)
                return;

            await PauseAsync();
            await voiceChannel.DisconnectAsync();
            AudioClient?.Dispose();
            VoiceChannelId = 0;
            AudioClient = null;
        }

        /// <summary>
        ///     Pauses playback.
        /// </summary>
        internal async ValueTask<bool> PauseAsync()
        {
            //if the audioclient isnt null
            if (Playing && SongQueue.TryPeek(out var song))
            {
                Log.Info("Pausing playback...");
                State = MusicServiceState.Paused;
                song.Progress.Stop();

                await SongCanceller.CancelAsync();
                return true;
            }

            //incase something nefarious is going on
            if (Playing)
                State = MusicServiceState.None;

            if (Streaming && StreamTarget != null)
            {
                Log.Info("Pausing stream playback...");
                State = MusicServiceState.Paused;
                await SongCanceller.CancelAsync();
                return true;
            }

            //incase something nefarious is going on
            if (Streaming)
                State = MusicServiceState.None;

            return false;
        }

        /// <summary>
        ///     Attempts to enqueue a song to be played.
        /// </summary>
        /// <param name="song">A song to be played.</param>
        internal async ValueTask EnqueueAsync(Song song)
        {
            SongQueue.Enqueue(song);

            if (Streaming)
                await PauseAsync();
        }

        /// <summary>
        ///     Sets a stream to play live when no other music is being played.
        /// </summary>
        /// <param name="stream">A stream to be played.</param>
        internal async ValueTask SetLiveAsync(Song stream)
        {
            if (Streaming)
            {
                await PauseAsync();
                await (StreamTarget?.DisposeAsync() ?? default);
                StreamTarget = stream;
                await PlayAsync();
            } else
            {
                await (StreamTarget?.DisposeAsync() ?? default);
                StreamTarget = stream;
            }
        }

        /// <summary>
        ///     An asynchronous loop that plays songs from the queue until it's empty.
        /// </summary>
        private async ValueTask AsyncPlay()
        {
            //play through the queue until otherwise told
            while (true)
                //if we're not playing a song and there's one available...
                if (!SongQueue.IsEmpty || StreamTarget != null)
                    try
                    {
                        await AudioClient.SetSpeakingAsync(true);
                        await PlaySongAsync();
                    } catch (Exception)
                    {
                        await AudioClient.SetSpeakingAsync(false);
                        return;
                    }
                else //otherwise exit this method
                {
                    State = MusicServiceState.None;
                    await AudioClient.SetSpeakingAsync(false);
                    return;
                }
        }

        /// <summary>
        ///     Begins the playback loop if it wasn't already running.
        /// </summary>
        internal ValueTask PlayAsync()
        {
            if (!Playing && !Streaming)
            {
                Log.Info("Beginning playback...");
                PlayingTask = AsyncPlay();
            }

            return default;
        }

        /// <summary>
        ///     Plays a song in the current voice channel.
        /// </summary>
        private async ValueTask PlaySongAsync()
        {
            var designatedChannel = await Client.GetTextChannelAsync(GuildId, DesignatedChannelId);
            await using var audioStream = AudioClient.CreatePCMStream(AudioApplication.Music, (int) Enums.b128k);

            try
            {
                //if there is a song in the queue, it takes priority
                if (SongQueue.TryPeek(out var song))
                {
                    State = MusicServiceState.Playing;
                    if (designatedChannel != null)
                        await designatedChannel.SendMessageAsync($"Now playing {song.ToString(false)}");

                    song.Progress.Start();
                    await song.StreamAsync(audioStream, SongCanceller);
                    song.Progress.Stop();

                    //if song ended too early, something probably went wrong
                    if (song.Progress.Elapsed < song.Duration.Subtract(TimeSpan.FromSeconds(10)))
                        Log.Error($"Playback failure. {song.ToString(true)}");

                    SongQueue.TryDequeue(out _);
                    await song.DisposeAsync();
                } else if (StreamTarget != null)
                {
                    State = MusicServiceState.Streaming;

                    if (designatedChannel != null)
                        await designatedChannel.SendMessageAsync($"Now streaming {StreamTarget.ToString(false)}");
                    await StreamTarget.StreamAsync(audioStream, SongCanceller);
                }
            } catch (Exception)
            {
                await audioStream.FlushAsync();
                throw;
            }
        }

        /// <summary>
        ///     Pauses the currently playing song and removes it from the queue.
        /// </summary>
        /// <param name="songTask">The song that was skipped</param>
        internal ValueTask<bool> SkipAsync(out ValueTask<Song> songTask)
        {
            var source = new TaskCompletionSource<Song>();
            songTask = new ValueTask<Song>(source.Task);

            async ValueTask<bool> InnerSkipSongAsync()
            {
                if (await PauseAsync() && SongQueue.TryDequeue(out var outSong))
                {
                    Log.Info($"Skipping {outSong.Title}...");

                    await PlayAsync();
                    await outSong.DisposeAsync();

                    source.TrySetResult(outSong);
                    return true;
                }

                source.TrySetResult(default);
                return false;
            }

            return InnerSkipSongAsync();
        }

        /// <summary>
        ///     Removes a song from the queue at a given index.
        /// </summary>
        /// <param name="index">The index in the queue from which the song should be removed. (1-based)</param>
        internal async ValueTask<Song> RemoveSongAsync(int index)
        {
            Song result;

            if (index < 1 || index > SongQueue.Count)
                return null;

            if (index == 1)
            {
                await SkipAsync(out var songTask);
                result = await songTask;
            } else
            {
                Log.Info($"Removing song at index {index}...");
                result = SongQueue.RemoveAt(index);
                await result.DisposeAsync();
            }

            return result;
        }

        /// <summary>
        ///     Clears the song queue.
        /// </summary>
        internal ValueTask ClearQueueAsync()
        {
            if (SongQueue.Count > 0)
            {
                Log.Info("Clearing the queue...");
                SongQueue.Clear();
            }

            return default;
        }

        public async ValueTask ConnectAsync()
        {
            Log.Warn("Connecting...");

            try
            {
                if (Connected)
                {
                    await DisconnectCanceller.CancelAsync();
                    var voiceChannel = await Client.GetVoiceChannelAsync(GuildId, VoiceChannelId);

                    await LeaveVoiceAsync();
                    await JoinVoiceAsync(voiceChannel);
                    await PlayAsync();
                } else
                {
                    Connected = true;
                    await PauseAsync();
                    await LeaveVoiceAsync();
                }
            } catch (Exception e)
            {
                Log.Error(e);
            }
        }

        public async ValueTask DisconnectAsync(bool wait)
        {
            Log.Warn($"Disconnecting... (WaitForReconnect={wait})");
            var token = DisconnectCanceller.Token;

            #pragma warning disable 4014
            if (wait)
                Task.Run(async () =>
                {
                    await Task.Delay(3000);

                    if (token.IsCancellationRequested)
                        Log.Warn("Disconnect canceled. Reconnect successful.");
                    else
                        await DisconnectAsync(false);
                });
            #pragma warning restore 4014
            else if (Connected)
            {
                Connected = false;
                await LeaveVoiceAsync();
            }
        }

        public Canceller DisconnectCanceller { get; }

        public ulong GuildId { get; }
        public Logger Log { get; }
    }
}