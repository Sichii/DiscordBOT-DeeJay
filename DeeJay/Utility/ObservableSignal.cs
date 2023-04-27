using System.Runtime.CompilerServices;

namespace DeeJay.Utility;

/// <summary>
/// Represents a payload that can be observed and awaited
/// </summary>
/// <typeparam name="T">The type of the payload</typeparam>
public sealed class ObservableSignal<T>
{
    private readonly TaskCompletionSource Source;
    /// <summary>
    /// The payload
    /// </summary>
    public T Signal { get; }
    
    /// <summary>
    /// Initializes a new instance of the <see cref="ObservableSignal{T}"/> class.
    /// </summary>
    /// <param name="signal">The payload</param>
    public ObservableSignal(T signal)
    {
        Signal = signal;
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