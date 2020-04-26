using System.Threading.Tasks;
using DeeJay.Utility;

namespace DeeJay.Interfaces
{
    /// <summary>
    ///     A service that has a dependent connection to something else.
    /// </summary>
    public interface IConnectedService : IService
    {
        bool Connected { get; set; }
        Canceller DisconnectCanceller { get; }

        /// <summary>
        ///     Reconnects the service to whatever it was connected to.
        /// </summary>
        ValueTask ConnectAsync();

        /// <summary>
        ///     Disconnects the service from whatever it was connected to.
        /// </summary>
        ValueTask DisconnectAsync(bool wait);
    }
}