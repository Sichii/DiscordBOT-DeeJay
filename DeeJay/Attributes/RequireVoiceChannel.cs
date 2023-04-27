using DeeJay.Extensions;
using Discord;
using Discord.Interactions;

namespace DeeJay.Attributes
{
    /// <summary>
    ///     Requires the user executing the command to be in a voice channel.
    /// </summary>
    public sealed class RequireVoiceChannel : PreconditionAttributeBase
    {
        /// <inheritdoc />
        public override async Task<PreconditionResult> CheckRequirementsAsync(
            IInteractionContext context,
            ICommandInfo commandInfo,
            IServiceProvider services
        )
        {
            if (!context.User.TryGetVoiceChannel(out _))
            {
                await context.Interaction.RespondAsync(
                    $"You must be in a voice channel to execute the \"{commandInfo.Name}\" command.",
                    ephemeral: true);

                return FailureResult(
                    $"{context.User.Username} attempted to issue command \"{commandInfo.Name}\" while not being in a voice channel.");
            }

            return SuccessResult();
        }
    }
}