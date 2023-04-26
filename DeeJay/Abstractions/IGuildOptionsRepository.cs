namespace DeeJay.Abstractions;

/// <summary>
/// Defines the methods needed to store and access guild options
/// </summary>
public interface IGuildOptionsRepository
{
    /// <summary>
    /// Gets the options for the guild with the specified id
    /// </summary>
    /// <param name="guildId">The id of the guild whose options to get</param>
    Task<IGuildOptions> GetOptionsAsync(ulong guildId);
    
    /// <summary>
    /// Saves the repository
    /// </summary>
    Task SaveAsync();
}