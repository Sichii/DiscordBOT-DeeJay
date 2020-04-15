using NLog;

namespace DeeJay.Interface
{
    public interface IService
    {
        ulong GuildId { get; }
        Logger Log { get; }
    }
}