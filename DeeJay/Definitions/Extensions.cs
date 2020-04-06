using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace DeeJay.Definitions
{
    internal static class Extensions
    {
        internal static string ToReadableString(this TimeSpan timeSpan)
        {
            var hours = timeSpan.Hours;
            var minutes = timeSpan.Minutes;
            var seconds = timeSpan.Seconds;

            return $"{(hours == 0 ? string.Empty : $"{hours}:")}{minutes}:{seconds:D2}";
        }

        internal static void AudioSeek(this Stream stream, TimeSpan progress, TimeSpan duration)
        {
            var bitRate = stream.Length / (duration.Minutes * 60 + duration.Seconds);

            var seekPosition = bitRate * (long) progress.TotalSeconds;

            if (seekPosition > stream.Length)
                stream.Seek(0, SeekOrigin.Begin);

            stream.Seek(seekPosition, SeekOrigin.Begin);
        }

        internal static Task RunAsync(this Process process, bool readOutput)
        {
            var source = new TaskCompletionSource<bool>();
            if (readOutput)
                process.StartInfo.RedirectStandardOutput = true;
            process.EnableRaisingEvents = true;
            process.Exited += (s, e) => source.SetResult(true);
            process.Start();
            process.BeginOutputReadLine();

            return source.Task;
        }

        internal static Task WaitForExitAsync(this Process process)
        {
            var source = new TaskCompletionSource<bool>();
            process.EnableRaisingEvents = true;
            process.Exited += (s, e) => source.SetResult(true);

            return source.Task;
        }

        internal static TItem RemoveAt<TItem>(this ConcurrentQueue<TItem> queue, int index)
        {
            TItem result = default;

            if (index > queue.Count)
                return result;

            var count = queue.Count;

            for (var i = 1; i <= count; i++)
            {
                queue.TryDequeue(out var item);

                if (index == i)
                    result = item;
                else
                    queue.Enqueue(item);
            }

            return result;
        }
    }
}