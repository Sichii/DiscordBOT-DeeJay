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
            Client.Initialize();

            Task.Delay(-1).GetAwaiter().GetResult();
        }
    }
}
