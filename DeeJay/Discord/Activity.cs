using Discord;

namespace DeeJay.Discord
{
    /// <inheritdoc cref="IActivity" />
    public class Activity : IActivity
    {
        /// <inheritdoc cref="IActivity" />
        public Activity(string status, ActivityType type, ActivityProperties flags, string details)
        {
            Name = status;
            Type = type;
            Flags = flags;
            Details = details;
        }

        /// <inheritdoc cref="IActivity.Name" />
        public string Name { get; }

        /// <inheritdoc cref="IActivity.Type" />
        public ActivityType Type { get; }

        /// <inheritdoc cref="IActivity.Flags" />
        public ActivityProperties Flags { get; }

        /// <inheritdoc cref="IActivity.Details" />
        public string Details { get; }
    }
}