using System.Diagnostics.CodeAnalysis;
using DeeJay.Definitions;
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
    public static bool TryGetVoiceChannel(this IUser user, [MaybeNullWhen(false)] out IVoiceChannel voiceChannel)
    {
        voiceChannel = default;

        if (user is IGuildUser guildUser)
            voiceChannel = guildUser.VoiceChannel;
        
        return voiceChannel != default;
    }
    
    /// <summary>
    ///    Checks if the user has the specified privilege.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public static bool HasPrivilege(this IGuildUser user, Privilege privilege) =>
        privilege switch
        {
            Privilege.None          => true,
            Privilege.Normal        => user.GuildPermissions is { SendMessages: true, Connect: true },
            Privilege.Elevated      => user.GuildPermissions.ManageChannels || user.GuildPermissions.KickMembers,
            Privilege.Administrator => user.GuildPermissions.Administrator,
            _                       => throw new ArgumentOutOfRangeException()
        };
}