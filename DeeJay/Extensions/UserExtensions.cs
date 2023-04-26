using System.Diagnostics.CodeAnalysis;
using Discord;

namespace DeeJay.Extensions;

/// <summary>
///     Provides extension methods for <see cref="IUser"/> objects.
/// </summary>
public static class UserExtensions
{
    /// <summary>
    ///     Gets the voice channel the user is in, if theyre in one.
    /// </summary>
    /// <param name="user">This user</param>
    /// <param name="voiceChannel"></param>
    internal static bool TryGetVoiceChannel(this IUser user, [MaybeNullWhen(false)] out IVoiceChannel voiceChannel)
    {
        voiceChannel = default;

        if (user is IGuildUser guildUser)
            voiceChannel = guildUser.VoiceChannel;
        
        return voiceChannel != default;
    }
}