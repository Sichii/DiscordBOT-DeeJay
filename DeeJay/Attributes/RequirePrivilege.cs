using DeeJay.Definitions;
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

            var hasPrivs = Privilege switch
            {
                Privilege.None          => true,
                Privilege.Normal        => guildUser.GuildPermissions is { SendMessages: true, Connect: true },
                Privilege.Elevated      => guildUser.GuildPermissions.ManageChannels || guildUser.GuildPermissions.KickMembers,
                Privilege.Administrator => guildUser.GuildPermissions.Administrator,
                _                            => throw new ArgumentOutOfRangeException()
            };

            if (hasPrivs)
                return Success();

            return Failure($"{guildUser.DisplayName} does not have the required privileges to run the \"{commandInfo.Name}\" command.");
        }
    }
}