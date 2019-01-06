using System.Threading.Tasks;

namespace DeeJay
{
    internal class Program
    {
        internal static void Main(string[] args)
        {
            Client.Initialize();

            Task.Delay(-1).GetAwaiter().GetResult();
        }
    }
}
