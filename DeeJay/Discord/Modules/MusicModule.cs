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
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using NLog;

namespace DeeJay.Discord.Modules
{
    /// <summary>
    ///     A module that accepts a music service, and handles commands per-server.
    /// </summary>
    [Name(nameof(MusicModule)), RequireContext(ContextType.Guild), RequireDesignation]
    public class MusicModule : DJModuleBase
    {
        private readonly MusicService MusicService;
        protected override Logger Log { get; }

        public MusicModule(MusicService musicService)
        {
            Log = LogManager.GetLogger($"MscMdl-{musicService.GuildId}");
            MusicService = musicService;
        }

        [Command("designate"), Summary("Only accept commands from a channel"), Attributes.RequirePrivilege(Privilege.Elevated)]
        public Task Designate()
        {
            MusicService.DesignatedChannelId = Context.Channel.Id;
            return LogReplyAsync(
                $"{Context.Channel.Name} is now the designated music channel for {Context.Guild.Name}. Ignoring commands in other channels.");
        }

        [Command("skip"), Summary("Skips the current song"), RequirePrivilege(Privilege.Elevated), RequireVoiceChannel]
        public async Task Skip()
        {
            if (!MusicService.Playing)
            {
                Warn("Attempted to skip when not already playing.");
                return;
            }

            var song = MusicService.NowPlaying;

            if (song == null)
            {
                Error("Music service is playing, but there are no songs in the queue.");
                MusicService.State = MusicServiceState.None;
                return;
            }

            await ReplyAsync($"Skipping {song.Title}..."); //RespondAsync($"Skipping {song.Title}...");
            await MusicService.SkipAsync(out _);
        }

        [Command("clear"), Summary("Clears all songs from the queue"), RequirePrivilege(Privilege.Elevated), RequireVoiceChannel]
        public async Task Clear()
        {
            var song = MusicService.NowPlaying;

            if (song != null)
                await MusicService.PauseAsync(out _);

            await ReplyAsync("Clearing the queue...");
            await MusicService.ClearQueueAsync();
        }

        [Command("slowmode"), Summary("Only allows a specified number of songs in the queue per person."),
         RequirePrivilege(Privilege.Elevated)]
        public async Task SlowMode(byte maxSongsPerUser = 0)
        {
            if (maxSongsPerUser == 0)
                await LogReplyAsync("SlowMode is now off.");
            else
                await LogReplyAsync($"SlowMode is now on. Max {maxSongsPerUser} songs per user.");

            MusicService.SlowMode = maxSongsPerUser;
        }

        [Command("q"), Summary("Executes a youtube search and enqueues the first result"), RequireVoiceChannel]
        public async Task Queue([Remainder] string songName = default)
        {
            //check validity of song name
            if (string.IsNullOrWhiteSpace(songName))
                await LogReplyAsync("No song name specified. (!q <songname>)");
            else
            {
                if (!MusicService.CanQueue(Context.User.Id))
                {
                    await LogReplyAsync(
                        $"Queue failed, \"!slowmode {MusicService.SlowMode}\" is currently activated. ({MusicService.SlowMode} queued song(s) per user)");
                    return;
                }

                Song song;
                IUserMessage searchMsg;
                var canceller = new Canceller();

                try
                {
                    var songTask = Song.FromRequest(Context.User, new YTRequest(songName), canceller);
                    var searchTask = LogReplyAsync($"Searching for {songName}...");
                    await Task.WhenAll(Context.Message.DeleteAsync(), songTask, searchTask);

                    song = await songTask;
                    searchMsg = await searchTask;
                } catch (Exception e)
                {
                    Error(e.ToString());
                    return;
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
                    await Task.WhenAll(searchMsg.ModifyAsync(msg => msg.Content = Warn($"{song.Title} was already queued.")),
                        canceller.CancelAsync(), song.DisposeAsync()
                            .AsTask());
                    return;
                }

                if (MusicService.SongQueue.Count < 3)
                    song.TrySetData();

                MusicService.SongQueue.Enqueue(song);

                //if we're not in a voice channel, join the caller's channel
                await MusicService.JoinVoiceAsync(Context.User.GetVoiceChannel());

                //if we're not currently playing audio, play the next song
                await MusicService.PlayAsync();

                await searchMsg.ModifyAsync(msg =>
                {
                    msg.Content = Info($"{song.Title} has been queued by {Context.User.Username}!");
                    msg.Embed = null;
                });
            }
        }

        [Command("play"), Summary("Begins playback of the current song"), RequireVoiceChannel]
        public async Task Play()
        {
            //if we're not in a voice channel, join the caller's channel
            await MusicService.JoinVoiceAsync(Context.User.GetVoiceChannel());

            //if we're not currently playing audio, play the next song
            await MusicService.PlayAsync();
        }

        [Command("pause"), Summary("Pauses playback of the current song"), RequireVoiceChannel]
        public async Task Pause()
        {
            if (MusicService.Playing)
            {
                var song = MusicService.NowPlaying;

                if (song != null)
                {
                    await ReplyAsync($"Pausing {song.Title}.");
                    await MusicService.PauseAsync(out _);
                } else
                    MusicService.State = MusicServiceState.None;
            } else
                Warn("Attempted to pause when not already playing.");
        }

        [Command("come"), Summary("Makes the bot join your voice channel"), RequireVoiceChannel]
        public Task Come() =>
            MusicService.JoinVoiceAsync(Context.User.GetVoiceChannel())
                .AsTask();

        [Command("leave"), Summary("Makes the bot leave it's voice channel"), RequireVoiceChannel]
        public async Task Leave()
        {
            await MusicService.PauseAsync(out _);

            if (MusicService.InVoice)
                await MusicService.LeaveVoiceAsync();
            else
                Warn("Attempting to leave voice channel when not in one.");
        }

        [Command("show"), Summary("Displays info about the current song")]
        public Task ShowSong()
        {
            var song = MusicService.NowPlaying;

            if (song == null)
            {
                Warn("Attempting to show song when no song is playing.");
                return Task.CompletedTask;
            }

            return LogReplyAsync(song.ToString(true));
        }

        [Command("shownext"), Summary("Displays info about the next song")]
        public async Task ShowNext()
        {
            if (MusicService.SongQueue.Count > 1)
            {
                var song = MusicService.SongQueue.Skip(1)
                    .First();
                await LogReplyAsync(song.ToString());
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
                await LogReplyAsync(queueStr);
            } else
                Warn("Attempting to display song queue when no songs are in queue.");
        }

        [Command("remove"), Summary("Removes a song from the queue at a given index"), RequireVoiceChannel]
        public async Task Remove([Remainder] string songIndex = default)
        {
            if (int.TryParse(songIndex, out var index))
            {
                var song = await MusicService.RemoveSongAsync(index);

                if (song != null)
                    await ReplyAsync($"Removed {song} from the queue.");
                else
                    await ReplyAsync($"No song found at index {songIndex}");
            } else
                Warn($"Argument supplied is not an integer. ({songIndex})");

            await Task.CompletedTask;
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

            return LogReplyAsync(builder.ToString());
        }

        #region Module Attributes

        /// <summary>
        ///     <inheritdoc />
        /// </summary>
        private class RequirePrivilege : Attributes.RequirePrivilege
        {
            internal RequirePrivilege(Privilege privilege)
                : base(privilege) { }

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
                    var guild = (SocketGuild) context.Guild;
                    var designatedChannel = guild.GetTextChannel(mServ.DesignatedChannelId);

                    return mServ.DesignatedChannelId == 0 || context.Channel.Id == mServ.DesignatedChannelId || command.Name == "Designate"
                        ? Success
                        : GenError($"Command({command.Name}) was not executed in the designated channel({designatedChannel?.Name})");
                }

                return GenError("This precondition should only be used on the music module.");
            }
        }

        #endregion
    }
}