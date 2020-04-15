using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using NLog;

namespace DeeJay.DiscordModel
{
    /// <summary>
    ///     Custom base class to handle logging and other common things done in a module.
    /// </summary>
    public abstract class CommandModuleBase : ModuleBase<SocketCommandContext>
    {
        protected abstract Logger Log { get; }

        protected string Error(string errorMsg, string logPrefix = null)
        {
            Log.Error($"{logPrefix ?? string.Empty} {errorMsg}".Trim());
            return errorMsg;
        }

        protected string Warn(string errorMsg, string logPrefix = null)
        {
            Log.Warn($"{logPrefix ?? string.Empty} {errorMsg}".Trim());
            return errorMsg;
        }

        protected string Info(string errorMsg, string logPrefix = null)
        {
            Log.Info($"{logPrefix ?? string.Empty} {errorMsg}".Trim());
            return errorMsg;
        }

        protected string Debug(string errorMsg, string logPrefix = null)
        {
            Log.Debug($"{logPrefix ?? string.Empty} {errorMsg}".Trim());
            return errorMsg;
        }

        protected string Trace(string errorMsg, string logPrefix = null)
        {
            Log.Trace($"{logPrefix ?? string.Empty} {errorMsg}".Trim());
            return errorMsg;
        }

        protected async Task<IUserMessage> RespondAsync(string response, string logPrefix = null) =>
            await ReplyAsync(Debug(response, logPrefix));

        protected Task<IUserMessage> DirectRespond(string response, string logPrefix = null) =>
            Context.User.SendMessageAsync(Debug(response, logPrefix));
    }
}