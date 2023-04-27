namespace DeeJay.Services.Options;

/// <summary>
///   Options for the <see cref="FfmpegStreamPlayer" />
/// </summary>
public sealed class FfmpegStreamPlayerOptions
{
    /// <summary>
    ///  The path to the ffmpeg executable
    /// </summary>
    public required string FfmpegPath { get; set; }
}