using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using DeeJay.Definitions;
using DeeJay.YouTube;
using Discord.Audio;
using NLog;

namespace DeeJay.Utility
{
    /// <summary>
    ///     Utility class to interact with ffmpeg.exe
    /// </summary>
    internal static class FFMPEG
    {
        private static readonly Logger Log = LogManager.GetLogger(nameof(FFMPEG));

        internal static async Task StreamAsync(
            Song song, AudioOutStream stream, CancellationToken token)
        {
            try
            {   //no banner, no logs, seek to elapsed if necessary, auto reconnect to input 1 with max delay of 4 seconds
                var preInput = $"-hide_banner -loglevel quiet{(song.Progress.Elapsed != default ? $" -ss {song.Progress.Elapsed.ToStringF()}" : string.Empty)} -reconnect 1 -reconnect_streamed 1 -reconnect_delay_max 4";
                //input is a direct link to media
                var input = $"-i \"{song.DirectLink}\"";
                //output has no video if there is video, 2 channels, normalized loudness, 15% volume, 16bit pcm/wav, 48000hz, piped to output 1 (standardoutput)
                var postInput =
                    $"{(song.ResultFrom.IsLive ? "-vn " : string.Empty)}-ac 2 -af \"loudnorm=I=-14:LRA=11:TP=-0, volume=0.15\" -f s16le -ar 48000 pipe:1";

                using var ffmpeg = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = CONSTANTS.FFMPEG_PATH,
                        Arguments = $@"{preInput} {input} {postInput}",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true
                    }
                };

                ffmpeg.Start();

                Log.Info($"Streaming \"{song.Title}\"...");
                if(!song.ResultFrom.IsLive)
                    song.Progress.Start();

                while (!token.IsCancellationRequested && !ffmpeg.HasExited)
                    await ffmpeg.StandardOutput.BaseStream.CopyToAsync(stream, token);

                await stream.FlushAsync();

                if (!song.ResultFrom.IsLive)
                    song.Progress.Stop();
            } catch (OperationCanceledException)
            {
                Log.Error("Operation canceled.");

                if (!song.ResultFrom.IsLive)
                    song.Progress.Stop();

                throw;
            }
        }
    }
}