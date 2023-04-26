namespace DeeJay.Abstractions;

/// <summary>
/// Defines the properties of a search result
/// </summary>
public interface ISearchResult
{
    /// <summary>
    /// Whether or not the search was successful
    /// </summary>
    bool Success { get; }
    /// <summary>
    /// The error message if the search was not successful
    /// </summary>
    string? ErrorMessage { get; }
    /// <summary>
    /// The original query that was searched for
    /// </summary>
    string OriginalQuery { get; }
    /// <summary>
    /// The title of the search result
    /// </summary>
    string? Title { get; }
    /// <summary>
    /// The URI of the search result
    /// </summary>
    Uri? Uri { get; }
    /// <summary>
    /// The duration of the search result
    /// </summary>
    TimeSpan? Duration { get; }
    /// <summary>
    /// The time the search was requested at
    /// </summary>
    DateTime RequestedAt { get; }
}