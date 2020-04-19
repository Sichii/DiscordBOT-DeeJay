using System.Threading.Tasks;

namespace DeeJay.Interfaces
{
    /// <summary>
    ///     A service that has a dependent connection to something else.
    /// </summary>
    public interface IConnectedService : IService
    {
        bool Connected { get; set; }

        /// <summary>
        ///     Reconnects the service to whatever it was connected to.
        /// </summary>
        Task ConnectAsync();

        /// <summary>
        ///     Disconnects the service from whatever it was connected to.
        /// </summary>
        Task DisconnectAsync(bool wait);
    }
}