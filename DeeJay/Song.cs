using Google.Apis.YouTube.v3.Data;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Linq;
using Discord.WebSocket;
using Google.Apis.YouTube.v3;

namespace DeeJay
{
    internal sealed class Song
    {
        internal Stopwatch Progress { get; }
        internal SocketUser RequestedBy { get; }
        internal SearchResult ResultFrom { get; }
        internal string YtLink { get; }
        internal string DirectLink { get; }
        internal string Title { get; }
        internal TimeSpan Duration { get; }

        private Song(SocketUser requestedBy, SearchResult resultFrom, string ytLink, string directLink, string songTitle, TimeSpan duration)
        {
            RequestedBy = requestedBy;
            ResultFrom = resultFrom;
            YtLink = ytLink;
            DirectLink = directLink;
            Title = songTitle;
            Duration = duration;
            Progress = new Stopwatch();
        }

        /// <summary>
        /// Creates a song object from a queue request.
        /// </summary>
        /// <param name="requestedBy">The user who requested the song.</param>
        /// <param name="request">The request object to use.</param>
        internal static async Task<Song> FromRequest(SocketUser requestedBy, SearchResource.ListRequest request)
        {
            //send the search request and grab the first video result
            var results = await request.ExecuteAsync();
            var result = results.Items.FirstOrDefault(item => item.Id.Kind == "youtube#video");

            //0 = direct link, 1 = duration string
            var output = new List<string>();

            //use youtube-dl to get a direct link to the song, and it's duration
            using (var youtubedl = new Process())
            {
                youtubedl.EnableRaisingEvents = true;
                youtubedl.OutputDataReceived += (s, e) => { output.Add(e.Data); };
                youtubedl.StartInfo = new ProcessStartInfo
                {
                    FileName = CONSTANTS.YOUTUBEDL_PATH,
                    //best audio stream, probe for direct link, get video duration
                    Arguments = $"-f bestaudio -g --get-duration \"{result?.Id.VideoId}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true
                };

                await youtubedl.RunAsync(true);
            }

            //parse the duration string
            var timeParts = Regex.Match(output[1], @"(\d+)(?::(\d+)(?::(\d+))?)?").Groups.OfType<Group>().Skip(1).Where(grp => !string.IsNullOrEmpty(grp.Value)).Select(grp => int.Parse(grp.Value)).ToArray();

            var duration = timeParts.Length switch
            {
                3 => new TimeSpan(timeParts[0], timeParts[1], timeParts[2]),
                2 => new TimeSpan(0, timeParts[0], timeParts[1]),
                1 => new TimeSpan(0, 0, timeParts[0]),
                _ => TimeSpan.Zero
            };

            //return the song object, ready to be used
            return new Song(requestedBy, result, $"https://www.youtube.com/watch?v={result?.Id.VideoId}", output[0].Trim(), result?.Snippet.Title, duration);
        }

        public string ToString(bool showProgress = false) =>
            $"{Title} [{(showProgress ? $"{Progress.Elapsed.ToReadableString()} / " : string.Empty)}{Duration.ToReadableString()}]";
    }
}
