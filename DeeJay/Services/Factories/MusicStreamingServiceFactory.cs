using DeeJay.Abstractions;
using Discord;
using Microsoft.Extensions.Logging;

namespace DeeJay.Services.Factories;

/// <inheritdoc />
public sealed class MusicStreamingServiceFactory : IStreamingServiceFactory
{
    private readonly ISearchService<ISearchResult> SearchService;
    private readonly IGuildOptionsRepository GuildOptionsRepository;
    private readonly ILoggerFactory LoggerFactory;
    private readonly IStreamPlayerFactory StreamPlayerFactory;

    /// <summary>
    ///    Creates a new <see cref="MusicStreamingServiceFactory"/>
    /// </summary>
    public MusicStreamingServiceFactory(
        ISearchService<ISearchResult> searchService,
        IGuildOptionsRepository guildOptionsRepository,
        ILoggerFactory loggerFactory,
        IStreamPlayerFactory streamPlayerFactory
    )
    {
        SearchService = searchService;
        GuildOptionsRepository = guildOptionsRepository;
        LoggerFactory = loggerFactory;
        StreamPlayerFactory = streamPlayerFactory;
    }

    /// <inheritdoc />
    public IStreamingService Create(IGuild guild)
    {
        var guildOptions = GuildOptionsRepository.GetOptionsAsync(guild.Id).GetAwaiter().GetResult();

        return new MusicStreamingService(
            guild,
            SearchService,
            guildOptions,
            LoggerFactory.CreateLogger<MusicStreamingService>(),
            StreamPlayerFactory);
    }
}