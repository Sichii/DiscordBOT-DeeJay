using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DeeJay.Definitions
{
    internal static class Extensions
    {
        internal static bool ContainsI(this IEnumerable<string> enumerable, string str) =>
            enumerable.Contains(str, StringComparer.OrdinalIgnoreCase);

        internal static bool ContainsI(this string str1, string str2) => str1.IndexOf(str2, StringComparison.OrdinalIgnoreCase) != -1;

        internal static bool EqualsI(this string str1, string str2) => str1.Equals(str2, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Converts a timespan into a more easily readable string.
        /// </summary>
        /// <param name="timeSpan">A timespan object.</param>
        internal static string ToReadableString(this TimeSpan timeSpan)
        {
            var hours = timeSpan.Hours;
            var minutes = timeSpan.Minutes;
            var seconds = timeSpan.Seconds;

            return $"{(hours == 0 ? string.Empty : $"{hours}:")}{minutes}:{seconds:D2}";
        }

        /// <summary>
        /// Detects the bitrate of the audio, and seeks to the specified time in the song.
        /// <inheritdoc cref="Stream.Seek"/>
        /// </summary>
        /// <param name="stream">A stream object.</param>
        /// <param name="progress">The time to seek to in the song.</param>
        /// <param name="duration">The duration of the song.</param>
        internal static void AudioSeek(this Stream stream, TimeSpan progress, TimeSpan duration)
        {
            var bitRate = stream.Length / (duration.Minutes * 60 + duration.Seconds);

            var seekPosition = bitRate * (long) progress.TotalSeconds;

            if (seekPosition > stream.Length)
                stream.Seek(0, SeekOrigin.Begin);

            stream.Seek(seekPosition, SeekOrigin.Begin);
        }

        /// <summary>
        /// Runs a process and asynchronously waits for it to exit.
        /// </summary>
        /// <param name="process">A process object.</param>
        /// <param name="readOutput">Whether or not to read output of the process.</param>
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

        /// <summary>
        /// Dequeues and re-queues objects one at a time, not inserting the object at the specified index.
        /// </summary>
        /// <typeparam name="TItem">The generic type of the queue.</typeparam>
        /// <param name="queue">A queue object.</param>
        /// <param name="index">The index to remove at.</param>
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