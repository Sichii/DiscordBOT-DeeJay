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
using Discord.WebSocket;
using NLog;

namespace DeeJay.Services
{
    /// <summary>
    ///     A service that represents music playback for an individual discord server.
    /// </summary>
    public class MusicService : IConnectedService
    {
        public bool Connected { get; set; }
        internal Task PlayingTask { get; set; }
        internal IAudioClient AudioClient { get; set; }
        internal ulong VoiceChannelId { get; set; }
        internal ulong DesignatedChannelId { get; set; }
        internal MusicServiceState State { get; set; }
        internal SocketTextChannel DesignatedChannel => DesignatedChannelId != 0 ? Guild?.GetTextChannel(DesignatedChannelId) : default;
        internal SocketVoiceChannel VoiceChannel => VoiceChannelId != 0 ? Guild?.GetVoiceChannel(VoiceChannelId) : default;
        internal SocketGuild Guild => GuildId != 0 ? Client.GetGuild(GuildId) : default;
        internal bool InVoice => AudioClient?.ConnectionState == ConnectionState.Connected;
        internal bool Playing => State == MusicServiceState.Playing;

        internal ConcurrentQueue<Song> SongQueue { get; }
        private Canceller Canceller { get; }

        internal MusicService(ulong guildId)
        {
            GuildId = guildId;
            Canceller = Canceller.New;
            SongQueue = new ConcurrentQueue<Song>();
            Log = LogManager.GetLogger($"MscServ-{guildId}");
            Connected = true;
        }

        /// <summary>
        ///     Pauses audio playback and joins a voice channel.
        /// </summary>
        /// <param name="channel"></param>
        internal async Task JoinVoiceAsync(IVoiceChannel channel)
        {
            if (channel == default || InVoice && VoiceChannelId == channel.Id)
                return;

            await PauseSongAsync(out _);
            VoiceChannelId = channel.Id;
            AudioClient = await channel.ConnectAsync();
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
                        State = MusicServiceState.Playing;
                        //preload the first 3 songs
                        foreach (var song in SongQueue.Take(3))
                            song.TrySetData();

                        await PlayNextSongAsync();
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
                var dataStream = await song.DataTask;

                await using var audioStream = AudioClient.CreatePCMStream(AudioApplication.Music, (int) Enums.b128k);

                //seek if we're resuming from a pause
                if (song.Progress.Elapsed > TimeSpan.Zero)
                    await song.SeekAsync(song.Progress.Elapsed);

                await (DesignatedChannel?.SendMessageAsync($"Now playing {song.ToString(false)}") ?? Task.CompletedTask);

                //song playback
                song.Progress.Start();
                await dataStream.CopyToAsync(audioStream, Canceller);
                await audioStream.FlushAsync(Canceller);
                song.Progress.Stop();

                //if song ended too early, something probably went wrong
                if (song.Progress.Elapsed < song.Duration.Subtract(TimeSpan.FromSeconds(10)))
                    Log.Error($"Playback failure. {song.ToString(true)}");

                SongQueue.TryDequeue(out _);
                await song.DisposeAsync();
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
                State = MusicServiceState.Paused;
                song.Progress.Stop();

                Log.Info("Pausing playback...");
                return Canceller.CancelAsync()
                    .ReType(true);
            }

            //incase something nefarious is going on
            if (Playing)
                State = MusicServiceState.None;

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

        public async Task ConnectAsync()
        {
            Log.Warn("Connecting...");

            if (Connected)
            {
                await LeaveVoiceAsync();
                await Canceller.CancelAsync();
                await JoinVoiceAsync(VoiceChannel);
                await PlayAsync();
            } else
            {
                Connected = true;
                await LeaveVoiceAsync();
            }
        }

        public async Task DisconnectAsync(bool wait)
        {
            Log.Warn($"Disconnecting... (WaitForReconnect={wait})");
            var token = Canceller.Token;

            #pragma warning disable 4014
            if (wait && State == MusicServiceState.Playing)
                Task.Run(async () =>
                {
                    await Task.Delay(1500);

                    if (token.IsCancellationRequested)
                        Log.Warn("Disconnect canceled. Reconnect successful.");
                    else
                        await DisconnectAsync(false);
                }, token);
            #pragma warning restore 4014
            else if (Connected)
            {
                Connected = false;
                await LeaveVoiceAsync();
            }
        }

        public ulong GuildId { get; }
        public Logger Log { get; }
    }
}