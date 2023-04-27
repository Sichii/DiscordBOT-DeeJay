using Microsoft.Extensions.Options;

namespace DeeJay.Services.Options;

/// <summary>
///   Configures options
/// </summary>
public sealed class OptionsConfigurer : IConfigureOptions<DiscordClientServiceOptions>
{
    /// <inheritdoc />
    public void Configure(DiscordClientServiceOptions options)
    {
        if (string.IsNullOrEmpty(options.TokenValue))
            options.TokenValue = File.ReadAllText(options.TokenPath);
    }
}