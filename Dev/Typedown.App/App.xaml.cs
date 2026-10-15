using Microsoft.UI.Xaml;
using System;
using System.IO;

namespace Typedown
{
    public partial class App : Application
    {
        private static readonly string LogPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Typedown", "startup.log");

        private MainWindow window;

        public App()
        {
            UnhandledException += (s, e) => Log("UNHANDLED: " + e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                Log("APPDOMAIN UNHANDLED: " + e.ExceptionObject);
            InitializeComponent();
        }

        /// <summary>Crash/diagnostic log (also used by Log.Report callers).</summary>
        internal static void Log(string message)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath));
                File.AppendAllText(LogPath, $"{DateTime.Now:HH:mm:ss.fff} {message}{Environment.NewLine}");
            }
            catch
            {
                // diagnostics only
            }
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            window = new MainWindow();
            window.Activate();
        }
    }
}
