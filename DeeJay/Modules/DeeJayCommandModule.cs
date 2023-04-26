using System.Text;
using DeeJay.Abstractions;
using DeeJay.Attributes;
using DeeJay.Definitions;
using Discord.Interactions;
using Microsoft.Extensions.Logging;

namespace DeeJay.Modules;

/// <summary>
///    Music bot commands.
/// </summary>
[Group("deejay", "DeeJay bot commands"), RequireContext(ContextType.Guild)]
public class DeeJayCommandModule : InteractionModuleBase
{
    private readonly ILogger<DeeJayCommandModule> Logger;
    private readonly IStreamingServiceProvider StreamingServiceProvider;
    private IStreamingService StreamingService => StreamingServiceProvider.GetStreamingService(Context.Guild);
    
    /// <summary>
    ///    Initializes a new instance of the <see cref="DeeJayCommandModule"/> class.
    /// </summary>
    protected DeeJayCommandModule(ILogger<DeeJayCommandModule> logger, IStreamingServiceProvider streamingServiceProvider)
    {
        Logger = logger;
        StreamingServiceProvider = streamingServiceProvider;
    }

    /// <summary>
    ///     Instructs the bot to always talk in a specific channel
    /// </summary>
    /// <returns></returns>
    [SlashCommand("talkhere", "Makes the bot use the current text channel for commands", runMode: RunMode.Async),
     RequirePrivilege(Privilege.Elevated)]
    public Task TalkHere() => StreamingService.SetDesignatedChannelAsync(Context);

    /// <summary>
    ///     Skips the current song or stream
    /// </summary>
    [SlashCommand("skip", "Skips the current song", runMode: RunMode.Async), RequirePrivilege(Privilege.Elevated), RequireVoiceChannel]
    public Task Skip() => StreamingService.SkipAsync(Context);


    /// <summary>
    ///     Clears all songs from the queue
    /// </summary>
    [SlashCommand("clear", "Clears all songs from the queue", runMode: RunMode.Async), RequirePrivilege(Privilege.Elevated),
     RequireVoiceChannel]
    public Task Clear() => StreamingService.ClearQueueAsync(Context);

    /// <summary>
    ///    Sets the maximum number of songs in the queue per person
    /// </summary>
    /// <param name="maxSongsPerUser"></param>
    [SlashCommand("slowmode", "Limits the number of songs in the queue per person", runMode: RunMode.Async),
     RequirePrivilege(Privilege.Elevated)]
    public Task SlowMode(byte maxSongsPerUser = 0) => StreamingService.SetSlowModeAsync(Context, maxSongsPerUser);

    /// <summary>
    ///    Queues a song by name
    /// </summary>
    /// <param name="songNameParts"></param>
    [SlashCommand("queue", "Queues a song by name", runMode: RunMode.Async), RequireVoiceChannel]
    public Task Queue(params string[] songNameParts)
    {
        var songName = string.Join(" ", songNameParts);

        return StreamingService.QueueSongAsync(Context, songName);
    }

    /// <summary>
    ///     Begins playback of the current song
    /// </summary>
    [SlashCommand("play", "Begins playback of the current song", runMode: RunMode.Async), RequireVoiceChannel]
    public Task Play() => StreamingService.PlayAsync(Context);

    /// <summary>
    ///    Paues playback of the current song
    /// </summary>
    [SlashCommand("pause", "Paues playback of the current song", runMode: RunMode.Async), RequireVoiceChannel]
    public Task Pause() => StreamingService.PauseAsync(Context);

    /// <summary>
    ///     Makes the bot leave the voice channel
    /// </summary>
    [SlashCommand("leave", "Makes the bot leave the voice channel", runMode: RunMode.Async), RequireVoiceChannel]
    public Task Leave() => StreamingService.LeaveVoiceAsync(Context);

    /// <summary>
    ///     Displays info about the current song
    /// </summary>
    [SlashCommand("showsong", "Displays info about the current song", runMode: RunMode.Async), RequireVoiceChannel]
    public Task ShowSong()
    {
        var nowPlaying = StreamingService.NowPlaying;

        if (nowPlaying == null)
            return Context.Interaction.RespondAsync("No songs are currently playing", ephemeral: true);

        return Context.Interaction.RespondAsync($"Now playing: \"{nowPlaying.Title}\" ({nowPlaying.Elapsed}/{nowPlaying.Duration})");
    }
    
    /// <summary>
    ///    Displays info about the next song
    /// </summary>
    [SlashCommand("shownext", "Displays info about the next song", runMode: RunMode.Async), RequireVoiceChannel]
    public async Task ShowNext()
    {
        var next = await StreamingService.GetQueueAsync(Context).Skip(1).FirstOrDefaultAsync();

        if (next == null)
        {
            await Context.Interaction.RespondAsync("No other songs in queue", ephemeral: true);

            return;
        }

        await Context.Interaction.RespondAsync($"Next up: \"{next.Title}\" ({next.Duration})", ephemeral: true);
    }
    
    /// <summary>
    ///   Displays info about all songs in the queue
    /// </summary>
    [SlashCommand("showqueue", "Displays info about all songs in the queue", runMode: RunMode.Async), RequireVoiceChannel]
    public async Task ShowQueue()
    {
        var builder = new StringBuilder();
        var index = 0;
        
        await foreach (var song in StreamingService.GetQueueAsync(Context))
        {
            if (index == 0)
                builder.AppendLine($"Now playing: \"{song.Title}\" ({song.Elapsed}/{song.Duration})");
            else
                builder.AppendLine($"{index}: \"{song.Title}\" ({song.Duration})");

            index++;
        }
    }

    /// <summary>
    ///   Removes a song from the queue
    /// </summary>
    /// <param name="songIndex"></param>
    [SlashCommand("remove", "Removes a song from the queue", runMode: RunMode.Async), RequireVoiceChannel]
    public Task Remove(int songIndex) => StreamingService.RemoveSongAsync(Context, songIndex);

    /// <summary>
    ///   Displays a list of commands
    /// </summary>
    [SlashCommand("help", "Displays a list of commands", runMode: RunMode.Async)]
    public Task Help() => throw new NotImplementedException();
    
    /// <summary>
    ///     Plays a live stream from a direct link
    /// </summary>
    /// <param name="streamUrlParts"></param>
    [SlashCommand("stream", "Plays a live stream from a direct link", runMode: RunMode.Async), RequireVoiceChannel]
    public async Task Stream(params string[] streamUrlParts)
    {
        var streamUrl = string.Join(" ", streamUrlParts);
        var uri = new Uri(streamUrl);

        if (!uri.IsAbsoluteUri)
            return;

        await StreamingService.SetLiveAsync(Context, uri);
    }
}