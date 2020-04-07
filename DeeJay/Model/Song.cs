﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DeeJay.Definitions;
using Discord.WebSocket;
using Google.Apis.YouTube.v3.Data;

namespace DeeJay.Model
{
    /// <summary>
    /// A song in the queue.
    /// </summary>
    internal sealed class Song
    {
        internal Stopwatch Progress { get; }
        internal SocketUser RequestedBy { get; }
        internal SearchResult ResultFrom { get; }
        internal string YtLink { get; }
        internal string DirectLink { get; }
        internal string Title { get; }
        internal TimeSpan Duration { get; }
        internal Task<MemoryStream> DataTask { get; private set; }
        internal string ErrorMsg { get; }

        /// <summary>
        /// Constructor to collect song info when an error occurs.
        /// </summary>
        private Song(SocketUser requestedBy, SearchResult resultFrom, string songTitle, TimeSpan duration, string errorMsg)
        {
            RequestedBy = requestedBy;
            ResultFrom = resultFrom;
            Title = songTitle;
            Duration = duration;
            ErrorMsg = errorMsg;
        }

        /// <summary>
        /// Primary constructor. Constructs the song and begins to asynchronously download and re-encode the data for streaming to discord.
        /// </summary>
        /// <param name="requestedBy">The user the song was requested by.</param>
        /// <param name="resultFrom">The search result the song came from.</param>
        /// <param name="ytLink">User friendly youtube link.</param>
        /// <param name="directLink">Youtube link used by ffmpeg to pull just the audio.</param>
        /// <param name="songTitle">Title of the song/video as returned by youtube.</param>
        /// <param name="duration">Duration of the song.</param>
        private Song(SocketUser requestedBy, SearchResult resultFrom, string ytLink, string directLink, string songTitle, TimeSpan duration)
        {
            RequestedBy = requestedBy;
            ResultFrom = resultFrom;
            YtLink = ytLink;
            DirectLink = directLink;
            Title = songTitle;
            Duration = duration;
            Progress = new Stopwatch();
            DataTask = GetDataStream();
        }

        /// <summary>
        /// Asynchronously downloads and re-encodes the song via ffmpeg.
        /// </summary>
        internal async Task<MemoryStream> GetDataStream()
        {
            using var ffmpeg = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = CONSTANTS.FFMPEG_PATH,
                    //no text, seek to previously elapsed if necessary, 2 channel, 75% volume, pcm s16le stream format, 48000hz, pipe 1
                    Arguments = $"-hide_banner -loglevel quiet -i \"{DirectLink}\" -ac 2 -af volume=0.1 -f s16le pipe:1",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true
                }
            };

            ffmpeg.Start();

            var dataStream = new MemoryStream();

            while (!ffmpeg.HasExited)
                await ffmpeg.StandardOutput.BaseStream.CopyToAsync(dataStream);

            await ffmpeg.StandardOutput.BaseStream.CopyToAsync(dataStream);

            dataStream.Position = 0;
            return dataStream;
        }

        /// <summary>
        ///     Creates a song object from a queue request.
        /// </summary>
        /// <param name="requestedBy">The user who requested the song.</param>
        /// <param name="result">The request object to use.</param>
        internal static async Task<Song> FromRequest(SocketUser requestedBy, SearchResult result)
        {
            //0 = direct link, 1 = duration string
            var output = new List<string>();

            //use youtube-dl to get a direct link to the song, and it's duration
            using var youtubedl = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = CONSTANTS.YOUTUBEDL_PATH,
                    //best audio stream, probe for direct link, get video duration
                    Arguments = $"-f bestaudio -g --get-duration \"{result?.Id.VideoId}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false
                }
            };

            youtubedl.OutputDataReceived += (s, e) => output.Add(e.Data);

            await youtubedl.RunAsync(true);

            if (output.Count <= 1)
            {
                Console.WriteLine("Problem getting youtube direct link with youtube-dl.");
                return null;
            }

            //parse the duration string
            var timeParts = Regex.Match(output[1], @"(\d+)(?::(\d+)(?::(\d+))?)?")
                .Groups.OfType<Group>()
                .Skip(1)
                .Where(grp => !string.IsNullOrEmpty(grp.Value))
                .Select(grp => int.Parse(grp.Value))
                .ToArray();

            var duration = timeParts.Length switch
            {
                3 => new TimeSpan(timeParts[0], timeParts[1], timeParts[2]),
                2 => new TimeSpan(0, timeParts[0], timeParts[1]),
                1 => new TimeSpan(0, 0, timeParts[0]),
                _ => TimeSpan.Zero
            };

            if (duration > CONSTANTS.MAX_DURATION)
                return new Song(requestedBy, result, result?.Snippet.Title, duration,
                    $"{result?.Snippet.Title} duration too long. Duratioon: {duration.ToReadableString()} MaxDuration: {CONSTANTS.MAX_DURATION.ToReadableString()}");

            //return the song object, ready to be used
            return new Song(requestedBy, result, $"https://www.youtube.com/watch?v={result?.Id.VideoId}", output[0]
                .Trim(), result?.Snippet.Title, duration);
        }

        /// <summary>
        /// Safely disposes of the song data.
        /// </summary>
        public async Task DisposeAsync()
        {
            try
            {
                await (await DataTask).DisposeAsync();
                DataTask.Dispose();
                DataTask = null;
                GC.Collect();
            } catch
            {
                // ignored
            }
        }

        public override string ToString() => ToString(false);

        /// <summary>
        /// Used when displaying the currently playing song.
        /// </summary>
        /// <param name="showProgress">Whether or not to show current progress in the song.</param>
        public string ToString(bool showProgress) =>
            $"{Title} [{(showProgress ? $"{Progress.Elapsed.ToReadableString()} / " : string.Empty)}{Duration.ToReadableString()}]";
    }
}