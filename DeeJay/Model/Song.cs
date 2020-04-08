using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DeeJay.Definitions;
using Discord.WebSocket;

namespace DeeJay.Model
{
    /// <summary>
    ///     A song in the queue.
    /// </summary>
    internal sealed class Song
    {
        private readonly SemaphoreSlim Sync = new SemaphoreSlim(1, 1);
        private readonly TaskCompletionSource<MemoryStream> DataSource;
        internal Task<MemoryStream> DataTask { get; private set; }
        internal Stopwatch Progress { get; }
        internal SocketUser RequestedBy { get; }
        internal YTRequest ResultFrom { get; }
        internal string DirectLink { get; }
        internal string Title { get; }
        internal TimeSpan Duration { get; }
        internal CancellationTokenSource Canceller { get; }
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
        private Song(SocketUser requestedBy, YTRequest resultFrom, string directLink, string songTitle, TimeSpan duration)
        {
            RequestedBy = requestedBy;
            ResultFrom = resultFrom;
            DirectLink = directLink;
            Title = songTitle;
            Duration = duration;
            Progress = new Stopwatch();
            Canceller = new CancellationTokenSource();
            DataSource = new TaskCompletionSource<MemoryStream>();
        }

        /// <summary>
        ///     Creates a song object from a queue request.
        /// </summary>
        /// <param name="requestedBy">The user who requested the song.</param>
        /// <param name="request">The request object to use.</param>
        internal static async Task<Song> FromRequest(SocketUser requestedBy, YTRequest request)
        {
            var result = await request.ExecuteAsync();

            if (result.Duration > CONSTANTS.MAX_DURATION)
                return new Song(requestedBy, result, result.Title, result.Duration,
                    $"{result.Title} duration too long. Duration: {result.Duration.ToReadableString()} MaxDuration: {CONSTANTS.MAX_DURATION.ToReadableString()}");

            //return the song object, ready to be used
            return new Song(requestedBy, result, result.DirectURI, result.Title, result.Duration);
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

                DataTask = DataSource.Task;

                using var ffmpeg = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = CONSTANTS.FFMPEG_PATH,
                        //no text, seek to previously elapsed if necessary, 2 channel, 75% volume, pcm s16le stream format, 48000hz, pipe 1
                        Arguments = $"-hide_banner -loglevel quiet -i \"{DirectLink}\" -ac 2 -af volume=0.1 -f s16le -ar 48000 pipe:1",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true
                    }
                };

                ffmpeg.Start();

                var dataStream = new MemoryStream();

                while (!ffmpeg.HasExited)
                    await ffmpeg.StandardOutput.BaseStream.CopyToAsync(dataStream, Canceller.Token);

                await ffmpeg.StandardOutput.BaseStream.CopyToAsync(dataStream, Canceller.Token);

                dataStream.Position = 0;
                DataSource.SetResult(dataStream);
            } catch
            {
                DataSource?.SetResult(new MemoryStream());
            } finally
            {
                Sync.Release();
            }
        }

        /// <summary>
        ///     Safely disposes of the song data.
        /// </summary>
        public async Task DisposeAsync()
        {
            try
            {
                if (!DataSource.TrySetResult(default))
                    DataSource.TrySetCanceled();

                await ((await DataTask)?.DisposeAsync() ?? default);
                DataTask?.Dispose();
            } catch
            {
                DataTask?.Dispose();
            } finally
            {
                DataTask = null;
                GC.Collect();
            }
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