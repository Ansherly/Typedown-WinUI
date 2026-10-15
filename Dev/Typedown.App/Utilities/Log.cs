using System;
using System.IO;
using System.Threading.Tasks;

namespace Typedown.Core.Utilities
{
    /// <summary>
    /// Failure/diagnostic reporting.
    ///
    /// This used to POST the report to a third-party server (typedown.ownbox.cn) that belongs to
    /// the upstream author. The port has no business phoning home, so reports now go to the same
    /// local log the app's own crash handler writes to
    /// (<c>%LOCALAPPDATA%\Typedown\startup.log</c>), which is also what the port's debug guidance
    /// tells you to read.
    /// </summary>
    public static class Log
    {
        private static readonly string LogPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Typedown", "startup.log");

        public static Task Report(string type, string content)
        {
            Write($"{type}: {content}");
            return Task.CompletedTask;
        }

        private static void Write(string message)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath));
                File.AppendAllText(LogPath, $"{DateTime.Now:HH:mm:ss.fff} {message}{Environment.NewLine}");
            }
            catch
            {
                // Diagnostics must never take the app down.
            }
        }
    }
}
