namespace DeeJay.Abstractions;

/// <summary>
///     Defines the methods required to provide search functionality
/// </summary>
public interface ISearchService<T>
{
    /// <summary>
    /// Makes a query and returns a result
    /// </summary>
    /// <param name="query">The query to perform</param>
    /// <param name="cancellationToken">A token used to signal cancellation</param>
    Task<T> SearchAsync(string query, CancellationToken? cancellationToken = default);
}