using System.Threading.Tasks;
using DeeJay.Model;

namespace DeeJay
{
    internal class Program
    {
        internal static async Task Main(string[] args)
        {
            NLog.Web.NLogBuilder.ConfigureNLog("NLog.config");

            await Client.Login();

            await Task.Delay(-1);
        }
    }
}