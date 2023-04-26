using DeeJay.Abstractions;

namespace DeeJay.Models;

/// <summary>
/// Represents a search result from youtube-dl
/// </summary>
public sealed class YtdlSearchResult : ISearchResult
{
    /// <summary>
    /// Whether or not the search was successful
    /// </summary>
    public bool Success { get; }
    /// <summary>
    /// The error message if the search was not successful
    /// </summary>
    public string? ErrorMessage { get; }
    /// <summary>
    /// The original query that was searched for
    /// </summary>
    public string OriginalQuery { get; }
    /// <summary>
    /// The title of the search result
    /// </summary>
    public string? Title { get; }
    /// <summary>
    /// The URI of the search result
    /// </summary>
    public Uri? Uri { get; }
    /// <summary>
    /// The duration of the search result
    /// </summary>
    public TimeSpan? Duration { get; }
    
    /// <summary>
    /// The time the search was requested at
    /// </summary>
    public DateTime RequestedAt { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="YtdlSearchResult"/> class.
    /// </summary>
    /// <param name="query">The query used to perform the search</param>
    /// <param name="results">The results of the search</param>
    public YtdlSearchResult(string query, params string[] results)
    {
        OriginalQuery = query;
        RequestedAt = DateTime.UtcNow;

        switch (results.Length)
        {
            case 1:
                ErrorMessage = results[0];
                
                break;
            case 3:
                Success = true;
                Title = results[0];
                Uri = new Uri(results[1]);
                Duration = TimeSpan.Parse(results[2]);

                break;
            default:
                ErrorMessage = "Unknown error";
                
                break;
        }
    }
}