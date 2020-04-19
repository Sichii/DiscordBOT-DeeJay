using NLog;

namespace DeeJay.Interfaces
{
    public interface IService
    {
        ulong GuildId { get; }
        Logger Log { get; }
    }
}