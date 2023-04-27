using DeeJay.Abstractions;
using DeeJay.Services.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DeeJay.Services.Factories;

/// <summary>
/// Represents a factory that creates <see cref="FfmpegStreamPlayer"/>s
/// </summary>
public sealed class FfmpegStreamPlayerFactory : IStreamPlayerFactory
{
    private readonly ILoggerFactory LoggerFactory;
    private readonly IOptionsMonitor<FfmpegStreamPlayerOptions> FfmpegStreamPlayerOptionsMonitor;

    /// <summary>
    /// Creates a new <see cref="FfmpegStreamPlayerFactory"/>
    /// </summary>
    public FfmpegStreamPlayerFactory(
        ILoggerFactory loggerFactory,
        IOptionsMonitor<FfmpegStreamPlayerOptions> ffmpegStreamPlayerOptionsMonitor
    )
    {
        LoggerFactory = loggerFactory;
        FfmpegStreamPlayerOptionsMonitor = ffmpegStreamPlayerOptionsMonitor;
    }

    /// <inheritdoc />
    public IStreamPlayer Create(ISong song) => new FfmpegStreamPlayer(
        song,
        FfmpegStreamPlayerOptionsMonitor.CurrentValue.FfmpegPath,
        LoggerFactory.CreateLogger<FfmpegStreamPlayer>());
}