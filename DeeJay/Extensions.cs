using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Tasks;

namespace DeeJay
{
    internal static class Extensions
    {
        internal static string ToReadableString(this TimeSpan timeSpan)
        {
            var hours = timeSpan.Hours;
            var minutes = timeSpan.Minutes;
            var seconds = timeSpan.Seconds;

            return $"{((hours == 0) ? string.Empty : $"{hours}:")}{minutes}:{seconds:D2}";
        }

        internal static Task RunAsync(this Process process, bool readOutput)
        {
            var source = new TaskCompletionSource<bool>();
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

            var counter = 0;
            while (queue.TryDequeue(out var item))
            {
                if (counter++ == index)
                {
                    result = item;
                    continue;
                }

                queue.Enqueue(item);
            }

            return result;
        }
    }
}
