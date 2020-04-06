using Discord;

namespace DeeJay.Model
{
    /// <inheritdoc cref="IActivity"/>
    public class DiscordActivity : IActivity
    {
        /// <inheritdoc cref="IActivity"/>
        public DiscordActivity(string status, ActivityType type)
        {
            Name = status;
            Type = type;
        }

        /// <inheritdoc cref="IActivity.Name"/>
        public string Name { get; }

        /// <inheritdoc cref="IActivity.Type"/>
        public ActivityType Type { get; }
    }
}