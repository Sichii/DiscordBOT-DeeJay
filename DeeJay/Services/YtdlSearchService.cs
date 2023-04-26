using System.Diagnostics;
using DeeJay.Abstractions;
using DeeJay.Models;
using DeeJay.Services.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DeeJay.Services;

/// <summary>
/// Represents a service that can search for songs using youtube-dl
/// </summary>
public sealed class YtdlSearchService : ISearchService<ISearchResult>
{
    private readonly ILogger<YtdlSearchService> Logger;
    private readonly IOptionsMonitor<YtdlSearchServiceOptions> OptionsMonitor;
    private YtdlSearchServiceOptions Options => OptionsMonitor.CurrentValue;
    private const string YtdlArgs =
        "-f bestaudio/best --simulate --get-url --get-title --get-duration --retries 2 --no-cache-dir --sleep-interval 2 --max-sleep-interval 4";

    /// <summary>
    /// Initializes a new instance of the <see cref="YtdlSearchService"/> class.
    /// </summary>
    public YtdlSearchService(ILogger<YtdlSearchService> logger, IOptionsMonitor<YtdlSearchServiceOptions> optionsMonitor)
    {
        Logger = logger;
        OptionsMonitor = optionsMonitor;
    }

    /// <inheritdoc />
    public async Task<ISearchResult> SearchAsync(string query, CancellationToken? cancellationToken = default)
    {
        var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        cancellationToken?.Register(() => source.TrySetCanceled());

        Logger.LogTrace("Starting ytdl search for {@Query}", query);

        var httpClient = new HttpClient();
        var response = await httpClient.GetAsync("");

        //construct args
        var args = YtdlArgs;
        
        if(Options.ProxyUrl is not null)
            args += $" --proxy {Options.ProxyUrl}";

        args += $" \"ytsearch:{query}\"";

        using var youtubedl = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "youtube-dl.exe",
                Arguments = args,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true
            },
            EnableRaisingEvents = true
        };

        var output = new List<string>();

        youtubedl.OutputDataReceived += (_, e) =>
        {
            if (string.IsNullOrEmpty(e.Data))
                return;

            output.Add(e.Data);
        };

        youtubedl.Exited += (_, _) => source.TrySetResult();
        youtubedl.Start();
        youtubedl.BeginOutputReadLine();

        try
        {
            await source.Task;
        }
        catch (OperationCanceledException)
        {
            Logger.LogTrace("Ytdl search for {@Query} was canceled", query);

            return new YtdlSearchResult(query, "Search canceled");
        }

        var ret = new YtdlSearchResult(query, output.ToArray());

        if (ret.Success)
            Logger.LogDebug("Finished ytdl search with result {@Result}", ret);
        else
            Logger.LogError("Ytdl search failed with result {@Result}", ret);

        return ret;
    }
}