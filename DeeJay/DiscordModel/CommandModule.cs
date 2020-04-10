using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DeeJay.Definitions;
using DeeJay.Model;
using Discord;
using Discord.Commands;

namespace DeeJay.DiscordModel
{
    /// <summary>
    ///     A module that accepts a music service, and handles commands per-server.
    /// </summary>
    public class CommandModule : CommandModuleBase
    {
        public CommandModule(MusicService musicService)
            : base(musicService) { }

        [Command("designate"), Summary("Only accept commands from a channel")]
        public Task Designate()
        {
            if (MusicService.DesignatedChannelId != 0 || Context.User.Username.EqualsI("sichi"))
            {
                MusicService.DesignatedChannelId = Context.Channel.Id;
                return RespondAsync(
                    $"{Context.Channel.Name} is now the designated music channel for {Context.Guild.Name}. Ignoring commands in other channels.");
            }

            return Task.CompletedTask;
        }

        [Command("q"), Summary("Executes a youtube search and enqueues the first result")]
        public async Task Queue([Remainder] string songName = default)
        {
            if (!IsDesignatedChannel)
                return;

            var logStr = $"[Command:{nameof(Queue)}][RequestedBy:{Context.User.Username}]";

            //check validity of song name
            if (string.IsNullOrWhiteSpace(songName))
                await RespondAsync("No song name specified. (!q <songname>)", logStr);
            else
            {
                logStr += $"[Request:{songName}]";
                //remove youtube link embeds if there is one
                await Context.Message.DeleteAsync();
                var searchMsg = await RespondAsync($"Searching for {songName}...", logStr);

                //create the song object from the request
                var song = await Song.FromRequest(Context.User, new YTRequest(songName));

                if (song == null)
                {
                    await searchMsg.ModifyAsync(msg => msg.Content = Warn($"Something went wrong searching for {songName}.", logStr));
                    return;
                }

                logStr += $"[Title:{song.Title}]";
                if (!string.IsNullOrEmpty(song.ErrorMsg))
                {
                    await searchMsg.ModifyAsync(msg => msg.Content = Warn(song.ErrorMsg, logStr));
                    return;
                }

                //if this song title is already in queue, dont queue it, cancel the data task
                if (MusicService.SongQueue.Any(innerSong => innerSong.Title.EqualsI(song.Title)))
                {
                    await searchMsg.ModifyAsync(msg => msg.Content = Warn($"{song.Title} was already queued.", logStr));
                    song.Canceller.Cancel();
                    await song.DisposeAsync();
                    return;
                }

                if (MusicService.SongQueue.Count < 3)
                    song.TrySetData();

                MusicService.SongQueue.Enqueue(song);

                //if we're not inf a voice channel, join the caller's channel
                if (!MusicService.InVoice)
                    await MusicService.JoinVoiceAsync(((IVoiceState) Context.User).VoiceChannel);

                //if we're not currently playing audio, play the next song
                if (!MusicService.Playing)
                {
                    await MusicService.JoinVoiceAsync(((IVoiceState) Context.User).VoiceChannel);
                    await MusicService.PlayAsync();
                } else //otherwise let them know it's queued up
                    await searchMsg.ModifyAsync(msg =>
                        msg.Content = Info($"{song.Title} has been queued by {Context.User.Username}!", logStr));

                await searchMsg.ModifyAsync(msg => msg.Embed = null);
            }
        }

        [Command("play"), Summary("Begins playback of the current song")]
        public async Task Play()
        {
            if (!IsDesignatedChannel)
                return;

            var logStr = $"[Command:{nameof(Play)}][RequestedBy:{Context.User.Username}]";

            //if we're already playing music then return
            if (MusicService.Playing)
            {
                Warn($"{logStr} Attempted to play when already playing.");
                return;
            }

            var songTitle = MusicService.SongQueue.FirstOrDefault()
                ?.Title;

            if (string.IsNullOrEmpty(songTitle))
            {
                Warn($"{logStr} Attempted to play while no song in the queue.");
                return;
            }

            await MusicService.JoinVoiceAsync(((IVoiceState) Context.User).VoiceChannel);
            await MusicService.PlayAsync();
        }

        [Command("pause"), Summary("Pauses playback of the current song")]
        public async Task Pause()
        {
            if (!IsDesignatedChannel)
                return;

            var logStr = $"[Command:{nameof(Pause)}][RequestedBy:{Context.User.Username}]";

            if (await MusicService.PauseSongAsync(out var song))
                await RespondAsync($"Pausing {song.Title}.", logStr);
            else
                Warn("Attempted to pause when not already playing.", logStr);
        }

        [Command("skip"), Summary("Skips the current song")]
        public async Task Skip()
        {
            if (!IsDesignatedChannel)
                return;

            var logStr = $"[Command:{nameof(Skip)}][RequestedBy:{Context.User.Username}]";

            if (await MusicService.SkipSongAsync(out var songTask))
                await RespondAsync($"Skipping {(await songTask).Title}", logStr);
            else
                Warn($"{logStr} Attempted to skip when not already playing.");
        }

        [Command("come"), Summary("Makes the bot join your voice channel")]
        public async Task Come()
        {
            if (!IsDesignatedChannel)
                return;

            var logStr = $"[Command:{nameof(Come)}][RequestedBy:{Context.User.Username}]";

            await MusicService.JoinVoiceAsync(((IVoiceState) Context.User).VoiceChannel);
            Info($"Joining {MusicService.VoiceChannel.Name}", logStr);
        }

        [Command("leave"), Summary("Makes the bot leave it's voice channel")]
        public async Task Leave()
        {
            if (!IsDesignatedChannel)
                return;

            var logStr = $"[Command:{nameof(Leave)}][RequestedBy:{Context.User.Username}]";

            if (await MusicService.PauseSongAsync(out var song))
                Info($"Pausing {song.Title}", logStr);

            if (MusicService.InVoice)
            {
                Info($"Leaving {MusicService.VoiceChannel.Name}", logStr);
                await MusicService.LeaveVoiceAsync();
            } else
                Warn("Attempting to leave voice channel when not in one.", logStr);
        }

        [Command("show"), Summary("Displays info about the current song")]
        public Task ShowSong()
        {
            if (!IsDesignatedChannel)
                return Task.CompletedTask;

            var logStr = $"[Command:{nameof(ShowSong)}][RequestedBy:{Context.User.Username}]";

            if (MusicService.SongQueue.TryPeek(out var song))
            {
                logStr += "Displaying song info.";
                return RespondAsync(song.ToString(true), logStr);
            }

            Warn("Attempting to show song when no songs in queue.", logStr);
            return Task.CompletedTask;
        }

        [Command("shownext"), Summary("Displays info about the next song")]
        public async Task ShowNext()
        {
            if (!IsDesignatedChannel)
                return;

            var logStr = $"[Command:{nameof(ShowNext)}][RequestedBy:{Context.User.Username}]";

            if (MusicService.SongQueue.Count > 1)
            {
                var song = MusicService.SongQueue.Skip(1)
                    .First();
                logStr += "Displaying next song info.";
                await RespondAsync(song.ToString(), logStr);
            } else
                Warn("Attempting to show next song info when there is no next song.", logStr);
        }

        [Command("showq"), Summary("Displays info about all songs in the queue")]
        public async Task ShowQueue()
        {
            if (!IsDesignatedChannel)
                return;

            var logStr = $"[Command:{nameof(ShowQueue)}][RequestedBy:{Context.User.Username}]";

            if (MusicService.SongQueue.Count > 0)
            {
                var i = 1;
                var queueStr = string.Join(Environment.NewLine, MusicService.SongQueue.Select(song => $"{i++}. {song}"));
                logStr += $"Displaying song queue.{Environment.NewLine}";

                await RespondAsync(queueStr, logStr);
            } else
                Warn("Attempting to display song queue when no songs are in queue.", logStr);
        }

        [Command("remove"), Summary("Removes a song from the queue at a given index")]
        public async Task Remove([Remainder] string songIndex = default)
        {
            if (!IsDesignatedChannel)
                return;

            var logStr = $"[Command:{nameof(Remove)}][RequestedBy:{Context.User.Username}]";

            if (int.TryParse(songIndex, out var index))
            {
                var song = await MusicService.RemoveSongAsync(index);

                if (song != null)
                    await RespondAsync($"Removed {song} from the queue.", logStr);
                else
                    await RespondAsync($"No song found at index {songIndex}", logStr);
            } else
                Warn($"Argument supplied is not an integer. ({songIndex})", logStr);

            await Task.CompletedTask;
        }

        [Command("clear"), Summary("Clears all songs from the queue")]
        public async Task Clear()
        {
            if (!IsDesignatedChannel)
                return;

            var logStr = $"[Command:{nameof(Clear)}][RequestedBy:{Context.User.Username}]";

            if (await MusicService.PauseSongAsync(out var song))
                Info($"Pausing {song.Title}", logStr);

            await RespondAsync("Clearing the queue.", logStr);
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

            names.AddRange(commands.Select(cmd => '!' + cmd.Name));
            names = names.NormalizeWidth(TextAlignment.LeftAlign)
                .ToList();
            parameters.AddRange(commands.Select(CreateParamStr));
            parameters = parameters.NormalizeWidth(TextAlignment.Center)
                .ToList();
            summaries.AddRange(commands.Select(cmd => cmd.Summary));

            for (var i = 0; i < names.Count; i++)
                builder.AppendLine($"{names[i]}\t\t{parameters[i]}\t\t{summaries[i]}");

            return RespondAsync(builder.ToString());
        }
    }
}