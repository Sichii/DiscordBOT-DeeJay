using System.Threading.Tasks;

namespace DeeJay
{
    internal class Program
    {
        internal static async Task Main(string[] args)
        {
            await Client.Initialize();

            await Task.Delay(-1);
        }
    }
}
