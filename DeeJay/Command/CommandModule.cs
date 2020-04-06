using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DeeJay.Definitions;
using DeeJay.Model;
using Discord;
using Discord.Commands;
using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using NLog;

namespace DeeJay.Command
{
    public class CommandModule : ModuleBase<SocketCommandContext>
    {
        private static readonly YouTubeService YouTubeService;
        private readonly GuildMusicService MusicService;
        private readonly Logger Log;

        static CommandModule() =>
            YouTubeService = new YouTubeService(new BaseClientService.Initializer
            {
                ApiKey = File.ReadAllText(CONSTANTS.API_KEY_PATH)
                    .Trim(),
                ApplicationName = "DiscordBOT-DeeJay",
            });

        public CommandModule(GuildMusicService musicService)
        {
            MusicService = musicService;
            Log = LogManager.GetLogger($"CommandModule-{MusicService.GuildId.ToString()}");
        }


        [Command("queue"), Alias("q")]
        public async Task Queue([Remainder] string songName = default)
        {
            var logStr = $"[Command:{nameof(Queue)}][RequestedBy:{Context.User.Username}]";

            //check validity of song name
            if (string.IsNullOrWhiteSpace(songName))
            {
                Log.Debug($"{logStr} No song name specified.");
                await Context.Channel.SendMessageAsync("No song name specified. (!q <songname>)");
            } else
            {
                logStr += $"[Request:{songName}]";
                //create a youtube search object we can use to find/create the song object
                var searchRequest = YouTubeService.Search.List("snippet");
                searchRequest.Q = songName;
                searchRequest.Type = "video";
                searchRequest.MaxResults = 5;

                Log.Info($"{logStr} Searching.");
                //create the song object from the request
                var song = await Song.FromRequest(Context.User, searchRequest);

                if (song == null)
                {
                    Log.Error($"{logStr} No results for youtube search.");
                    await Context.Channel.SendMessageAsync($"Something went wrong searching for {songName}.");
                    return;
                }

                logStr += $"[Title:{song.Title}]";
                if (!string.IsNullOrEmpty(song.ErrorMsg))
                {
                    Log.Error($"{logStr}{song.ErrorMsg}");
                    await Context.Channel.SendMessageAsync(song.ErrorMsg);
                    return;
                }

                Log.Info($"{logStr} Queueing.");
                MusicService.SongQueue.Enqueue(song);

                //if we're not inf a voice channel, join the caller's channel
                if (MusicService.VoiceChannel == null)
                    await Come();

                //if we're not currently playing audio, play the next song
                if (!MusicService.Playing)
                {
                    await Come();
                    await Play();
                } else //otherwise let them know it's queued up
                    await Context.Channel.SendMessageAsync($"{song.Title} has been queued!");
            }
        }

        [Command("play")]
        public async Task Play()
        {
            var logStr = $"[Command:{nameof(Play)}][RequestedBy:{Context.User.Username}]";

            //if we're already playing music then return
            if (MusicService.Playing)
            {
                Log.Warn($"{logStr} Attempted to play when already playing.");
                return;
            }

            Log.Info($"{logStr} Playing {MusicService.SongQueue.First().Title}");
            await Come();
            await MusicService.Play();
        }

        [Command("pause"), Alias("stop")]
        public async Task Pause()
        {
            var logStr = $"[Command:{nameof(Pause)}][RequestedBy:{Context.User.Username}]";

            if(await MusicService.StopSongAsync())
                Log.Info($"{logStr} Stopping {MusicService.SongQueue.First().Title}");
            else
                Log.Warn($"{logStr} Attempted to pause when not already playing.");
        }

        [Command("skip")]
        public async Task Skip()
        {
            var logStr = $"[Command:{nameof(Skip)}][RequestedBy:{Context.User.Username}]";

            if (await MusicService.SkipSongAsync())
            {
                Log.Info($"{logStr} Skipping {MusicService.SongQueue.First().Title}");
            } else
                Log.Warn($"{logStr} Attempted to skip when not already playing.");
        }

        [Command("come")]
        public async Task Come()
        {
            var logStr = $"[Command:{nameof(Come)}][RequestedBy:{Context.User.Username}]";

            await MusicService.JoinVoiceAsync(((IVoiceState) Context.User).VoiceChannel);
            Log.Info($"{logStr} Joining {MusicService.VoiceChannel.Name}");
        }

        [Command("leave")]
        public async Task Leave()
        {
            var logStr = $"[Command:{nameof(Leave)}][RequestedBy:{Context.User.Username}]";

            if(await MusicService.StopSongAsync())
                Log.Info($"{logStr} Stopping {MusicService.SongQueue.First().Title}");

            if (MusicService.VoiceChannel != null)
            {
                Log.Info($"{logStr} Leaving {MusicService.VoiceChannel.Name}");
                await MusicService.LeaveVoiceAsync();
            } else
                Log.Warn($"{logStr} Attempting to leave voice channel when not in one.");
        }

        [Command("show"), Alias("song")]
        public Task ShowSong()
        {
            var logStr = $"[Command:{nameof(ShowSong)}][RequestedBy:{Context.User.Username}]";

            if (MusicService.SongQueue.TryPeek(out var song))
            {
                var songInfo = song.ToString(true);
                Log.Info($"{logStr} Displaying song info. \"{songInfo}\"");
                return Context.Channel.SendMessageAsync(songInfo);
            }

            Log.Warn($"{logStr} Attempting to show song when no songs in queue.");
            return Task.CompletedTask;
        }

        [Command("shownext"), Alias("next")]
        public async Task ShowNext()
        {
            var logStr = $"[Command:{nameof(ShowNext)}][RequestedBy:{Context.User.Username}]";

            if (MusicService.SongQueue.Count > 1)
            {
                var song = MusicService.SongQueue.Skip(1)
                    .First();
                var songInfo = song.ToString();
                Log.Info($"{logStr} Displaying next song info. \"{songInfo}\"");
                await Context.Channel.SendMessageAsync(songInfo);
            } else
                Log.Warn($"{logStr} Attempting to show next song info when there is no next song.");
        }

        [Command("showqueue"), Alias("showq")]
        public async Task ShowQueue()
        {
            var logStr = $"[Command:{nameof(ShowQueue)}][RequestedBy:{Context.User.Username}]";

            if (MusicService.SongQueue.Count > 0)
            {
                var i = 1;
                var queueStr = string.Join(Environment.NewLine, MusicService.SongQueue.Select(song => $"{i++}. {song}"));

                Log.Info($"{logStr} Displaying song queue.{Environment.NewLine}\"{queueStr}\"");
                await Context.Channel.SendMessageAsync(queueStr);
            } else
                Log.Warn($"{logStr} Attempting to display song queue when no songs are in queue.");
        }

        [Command("remove")]
        public async Task Remove([Remainder] string arg = default)
        {
            var logStr = $"[Command:{nameof(Remove)}][RequestedBy:{Context.User.Username}]";

            if (int.TryParse(arg, out var index))
            {
                var song = await MusicService.RemoveSongAsync(index);

                if (song != null)
                    Log.Info($"{logStr} Removed song at index {arg}. (Song: {song.Title}");
                else
                    Log.Warn($"{logStr} Failed to remove song at index {arg}. (Queue length:{MusicService.SongQueue.Count})");

                await Context.Channel.SendMessageAsync($"Removed {song} from the queue");
            } else
                Log.Warn($"{logStr} Argument supplied is not an integer. ({arg})");

            await Task.CompletedTask;
        }

        [Command("clear")]
        public async Task Clear()
        {
            var logStr = $"[Command:{nameof(Clear)}][RequestedBy:{Context.User.Username}]";

            if(await MusicService.StopSongAsync())
                Log.Info($"{logStr} Stopping {MusicService.SongQueue.First().Title}");

            Log.Info($"{logStr} Clearing song queue.");
            MusicService.SongQueue.Clear();
        }

        [Command("help"), Alias("commands")]
        public Task Help() =>
            Context.User.SendMessageAsync($"COMMAND | ALIASES [arguments] -- DESCRIPTION{Environment.NewLine}" +
                                          $"!queue | !q [song name] -- queues the first youtube result{Environment.NewLine}" +
                                          $"!play | !start -- begins playback in current voice channel{Environment.NewLine}" +
                                          $"!pause | !stop -- stops playback of current song{Environment.NewLine}" +
                                          $"!skis -- skips the current song and begins playback of the next{Environment.NewLine}" +
                                          $"!come -- joins your voice channel{Environment.NewLine}" +
                                          $"!leave -- leaves voice channel{Environment.NewLine}" +
                                          $"!showsong -- displays this song's information and progress{Environment.NewLine}" +
                                          $"!shownext -- displays the next song's information{Environment.NewLine}" +
                                          $"!showqueue | !showq -- dms you info on all songs in the queue{Environment.NewLine}" +
                                          $"!help | !commands -- this, obviously{Environment.NewLine}");
    }
}