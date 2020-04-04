using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using System.Linq;

namespace DeeJay
{
    public class CommandHandler : ModuleBase<SocketCommandContext>
    {
        private static YouTubeService YouTubeService;
        private readonly CommandService CommandService;

        internal CommandHandler()
        {
            CommandService = new CommandService(new CommandServiceConfig()
            {
                LogLevel = LogSeverity.Info,
                CaseSensitiveCommands = false,
                DefaultRunMode = RunMode.Async
            });
            YouTubeService = new YouTubeService(new BaseClientService.Initializer()
            {
                ApiKey = File.ReadAllText(CONSTANTS.API_KEY_PATH)
                    .Trim(),
                ApplicationName = "DiscordBOT-DeeJay"
            });
        }

        internal Task Initialize() => CommandService.AddModulesAsync(Assembly.GetEntryAssembly(), null);

        /// <summary>
        /// Parse input. If it's a command, executes relevant method.
        /// </summary>
        /// <param name="message">The message to parse.</param>
        internal async Task TryHandleAsync(SocketMessage message)
        {
            //run this in another thread and return task completed to the caller, to allow the bot to more easily accept other commands
            var msg = message as SocketUserMessage;
            var context = new SocketCommandContext(Client.SocketClient, msg);

            //pos will be the place we're at in the message after we check for the command prefix
            var pos = 0;

            //check if the message is null/etc, checks if it's command prefixed or user mentioned
            if (!string.IsNullOrWhiteSpace(context.Message?.Content) && !context.User.IsBot &&
                (msg.HasCharPrefix('!', ref pos) || msg.HasMentionPrefix(Client.SocketClient.CurrentUser, ref pos)))
                try
                {
                    //if it is, try to execute the command
                    var result = await CommandService.ExecuteAsync(context, pos, null, MultiMatchHandling.Best);

                    //log success/failure
                    Console.WriteLine(!result.IsSuccess ? result.ErrorReason : "Command Success.");
                } catch (Exception ex)
                {
                    //exceptions shouldnt reach this far, but just in case
                    Console.WriteLine(
                        $"{Environment.NewLine}{Environment.NewLine}UNKNOWN EXCEPTION - SEVERE{Environment.NewLine}{ex.Message}{Environment.NewLine}{Environment.NewLine}");
                }
        }

        [Command("queue"), Alias("q")]
        public async Task Queue([Remainder] string songName = default)
        {
            //check validity of song name
            if (string.IsNullOrWhiteSpace(songName))
                await Context.Channel.SendMessageAsync($"No song name specified. (!q <songname>)");
            else
            {
                //create a youtube search object we can use to find/create the song object
                var searchRequest = YouTubeService.Search.List("snippet");
                searchRequest.Q = songName;
                searchRequest.Type = "video";
                searchRequest.MaxResults = 5;

                //create the song object from the request
                var song = await Song.FromRequest(Context.User, searchRequest);

                //queue the song
                if (song != null)
                    Client.SongQueue.Enqueue(song);
                else
                {
                    await Context.Channel.SendMessageAsync($"Something went wrong searching for {songName}.");
                    return;
                }

                //if we're not inf a voice channel, join the caller's channel
                if (Client.VoiceChannel == null)
                    await Come();

                //if we're not currently playing audio, play the next song
                if (!Client.Playing)
                {
                    await Come();
                    await Play();
                } else //otherwise let them know it's queued up
                    await Context.Channel.SendMessageAsync($"{song.Title} has been queued!");
            }
        }

        [Command("play"), Alias("start", "begin")]
        public async Task Play()
        {
            //if we're already playing music then return
            if (Client.Playing)
                return;

            await Come();
            await Client.Play();
        }

        [Command("pause"), Alias("stop")]
        public Task Pause() => Client.StopSongAsync();

        [Command("skip")]
        public Task Skip() => Client.SkipSongAsync();

        [Command("come")]
        public async Task Come() => await Client.JoinVoiceAsync(((IVoiceState) Context.User).VoiceChannel);

        [Command("leave")]
        public async Task Leave()
        {
            await Client.StopSongAsync();
            await Client.LeaveVoiceAsync();
        }

        [Command("showsong")]
        public Task ShowSong() =>
            Client.SongQueue.TryPeek(out var song) ? Context.Channel.SendMessageAsync(song.ToString(true)) : Task.CompletedTask;

        [Command("shownext")]
        public async Task ShowNext()
        {
            if (Client.SongQueue.Count > 1)
            {
                var song = Client.SongQueue.ToList()[1];
                await Context.Channel.SendMessageAsync(song.ToString());
            }
        }

        [Command("showqueue"), Alias("showq")]
        public async Task ShowQueue()
        {
            if (Client.SongQueue.Count > 1)
            {
                var i = 1;
                var queueStr = string.Join(Environment.NewLine, Client.SongQueue.Select(song => $"{i++}. {song}"));

                await Context.User.SendMessageAsync(queueStr);
            }
        }

        [Command("remove")]
        public Task Remove([Remainder] string arg = default) =>
            int.TryParse(arg, out var index) ? Client.RemoveSongAsync(index) : Task.CompletedTask;

        [Command("clear")]
        public async Task Clear()
        {
            if (await Client.StopSongAsync())
                Client.SongQueue.Clear();
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
