using System.Threading.Tasks;
using DeeJay.Model;
using Discord;
using Discord.Commands;
using NLog;

namespace DeeJay.DiscordModel
{
    public abstract class CommandModuleBase : ModuleBase<SocketCommandContext>
    {
        private readonly Logger Log;
        protected readonly MusicService MusicService;

        protected bool IsDesignatedChannel =>
            MusicService.DesignatedChannelId == 0 || Context.Channel.Id == MusicService.DesignatedChannelId;

        protected CommandModuleBase(MusicService musicService)
        {
            MusicService = musicService;
            Log = LogManager.GetLogger($"CmdServ-{MusicService.GuildId.ToString()}");
        }

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