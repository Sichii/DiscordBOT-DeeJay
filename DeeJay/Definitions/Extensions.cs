using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DeeJay.Model.YouTube;
using Discord;

namespace DeeJay.Definitions
{
    internal static class Extensions
    {
        internal static bool ContainsI(this IEnumerable<string> enumerable, string str) =>
            enumerable.Contains(str, StringComparer.OrdinalIgnoreCase);

        internal static bool ContainsI(this string str1, string str2) => str1.IndexOf(str2, StringComparison.OrdinalIgnoreCase) != -1;

        internal static bool EqualsI(this string str1, string str2) => str1.Equals(str2, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        ///     Gets the voice channel the user is in, if theyre in one.
        /// </summary>
        /// <param name="user">An IUser object.</param>
        internal static IVoiceChannel GetVoiceChannel(this IUser user) =>
            user is IVoiceState voiceState ? voiceState.VoiceChannel : default;

        /// <summary>
        ///     Checks if a string is a valid URI.
        /// </summary>
        /// <param name="str">A string object.</param>
        internal static bool IsValidURI(this string str) =>
            !string.IsNullOrWhiteSpace(str) && Uri.TryCreate(str, UriKind.Absolute, out var result) && result.Scheme.Contains("http");

        /// <summary>
        ///     Converts a timespan into a more easily readable string.
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
        ///     Calculates the pixel width of a string.
        /// </summary>
        /// <param name="str">A string object.</param>
        internal static float CalculateWidth(this string str) =>
            CONSTANTS.GRAPHICS.MeasureString(str, CONSTANTS.WHITNEY_FONT)
                .Width;

        /// <summary>
        ///     Normalizes the widths of all strings in the collection by adding spaces to shorter strings so that they match the
        ///     pixel width of longer strings.
        /// </summary>
        /// <param name="strings">An IEnumerable of strings.</param>
        /// <param name="alignment">The type of alignment to perform while normalizing widths.</param>
        internal static IEnumerable<string> NormalizeWidth(this IEnumerable<string> strings, TextAlignment alignment)
        {
            var enumerable = strings as string[] ?? strings.ToArray();
            var widths = enumerable.Select(str => str.CalculateWidth())
                .ToArray();
            var maxWidth = widths.Max();

            for (var i = 0; i < enumerable.Length; i++)
            {
                var str = enumerable[i];
                var width = widths[i];

                if (width < maxWidth)
                {
                    var spacesToAdd = (int) Math.Round((maxWidth - width) / CONSTANTS.SPACE_LENGTH, MidpointRounding.AwayFromZero);

                    switch (alignment)
                    {
                        case TextAlignment.LeftAlign:
                            yield return string.Create(spacesToAdd + str.Length, str, (chars, state) =>
                            {
                                state.AsSpan()
                                    .CopyTo(chars);

                                var position = str.Length;

                                for (var x = 0; x < chars.Length - str.Length; x++)
                                    chars[position++] = ' ';
                            });
                            break;
                        case TextAlignment.Center:
                            yield return string.Create(spacesToAdd + str.Length, str, (chars, state) =>
                            {
                                var position = 0;
                                var spacesPerSide = (int) Math.Round((chars.Length - (float) str.Length) / 2, MidpointRounding.ToEven);

                                for (; position < spacesPerSide; position++)
                                    chars[position] = ' ';

                                state.AsSpan()
                                    .CopyTo(chars.Slice(position));

                                position += state.Length;

                                for (; position < chars.Length; position++)
                                    chars[position] = ' ';
                            });
                            break;
                        case TextAlignment.RightAlign:
                            yield return string.Create(spacesToAdd + str.Length, str, (chars, state) =>
                            {
                                var position = 0;

                                for (; position < chars.Length - str.Length; position++)
                                    chars[position] = ' ';

                                state.AsSpan()
                                    .CopyTo(chars.Slice(position));
                            });
                            break;
                        default:
                            throw new ArgumentOutOfRangeException(nameof(alignment), alignment, null);
                    }
                } else
                    yield return str;
            }
        }

        /// <summary>
        ///     Detects the bitrate of the audio, and seeks to the specified time in the song.
        ///     <inheritdoc cref="Stream.Seek" />
        /// </summary>
        /// <param name="song">A song object.</param>
        internal static async Task AutoSeekAsync(this Song song)
        {
            var stream = await song.DataTask;
            var bitRate = stream.Length / (long) song.Duration.TotalSeconds;
            var seekPosition = bitRate * (long) song.Progress.Elapsed.TotalSeconds;

            if (seekPosition > stream.Length)
                stream.Seek(0, SeekOrigin.Begin);

            stream.Seek(seekPosition, SeekOrigin.Begin);
        }

        /// <summary>
        ///     Runs a process and asynchronously waits for it to exit.
        /// </summary>
        /// <param name="process">A process object.</param>
        /// <param name="readOutput">Whether or not to read output of the process.</param>
        internal static Task RunAsync(this Process process, bool readOutput)
        {
            var source = new TaskCompletionSource<bool>();
            if (readOutput)
                process.StartInfo.RedirectStandardOutput = true;
            process.EnableRaisingEvents = true;
            process.Exited += (s, e) => source.TrySetResult(true);
            process.Start();
            process.BeginOutputReadLine();

            return source.Task;
        }

        /// <summary>
        ///     Dequeues and re-queues objects one at a time, not inserting the object at the specified index.
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