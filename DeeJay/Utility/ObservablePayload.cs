using System.Runtime.CompilerServices;
using DeeJay.Services;

namespace DeeJay.Utility;

/// <summary>
/// Represents a payload that can be observed and awaited
/// </summary>
/// <typeparam name="T">The type of the payload</typeparam>
public class ObservablePayload<T>
{
    private readonly TaskCompletionSource Source;
    /// <summary>
    /// The payload
    /// </summary>
    public T Obj { get; }
    
    /// <summary>
    /// Initializes a new instance of the <see cref="ObservablePayload{T}"/> class.
    /// </summary>
    /// <param name="obj">The payload</param>
    public ObservablePayload(T obj)
    {
        Obj = obj;
        Source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    /// <summary>
    /// Attempts to signal that the payload has been processed
    /// </summary>
    public void Complete() => Source.TrySetResult();
    
    /// <summary>
    /// Gets a task that completes when the payload is complete
    /// </summary>
    public TaskAwaiter GetAwaiter() => Source.Task.GetAwaiter();
}