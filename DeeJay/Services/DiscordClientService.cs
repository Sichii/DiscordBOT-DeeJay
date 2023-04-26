using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using DeeJay.Abstractions;
using DeeJay.Services.Options;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DeeJay.Services;

/// <inheritdoc cref="IDiscordClientService"/>
public class DiscordClientService : BackgroundService, IDiscordClientService
{
    /// <inheritdoc />
    public DiscordSocketClient SocketClient { get; }
    
    private readonly IOptionsMonitor<DiscordClientServiceOptions> OptionsMonitor;
    private readonly ILogger<DiscordClientService> Logger;
    private DiscordClientServiceOptions Options => OptionsMonitor.CurrentValue;
    private readonly InteractionService InteractionService;
    private readonly IServiceProvider ServiceProvider;
    private readonly ConcurrentDictionary<ulong, IServiceProvider> GuildProviders;

    /// <summary>
    ///     Creates a new <see cref="DiscordClientService"/>
    /// </summary>
    /// <param name="socketClient">The discord socket client</param>
    /// <param name="optionsMonitor">An object that monitors the configuration options</param>
    /// <param name="logger">A logger to log with</param>
    /// <param name="serviceProvider">The service provider for this service</param>
    public DiscordClientService(
        DiscordSocketClient socketClient,
        IOptionsMonitor<DiscordClientServiceOptions> optionsMonitor,
        ILogger<DiscordClientService> logger,
        IServiceProvider serviceProvider
    )
    {
        GuildProviders = new ConcurrentDictionary<ulong, IServiceProvider>();
        SocketClient = socketClient;
        OptionsMonitor = optionsMonitor;
        Logger = logger;
        ServiceProvider = serviceProvider;

        var config = new InteractionServiceConfig()
        {
            DefaultRunMode = RunMode.Async,
            LogLevel = LogSeverity.Info,
            UseCompiledLambda = true,
            ExitOnMissingModalField = true
        };

        InteractionService = new InteractionService(SocketClient, config);
        
        SocketClient.Log += LogMessage;
        SocketClient.SlashCommandExecuted += ExecuteSlashCommand;
        SocketClient.Ready += () => SocketClient.SetActivityAsync(new Game("hard to get"));
    }

    private async Task ExecuteSlashCommand(SocketSlashCommand command)
    {
        if (!command.GuildId.HasValue)
            return;

        var guildProvider = GuildProviders.GetOrAdd(
            command.GuildId.Value,
            static (_, b) => b.CreateScope().ServiceProvider,
            ServiceProvider);

        var context = new InteractionContext(SocketClient, command, command.Channel);
        await InteractionService.ExecuteCommandAsync(context, guildProvider);
    }
    
    [SuppressMessage("ReSharper", "TemplateIsNotCompileTimeConstantProblem"),
     SuppressMessage("Usage", "CA2254:Template should be a static expression")]
    private Task LogMessage(LogMessage message)
    {
        switch (message.Severity)
        {
            case LogSeverity.Critical:
                Logger.LogCritical(message.Exception, message.Message);

                break;
            case LogSeverity.Error:
                Logger.LogError(message.Exception, message.Message);

                break;
            case LogSeverity.Warning:
                Logger.LogWarning(message.Exception, message.Message);

                break;
            case LogSeverity.Info:
                Logger.LogInformation(message.Exception, message.Message);

                break;
            case LogSeverity.Debug:
                Logger.LogDebug(message.Exception, message.Message);

                break;
            case LogSeverity.Verbose:
                Logger.LogTrace(message.Exception, message.Message);

                break;
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await SocketClient.LoginAsync(TokenType.Bot, Options.Token);
        await SocketClient.StartAsync();

        await Task.Delay(-1);
    }
}