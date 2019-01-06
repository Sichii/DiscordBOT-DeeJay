using System;
using System.Collections.Generic;
using System.Text;

namespace DeeJay
{
    public static class Extensions
    {
        public static string ToReadableString(this TimeSpan timeSpan)
        {
            int hours = timeSpan.Hours;
            int minutes = timeSpan.Minutes;
            int seconds = timeSpan.Seconds;

            return $"{((hours == 0) ? string.Empty : $"{hours}:")}{minutes}:{seconds.ToString("D2")}";
        }
    }
}
