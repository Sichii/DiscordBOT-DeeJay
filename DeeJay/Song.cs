using Google.Apis.YouTube.v3.Data;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Linq;
using Discord.WebSocket;
using Google.Apis.YouTube.v3;

namespace DeeJay
{
    internal sealed class Song
    {
        internal static readonly Stopwatch PlayTime = new Stopwatch();
        internal SocketUser RequestedBy { get; }
        internal SearchResult ResultFrom { get; }
        internal string YTLink { get; }
        internal string DirectLink { get; }
        internal string SongTitle { get; }
        internal TimeSpan Duration { get; }

        private Song(SocketUser requestedBy, SearchResult resultFrom, string ytLink, string directLink, string songTitle, TimeSpan duration)
        {
            ResultFrom = resultFrom;
            YTLink = ytLink;
            DirectLink = directLink;
            SongTitle = songTitle;
            Duration = duration;
        }

        internal static async Task<Song> FromRequest(SocketUser requestedBy, SearchResource.ListRequest request)
        {
            //send the search request and grab the first video result
            SearchListResponse results = await request.ExecuteAsync();
            SearchResult result = results.Items.FirstOrDefault(item => item.Id.Kind == "youtube#video");

            //use youtube-dl to get a direct link to the song, and it's duration
            var output = new List<string>();

            using (var youtubedl = new Process())
            {
                youtubedl.EnableRaisingEvents = true;
                youtubedl.OutputDataReceived += (s, e) => { output.Add(e.Data); };
                youtubedl.StartInfo = new ProcessStartInfo
                {
                    FileName = CONSTANTS.YOUTUBEDL_PATH,
                    Arguments = $"-f bestaudio -g --get-duration \"{result.Id.VideoId}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true
                };

                youtubedl.Start();
                youtubedl.BeginOutputReadLine();
                youtubedl.WaitForExit();
            }

            //parse the output of youtube-dl to get the parts i want
            int[] timeParts = Regex.Match(output[1], @"(\d+)(?::(\d+)(?::(\d+))?)?").Groups.Skip(1).Where(grp => !string.IsNullOrEmpty(grp.Value)).Select(grp => int.Parse(grp.Value)).ToArray();
            TimeSpan duration;

            switch(timeParts.Length)
            {
                case 3:
                    duration = new TimeSpan(timeParts[0], timeParts[1], timeParts[2]);
                    break;
                case 2:
                    duration = new TimeSpan(0, timeParts[0], timeParts[1]);
                    break;
                case 1:
                    duration = new TimeSpan(0, 0, timeParts[0]);
                    break;
                default:
                    duration = TimeSpan.Zero;
                    break;
            }

            //return the song object, ready to be used
            return new Song(requestedBy, result, $"https://www.youtube.com/watch?v={result.Id.VideoId}", output[0].Trim(), result.Snippet.Title, duration);
        }
    }
}
