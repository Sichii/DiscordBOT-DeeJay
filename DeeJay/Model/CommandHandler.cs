using System;
using System.Threading.Tasks;
using DeeJay.Discord.Modules;
using DeeJay.Services;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using NLog;

namespace DeeJay.Model
{
    /// <summary>
    ///     Handles commands that appear in any server that the bot is part of.
    /// </summary>
    public static class CommandHandler
    {
        internal static readonly CommandService CommandService = new CommandService(new CommandServiceConfig
        {
            LogLevel = LogSeverity.Info,
            CaseSensitiveCommands = false,
            DefaultRunMode = RunMode.Async
        });

        private static readonly Logger Log = LogManager.GetLogger("CmdHnd");

        static CommandHandler() => CommandService.AddModuleAsync<MusicModule>(ServiceProvider.DefaultInstance);

        /// <summary>
        ///     Parse input. If it's a command, executes relevant method.
        /// </summary>
        /// <param name="client">The bot's discord api client.</param>
        /// <param name="message">The message to parse.</param>
        public static async Task TryHandleAsync(DiscordSocketClient client, SocketMessage message)
        {
            var msg = (SocketUserMessage) message;
            var context = new SocketCommandContext(client, msg);

            //if we dont have a serviceprovider for this guild
            if (!Client.Providers.TryGetValue(context.Guild.Id, out var serviceProvider))
            {
                Log.Debug($"Creating new service provider for guild {context.Guild.Id}.");
                //create the serviceprovider and store it under the guild id
                serviceProvider = new ServiceProvider(context.Guild.Id);
                Client.Providers[context.Guild.Id] = serviceProvider;
            }

            //pos will be the place we're at in the message after we check for the command prefix
            var pos = 0;

            //check if the message is null/etc, checks if it's command prefixed or user mentioned
            if (!string.IsNullOrWhiteSpace(context.Message?.Content) && !context.User.IsBot &&
                (msg.HasCharPrefix('!', ref pos) || msg.HasMentionPrefix(client.CurrentUser, ref pos)))
                try
                {
                    //if it is, try to execute the command using serviceprovider
                    var result = await CommandService.ExecuteAsync(context, pos, serviceProvider, MultiMatchHandling.Best);

                    //print errors
                    if (!result.IsSuccess)
                        Log.Error($"Guild: {context.Guild.Id} ERROR: {result.ErrorReason}");
                } catch (Exception ex)
                {
                    //exceptions shouldnt reach this far, but just in case
                    Log.Error(
                        $"{Environment.NewLine}{Environment.NewLine}UNKNOWN EXCEPTION - SEVERE{Environment.NewLine}{ex.Message}{Environment.NewLine}{Environment.NewLine}");
                }
        }
    }
}