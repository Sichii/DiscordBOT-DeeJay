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
using System.Threading;
using System.Collections.Generic;

namespace DeeJay
{
    public class CommandHandler : ModuleBase<SocketCommandContext>
    {
        private static YouTubeService YouTubeService;
        private CommandService CommandService;

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
                ApiKey = File.ReadAllText(CONSTANTS.API_KEY_PATH).Trim(),
                ApplicationName = "DiscordBOT-DeeJay"
            });
        }

        internal async Task Initialize()
        {
            await CommandService.AddModulesAsync(Assembly.GetEntryAssembly(), null);
        }

        /// <summary>
        /// Parse input. If it's a command, executes relevant method.
        /// </summary>
        /// <param name="message">The message to parse.</param>
        internal Task TryHandleAsync(SocketMessage message)
        {
            //run this in another thread and return task completed to the caller, to allow the bot to more easily accept other commands
            Task.Run(async () =>
            {
                var msg = message as SocketUserMessage;
                var context = new SocketCommandContext(Client.SocketClient, msg);

                //pos will be the place we're at in the message after we check for the command prefix
                int pos = 0;

                //check if the message is null/etc, checks if it's command prefixed or user mentioned
                if (!string.IsNullOrWhiteSpace(context.Message?.Content) && !context.User.IsBot && (msg.HasCharPrefix('!', ref pos) || msg.HasMentionPrefix(Client.SocketClient.CurrentUser, ref pos)))
                {
                    try
                    {
                        //if it is, try to execute the command
                        IResult result = await CommandService.ExecuteAsync(context, pos, null, MultiMatchHandling.Best);

                        //log success/failure
                        if (!result.IsSuccess)
                            Console.WriteLine(result.ErrorReason);
                        else
                            Console.WriteLine("Command Success.");
                    }
                    catch (Exception ex)
                    {
                        //exceptions shouldnt reach this far, but just in case
                        Console.WriteLine($"{Environment.NewLine}{Environment.NewLine}UNKNOWN EXCEPTION - SEVERE{Environment.NewLine}{ex.Message}{Environment.NewLine}{Environment.NewLine}");
                    }
                }
            });

            return Task.CompletedTask;
        }

        [Command("queue"), Alias("q")]
        public async Task Queue([Remainder]string songName = default)
        {
            //check validity of song name
            if (string.IsNullOrWhiteSpace(songName))
                await Context.Channel.SendMessageAsync($"Invalid song name.");
            else
            {
                //create a youtube search object we can use to find/create the song object
                SearchResource.ListRequest searchRequest = YouTubeService.Search.List("snippet");
                searchRequest.Q = songName;
                searchRequest.Type = "video";
                searchRequest.MaxResults = 5;

                //searching for song...
                await Context.Channel.SendMessageAsync($"Searching for {songName}...");

                //create the song object from the request
                Song song = await Song.FromRequest(Context.User, searchRequest);

                //queue the song
                if (song != null)
                    Client.SongQueue.Enqueue(song);
                else
                    await Context.Channel.SendMessageAsync($"Something went wrong searching for {songName}.");

                //if we're already in a channel, begin playback
                if (Client.SongQueue.TryPeek(out Song s) && s == song && Client.AudioClient.Item2 != null)
                    await Play();
                else //otherwise let them know it's queued up
                    await Context.Channel.SendMessageAsync($"{song.SongTitle} has been queued! Use !come and !play to let it play for you.");
            }
        }

        [Command("play"), Alias("start", "begin")]
        public async Task Play()
        {
            //play through the queue until otherwise told
            while (true)
                //if we're not playing a song and there's one available...
                if (!Song.PlayTime.IsRunning && Client.SongQueue.TryPeek(out Song song))
                    try
                    {
                        //if we're already in a channel, rejoin it (this is to avoid some buggy shit with the api)
                        if (Client.AudioClient.Item2 != default)
                            await Client.JoinVoiceAsync((Context.User as IVoiceState).VoiceChannel);
                        else //if we're not in a channel, exit this method
                            return;

                        //send message saying what we're playing, then begin playing
                        await Context.Channel.SendMessageAsync($"Playing {song.SongTitle}!");
                        await Client.PlayAudioAsync(song, Song.PlayTime.ElapsedMilliseconds != 0);
                    }
                    catch (OperationCanceledException)
                    {
                        //thread abort exceptions from pausing/leaving will propogate to here, reset the token just incase it was something else
                        Client.CancellationTokenSource = new CancellationTokenSource();
                        Console.WriteLine("COMMAND - Playback paused.");
                        return;
                    }
                else //otherwise exit this method
                    return;
        }

        [Command("pause"), Alias("stop")]
        public async Task Pause()
        {
            Song.PlayTime.Stop();
            await Client.StopAudioAsync();
        }

        [Command("skip")]
        public async Task Skip()
        {
            await Pause();
            Song.PlayTime.Reset();
            Client.SongQueue.TryDequeue(out Song song);
            await Task.Run(async() => await Play());
        }

        [Command("come")]
        public async Task Come()
        {
            await Client.JoinVoiceAsync((Context.User as IVoiceState).VoiceChannel);

            if (Song.PlayTime.IsRunning)
            {
                Song.PlayTime.Stop();
                await Task.Run(async () => await Play());
            }
        }

        [Command("leave")]
        public async Task Leave()
        {
            await Pause();
            await Client.LeaveVoiceAsync();
        }

        [Command("showsong")]
        public async Task ShowSong()
        {
            if (Client.SongQueue.TryPeek(out Song song))
            {
                await Context.Channel.SendMessageAsync($"{song.SongTitle} [{Song.PlayTime.Elapsed.ToReadableString()} of {song.Duration.ToReadableString()}]");
            }
        }

        [Command("shownext")]
        public async Task ShowNext()
        {
            try
            {
                if (Client.SongQueue.Count > 1)
                {
                    Song song = Client.SongQueue.ToList()[1];
                    await Context.Channel.SendMessageAsync($"{song.SongTitle} [{song.Duration.ToReadableString()}]");
                }
            }
            catch { }
        }

        [Command("showqueue"), Alias("showq")]
        public async Task ShowQueue()
        {
            try
            {
                if (Client.SongQueue.Count > 1)
                {
                    var songs = new List<string>();

                    int i = 0;
                    foreach (Song song in Client.SongQueue.ToList())
                        songs.Add($"{i++}. {song.SongTitle} [{song.Duration.ToReadableString()}]");

                    await Context.User.SendMessageAsync(string.Join('\n', songs));
                }
            }
            catch { }
        }

        [Command("help"), Alias("commands")]
        public async Task Help()
        {
            await Context.User.SendMessageAsync(
                $"COMMAND | ALIASES [arguments] -- DESCRIPTION{Environment.NewLine}" +
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
}
