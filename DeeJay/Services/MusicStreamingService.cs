using System.Collections.Concurrent;
using DeeJay.Abstractions;
using DeeJay.Definitions;
using DeeJay.Models;
using DeeJay.Utility;
using DeeJay.Extensions;
using Discord;
using Discord.Audio;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DeeJay.Services;

/// <summary>
/// Represents a service that streams audio
/// </summary>
public sealed class MusicStreamingService : BackgroundService, IStreamingService
{
    private readonly ISearchService<ISearchResult> SearchService;
    private readonly ConcurrentQueue<Song> Queue;
    private IVoiceChannel? CurrentVoiceChannel;
    private IAudioClient? AudioClient;
    private MusicStreamingServiceState State;
    private readonly AutoReleasingSemaphoreSlim Sync;
    private readonly IGuildOptions GuildOptions;
    private readonly IGuild Guild;
    private ITextChannel? DesignatedChannel;
    private ISong? LiveStream;
    private readonly ILogger<MusicStreamingService> Logger;
    private readonly ConcurrentQueue<ObservableSignal<StateAction>> StateActionRequests;
    private readonly IStreamPlayerFactory StreamPlayerFactory;
    /// <inheritdoc />
    public ISong? NowPlaying => Queue.TryPeek(out var stream) ? stream : LiveStream;
    private IStreamPlayer? Player;
    private DateTime? LastIdleTransition;

    /// <summary>
    /// Creates a new instance of <see cref="MusicStreamingService"/>
    /// </summary>
    public MusicStreamingService(
        IGuild guild,
        ISearchService<ISearchResult> searchService,
        IGuildOptions guildOptions,
        ILogger<MusicStreamingService> logger,
        IStreamPlayerFactory streamPlayerFactory
    )
    {
        Guild = guild;
        SearchService = searchService;
        Queue = new ConcurrentQueue<Song>();
        SetState(MusicStreamingServiceState.Idle);
        Sync = new AutoReleasingSemaphoreSlim(1, 1);
        GuildOptions = guildOptions;
        Logger = logger;
        StreamPlayerFactory = streamPlayerFactory;
        StateActionRequests = new ConcurrentQueue<ObservableSignal<StateAction>>();
    }

    /// <inheritdoc />
    public async Task SetSlowModeAsync(IInteractionContext context, int amountPerPerson)
    {
        await using var @lock = await Sync.WaitAsync();
        
        if (amountPerPerson == -1)
        {
            GuildOptions.MaxSongsPerPerson = null;

            return;
        }

        GuildOptions.MaxSongsPerPerson = amountPerPerson;

        var streamsByUser = Queue.Reverse().GroupBy(x => x.RequestedBy.Id).ToList();
        var removeAmount = 0;
        
        foreach(var group in streamsByUser)
            if (group.Count() > amountPerPerson)
            {
                var toRemove = group.Skip(amountPerPerson);

                foreach (var stream in toRemove)
                {
                    Queue.Remove(stream);
                    removeAmount++;
                }
            }

        await context.Interaction.RespondAsync(
            $"Slow mode set to {amountPerPerson}. {removeAmount} songs removed from queue",
            ephemeral: true);
    }

    /// <inheritdoc />
    public async Task SetDesignatedChannelAsync(IInteractionContext context)
    {
        await using var @lock = await Sync.WaitAsync();
        
        var originChannel = context.Channel;

        if (originChannel is ITextChannel textChannel)
        {
            DesignatedChannel = textChannel;
            GuildOptions.DesignatedTextChannelId = textChannel.Id;
        }

        await ReplyAsync(context, "Now using this channel to communicate");
    }
    
    /// <inheritdoc />
    public async Task QueueSongAsync(IInteractionContext context, string arg)
    {
        await using var @lock = await Sync.WaitAsync();
        
        if(context.User is not IGuildUser guildUser)
            return;

        if (!CanQueue(guildUser.Id))
            await context.Interaction.ModifyOriginalResponseAsync(
                msg => msg.Content = $"You have reached the maximum amount of songs you can queue ({GuildOptions.MaxSongsPerPerson!.Value
                })");
        
        await context.Interaction.RespondAsync($"Searching for \"{arg}\"");
        var searchResult = await SearchService.SearchAsync(arg);

        if (!searchResult.Success)
        {
            await context.Interaction.ModifyOriginalResponseAsync(
                msg => msg.Content = $"Search for \"{searchResult.OriginalQuery}\" failed with \"{searchResult.ErrorMessage}\"");

            return;
        }
        
        var stream = new Song(guildUser, searchResult, false);
        Queue.Enqueue(stream);
        
        await context.Interaction.ModifyOriginalResponseAsync(msg => msg.Content = $"Added \"{stream.Title}\" to queue");
        
        if (CurrentVoiceChannel is null)
            await InnerJoinVoiceAsync(context);

        await RequestPlayAsync();
    }

    private bool CanQueue(ulong userId) => !GuildOptions.MaxSongsPerPerson.HasValue
                                           || (Queue.Count(strm => strm.RequestedBy.Id == userId) < GuildOptions.MaxSongsPerPerson.Value);

    /// <inheritdoc />
    public async Task PlayAsync(IInteractionContext context)
    {
        await using var @lock = await Sync.WaitAsync();

        if (context.User is not IGuildUser)
            return;

        if (CurrentVoiceChannel is null)
            await JoinVoiceAsync(context);

        if (Queue.IsEmpty)
            return;

        await RequestPlayAsync();
    }

    /// <inheritdoc />
    public async Task PauseAsync(IInteractionContext context)
    {
        await using var @lock = await Sync.WaitAsync();
        
        if (context.User is not IGuildUser)
            return;

        await RequestPauseAsync();
    }

    /// <inheritdoc />
    public async Task SkipAsync(IInteractionContext context)
    {
        await using var @lock = await Sync.WaitAsync();
        
        if (context.User is not IGuildUser)
            return;

        await RequestSkipAsync();
    }

    /// <inheritdoc />
    public async Task SetLiveAsync(IInteractionContext context, Uri uri)
    {
        await using var @lock = await Sync.WaitAsync();
        
        if (context.User is not IGuildUser guildUser)
            return;

        await context.Interaction.RespondAsync($"Verifying live stream link");
        var searchResult = await SearchService.SearchAsync(uri.ToString());

        if (!searchResult.Success || ((searchResult.Duration != TimeSpan.Zero) && (searchResult.Duration < TimeSpan.FromMinutes(10))))
        {
            var reason = searchResult.ErrorMessage ?? "Not a live stream";

            await context.Interaction.ModifyOriginalResponseAsync(msg => msg.Content = $"Invalid live stream link for reason \"{reason}\"");
            
            return;
        }

        LiveStream = new Song(guildUser, searchResult, true);
        await context.Interaction.ModifyOriginalResponseAsync(msg => msg.Content = $"Live stream set to {LiveStream.Title}");
    }

    /// <inheritdoc />
    public async Task JoinVoiceAsync(IInteractionContext context)
    {
        await using var @lock = await Sync.WaitAsync();

        await InnerJoinVoiceAsync(context);
    }

    /// <inheritdoc />
    public async Task LeaveVoiceAsync(IInteractionContext context)
    {
        await using var @lock = await Sync.WaitAsync();

        await InnerLeaveVoiceAsync();
    }

    /// <inheritdoc />
    public async Task RemoveSongAsync(IInteractionContext context, int index)
    {
        await using var @lock = await Sync.WaitAsync();
        
        if (context.User is not IGuildUser)
            return;

        if ((index < 0) || (index >= Queue.Count))
            return;

        if (index == 0)
        {
            await RequestSkipAsync();

            return;
        }

        var streamToRemove = Queue.ElementAt(index);
        Queue.Remove(streamToRemove);
        
        await context.Interaction.ModifyOriginalResponseAsync(msg => msg.Content = $"Removed \"{streamToRemove.Title}\" from queue");
    }

    /// <inheritdoc />
    public async Task ClearQueueAsync(IInteractionContext context)
    {
        await using var @lock = await Sync.WaitAsync();
        
        if (context.User is not IGuildUser)
            return;

        var wasPlaying = State is MusicStreamingServiceState.Playing or MusicStreamingServiceState.Streaming;

        if (wasPlaying)
            await RequestPauseAsync();
        
        Queue.Clear();

        if (wasPlaying)
            await RequestPlayAsync();

        await context.Interaction.ModifyOriginalResponseAsync(msg => msg.Content = $"Queue cleared");
    }

    /// <inheritdoc />
    public IAsyncEnumerable<ISong> GetQueueAsync(IInteractionContext context) => Queue.ToList().ToAsyncEnumerable();

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if(GuildOptions.DesignatedTextChannelId.HasValue)
            DesignatedChannel = await Guild.GetTextChannelAsync(GuildOptions.DesignatedTextChannelId.Value);

        var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(100));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);

                await ProcessRequestAsync();

                switch (State)
                {
                    case MusicStreamingServiceState.Idle:
                    case MusicStreamingServiceState.Paused:
                        if (CurrentVoiceChannel is not null
                            && LastIdleTransition.HasValue
                            && (DateTime.UtcNow.Subtract(LastIdleTransition.Value).TotalMinutes > 5))
                        {
                            SetState(MusicStreamingServiceState.Idle);
                            LastIdleTransition = null;

                            _ = Task.Run(
                                async () =>
                                {
                                    await using var @lock = await Sync.WaitAsync();
                                    await InnerLeaveVoiceAsync();
                                });
                        }

                        break;
                    case MusicStreamingServiceState.Playing:
                        if (Player is null)
                        {
                            SetState(MusicStreamingServiceState.Idle);

                            break;
                        }

                        if (Player.EoS)
                        {
                            Player = null;
                            Queue.TryDequeue(out _);

                            if (!Queue.IsEmpty)
                                await StartPlayingAsync();
                            else if (LiveStream is not null)
                                await StartStreamingAsync();
                            else
                                SetState(MusicStreamingServiceState.Idle);
                        }

                        break;
                    case MusicStreamingServiceState.Streaming:
                        if (!Queue.IsEmpty)
                        {
                            await DoPauseAsync();
                            await StartPlayingAsync();

                            break;
                        }

                        if (Player is null || LiveStream is null)
                            SetState(MusicStreamingServiceState.Idle);

                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            } catch (OperationCanceledException)
            {
                //ignored
                return;
            } catch (Exception e)
            {
                Logger.LogError(e, "Error in music streaming service");
                //ignored
            }
        }
    }
    
    #region Utility
    private Task<IUserMessage> ReplyAsync(IInteractionContext context, string message, Embed? embed = null)
    {
        var channel = DesignatedChannel ?? context.Channel;
        return channel.SendMessageAsync(message, embed: embed);
    }

    private void SetState(MusicStreamingServiceState newState)
    {
        if (newState is MusicStreamingServiceState.Idle or MusicStreamingServiceState.Paused && CurrentVoiceChannel is not null)
            LastIdleTransition = DateTime.UtcNow;
        else
            LastIdleTransition = null;

        State = newState;
    }

    private async Task InnerJoinVoiceAsync(IInteractionContext context)
    {
        if (context.User is not IGuildUser guildUser)
            return;
        
        if(CurrentVoiceChannel is not null && (CurrentVoiceChannel.Id == guildUser.VoiceChannel.Id))
            return;

        var wasPlaying = State is MusicStreamingServiceState.Playing or MusicStreamingServiceState.Streaming;

        await InnerLeaveVoiceAsync();
        
        CurrentVoiceChannel = guildUser.VoiceChannel;
        AudioClient = await CurrentVoiceChannel.ConnectAsync();

        if (wasPlaying)
            await RequestPlayAsync();
    }

    private async Task InnerLeaveVoiceAsync()
    {
        if (CurrentVoiceChannel is null)
            return;
        
        var wasPlaying = State is MusicStreamingServiceState.Playing or MusicStreamingServiceState.Streaming;

        if (wasPlaying)
            await RequestPauseAsync();
        
        AudioClient?.Dispose();
        await CurrentVoiceChannel.DisconnectAsync();
    }
    #endregion
    
    #region StateRequests
    private async Task RequestPlayAsync()
    {
        var observable = new ObservableSignal<StateAction>(StateAction.Play);
        StateActionRequests.Enqueue(observable);

        await observable;
    }
    
    private async Task RequestPauseAsync()
    {
        var observable = new ObservableSignal<StateAction>(StateAction.Pause);
        StateActionRequests.Enqueue(observable);

        await observable;
    }
    
    private async Task RequestSkipAsync()
    {
        var observable = new ObservableSignal<StateAction>(StateAction.Skip);
        StateActionRequests.Enqueue(observable);

        await observable;
    }
    #endregion
    

    #region PlayerActions
    private Task StartStreamingAsync()
    {
        if (AudioClient is null || LiveStream is null)
            return Task.CompletedTask;

        Player = StreamPlayerFactory.Create(LiveStream); 
        _ = Player.PlayAsync(AudioClient);

        SetState(MusicStreamingServiceState.Streaming);

        return Task.CompletedTask;
    }

    private Task StartPlayingAsync()
    {
        try
        {
            if (!Queue.TryPeek(out var currentStream) || AudioClient is null)
                return Task.CompletedTask;

            Player = StreamPlayerFactory.Create(currentStream);
            _ = Player.PlayAsync(AudioClient);

            SetState(MusicStreamingServiceState.Playing);
        } catch
        {
            //ignored
        }

        return Task.CompletedTask;
    }
    
    private async Task DoPauseAsync()
    {
        try
        {
            if (AudioClient is null || Player is null)
                return;

            await Player.StopAsync();
            SetState(MusicStreamingServiceState.Paused);
        } catch
        {
            //ignored
        }
    }
    #endregion

    #region Request Handling
    private async Task HandlePlayRequestAsync()
    {
        if (CurrentVoiceChannel is null || AudioClient is null)
            return;

        switch (State)
        {
            case MusicStreamingServiceState.Playing:
                if (!Queue.IsEmpty)
                    break;

                await DoPauseAsync();
                await StartStreamingAsync();

                break;
            case MusicStreamingServiceState.Streaming:
                if (Queue.IsEmpty)
                    break;

                await DoPauseAsync();
                await StartPlayingAsync();

                break;
            case MusicStreamingServiceState.Idle:
            case MusicStreamingServiceState.Paused:
                if (!Queue.IsEmpty)
                    await StartPlayingAsync();
                else if (LiveStream is not null)
                    await StartStreamingAsync();

                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private async Task HandlePauseRequestAsync()
    {
        switch (State)
        {
            case MusicStreamingServiceState.Playing:
            case MusicStreamingServiceState.Streaming:
                await DoPauseAsync();

                break;
            case MusicStreamingServiceState.Idle:
            case MusicStreamingServiceState.Paused:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private async Task HandleSkipRequestAsync()
    {
        switch (State)
        {
            case MusicStreamingServiceState.Playing:
                await DoPauseAsync();
                Queue.TryDequeue(out _);

                if (Queue.IsEmpty)
                    await StartStreamingAsync();
                else
                    await StartPlayingAsync();
                
                break;
            case MusicStreamingServiceState.Streaming:
                break;
            case MusicStreamingServiceState.Idle:
            case MusicStreamingServiceState.Paused:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }
    
    private async Task ProcessRequestAsync()
    {
        if (!StateActionRequests.TryDequeue(out var request))
            return;

        try
        {
            switch (request.Signal)
            {
                case StateAction.Play:
                    await HandlePlayRequestAsync();

                    break;
                case StateAction.Pause:
                    await HandlePauseRequestAsync();

                    break;
                case StateAction.Skip:
                    await HandleSkipRequestAsync();

                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        } finally
        {
            request.Complete();
        }
    }
    #endregion
}