using DeeJay.Definitions;
using DeeJay.Extensions;
using Discord;
using Discord.Interactions;

namespace DeeJay.Attributes
{
    /// <summary>
    ///     Required a user to have certain privileges to execute a command or module.
    /// </summary>
    internal sealed class RequirePrivilege : PreconditionAttributeBase
    {
        private readonly Privilege Privilege;

        internal RequirePrivilege(Privilege privilege) => Privilege = privilege;

        /// <inheritdoc />
        public override Task<PreconditionResult> CheckRequirementsAsync(
            IInteractionContext context,
            ICommandInfo commandInfo,
            IServiceProvider services
        )
        {
            if(context.User is not IGuildUser guildUser)
                return Failure($"The \"{commandInfo.Name}\" command can only be used in a guild.");

            if (guildUser.HasPrivilege(Privilege))
                return Success();

            return Failure($"{guildUser.DisplayName} does not have the required privileges to run the \"{commandInfo.Name}\" command.");
        }
    }
}