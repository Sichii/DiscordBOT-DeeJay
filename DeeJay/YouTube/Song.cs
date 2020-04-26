using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DeeJay.Definitions;
using DeeJay.Utility;
using Discord.Audio;
using Discord.WebSocket;

namespace DeeJay.YouTube
{
    /// <summary>
    ///     A song in the queue.
    /// </summary>
    internal sealed class Song : IAsyncDisposable
    {
        private SemaphoreSlim Sync = new SemaphoreSlim(1, 1);
        internal Canceller Canceller { get; private set; }
        internal Stopwatch Progress { get; }
        internal SocketUser RequestedBy { get; }
        internal YTRequest ResultFrom { get; }
        internal string DirectLink { get; }
        internal string Title { get; }
        internal TimeSpan Duration { get; }
        internal string ErrorMsg { get; }
        internal bool IsLive => ResultFrom.IsLive;

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

        internal static Song Copy(Song song) =>
            new Song(song.RequestedBy, song.ResultFrom, song.DirectLink, song.Title, song.Duration, new Canceller());

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
            if (!result.IsLive && result.Duration > CONSTANTS.MAX_DURATION)
            {
                await Task.Delay(2000, canceller);
                result = await request.ExecuteAsync(true, canceller);
            }

            //if the result was still too long, set an appropriate error
            if (!result.IsLive && result.Duration > CONSTANTS.MAX_DURATION)
                return new Song(requestedBy, result, result.Title, result.Duration,
                    $"{result.Title} duration too long. Duration: {result.Duration.ToStringF()} MaxDuration: {CONSTANTS.MAX_DURATION.ToStringF()}");

            if (result.IsLive)
                return new Song(requestedBy, result, result.DirectURI, result.Title, TimeSpan.MaxValue, canceller);

            //if search failed, duration will be 0
            return result.Duration == TimeSpan.MinValue
                ? new Song(requestedBy, result, result.Title, result.Duration, "Search failed.")
                : new Song(requestedBy, result, result.DirectURI, result.Title, result.Duration, canceller);
        }

        internal async Task StreamAsync(AudioOutStream outStream, CancellationToken token)
        {
            await Sync.WaitAsync();

            try
            {
                await FFMPEG.StreamAsync(this, outStream, token);
            } finally
            {
                Sync.Release();
            }
        }

        /// <summary>
        ///     Safely disposes of the song data.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            try
            {
                await Canceller.CancelAsync();
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
            Canceller?.Dispose();
            Sync = null;
            Canceller = null;
            GC.Collect();
        }

        public override string ToString() => ToString(false);

        /// <summary>
        ///     Used when displaying the currently playing song.
        /// </summary>
        /// <param name="showProgress">Whether or not to show current progress in the song.</param>
        public string ToString(bool showProgress)
        {
            var builder = new StringBuilder();

            builder.Append(Title);

            if (Duration == TimeSpan.MaxValue || Duration == TimeSpan.Zero || Duration == TimeSpan.MinValue)
                return builder.ToString();

            builder.Append(" [");

            if (showProgress)
                builder.Append($"{Progress.Elapsed.ToStringF()} / ");

            builder.Append($"{Duration.ToStringF()}] (R: {RequestedBy.Username})");
            return builder.ToString();
        }
    }
}