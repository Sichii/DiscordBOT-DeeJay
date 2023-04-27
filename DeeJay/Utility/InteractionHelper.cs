using Discord;

namespace DeeJay.Utility;

/// <summary>
///    Provides a helper class for interactions that automatically defers interactions and makes responding easier.
/// </summary>
public sealed class InteractionHelper : IAsyncDisposable
{
    private readonly IDiscordInteraction Interaction;
    private bool HasResponded;

    /// <summary>
    ///   Creates a new <see cref="InteractionHelper"/> instance.
    /// </summary>
    public InteractionHelper(IDiscordInteraction interaction) => Interaction = interaction;

    /// <summary>
    /// Acknowledges the interaction, returns an <see cref="InteractionHelper"/> that can be used to respond to the interaction.
    /// </summary>
    /// <param name="interaction">The interaction</param>
    /// <param name="ephemeral">Whether or not the responses are hidden from everyone except the user</param>
    public static async Task<InteractionHelper> DeferAsync(IDiscordInteraction interaction, bool ephemeral = false)
    {
        var ret = new InteractionHelper(interaction);
        await interaction.DeferAsync(ephemeral);

        return ret;
    }

    /// <summary>
    ///    Responds to the interaction, reusing an existing response if it exists
    /// </summary>
    /// <param name="message">The message to respond with</param>
    public Task RespondAsync(string message)
    {
        HasResponded = true;
        return Interaction.ModifyOriginalResponseAsync(mp => mp.Content = message);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (!HasResponded)
            await Interaction.DeleteOriginalResponseAsync();
    }
}