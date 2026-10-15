using System;
using System.Reactive.Disposables;
using Typedown.Core.Utilities;
using Typedown.Core.ViewModels;
using Windows.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Typedown.Core.Controls
{
    public sealed partial class MenuBar : UserControl
    {
        public AppViewModel ViewModel => DataContext as AppViewModel;
        public SettingsViewModel Settings => ViewModel?.SettingsViewModel;

        private readonly CompositeDisposable disposables = new();

        public MenuBar()
        {
            InitializeComponent();
        }

        private void OnSizeChanged(object sender, Microsoft.UI.Xaml.SizeChangedEventArgs e)
        {
            if (TitleGrid != null)
            {
                if (ActualWidth / 2 > MenuBarControl.ActualWidth + TitleTextBlock.ActualWidth / 2 + 16)
                {
                    TitleGrid.Margin = new(0);
                    Grid.SetColumn(TitleGrid, 0);
                    Grid.SetColumnSpan(TitleGrid, 3);
                }
                else
                {
                    TitleGrid.Margin = new(0, 0, 46 * 3, 0);
                    Grid.SetColumn(TitleGrid, 1);
                    Grid.SetColumnSpan(TitleGrid, 2);
                }
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (Settings.AppCompactMode)
            {
                var uiViewModel = ViewModel.UIViewModel;
                var oldCaptionHeight = uiViewModel.CaptionHeight;
                uiViewModel.CaptionHeight = 40;
                disposables.Add(Disposable.Create(() => uiViewModel.CaptionHeight = oldCaptionHeight));
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            disposables.Dispose();
            Bindings?.StopTracking();
        }

        private Visibility IsCollapsed(bool boolean) => boolean ? Visibility.Collapsed : Visibility.Visible;

        private DateTime prevLeftButtonPressedTime = DateTime.Now;

        private void OnMenuBarPointerEvent(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (Settings.AppCompactMode && e.OriginalSource is Grid)
            {
                var hwnd = Utilities.Common.GetWindowHandle(this);
                if (hwnd == IntPtr.Zero)
                    return;
                DispatcherQueue.TryEnqueue(() =>
                {
                    PInvoke.GetCursorPos(out var point);
                    var packedPoint = (point.Y << 16) + point.X;
                    var kind = e.GetCurrentPoint(this).Properties.PointerUpdateKind;
                    if (kind.Equals(PointerUpdateKind.LeftButtonPressed))
                    {
                        if ((DateTime.Now - prevLeftButtonPressedTime).TotalMilliseconds < PInvoke.GetDoubleClickTime())
                            PInvoke.PostMessage(hwnd, (uint)PInvoke.WindowMessage.WM_NCLBUTTONDBLCLK, (nint)(uint)PInvoke.HitTestFlags.CAPTION, (nint)(long)packedPoint);
                        else
                            PInvoke.PostMessage(hwnd, (uint)PInvoke.WindowMessage.WM_NCLBUTTONDOWN, (nint)(uint)PInvoke.HitTestFlags.CAPTION, (nint)(long)packedPoint);
                        prevLeftButtonPressedTime = DateTime.Now;
                    }
                    if (kind.Equals(PointerUpdateKind.RightButtonReleased))
                    {
                        PInvoke.PostMessage(hwnd, (uint)PInvoke.WindowMessage.WM_NCRBUTTONUP, (nint)(uint)PInvoke.HitTestFlags.CAPTION, (nint)(long)packedPoint);
                    }
                });
            }
        }
    }
}
