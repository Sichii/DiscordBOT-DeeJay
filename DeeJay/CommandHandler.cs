using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Discord.Audio;
using Discord.API;
using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using System.Linq;
using System.Diagnostics;
using System.Threading;
using SearchResult = Google.Apis.YouTube.v3.Data.SearchResult;

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

        internal Task TryHandleAsync(SocketMessage message)
        {
            Task.Run(async () =>
            {
                var msg = message as SocketUserMessage;
                var context = new SocketCommandContext(Client.SocketClient, msg);

                int pos = 0;
                if (!string.IsNullOrWhiteSpace(context.Message?.Content) && !context.User.IsBot && (msg.HasCharPrefix('!', ref pos) || msg.HasMentionPrefix(Client.SocketClient.CurrentUser, ref pos)))
                {
                    try
                    {
                        IResult result = await CommandService.ExecuteAsync(context, pos, null, MultiMatchHandling.Best);

                        if (!result.IsSuccess)
                            Console.WriteLine(result.ErrorReason);
                        else
                            Console.WriteLine("Command Success.");
                    }
                    catch (OperationCanceledException)
                    {
                        Console.WriteLine("Previous playback stopped.");
                    }
                }
            });

            return Task.CompletedTask;
        }

        [Command("queue"), Alias("q")]
        public async Task Queue([Remainder]string songName = default)
        {
            if (string.IsNullOrWhiteSpace(songName))
                await Context.Channel.SendMessageAsync($"Invalid song name.");
            else
            {
                Song song = default;
                SearchResource.ListRequest searchRequest = YouTubeService.Search.List("snippet");
                searchRequest.Q = songName;
                searchRequest.Type = "video";
                searchRequest.MaxResults = 5;

                Task<Discord.Rest.RestUserMessage> messageTask = Context.Channel.SendMessageAsync($"Searching for {songName}...");
                var songTask = Task.Run(async() => { song = await Song.FromRequest(Context.User, searchRequest); });
                await messageTask;
                await songTask;

                if (song != null)
                    Client.SongQueue.Enqueue(song);
                else
                    await Context.Channel.SendMessageAsync($"Something went wrong searching for {songName}.");

                if (Client.SongQueue.TryPeek(out Song s) && s == song && Client.AudioClient.Item2 != null)
                    await Play();
                else
                    await Context.Channel.SendMessageAsync($"{song.SongTitle} has been queued! Use !come and !play to let it play for you.");
            }
        }

        [Command("play"), Alias("start", "begin")]
        public async Task Play()
        {
            //play the next song in the queue if it's not already playing
            //if playtime has elapsed, instruct the player to seek to elapsed time
            while (true)
                if (!Song.PlayTime.IsRunning && Client.SongQueue.TryPeek(out Song song))
                    try
                    {
                        if (Client.AudioClient.Item2 != default)
                            await Client.JoinVoiceAsync((Context.User as IVoiceState).VoiceChannel);
                        else
                            return;

                        await Context.Channel.SendMessageAsync($"Playing {song.SongTitle}!");
                        await Client.PlayAudioAsync(song, Song.PlayTime.ElapsedMilliseconds != 0);
                    }
                    catch (OperationCanceledException)
                    {
                        Client.CancellationTokenSource = new CancellationTokenSource();
                        return;
                    }
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
                await Context.Channel.SendMessageAsync($"{song.SongTitle} [{Song.PlayTime.Elapsed.ToString("g")} of {song.Duration.ToString("g")}]");
        }

        [Command("shownext")]
        public async Task ShowNext()
        {
            try
            {
                if (Client.SongQueue.Count > 1)
                {
                    Song song = Client.SongQueue.ToList()[1];
                    await Context.Channel.SendMessageAsync($"{song.SongTitle} [{song.Duration.ToString("g")}]");
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
                    foreach (Song song in Client.SongQueue.ToList())
                        await Context.User.SendMessageAsync($"{song.SongTitle} [{song.Duration.ToString("g")}]");
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
