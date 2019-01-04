using System;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace DeeJay
{
    internal class Program
    {
        internal static void Main(string[] args)
        {
            var client = new Client();
            Task init = client.Initialize();
            init.Wait();

            Task.Delay(-1).GetAwaiter().GetResult();
        }
    }
}
