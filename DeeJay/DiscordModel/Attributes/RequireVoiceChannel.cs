using System;
using System.Threading.Tasks;
using DeeJay.Definitions;
using Discord.Commands;

namespace DeeJay.DiscordModel.Attributes
{
    /// <summary>
    ///     Requires the user executing the command to be in a voice channel.
    /// </summary>
    public class RequireVoiceChannel : PreconditionAttribute
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
            ICommandContext context, CommandInfo command, IServiceProvider provider) =>
            context.User.GetVoiceChannel() != default
                ? Task.FromResult(PreconditionResult.FromSuccess())
                : Task.FromResult(PreconditionResult.FromError(
                    $"{context.User.Username} attempted to issue command({command.Name}) that required being in a voice channel."));
    }
}