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
        internal CommandService CommandService;

        internal async Task Initialize()
        {
            CommandService = new CommandService(new CommandServiceConfig()
            {
                LogLevel = LogSeverity.Debug,
                CaseSensitiveCommands = false,
                DefaultRunMode = RunMode.Async
            });

            YouTubeService = new YouTubeService(new BaseClientService.Initializer()
            {
                ApiKey = await File.ReadAllTextAsync(CONSTANTS.API_KEY_PATH),
                ApplicationName = "DiscordBOT-DeeJay"
            });

            await CommandService.AddModulesAsync(Assembly.GetEntryAssembly(), null);
        }

        internal Task TryHandle(DiscordSocketClient client, SocketMessage message) => Task.Run(async() =>
        {
            var msg = message as SocketUserMessage;
            var context = new SocketCommandContext(client, msg);

            int pos = 0;
            if (!string.IsNullOrWhiteSpace(context.Message?.Content) && !context.User.IsBot && (msg.HasCharPrefix('!', ref pos) || msg.HasMentionPrefix(client.CurrentUser, ref pos)))
            {
                IResult result = await CommandService.ExecuteAsync(context, pos, null, MultiMatchHandling.Best);

                if (!result.IsSuccess)
                    Console.WriteLine(result.ErrorReason);
                else
                    Console.WriteLine("success");
            }
        });

        [Command("playsong"), Alias("play")]
        public async Task PlaySong([Remainder]string songName = default)
        {
            if (string.IsNullOrWhiteSpace(songName))
                await Context.Channel.SendMessageAsync($"Invalid song name. ({songName})");
            else
            {
                Task<Discord.Rest.RestUserMessage> t1 = Context.Channel.SendMessageAsync($"Searching for {songName}...");

                SearchResource.ListRequest searchRequest = YouTubeService.Search.List("snippet");
                searchRequest.Q = songName;
                searchRequest.Type = "video";
                searchRequest.MaxResults = 5;

                SearchListResponse searchResponse = await searchRequest.ExecuteAsync();
                Google.Apis.YouTube.v3.Data.SearchResult useMe = searchResponse.Items.FirstOrDefault(result => result.Id.Kind == "youtube#video");
                string targetURL = "https://www.youtube.com/watch?v=" + useMe.Id.VideoId;

                var youtubedl = new Process()
                {
                    EnableRaisingEvents = true,
                    StartInfo = new ProcessStartInfo("youtube-dl.exe", "-g")
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true
                    }
                };
                youtubedl.OutputDataReceived += (s, e) =>
                {
                    //use target url to get audio stream
                    //use audio stream to play audio in discord channel
                };

                youtubedl.Start();
                youtubedl.BeginOutputReadLine();
            }
        }

    }
}
