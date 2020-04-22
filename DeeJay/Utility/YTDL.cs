using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DeeJay.Definitions;
using NLog;

namespace DeeJay.Utility
{
    /// <summary>
    ///     A utility class for interacting with youtube-dl.exe
    /// </summary>
    internal static class YTDL
    {
        private static readonly Logger Log = LogManager.GetLogger(nameof(YTDL));

        /// <summary>
        ///     Runs youtube-dl.exe with predefined arguments.
        /// </summary>
        /// <param name="args">A song name, video title, and any other additional arguments to pass.</param>
        /// <param name="token">A cancellation token.</param>
        internal static async Task<IEnumerable<string>> RunAsync(string args, CancellationToken token)
        {
            var output = new List<string>();

            try
            {
                var source = new TaskCompletionSource<bool>();
                token.Register(() => source.TrySetCanceled(token));

                using var youtubedl = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = CONSTANTS.YOUTUBEDL_PATH,
                        //best audio stream, probe for direct link, get video duration
                        Arguments =
                            $"-f bestaudio -sge -R 2 --rm-cache-dir --no-cache-dir --min-sleep-interval 2 --max-sleep-interval 4 --get-duration {args}",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true
                    },
                    EnableRaisingEvents = true
                };

                youtubedl.OutputDataReceived += (s, e) => output.Add(e.Data);
                youtubedl.Exited += (s, e) => source.TrySetResult(true);
                youtubedl.Start();
                youtubedl.BeginOutputReadLine();

                await source.Task;
                Log.Debug($"Success. Args: \"{args}\" Indexes: {output.Count}");
            } catch (OperationCanceledException)
            {
                Log.Error($"Failure. Args: \"{args}\" Indexes: {output.Count}");
            }

            return output;
        }
    }
}