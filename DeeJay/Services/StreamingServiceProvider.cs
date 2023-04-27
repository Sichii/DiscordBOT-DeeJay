using System.Collections.Concurrent;
using DeeJay.Abstractions;
using Discord;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DeeJay.Services;

/// <summary>
/// Provides streaming services
/// </summary>
public sealed class StreamingServiceProvider : IStreamingServiceProvider
{
    private readonly IServiceProvider Provider;
    private readonly ConcurrentDictionary<ulong, IStreamingService> Services;
    private readonly CancellationTokenSource Ctx;

    /// <summary>
    /// Creates a new <see cref="StreamingServiceProvider"/>
    /// </summary>
    public StreamingServiceProvider(IServiceProvider provider)
    {
        Provider = provider;
        Services = new ConcurrentDictionary<ulong, IStreamingService>();
        Ctx = provider.GetRequiredService<CancellationTokenSource>();
    }

    /// <inheritdoc />
    public IStreamingService GetStreamingService(IGuild guild) => Services.GetOrAdd(
        guild.Id,
        InnerGetStreamingService,
        (Provider, guild, Ctx.Token));

    private static IStreamingService InnerGetStreamingService(ulong gid, (IServiceProvider provider, IGuild guild, CancellationToken stoppingToken) data)
    {
        var svc = data.provider.GetRequiredService<IStreamingServiceFactory>().Create(data.guild);

        if (svc is IHostedService hs)
            hs.StartAsync(data.stoppingToken);

        return svc;
    }
}