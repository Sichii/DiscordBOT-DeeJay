namespace DeeJay.Abstractions;

/// <summary>
/// Provides the methods for creating <see cref="IStreamPlayer"/>s
/// </summary>
public interface IStreamPlayerFactory
{
    /// <summary>
    /// Creates a new <see cref="IStreamPlayer"/> for the given <see cref="ISong"/>
    /// </summary>
    /// <param name="song">The song that will be played by the stream player</param>
    IStreamPlayer Create(ISong song);
}