using Discord;
using System;
using System.Collections.Generic;
using System.Text;

namespace DeeJay
{
    public class DiscordActivity : IActivity
    {
        public string Name { get; }
        public ActivityType Type { get; }

        public DiscordActivity(string status, ActivityType type)
        {
            Name = status;
            Type = type;
        }
    }
}
