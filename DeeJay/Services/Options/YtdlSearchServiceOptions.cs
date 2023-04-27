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
    
    /// <summary>
    ///  The path to the youtube-dl executable
    /// </summary>
    public required string YoutubeDlPath { get; set; }
}