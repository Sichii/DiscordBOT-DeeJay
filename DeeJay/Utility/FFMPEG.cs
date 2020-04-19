using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DeeJay.Definitions;
using NLog;

namespace DeeJay.Utility
{
    /// <summary>
    ///     Utility class to interact with ffmpeg.exe
    /// </summary>
    internal static class FFMPEG
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        /// <summary>
        ///     Runs ffmpeg.exe with pre-defined arguments
        /// </summary>
        /// <param name="args">Input argument.</param>
        /// <param name="token">Cancellation token.</param>
        internal static async Task<MemoryStream> RunAsync(string args, CancellationToken token)
        {
            var dataStream = new MemoryStream();

            try
            {
                using var ffmpeg = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = CONSTANTS.FFMPEG_PATH,
                        //no text, seek to previously elapsed if necessary, 2 channel, 75% volume, pcm s16le stream format, 48000hz, pipe 1
                        Arguments =
                            $"-hide_banner -loglevel quiet -i \"{args}\" -ac 2 -af \"loudnorm=I=-14:LRA=11:TP=-0, volume=0.15\" -f s16le -ar 48000 pipe:1",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true
                    }
                };

                ffmpeg.Start();

                while (!ffmpeg.HasExited)
                    await ffmpeg.StandardOutput.BaseStream.CopyToAsync(dataStream, token);

                await ffmpeg.StandardOutput.BaseStream.CopyToAsync(dataStream, token);
                Log.Debug("Success.");
                dataStream.Position = 0;
                return dataStream;
            } catch (OperationCanceledException)
            {
                Log.Error("Failure.");
                await dataStream.DisposeAsync();
                return new MemoryStream();
            }
        }
    }
}