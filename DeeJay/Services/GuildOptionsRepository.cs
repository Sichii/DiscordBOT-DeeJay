using System.Collections.Concurrent;
using System.Text.Json;
using DeeJay.Abstractions;
using DeeJay.Models;
using DeeJay.Services.Options;
using DeeJay.Utility;
using Microsoft.Extensions.Options;

namespace DeeJay.Services;

/// <summary>
///     Represents an object used to store guild options
/// </summary>
public sealed class GuildOptionsRepository : IGuildOptionsRepository
{
    private readonly ConcurrentDictionary<ulong, IGuildOptions> GuildOptions;
    private readonly AutoReleasingSemaphoreSlim SaveSync;
    private readonly IOptionsMonitor<GuildOptionsRepositoryOptions> OptionsMonitor;
    private readonly JsonSerializerOptions JsonSerializerOptions;
    private GuildOptionsRepositoryOptions Options => OptionsMonitor.CurrentValue;

    /// <summary>
    ///    Initializes a new instance of the <see cref="GuildOptionsRepository" /> class
    /// </summary>
    public GuildOptionsRepository(
        JsonSerializerOptions jsonSerializerOptions,
        IOptionsMonitor<GuildOptionsRepositoryOptions> optionsMonitor
    )
    {
        JsonSerializerOptions = jsonSerializerOptions;
        OptionsMonitor = optionsMonitor;
        SaveSync = new AutoReleasingSemaphoreSlim(1, 1);

        if (!Directory.Exists(Options.Directory))
            Directory.CreateDirectory(Options.Directory);

        var optionsPath = Path.Combine(Options.Directory, "guildOptions.json");

        if (!File.Exists(optionsPath))
        {
            GuildOptions = new ConcurrentDictionary<ulong, IGuildOptions>();

            return;
        }

        //read existing options
        var existingOptionsJson = File.ReadAllText(optionsPath);

        //serialize into concrete types
        var existingOptions =
            JsonSerializer.Deserialize<Dictionary<ulong, GuildOptions>>(existingOptionsJson, JsonSerializerOptions);

        //convert key value pairs to the interface type
        var convertedOptions = existingOptions?.Select(kvp => new KeyValuePair<ulong, IGuildOptions>(kvp.Key, kvp.Value))
                               ?? Enumerable.Empty<KeyValuePair<ulong, IGuildOptions>>();

        //initialize the dictionary
        GuildOptions = new ConcurrentDictionary<ulong, IGuildOptions>(convertedOptions);
    }

    /// <inheritdoc />
    public Task<IGuildOptions> GetOptionsAsync(ulong guildId) => Task.FromResult(GuildOptions.GetOrAdd(guildId, _ => new GuildOptions()));

    /// <inheritdoc />
    public async Task SaveAsync()
    {
        await using var @lock = await SaveSync.WaitAsync();

        var options = Options;
        var savePath = Path.Combine(options.Directory, "guildOptions.json");
        
        if(!Directory.Exists(options.Directory))
            Directory.CreateDirectory(options.Directory);
        
        await using var fileStream = File.Create(savePath);
        await JsonSerializer.SerializeAsync(fileStream, GuildOptions.ToDictionary(kvp => kvp.Key, kvp => kvp.Value), JsonSerializerOptions);
    }
}