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
                LogLevel = LogSeverity.Debug,
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

        internal Task TryHandle(SocketMessage message) => Task.Run(async() =>
        {
            var msg = message as SocketUserMessage;
            var context = new SocketCommandContext(Client.SocketClient, msg);

            int pos = 0;
            if (!string.IsNullOrWhiteSpace(context.Message?.Content) && !context.User.IsBot && (msg.HasCharPrefix('!', ref pos) || msg.HasMentionPrefix(Client.SocketClient.CurrentUser, ref pos)))
            {
                IResult result = await CommandService.ExecuteAsync(context, pos, null, MultiMatchHandling.Best);

                if (!result.IsSuccess)
                    Console.WriteLine(result.ErrorReason);
                else
                    Console.WriteLine("success");
            }
        });

        [Command("play")]
        public async Task Play([Remainder]string songName = default)
        {
            if (string.IsNullOrWhiteSpace(songName))
                await Context.Channel.SendMessageAsync($"Invalid song name. ({songName})");
            else
            {
                await Context.Channel.SendMessageAsync($"Searching for {songName}...");

                SearchResource.ListRequest searchRequest = YouTubeService.Search.List("snippet");
                searchRequest.Q = songName;
                searchRequest.Type = "video";
                searchRequest.MaxResults = 5;

                SearchListResponse searchResponse = await searchRequest.ExecuteAsync();
                Google.Apis.YouTube.v3.Data.SearchResult result = searchResponse.Items.FirstOrDefault(item => item.Id.Kind == "youtube#video");
                string targetURL = "https://www.youtube.com/watch?v=" + result.Id.VideoId;

                await Client.JoinVoice((Context.User as IVoiceState).VoiceChannel);

                var youtubedl = new Process()
                {
                    EnableRaisingEvents = true,
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = CONSTANTS.YOUTUBEDL_PATH,
                        Arguments = $"-g {targetURL}",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true
                    }
                };
                string output = "";
                youtubedl.OutputDataReceived += (s, e) => Task.Run(() =>
                {
                    output += e.Data;
                });

                youtubedl.Start();
                youtubedl.BeginOutputReadLine();
                youtubedl.WaitForExit();
                youtubedl.Dispose();

                await Context.Channel.SendMessageAsync($"Playing {result.Snippet.Title}!");
                await Client.PlayAudio(output.Trim());
                await Client.LeaveVoice();
            }
        }

        [Command("stop")]
        public async Task StopSong()
        {
            await Client.StopAudio();
        }

        [Command("leave")]
        public async Task Leave()
        {
            await Client.LeaveVoice();
        }

        [Command("help"), Alias("commands")]
        public async Task Help()
        {
            await Context.User.SendMessageAsync(
                $"COMMAND | ALIASES : DESCRIPTION{Environment.NewLine}" +
                $"!play [song name] : plays the first youtube result{Environment.NewLine}" +
                $"!stop : stops playback of current song{Environment.NewLine}" +
                $"!leave : forces bot to leave voice chat{Environment.NewLine}" +
                $"!help | !commands : this, obviously{Environment.NewLine}");
        }
    }
}
