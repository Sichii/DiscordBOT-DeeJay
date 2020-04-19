using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DeeJay.Definitions;
using DeeJay.Utility;
using Discord.WebSocket;

namespace DeeJay.YouTube
{
    /// <summary>
    ///     A song in the queue.
    /// </summary>
    internal sealed class Song : IAsyncDisposable
    {
        private SemaphoreSlim Sync = new SemaphoreSlim(1, 1);
        internal Task<MemoryStream> DataTask { get; private set; }
        internal Canceller Canceller { get; private set; }
        internal Stopwatch Progress { get; }
        internal SocketUser RequestedBy { get; }
        internal YTRequest ResultFrom { get; }
        internal string DirectLink { get; }
        internal string Title { get; }
        internal TimeSpan Duration { get; }
        internal string ErrorMsg { get; }

        /// <summary>
        ///     Constructor to collect song info when an error occurs.
        /// </summary>
        private Song(SocketUser requestedBy, YTRequest resultFrom, string songTitle, TimeSpan duration, string errorMsg)
        {
            RequestedBy = requestedBy;
            ResultFrom = resultFrom;
            Title = songTitle;
            Duration = duration;
            ErrorMsg = errorMsg;
        }

        /// <summary>
        ///     Primary constructor. Constructs the song and begins to asynchronously download and re-encode the data for streaming
        ///     to discord.
        /// </summary>
        /// <param name="requestedBy">The user the song was requested by.</param>
        /// <param name="resultFrom">The search request the song came from.</param>
        /// <param name="directLink">Youtube link used by ffmpeg to pull just the audio.</param>
        /// <param name="songTitle">Title of the song/video as returned by youtube.</param>
        /// <param name="duration">Duration of the song.</param>
        /// <param name="canceller">A utility object for handling task cancellations.</param>
        private Song(
            SocketUser requestedBy, YTRequest resultFrom, string directLink, string songTitle, TimeSpan duration, Canceller canceller)
        {
            RequestedBy = requestedBy;
            ResultFrom = resultFrom;
            DirectLink = directLink;
            Title = songTitle;
            Duration = duration;
            Progress = new Stopwatch();
            Canceller = canceller;
        }

        /// <summary>
        ///     Creates a song object from a queue request.
        /// </summary>
        /// <param name="requestedBy">The user who requested the song.</param>
        /// <param name="request">The request object to use.</param>
        /// <param name="canceller">A utility object for handling task cancellations.</param>
        internal static async Task<Song> FromRequest(SocketUser requestedBy, YTRequest request, Canceller canceller)
        {
            var result = await request.ExecuteAsync(false, canceller);

            //if the result was too long to play, retry and omit "extended" results
            if (result.Duration > CONSTANTS.MAX_DURATION)
            {
                await Task.Delay(2000, canceller);
                result = await request.ExecuteAsync(true, canceller);
            }

            //if the result was still too long, set an appropriate error
            if (result.Duration > CONSTANTS.MAX_DURATION)
                return new Song(requestedBy, result, result.Title, result.Duration,
                    $"{result.Title} duration too long. Duration: {result.Duration.ToReadableString()} MaxDuration: {CONSTANTS.MAX_DURATION.ToReadableString()}");

            //if search failed, duration will be 0
            return result.Duration == TimeSpan.MinValue
                ? new Song(requestedBy, result, result.Title, result.Duration, "Search failed.")
                : new Song(requestedBy, result, result.DirectURI, result.Title, result.Duration, canceller);
        }

        /// <summary>
        ///     Asynchronously downloads and re-encodes the song via ffmpeg.
        /// </summary>
        internal async void TrySetData()
        {
            await Sync.WaitAsync();

            try
            {
                if (DataTask != null)
                    return;

                DataTask = FFMPEG.RunAsync(DirectLink, Canceller);
            } finally
            {
                Sync.Release();
            }
        }

        /// <summary>
        ///     Detects the bitrate of the audio, and seeks to the specified time in the song.
        ///     <inheritdoc cref="Stream.Seek" />
        /// </summary>
        /// <param name="seekTime">The time to seek to.</param>
        internal async Task SeekAsync(TimeSpan seekTime)
        {
            var stream = await DataTask;
            var bitRate = stream.Length / (long) Duration.TotalSeconds;
            var seekPosition = bitRate * (long) seekTime.TotalSeconds;

            if (seekPosition > stream.Length)
                stream.Seek(0, SeekOrigin.Begin);

            stream.Seek(seekPosition, SeekOrigin.Begin);
        }

        /// <summary>
        ///     Safely disposes of the song data.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            try
            {
                await Canceller.CancelAsync();
                await ((await DataTask)?.DisposeAsync() ?? default);
            } catch
            {
                //ignored
            } finally
            {
                Dispose();
            }
        }

        /// <summary>
        ///     Internal dispose, for actually disposing the objects and cleaning memory.
        /// </summary>
        private void Dispose()
        {
            Sync?.Dispose();
            DataTask?.Dispose();
            Canceller?.Dispose();
            Sync = null;
            DataTask = null;
            Canceller = null;
            GC.Collect();
        }

        public override string ToString() => ToString(false);

        /// <summary>
        ///     Used when displaying the currently playing song.
        /// </summary>
        /// <param name="showProgress">Whether or not to show current progress in the song.</param>
        public string ToString(bool showProgress) =>
            $"{Title} [{(showProgress ? $"{Progress.Elapsed.ToReadableString()} / " : string.Empty)}{Duration.ToReadableString()}] (R: {RequestedBy.Username})";
    }
}