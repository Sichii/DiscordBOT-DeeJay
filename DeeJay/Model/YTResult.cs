using System;

namespace DeeJay.Model
{
    internal class YTResult : YTRequest
    {
        internal string Title { get; }
        internal string DirectURI { get; }
        internal TimeSpan Duration { get; }

        internal YTResult(string query, string title, string directURI, TimeSpan duration)
            : base(query)
        {
            Title = title;
            DirectURI = directURI;
            Duration = duration;
        }
    }
}