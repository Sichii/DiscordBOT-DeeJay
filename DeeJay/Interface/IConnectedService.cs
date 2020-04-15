using System.Threading.Tasks;

namespace DeeJay.Interface
{
    /// <summary>
    ///     A service that has a dependent connection to something else.
    /// </summary>
    public interface IConnectedService : IService
    {
        /// <summary>
        ///     Reconnects the service to whatever it was connected to.
        /// </summary>
        Task Reconnect();
    }
}