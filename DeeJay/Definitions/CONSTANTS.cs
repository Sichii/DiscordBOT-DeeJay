using System;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.Runtime.InteropServices;
using DeeJay.Properties;

namespace DeeJay.Definitions
{
    internal static class CONSTANTS
    {
        internal const string TOKEN_PATH = @"Data\DiscordAuthToken.txt";
        internal const string YOUTUBEDL_PATH = @"Services\youtube-dl.exe";
        internal const string FFMPEG_PATH = @"Services\ffmpeg.exe";
        internal static readonly TimeSpan MAX_DURATION = TimeSpan.FromMinutes(15);
        internal static readonly Graphics GRAPHICS;
        internal static readonly Font WHITNEY_FONT;
        internal static readonly double SPACE_LENGTH;
        private static readonly PrivateFontCollection PFC = new PrivateFontCollection();

        static CONSTANTS()
        {
            using var fontStream = new MemoryStream(Resources.whitney_light);

            var pfcData = fontStream.ToArray();
            var pinnedArr = GCHandle.Alloc(pfcData, GCHandleType.Pinned);
            PFC.AddMemoryFont(pinnedArr.AddrOfPinnedObject(), pfcData.Length);
            pinnedArr.Free();

            var bmp = new Bitmap(1, 1);
            GRAPHICS = Graphics.FromImage(bmp);
            var ff = PFC.Families[0];
            WHITNEY_FONT = new Font(ff, 16, FontStyle.Regular);
            SPACE_LENGTH = GRAPHICS.MeasureString(" ", WHITNEY_FONT)
                               .Width - 2;
        }
    }
}