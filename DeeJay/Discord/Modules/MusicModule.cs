using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DeeJay.Definitions;
using DeeJay.Discord.Attributes;
using DeeJay.Model;
using DeeJay.Services;
using DeeJay.Utility;
using DeeJay.YouTube;
using Discord.Commands;
using NLog;

namespace DeeJay.Discord.Modules
{
    /// <summary>
    ///     A module that accepts a music service, and handles commands per-server.
    /// </summary>
    [Name(nameof(MusicModule)), RequireContext(ContextType.Guild), RequireDesignation, RequireVoiceChannel]
    public class MusicModule : DJModuleBase
    {
        private readonly MusicService MusicService;
        protected override Logger Log { get; }

        public MusicModule(MusicService musicService)
        {
            Log = LogManager.GetLogger($"MusMod-{musicService.GuildId}");
            MusicService = musicService;
        }

        [Command("designate"), Summary("Only accept commands from a channel"), Attributes.RequirePrivilege(PrivilegeLevel.Elevated)]
        public Task Designate()
        {
            MusicService.DesignatedChannelId = Context.Channel.Id;
            return RespondAsync(
                $"{Context.Channel.Name} is now the designated music channel for {Context.Guild.Name}. Ignoring commands in other channels.");
        }

        [Command("q"), Summary("Executes a youtube search and enqueues the first result")]
        public async Task Queue([Remainder] string songName = default)
        {
            //check validity of song name
            if (string.IsNullOrWhiteSpace(songName))
                await RespondAsync("No song name specified. (!q <songname>)");
            else
            {
                await Context.Message.DeleteAsync();
                var searchMsg = await RespondAsync($"Searching for {songName}...");
                Song song = null;
                var canceller = Canceller.New;

                try
                {
                    //create the song object from the request
                    song = await Song.FromRequest(Context.User, new YTRequest(songName), canceller);
                } catch (Exception e)
                {
                    Error(e.ToString());
                }

                if (song == null)
                {
                    await searchMsg.ModifyAsync(msg => msg.Content = Warn($"Something went wrong searching for {songName}."));
                    return;
                }

                if (!string.IsNullOrEmpty(song.ErrorMsg))
                {
                    await searchMsg.ModifyAsync(msg => msg.Content = Warn(song.ErrorMsg));
                    return;
                }

                //if this song title is already in queue, dont queue it, cancel the data task
                if (MusicService.SongQueue.Any(innerSong => innerSong.Title.EqualsI(song.Title)))
                {
                    await searchMsg.ModifyAsync(msg => msg.Content = Warn($"{song.Title} was already queued."));
                    await canceller.CancelAsync();
                    await song.DisposeAsync();
                    return;
                }

                if (MusicService.SongQueue.Count < 3)
                    song.TrySetData();

                MusicService.SongQueue.Enqueue(song);

                //if we're not inf a voice channel, join the caller's channel
                if (!MusicService.InVoice)
                    await MusicService.JoinVoiceAsync(Context.User.GetVoiceChannel());

                //if we're not currently playing audio, play the next song
                if (MusicService.State != MusicServiceState.Playing)
                {
                    await MusicService.JoinVoiceAsync(Context.User.GetVoiceChannel());
                    await MusicService.PlayAsync();
                }

                await searchMsg.ModifyAsync(msg => msg.Content = Info($"{song.Title} has been queued by {Context.User.Username}!"));
                await searchMsg.ModifyAsync(msg => msg.Embed = null);
            }
        }

        [Command("play"), Summary("Begins playback of the current song")]
        public async Task Play()
        {
            await MusicService.JoinVoiceAsync(Context.User.GetVoiceChannel());
            await MusicService.PlayAsync();
        }

        [Command("pause"), Summary("Pauses playback of the current song")]
        public async Task Pause()
        {
            if (await MusicService.PauseSongAsync(out var song))
                await RespondAsync($"Pausing {song.Title}.");
            else
                Warn("Attempted to pause when not already playing.");
        }

        [Command("skip"), Summary("Skips the current song"), RequirePrivilege(PrivilegeLevel.Elevated)]
        public async Task Skip()
        {
            if (await MusicService.SkipSongAsync(out var songTask))
                await RespondAsync($"Skipping {(await songTask).Title}");
            else
                Warn("Attempted to skip when not already playing.");
        }

        [Command("come"), Summary("Makes the bot join your voice channel")]
        public async Task Come()
        {
            await MusicService.JoinVoiceAsync(Context.User.GetVoiceChannel());
            Info($"Joining {MusicService.VoiceChannel.Name}");
        }

        [Command("leave"), Summary("Makes the bot leave it's voice channel")]
        public async Task Leave()
        {
            if (await MusicService.PauseSongAsync(out var song))
                Info($"Pausing {song.Title}");

            if (MusicService.InVoice)
            {
                Info($"Leaving {MusicService.VoiceChannel.Name}");
                await MusicService.LeaveVoiceAsync();
            } else
                Warn("Attempting to leave voice channel when not in one.");
        }

        [Command("show"), Summary("Displays info about the current song")]
        public Task ShowSong()
        {
            if (MusicService.SongQueue.TryPeek(out var song))
                return RespondAsync(song.ToString(true));

            Warn("Attempting to show song when no songs in queue.");
            return Task.CompletedTask;
        }

        [Command("shownext"), Summary("Displays info about the next song")]
        public async Task ShowNext()
        {
            if (MusicService.SongQueue.Count > 1)
            {
                var song = MusicService.SongQueue.Skip(1)
                    .First();
                await RespondAsync(song.ToString());
            } else
                Warn("Attempting to show next song info when there is no next song.");
        }

        [Command("showq"), Summary("Displays info about all songs in the queue")]
        public async Task ShowQueue()
        {
            if (MusicService.SongQueue.Count > 0)
            {
                var i = 1;
                var queueStr =
                    $"Displaying song queue.{Environment.NewLine}{string.Join(Environment.NewLine, MusicService.SongQueue.Select(song => $"{i++}. {song}"))}";
                await RespondAsync(queueStr);
            } else
                Warn("Attempting to display song queue when no songs are in queue.");
        }

        [Command("remove"), Summary("Removes a song from the queue at a given index")]
        public async Task Remove([Remainder] string songIndex = default)
        {
            if (int.TryParse(songIndex, out var index))
            {
                var song = await MusicService.RemoveSongAsync(index);

                if (song != null)
                    await RespondAsync($"Removed {song} from the queue.");
                else
                    await RespondAsync($"No song found at index {songIndex}");
            } else
                Warn($"Argument supplied is not an integer. ({songIndex})");

            await Task.CompletedTask;
        }

        [Command("clear"), Summary("Clears all songs from the queue"), RequirePrivilege(PrivilegeLevel.Elevated)]
        public async Task Clear()
        {
            if (await MusicService.PauseSongAsync(out var song))
                Info($"Pausing {song.Title}");

            await RespondAsync("Clearing the queue.");
            MusicService.SongQueue.Clear();
        }

        [Command("help"), Alias("commands"), Summary("The message you're currently reading")]
        public Task Help()
        {
            var commands = CommandHandler.CommandService.Commands.ToArray();
            var builder = new StringBuilder();

            string CreateParamStr(CommandInfo cmdInfo) =>
                cmdInfo.Parameters.Count > 0 ? $"<{string.Join("> ", cmdInfo.Parameters)}>" : "--";

            var names = new List<string> { "COMMAND" };
            var parameters = new List<string> { "PARAMETERS" };
            var summaries = new List<string> { "SUMMARY" };

            names.AddRange(commands.Select(cmd => '!' + cmd.Name)
                .NormalizeWidth(TextAlignment.LeftAlign));
            parameters.AddRange(commands.Select(CreateParamStr)
                .NormalizeWidth(TextAlignment.Center));
            summaries.AddRange(commands.Select(cmd => cmd.Summary));

            for (var i = 0; i < names.Count; i++)
                builder.AppendLine($"{names[i]}\t\t{parameters[i]}\t\t{summaries[i]}");

            return RespondAsync(builder.ToString());
        }

        #region Module Attributes

        /// <summary>
        ///     <inheritdoc />
        /// </summary>
        private class RequirePrivilege : Attributes.RequirePrivilege
        {
            internal RequirePrivilege(PrivilegeLevel privilegeLevel)
                : base(privilegeLevel) { }

            public override async Task<PreconditionResult> CheckPermissionsAsync(
                ICommandContext context, CommandInfo command, IServiceProvider services)
            {
                var baseResult = await base.CheckPermissionsAsync(context, command, services);
                var sProvider = (ServiceProvider) services;
                var mService = sProvider.GetService<MusicService>();

                if (!baseResult.IsSuccess && mService.SongQueue.TryPeek(out var song) && song.RequestedBy.Id == context.User.Id)
                    return PreconditionResult.FromSuccess();

                return baseResult;
            }
        }

        /// <summary>
        ///     Requires a command or module to execute within a designated text channel.
        ///     <para />
        /// </summary>
        public class RequireDesignation : PreconditionAttributeBase
        {
            public override Task<PreconditionResult> CheckPermissionsAsync(
                ICommandContext context, CommandInfo command, IServiceProvider services)
            {
                if (command.Module.Name == nameof(MusicModule))
                {
                    var sProvider = (ServiceProvider) services;
                    var mServ = sProvider.GetService<MusicService>();

                    return mServ.DesignatedChannelId == 0 || context.Channel.Id == mServ.DesignatedChannelId || command.Name == "Designate"
                        ? Success
                        : GenError($"Command({command.Name}) was not executed in the designated channel({mServ.DesignatedChannel?.Name})");
                }

                return GenError("This precondition should only be used on the music module.");
            }
        }

        #endregion
    }
}