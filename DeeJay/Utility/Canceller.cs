using System;
using System.Threading;
using System.Threading.Tasks;

namespace DeeJay.Utility
{
    /// <summary>
    ///     A utility object for handling task cancellations.
    /// </summary>
    internal class Canceller : IDisposable
    {
        private readonly SemaphoreSlim Sync;
        private CancellationTokenSource Source { get; set; }
        internal CancellationToken Token => Source.Token;

        internal static Canceller New => new Canceller();

        public static implicit operator CancellationToken(Canceller canceller) => canceller.Token;

        private Canceller()
        {
            Source = new CancellationTokenSource();
            Sync = new SemaphoreSlim(1, 1);
        }


        /// <inheritdoc cref="CancellationTokenSource.Cancel()" />
        internal async Task CancelAsync()
        {
            // ReSharper disable once MethodSupportsCancellation
            await Sync.WaitAsync();

            try
            {
                Source.Cancel();
                Source.Dispose();
                Source = new CancellationTokenSource();
            } finally
            {
                Sync.Release();
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Sync?.Dispose();
            Source?.Dispose();
        }
    }
}