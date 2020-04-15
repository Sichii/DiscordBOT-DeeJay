using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DeeJay.Definitions;

namespace DeeJay.Model.YouTube
{
    /// <summary>
    ///     An object used to request a search result from youtube.
    /// </summary>
    internal class YTRequest
    {
        /// <summary>
        ///     The query used to obtain the song.
        /// </summary>
        internal string Query { get; private set; }

        /// <summary>
        ///     Constructs an object that can be executed to perform a youtube search.
        /// </summary>
        /// <param name="query">The query to search youtube with.</param>
        internal YTRequest(string query) => Query = query;

        /// <summary>
        ///     Executes the youtube search, retreiving information for the top result.
        /// </summary>
        /// <param name="cullExtended">Use this flag to auto include the flag -"extended", to avoid long songs.</param>
        internal async Task<YTResult> ExecuteAsync(bool cullExtended = false)
        {
            //0 = title, 1 = direct link, 2 = duration string
            var output = new List<string>();
            var isURI = Query.IsValidURI();

            //"ytsearch:{Query} -\"extended\"" => ytsearch:{Query} -"extended"
            if (cullExtended && !isURI)
                Query = $"{Query} -\\\"extended\\\"";

            var queryStr = !isURI ? $"\"ytsearch:{Query}\"" : Query;

            //use youtube-dl to get a direct link to the song, and it's duration
            using var youtubedl = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = CONSTANTS.YOUTUBEDL_PATH,
                    //best audio stream, probe for direct link, get video duration
                    Arguments = $"-f bestaudio -sge --get-duration {queryStr}",
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