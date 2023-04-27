using DeeJay.Abstractions;
using Microsoft.Extensions.Logging;

namespace DeeJay.Services.Factories;

/// <summary>
/// Represents a factory that creates <see cref="FfmpegStreamPlayer"/>s
/// </summary>
public class FfmpegStreamPlayerFactory : IStreamPlayerFactory
{
    private readonly ILoggerFactory LoggerFactory;
    
    /// <summary>
    /// Creates a new <see cref="FfmpegStreamPlayerFactory"/>
    /// </summary>
    /// <param name="loggerFactory"></param>
    public FfmpegStreamPlayerFactory(ILoggerFactory loggerFactory) => LoggerFactory = loggerFactory;

    /// <inheritdoc />
    public IStreamPlayer Create(ISong song) => new FfmpegStreamPlayer(song, "", LoggerFactory.CreateLogger<FfmpegStreamPlayer>());
}