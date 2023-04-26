using System.Collections.Concurrent;
using DeeJay.Abstractions;
using Discord;
using Microsoft.Extensions.DependencyInjection;

namespace DeeJay.Services;

/// <summary>
/// Provides streaming services
/// </summary>
public class StreamingServiceProvider : IStreamingServiceProvider
{
    private readonly IServiceProvider Provider;
    private readonly ConcurrentDictionary<ulong, IStreamingService> Services;

    /// <summary>
    /// Creates a new <see cref="StreamingServiceProvider"/>
    /// </summary>
    public StreamingServiceProvider(IServiceProvider provider)
    {
        Provider = provider;
        Services = new ConcurrentDictionary<ulong, IStreamingService>();
    }

    /// <inheritdoc />
    public IStreamingService GetStreamingService(IGuild guild) => Services.GetOrAdd(
        guild.Id,
        InnerGetStreamingService,
        (Provider, guild));

    private static IStreamingService InnerGetStreamingService(ulong gid, (IServiceProvider provider, IGuild guild) data) =>
        data.provider.GetRequiredService<IStreamingServiceFactory>().Create(data.guild);
}