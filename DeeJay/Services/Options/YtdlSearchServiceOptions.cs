namespace DeeJay.Services.Options;

/// <summary>
///    Options for the <see cref="YtdlSearchService" />
/// </summary>
public sealed class YtdlSearchServiceOptions
{
    /// <summary>
    ///   The proxy url to use for youtube-dl
    /// </summary>
    public string? ProxyUrl { get; set; }
}