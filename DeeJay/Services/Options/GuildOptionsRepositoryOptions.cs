namespace DeeJay.Services.Options;

/// <summary>
///    Represents the options for a repository of guild options
/// </summary>
public sealed class GuildOptionsRepositoryOptions
{
    /// <summary>
    ///    Gets or sets the directory to store the guild options in
    /// </summary>
    public string Directory { get; set; } = null!;
}