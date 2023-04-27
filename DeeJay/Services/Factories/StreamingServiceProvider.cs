using System.Collections.Concurrent;
using DeeJay.Abstractions;
using Discord;
using Microsoft.Extensions.Hosting;

namespace DeeJay.Services.Factories;

/// <summary>
/// Provides streaming services
/// </summary>
public sealed class StreamingServiceProvider : IStreamingServiceProvider
{
    private readonly IStreamingServiceFactory StreamingServiceFactory;
    private readonly ConcurrentDictionary<ulong, IStreamingService> Services;
    private readonly CancellationTokenSource Ctx;

    /// <summary>
    /// Creates a new <see cref="StreamingServiceProvider"/>
    /// </summary>
    public StreamingServiceProvider(IStreamingServiceFactory streamingServiceFactory, CancellationTokenSource ctx)
    {
        Services = new ConcurrentDictionary<ulong, IStreamingService>();
        Ctx = ctx;
        StreamingServiceFactory = streamingServiceFactory;
    }

    /// <inheritdoc />
    public IStreamingService GetStreamingService(IGuild guild) => Services.GetOrAdd(
        guild.Id,
        InnerGetStreamingService,
        (StreamingServiceFactory, guild, Ctx.Token));

    private static IStreamingService InnerGetStreamingService(
        ulong gid,
        (IStreamingServiceFactory Factory, IGuild Guild, CancellationToken StoppingToken) data
    )
    {
        var svc = data.Factory.Create(data.Guild);

        if (svc is IHostedService hs)
            hs.StartAsync(data.StoppingToken);

        return svc;
    }
}