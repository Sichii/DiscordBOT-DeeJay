using System.Collections.Concurrent;

namespace DeeJay.Extensions;

/// <summary>
/// Extensions for <see cref="Queue{T}"/>
/// </summary>
public static class QueueExtensions
{
    /// <summary>
    /// Removes an object from the queue
    /// </summary>
    /// <param name="queue">This queue</param>
    /// <param name="obj">The object to remove from the queue</param>
    /// <param name="comparer">The comparer to use when trying to find the object in the queue</param>
    /// <typeparam name="T">The type of the object</typeparam>
    /// <exception cref="InvalidOperationException"></exception>
    public static void Remove<T>(this ConcurrentQueue<T> queue, T obj, IEqualityComparer<T>? comparer = default)
    {
        var count = queue.Count;
        comparer ??= EqualityComparer<T>.Default;

        for (var i = 0; i < count; i++)
        {
            if (!queue.TryDequeue(out var popped))
                throw new InvalidOperationException("Collection was modified during enumeration");

            if (!comparer.Equals(popped, obj))
                queue.Enqueue(popped);
        }
    }
    
    /// <summary>
    /// Removes an object from the queue at the specified index
    /// </summary>
    /// <param name="queue">This queue</param>
    /// <param name="index">The index of the object to remove</param>
    /// <typeparam name="T">The type of the object</typeparam>
    /// <exception cref="InvalidOperationException"></exception>
    public static void RemoveAt<T>(this ConcurrentQueue<T> queue, int index)
    {
        var count = queue.Count;

        for (var i = 0; i < count; i++)
        {
            if (!queue.TryDequeue(out var popped))
                throw new InvalidOperationException("Collection was modified during enumeration");

            if (i != index)
                queue.Enqueue(popped);
        }
    }
}