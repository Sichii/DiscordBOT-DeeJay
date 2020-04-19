using System;
using System.Threading.Tasks;
using DeeJay.Definitions;
using Discord.Commands;
using Discord.WebSocket;

namespace DeeJay.Discord.Attributes
{
    /// <summary>
    ///     Required a user to have certain privileges to execute a command or module.
    /// </summary>
    internal class RequirePrivilege : PreconditionAttributeBase
    {
        private readonly PrivilegeLevel PrivilegeLevel;

        internal RequirePrivilege(PrivilegeLevel privilegeLevel) => PrivilegeLevel = privilegeLevel;

        public override Task<PreconditionResult> CheckPermissionsAsync(
            ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            var user = (SocketGuildUser) context.User;
            ErrorMessage =
                $"{user.Username} does not have the required privilege({PrivilegeLevel.ToString()}) to run this command({command.Name}).";

            return PrivilegeLevel switch
            {
                PrivilegeLevel.None   => Success,
                PrivilegeLevel.Normal => (user.GuildPermissions.SendMessages && user.GuildPermissions.Connect ? Success : Error),
                PrivilegeLevel.Elevated => (user.GuildPermissions.ManageChannels || user.GuildPermissions.KickMembers
                    ? Success
                    : Error),
                PrivilegeLevel.Administrator => (user.GuildPermissions.Administrator ? Success : Error),
                _                            => throw new ArgumentOutOfRangeException()
            };
        }
    }
}