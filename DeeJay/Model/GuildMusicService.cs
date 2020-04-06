using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using DeeJay.Definitions;
using Discord;
using Discord.Audio;

namespace DeeJay.Model
{
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
        ///     Joins a voice channel.
        /// </summary>
        /// <param name="channel"></param>
        internal async Task JoinVoiceAsync(IVoiceChannel channel)
        {
            if (VoiceChannel != channel)
            {
                await StopSongAsync();
                VoiceChannel = channel;
                AudioClient = await channel.ConnectAsync();
            }
        }

        /// <summary>
        ///     Pauses audio playback and leaves the current voice channel.
        /// </summary>
        internal async Task LeaveVoiceAsync()
        {
            await StopSongAsync();
            await VoiceChannel.DisconnectAsync();
            AudioClient.Dispose();

            VoiceChannel = null;
            AudioClient = null;
        }

        private async Task PlayTask()
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

        internal Task Play()
        {
            if (!Playing)
                PlayingTask = PlayTask();

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
        ///     Stops playback of the current audio stream, if there is one.
        /// </summary>
        /// <returns></returns>
        internal Task<bool> StopSongAsync()
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

        internal async Task<bool> SkipSongAsync()
        {
            if (await StopSongAsync() && SongQueue.TryDequeue(out _))
            {
                await Play();
                return true;
            }

            return false;
        }

        internal async Task<Song> RemoveSongAsync(int index)
        {
            if (index < 1)
                return null;

            var result = SongQueue.RemoveAt(index);

            if (index == 1)
                await SkipSongAsync();

            return result;
        }

        public object GetService(Type serviceType) => this;
    }
}