using System;
using System.Threading.Tasks;
using DeeJay.DiscordModel.Modules;
using DeeJay.Model.Services;
using Discord.Commands;

namespace DeeJay.DiscordModel.Attributes
{
    /// <summary>
    ///     Requires a command or module to execute within a designated text channel.
    ///     <para />
    /// </summary>
    public class RequireDesignation : PreconditionAttribute
    {
        /// <summary>
        ///     <inheritdoc />
        /// </summary>
        /// <param name="context">
        ///     <inheritdoc />
        /// </param>
        /// <param name="command">
        ///     <inheritdoc />
        /// </param>
        /// <param name="provider">
        ///     <inheritdoc />
        /// </param>
        public override Task<PreconditionResult> CheckPermissionsAsync(
            ICommandContext context, CommandInfo command, IServiceProvider provider)
        {
            if (command.Module.Name == nameof(MusicModule))
            {
                var sProvider = (ServiceProvider) provider;
                var mServ = sProvider.GetService<MusicService>();

                return mServ.DesignatedChannelId == 0 || context.Channel.Id == mServ.DesignatedChannelId
                    ? Task.FromResult(PreconditionResult.FromSuccess())
                    : Task.FromResult(PreconditionResult.FromError(
                        $"Command({command.Name}) was not executed in the designated channel({mServ.DesignatedChannel?.Name})"));
            }

            return Task.FromResult(PreconditionResult.FromError("This precondition should only be used on the music module."));
        }
    }
}