using System;
using System.Threading.Tasks;
using DeeJay.Model;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using NLog;

namespace DeeJay.Command
{
    /// <summary>
    /// Handles commands that appear in any server that the bot is part of.
    /// </summary>
    public static class CommandHandler
    {
        private static readonly Logger Log = LogManager.GetLogger("CommandHandler");
        private static readonly CommandService CommandService = new CommandService(new CommandServiceConfig
        {
            LogLevel = LogSeverity.Info,
            CaseSensitiveCommands = false,
            DefaultRunMode = RunMode.Async
        });

        /// <summary>
        ///     Parse input. If it's a command, executes relevant method.
        /// </summary>
        /// <param name="message">The message to parse.</param>
        public static async Task TryHandleAsync(SocketMessage message)
        {
            var client = Client.SocketClient;
            var msg = (SocketUserMessage) message;
            var context = new SocketCommandContext(client, msg);

            //if we dont have a music service for this guild
            if (!Client.Services.TryGetValue(context.Guild.Id, out var musicService))
            {
                Log.Debug($"Creating new service and module for guild {context.Guild.Id}.");
                //create the music service and store it under the guild id
                musicService = new GuildMusicService(context.Guild.Id);

                if(Client.Services.IsEmpty)
                    await CommandService.AddModuleAsync<CommandModule>(musicService);

                Client.Services[context.Guild.Id] = musicService;
            }

            var guildLog = LogManager.GetLogger($"CommandModule-{context.Guild.Id.ToString()}");

            //pos will be the place we're at in the message after we check for the command prefix
            var pos = 0;

            //check if the message is null/etc, checks if it's command prefixed or user mentioned
            if (!string.IsNullOrWhiteSpace(context.Message?.Content) && !context.User.IsBot &&
                (msg.HasCharPrefix('!', ref pos) || msg.HasMentionPrefix(client.CurrentUser, ref pos)))
                try
                {
                    guildLog.Info($"Executing command {msg.Content}");
                    //if it is, try to execute the command using music service
                    var result = await CommandService.ExecuteAsync(context, pos, musicService, MultiMatchHandling.Best);

                    //print errors
                    if (!result.IsSuccess)
                        guildLog.Error($"ERROR: {result.ErrorReason}");
                } catch (Exception ex)
                {
                    //exceptions shouldnt reach this far, but just in case
                    guildLog.Error(
                        $"{Environment.NewLine}{Environment.NewLine}UNKNOWN EXCEPTION - SEVERE{Environment.NewLine}{ex.Message}{Environment.NewLine}{Environment.NewLine}");
                }
        }
    }
}