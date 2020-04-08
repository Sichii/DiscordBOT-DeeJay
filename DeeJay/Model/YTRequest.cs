using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DeeJay.Definitions;

namespace DeeJay.Model
{
    internal class YTRequest
    {
        internal string Query { get; }

        internal YTRequest(string query) => Query = query;

        internal async Task<YTResult> ExecuteAsync()
        {
            //0 = title, 1 = direct link, 2 = duration string
            var output = new List<string>();

            //use youtube-dl to get a direct link to the song, and it's duration
            using var youtubedl = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = CONSTANTS.YOUTUBEDL_PATH,
                    //best audio stream, probe for direct link, get video duration
                    Arguments = $"-f bestaudio -s -g -e --get-duration \"ytsearch:{Query}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false
                }
            };

            youtubedl.OutputDataReceived += (s, e) => output.Add(e.Data);
            await youtubedl.RunAsync(true);

            var timeParts = new List<int>();
            foreach (var o in Regex.Matches(output[2], @":?(\d+)"))
                if (o is Match match && match.Success && int.TryParse(match.Groups[1]
                        .Value, out var timePart))
                    timeParts.Add(timePart);

            var duration = timeParts.Count switch
            {
                1 => new TimeSpan(0, 0, 0, timeParts[0]),
                2 => new TimeSpan(0, 0, timeParts[0], timeParts[1]),
                3 => new TimeSpan(0, timeParts[0], timeParts[1], timeParts[2]),
                _ => TimeSpan.MaxValue
            };

            return new YTResult(Query, output[0], output[1], duration);
        }
    }
}