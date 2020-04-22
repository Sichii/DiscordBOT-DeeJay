using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DeeJay.Definitions;
using DeeJay.Utility;

namespace DeeJay.YouTube
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
        /// <param name="token">A cancellation token.</param>
        internal async Task<YTResult> ExecuteAsync(bool cullExtended, CancellationToken token)
        {
            //0 = title, 1 = direct link, 2 = duration string
            var isURI = Query.IsValidURI();
            var isYouTube = isURI && (Query.Contains("youtube") || Query.Contains("youtu.be"));

            //"ytsearch:{Query} -\"extended\"" => ytsearch:{Query} -"extended"
            //if the query is not a uri and we're trying to cull extended
            if (cullExtended && !isURI)
                Query = $"{Query} -\\\"extended\\\"";

            //if the song is not a uri, or it's a youtube link (and the cookies file exists)
            //then use the cookies file
            if ((!isURI || isYouTube) && File.Exists(CONSTANTS.COOKIES_PATH) && (await File.ReadAllLinesAsync(CONSTANTS.COOKIES_PATH)).Length > 0)
                Query += $" --cookies {CONSTANTS.COOKIES_PATH}";

            var queryStr = !isURI ? $"\"ytsearch:{Query}\"" : Query;
            var output = (await YTDL.RunAsync(queryStr, token)).ToArray();

            if (output.Length >= 3)
            {
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

            return new YTResult(Query, string.Empty, string.Empty, TimeSpan.MinValue);
        }
    }
}