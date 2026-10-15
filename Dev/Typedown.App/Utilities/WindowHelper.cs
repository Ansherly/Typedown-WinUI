using Microsoft.UI.Xaml;
using System;
using System.Linq;
using Typedown.Core.Utilities;
using Typedown.Core.ViewModels;
using Windows.Foundation;

namespace Typedown.Utilities
{
    /// <summary>
    /// WinUI 3 window helpers. Replaces the XAML-Islands versions that relied on
    /// <c>XamlWindow</c> / <c>ShowWindowCommand</c>. Named <c>WindowHelper</c> (not
    /// <c>Common</c>) to avoid colliding with <see cref="Typedown.Core.Utilities.Common"/>.
    /// </summary>
    public static class WindowHelper
    {
        public static Point MakePoint(this IntPtr p) => new(GetLowWord(p), GetHighWord(p));

        public static nint PackPoint(this Point point) => ((int)point.X) | (((int)point.Y) << 16);

        public static int GetHighWord(IntPtr p) => (int)(p.ToInt64() >> 16);

        public static int GetLowWord(IntPtr p) => (int)(p.ToInt64() & 0xFFFF);

        /// <summary>
        /// Resolves the Win32 top-level window handle (HWND) hosting a XAML element.
        /// WinUI 3 replacement for XAML-Islands <c>XamlWindow.GetWindow</c>.
        /// </summary>
        public static nint GetWindowHandle(DependencyObject element)
        {
            try
            {
                var xamlRoot = (element as UIElement)?.XamlRoot;
                if (xamlRoot == null)
                    return default;
                var windowId = xamlRoot.ContentIslandEnvironment.AppWindowId;
                return Microsoft.UI.Win32Interop.GetWindowFromWindowId(windowId);
            }
            catch
            {
                return default;
            }
        }

        public static double GetScalingFactor(nint handle)
        {
            try
            {
                var dpi = PInvoke.GetDpiForWindow(handle);
                return dpi == 0 ? 1.0 : dpi / 96.0;
            }
            catch
            {
                return 1.0;
            }
        }

        public static void TrySaveWindowPlacement(this MainWindow window, Point offset = default)
        {
            var handle = window.Handle;
            if (handle == IntPtr.Zero)
                return;

            try
            {
                if (!PInvoke.GetWindowPlacement(handle, out var placement))
                    return;

                var rawX = (int)(offset.X * GetScalingFactor(handle));
                var rawY = (int)(offset.Y * GetScalingFactor(handle));
                placement.rcNormalPosition.left += rawX;
                placement.rcNormalPosition.top += rawY;
                placement.rcNormalPosition.right += rawX;
                placement.rcNormalPosition.bottom += rawY;
                window.AppViewModel.SettingsViewModel.StartupPlacement = placement;
            }
            catch
            {
                // Ignore
            }
        }

        public static bool RestoreWindowPlacement(this MainWindow window)
        {
            var handle = window.Handle;
            if (handle == IntPtr.Zero)
                return false;

            try
            {
                var placement = window.AppViewModel?.SettingsViewModel?.StartupPlacement;
                if (!placement.HasValue)
                    return false;

                var value = placement.Value;
                if (value.showCmd != PInvoke.ShowWindowCommand.ShowMaximized)
                    value.showCmd = PInvoke.ShowWindowCommand.Normal;
                return PInvoke.SetWindowPlacement(handle, ref value);
            }
            catch
            {
                return false;
            }
        }

        public static IntPtr OpenNewWindow(string[] args)
        {
            var filePath = CommandLine.GetOpenFilePath(args);
            if (string.IsNullOrEmpty(filePath) || !FileViewModel.TryGetOpenedWindow(filePath, out var windowHWnd))
            {
                var newWindow = new MainWindow();
                newWindow.AppViewModel.CommandLineArgs = args;
                newWindow.Activate();
                return newWindow.Handle;
            }

            if (PInvoke.IsIconic(windowHWnd))
                PInvoke.ShowWindow(windowHWnd, PInvoke.ShowWindowCommand.Restore);
            return windowHWnd;
        }
    }
}
