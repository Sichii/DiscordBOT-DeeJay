using System;

namespace DeeJay.Model.YouTube
{
    /// <summary>
    ///     The result of a <see cref="YTRequest" /> search execution.
    /// </summary>
    internal class YTResult : YTRequest
    {
        /// <summary>
        ///     The title of the song result.
        /// </summary>
        internal string Title { get; }

        /// <summary>
        ///     A direct link to the audio of the song.
        /// </summary>
        internal string DirectURI { get; }

        /// <summary>
        ///     The duration of the song.
        /// </summary>
        internal TimeSpan Duration { get; }

        /// <summary>
        ///     Constructs the result of a youtube search.
        /// </summary>
        /// <param name="query">The query to search youtube with.</param>
        /// <param name="title">The title of the song result.</param>
        /// <param name="directURI">A direct link to the audio of the song.</param>
        /// <param name="duration">The duration of the song.</param>
        internal YTResult(string query, string title, string directURI, TimeSpan duration)
            : base(query)
        {
            Title = title;
            DirectURI = directURI;
            Duration = duration;
        }
    }
}