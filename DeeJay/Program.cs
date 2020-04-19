using System.Threading.Tasks;
using DeeJay.Model;
using NLog.Web;

namespace DeeJay
{
    internal class Program
    {
        internal static async Task Main(string[] args)
        {
            NLogBuilder.ConfigureNLog("NLog.config");
            await Client.Login();
            await Task.Delay(-1);
        }
    }
}