using System.Diagnostics;
using System.Text;
using DeeJay.Abstractions;
using DeeJay.Definitions;
using Discord.Audio;
using Microsoft.Extensions.Logging;

namespace DeeJay.Services;

/// <summary>
/// Represents a service that can stream 
/// </summary>
public sealed class FfmpegStreamPlayer : IStreamPlayer
{
    private readonly ISong Song;
    private readonly CancellationTokenSource Ctx;
    private readonly ILogger<FfmpegStreamPlayer> Logger;
    private TaskCompletionSource? Completion;
    private readonly string FfmpegPath;

    /// <summary>
    /// Initializes a new instance of the <see cref="FfmpegStreamPlayer"/> class.
    /// </summary>
    /// <param name="song">The stream to be streamed</param>
    /// <param name="ffmpegPath"></param>
    /// <param name="logger">The logger used by this instance</param>
    public FfmpegStreamPlayer(ISong song, string ffmpegPath, ILogger<FfmpegStreamPlayer> logger)
    {
        Song = song;
        Logger = logger;
        FfmpegPath = ffmpegPath;
        Ctx = new CancellationTokenSource();
    }

    /// <inheritdoc />
    public bool EoS { get; private set; }

    /// <inheritdoc />
    public async Task PlayAsync(IAudioClient audioClient)
    {
        if (EoS)
            return;
        
        var builder = new StringBuilder();
        
        //no banner, no logs
        builder.Append("-hide_banner -loglevel quiet");
        
        //seek to elapsed if necessary
        if(!Song.IsLive && (Song.Elapsed != TimeSpan.Zero))
            builder.Append(" -ss " + Song.Elapsed);

        //auto reconnect to input 1 with max delay of 4 seconds
        builder.Append(" -reconnect 1 -reconnect_streamed 1 -reconnect_delay_max 4");
        
        //streaming media uri
        builder.Append($" -i \"{Song.Uri}\"");

        //if stream is live, discard the video stream
        if (Song.IsLive)
            builder.Append(" -vn");
        
        //output has 2 channels, normalized loudness, 15% volume, 16bit pcm/wav, 48000hz, piped to output 1 (standardoutput)
        builder.Append(" -ac 2 -af \"loudnorm=I=-14:LRA=11:TP=-1, volume=0.35\" -f s16le -ar 48000 pipe:1");
        
        using var ffmpeg = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = FfmpegPath,
                Arguments = builder.ToString(),
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true
            }
        };
        
        ffmpeg.Start();
        
        if(!Song.IsLive)
            Song.Start();

        await audioClient.SetSpeakingAsync(true);
        var completion = new TaskCompletionSource();
        Completion = completion;
        Logger.LogDebug("Now streaming {@Stream}", Song);

        try
        {
            await using var audioStream = audioClient.CreatePCMStream(AudioApplication.Music, (int)BitRate.b128k, 2500);

            while (!Ctx.IsCancellationRequested && !ffmpeg.HasExited)
                await ffmpeg.StandardOutput.BaseStream.CopyToAsync(audioStream, Ctx.Token);

            if (ffmpeg.HasExited)
                EoS = true;
        }
        catch (OperationCanceledException)
        {
            //ignored
        }
        finally
        {
            if(!Song.IsLive)
                Song.Stop();

            await audioClient.SetSpeakingAsync(false);
            completion.TrySetResult();
        }
    }

    /// <inheritdoc />
    public Task StopAsync()
    {
        Logger.LogDebug("Stopping stream {@Stream}", Song);
        Ctx.Cancel();

        return Completion!.Task;
    }
}