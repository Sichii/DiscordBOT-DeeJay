﻿using System;

namespace DeeJay.Definitions
{
    internal static class CONSTANTS
    {
        internal const string TOKEN_PATH = @"Data\DiscordAuthToken.txt";
        internal const string YOUTUBEDL_PATH = @"Services\youtube-dl.exe";
        internal const string FFMPEG_PATH = @"Services\ffmpeg.exe";

        internal static readonly TimeSpan MAX_DURATION = TimeSpan.FromMinutes(15);
    }
}