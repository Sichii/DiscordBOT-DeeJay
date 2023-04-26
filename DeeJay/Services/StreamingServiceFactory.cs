using DeeJay.Abstractions;
using Discord;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DeeJay.Services;

/// <inheritdoc />
public class StreamingServiceFactory : IStreamingServiceFactory
{
    private readonly ISearchService<ISearchResult> SearchService;
    private readonly IGuildOptionsRepository GuildOptionsRepository;
    private readonly ILoggerFactory LoggerFactory;

    /// <summary>
    ///    Creates a new <see cref="StreamingServiceFactory"/>
    /// </summary>
    public StreamingServiceFactory(
        ISearchService<ISearchResult> searchService,
        IGuildOptionsRepository guildOptionsRepository,
        ILoggerFactory loggerFactory
    )
    {
        SearchService = searchService;
        GuildOptionsRepository = guildOptionsRepository;
        LoggerFactory = loggerFactory;
    }

    /// <inheritdoc />
    public IStreamingService Create(IGuild guild)
    {
        var guildOptions = GuildOptionsRepository.GetOptionsAsync(guild.Id).GetAwaiter().GetResult();

        return new MusicStreamingService(
            guild,
            SearchService,
            guildOptions,
            LoggerFactory.CreateLogger<MusicStreamingService>());
    }
}