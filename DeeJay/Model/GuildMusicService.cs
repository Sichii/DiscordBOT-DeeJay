using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using DeeJay.Definitions;
using Discord;
using Discord.Audio;

namespace DeeJay.Model
{
    /// <summary>
    /// A service that represents music playback for an individual discord server.
    /// </summary>
    public class GuildMusicService : IServiceProvider
    {
        internal ulong GuildId { get; }
        internal CancellationTokenSource CancellationTokenSource { get; set; }
        internal Task PlayingTask { get; set; }
        internal IVoiceChannel VoiceChannel { get; set; }
        internal IAudioClient AudioClient { get; set; }
        internal ConcurrentQueue<Song> SongQueue { get; }
        internal bool Playing => PlayingTask?.Status == TaskStatus.WaitingForActivation;

        internal GuildMusicService(ulong guildId)
        {
            GuildId = guildId;
            CancellationTokenSource = new CancellationTokenSource();
            SongQueue = new ConcurrentQueue<Song>();
        }

        /// <summary>
        ///     Pauses audio playback and joins a voice channel.
        /// </summary>
        /// <param name="channel"></param>
        internal async Task JoinVoiceAsync(IVoiceChannel channel)
        {
            if (VoiceChannel != channel)
            {
                await StopSongAsync(out _);
                VoiceChannel = channel;
                AudioClient = await channel.ConnectAsync();
            }
        }

        /// <summary>
        ///     Pauses audio playback and leaves the current voice channel.
        /// </summary>
        internal async Task LeaveVoiceAsync()
        {
            await StopSongAsync(out _);
            await VoiceChannel.DisconnectAsync();
            AudioClient.Dispose();

            VoiceChannel = null;
            AudioClient = null;
        }

        /// <summary>
        /// An asynchronous loop that plays songs from the queue until it's empty.
        /// </summary>
        private async Task AsyncPlay()
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
        /// Begins the playback loop if it wasn't already running.
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
                await using var audioStream = AudioClient.CreatePCMStream(AudioApplication.Music, (int) BitRate.b128k);
                var dataStream = await song.DataTask;

                //seek if we paused
                if (dataStream.Position != 0)
                    dataStream.AudioSeek(song.Progress.Elapsed, song.Duration);

                try
                {
                    song.Progress.Start();
                    await dataStream.CopyToAsync(audioStream, CancellationTokenSource.Token);
                } finally
                {
                    await audioStream.FlushAsync(CancellationTokenSource.Token);
                    song.Progress.Stop();

                    if (song.Progress.Elapsed < song.Duration.Subtract(TimeSpan.FromSeconds(10)))
                        Console.WriteLine("Something went wrong with playback.");
                    else
                    {
                        dataStream.Dispose();
                        SongQueue.TryDequeue(out _);
                    }
                }
            }
        }

        /// <summary>
        ///     Pauses playback.
        /// </summary>
        /// <param name="song">The song that was paused.</param>
        internal Task<bool> StopSongAsync(out Song song)
        {
            song = default;

            //if the audioclient isnt null
            if (Playing && SongQueue.TryPeek(out song))
            {
                song.Progress.Stop();
                CancellationTokenSource.Cancel();
                CancellationTokenSource = new CancellationTokenSource();

                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }

        /// <summary>
        /// Pauses the currently playing song and removes it from the queue.
        /// </summary>
        /// <param name="songTask">The song that was skipped</param>
        internal Task<bool> SkipSongAsync(out ValueTask<Song> songTask)
        {
            var source = new TaskCompletionSource<Song>();
            songTask = new ValueTask<Song>(source.Task);

            async Task<bool> InnerSkipSongAsync()
            {
                if (await StopSongAsync(out _) && SongQueue.TryDequeue(out var outSong))
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
        /// Removes a song from the queue at a given index.
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