using Discord;

namespace DeeJay.Model
{
    public class DiscordActivity : IActivity
    {
        public DiscordActivity(string status, ActivityType type)
        {
            Name = status;
            Type = type;
        }

        public string Name { get; }
        public ActivityType Type { get; }
    }
}