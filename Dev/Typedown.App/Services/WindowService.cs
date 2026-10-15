using System.Reactive.Subjects;
using Typedown.Core.Interfaces;
using Typedown.Core.Utilities;
using Typedown.Utilities;
using Microsoft.UI.Xaml;
using Windows.Foundation;

namespace Typedown.Services
{
    /// <summary>
    /// WinUI 3 replacement for the XAML-Islands window service.
    /// In a pure WinUI 3 app the XAML island and the window are the same object,
    /// so the "XAML source handle" is just the window handle.
    /// </summary>
    public class WindowService : IWindowService
    {
        public Subject<nint> WindowStateChanged { get; } = new();

        public Subject<nint> WindowIsActivedChanged { get; } = new();

        public void RaiseWindowStateChanged(nint hWnd) => WindowStateChanged.OnNext(hWnd);

        public void RaiseWindowIsActivedChanged(nint hWnd) => WindowIsActivedChanged.OnNext(hWnd);

        public nint GetWindow(UIElement element) => WindowHelper.GetWindowHandle(element);

        public nint GetXamlSourceHandle(UIElement element) => WindowHelper.GetWindowHandle(element);

        public Point GetCursorPos(UIElement relativeTo)
        {
            var hwnd = WindowHelper.GetWindowHandle(relativeTo);
            PInvoke.GetCursorPos(out var screenPos);

            if (hwnd != default)
            {
                PInvoke.GetWindowRect(hwnd, out var windowRect);
                var dpi = PInvoke.GetDpiForWindow(hwnd);
                var scale = dpi == 0 ? 1.0 : dpi / 96.0;
                var local = new Point(
                    (screenPos.X - windowRect.left) / scale,
                    (screenPos.Y - windowRect.top) / scale);

                try
                {
                    return relativeTo.XamlRoot?.Content?.TransformToVisual(relativeTo).TransformPoint(local)
                        ?? local;
                }
                catch
                {
                    return local;
                }
            }

            return new Point(screenPos.X, screenPos.Y);
        }
    }
}
