using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CookieTool
{
    internal class Program
    {
        private static async Task Main()
        {
            await RunChromeAsync();

            Console.Write("Press any key after cookies.txt is generated from chrome.");
            Console.ReadKey();

            var profileDir = Environment.ExpandEnvironmentVariables("%userprofile%");
            var sourcePath = $@"{profileDir}\downloads\cookies.txt";
            var djPath = $@"{profileDir}\desktop\deejay\externalservices\cookies.txt";

            var cookieLines = await File.ReadAllTextAsync(sourcePath);
            cookieLines = Regex.Replace(cookieLines, "\r\n?|\n", Environment.NewLine);

            await File.WriteAllTextAsync(djPath, cookieLines);
            File.Delete(sourcePath);
        }

        private static async Task RunChromeAsync()
        {
            var source = new TaskCompletionSource<bool>();

            using var chrome = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "cmd",
                    Arguments = @"/c start https://www.youtube.com/watch?v=kLc2f6HbQiY",
                    UseShellExecute = false
                },
                EnableRaisingEvents = true,
            };

            chrome.Exited += (s, e) => source.TrySetResult(true);
            chrome.Start();

            await source.Task;
        }
    }
}