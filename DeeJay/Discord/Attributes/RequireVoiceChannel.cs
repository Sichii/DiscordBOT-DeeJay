using System;
using System.Threading.Tasks;
using DeeJay.Definitions;
using Discord.Commands;

namespace DeeJay.Discord.Attributes
{
    /// <summary>
    ///     Requires the user executing the command to be in a voice channel.
    /// </summary>
    public class RequireVoiceChannel : PreconditionAttributeBase
    {
        public override Task<PreconditionResult> CheckPermissionsAsync(
            ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            if (context.User.GetVoiceChannel() != default)
                return Success;

            context.Channel.SendMessageAsync(
                $"{context.User.Mention}, you must be in a voice channel to execute the \"{command.Name}\" command.");
            return GenError($"{context.User.Username} attempted to issue command({command.Name}) while not being in a voice channel.");
        }
    }
}