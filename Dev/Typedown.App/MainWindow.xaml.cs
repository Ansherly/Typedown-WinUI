using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Typedown.Core;
using Typedown.Core.Controls;
using Typedown.Core.Enums;
using Typedown.Core.Interfaces;
using Typedown.Core.Utilities;
using Typedown.Core.ViewModels;
using Typedown.Services;
using Typedown.Utilities;
using Windows.Globalization;
using Windows.Graphics;
using SysMath = System.Math;

namespace Typedown
{
    /// <summary>
    /// WinUI 3 main window hosting <see cref="RootControl"/> (the real Typedown UI:
    /// menu bar, editor surface, status bar and settings frame).
    /// Replaces the XAML-Islands <c>XamlWindow</c>-derived shell.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        /// <summary>Height of the custom caption strip, in DIPs (matches Core's Caption).</summary>
        private const int CaptionHeight = 32;

        /// <summary>Chinese is the default UI language; zh-Hans resources surface as zh-CN.</summary>
        private const string DefaultLanguage = "zh-CN";

        private static readonly List<MainWindow> OpenWindows = new();

        public IServiceScope ServiceScope { get; private set; } = Injection.ServiceProvider.CreateScope();

        public IServiceProvider ServiceProvider => ServiceScope?.ServiceProvider;

        public AppViewModel AppViewModel => ServiceProvider?.GetService<AppViewModel>();

        public RootControl RootControl { get; private set; }

        public WindowService WindowService => ServiceProvider?.GetService<IWindowService>() as WindowService;

        public nint Handle => WindowHelper.GetWindowHandle(RootControl);

        private readonly CompositeDisposable disposables = new();

        private InputNonClientPointerSource nonClientSource;

        private bool dragRegionsLogged;

        public MainWindow()
        {
            TrySetPrimaryLanguage();

            Title = Config.AppName;
            AppWindow.Resize(new SizeInt32(1130, 700));

            RootControl = new RootControl { DataContext = AppViewModel };
            Content = RootControl;

            InitializeCustomTitleBar();

            Activated += OnActivated;
            Closed += OnClosed;
            InitializeBinding();

            OpenWindows.Add(this);
        }

        /// <summary>
        /// All open Typedown windows. Deliberately not named <c>Windows</c>, which would
        /// shadow the <c>Windows.*</c> namespaces used across this file.
        /// </summary>
        public static IReadOnlyList<MainWindow> OpenedWindows => OpenWindows;

        public static MainWindow FindByHandle(nint handle) =>
            OpenWindows.FirstOrDefault(w => w.Handle == handle);

        // ------------------------------------------------------------- custom title bar
        //
        // The original app draws its own caption strip (Core's Caption / MenuBar
        // LeftDragBar / RightDragBar). In a pure WinUI 3 window that means:
        //   1. ExtendsContentIntoTitleBar so app content occupies the caption area, and
        //   2. explicitly declaring which regions act as the caption (drag / double-click
        //      to maximize), since the system no longer reads an attached property.

        private void InitializeCustomTitleBar()
        {
            try
            {
                ExtendsContentIntoTitleBar = true;

                var titleBar = AppWindow.TitleBar;
                titleBar.PreferredHeightOption = TitleBarHeightOption.Standard;

                // The app renders its own chrome; keep the system buttons transparent
                // so they sit on the Mica/acrylic backdrop.
                titleBar.ButtonBackgroundColor = Microsoft.UI.Colors.Transparent;
                titleBar.ButtonInactiveBackgroundColor = Microsoft.UI.Colors.Transparent;

                nonClientSource = InputNonClientPointerSource.GetForWindowId(AppWindow.Id);

                if (Content is FrameworkElement root)
                {
                    root.Loaded += (s, e) => UpdateDragRegions();
                    root.SizeChanged += (s, e) => UpdateDragRegions();
                    root.LayoutUpdated += (s, e) => UpdateDragRegions();
                }

                SyncCaptionButtonColors();
            }
            catch (Exception ex)
            {
                App.Log("InitializeCustomTitleBar failed: " + ex.Message);
            }
        }

        /// <summary>
        /// Publishes the caption drag regions. MenuBar's own drag bars are used when they
        /// are realised (compact mode); otherwise the caption strip minus the system
        /// caption-button inset is draggable.
        /// </summary>
        private void UpdateDragRegions()
        {
            if (nonClientSource == null || Content is not FrameworkElement root)
                return;

            try
            {
                var rects = new List<RectInt32>();

                var dragBars = FindDescendants(root, "LeftDragBar")
                    .Concat(FindDescendants(root, "RightDragBar"))
                    .Where(e => e.ActualWidth > 0 && e.ActualHeight > 0)
                    .ToList();

                foreach (var bar in dragBars)
                {
                    var origin = bar.TransformToVisual(root)
                        .TransformPoint(new Windows.Foundation.Point(0, 0));
                    rects.Add(new RectInt32(
                        (int)SysMath.Round((double)origin.X),
                        (int)SysMath.Round((double)origin.Y),
                        (int)SysMath.Round((double)bar.ActualWidth),
                        (int)SysMath.Round(SysMath.Max((double)bar.ActualHeight, (double)CaptionHeight))));
                }

                if (rects.Count == 0)
                {
                    var titleBar = AppWindow.TitleBar;
                    var left = (double)titleBar.LeftInset;
                    var usableWidth = (double)root.ActualWidth - left - (double)titleBar.RightInset;
                    if (usableWidth > 0)
                        rects.Add(new RectInt32((int)SysMath.Round(left), 0, (int)SysMath.Round(usableWidth), CaptionHeight));
                }

                nonClientSource.SetRegionRects(NonClientRegionKind.Caption, rects.ToArray());

                if (!dragRegionsLogged)
                {
                    dragRegionsLogged = true;
                    App.Log($"title bar: {rects.Count} drag region(s), content width={root.ActualWidth:F0}");
                }
            }
            catch (Exception ex)
            {
                if (!dragRegionsLogged)
                {
                    dragRegionsLogged = true;
                    App.Log("UpdateDragRegions failed: " + ex.Message);
                }
            }
        }

        private static IEnumerable<FrameworkElement> FindDescendants(DependencyObject root, string name)
        {
            if (root == null)
                yield break;

            var count = VisualTreeHelper.GetChildrenCount(root);
            for (var i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is FrameworkElement fe && fe.Name == name)
                    yield return fe;

                foreach (var nested in FindDescendants(child, name))
                    yield return nested;
            }
        }

        /// <summary>Keeps the system caption buttons legible against the app backdrop.</summary>
        private void SyncCaptionButtonColors()
        {
            try
            {
                var dark = RootControl?.ActualTheme == ElementTheme.Dark;
                var foreground = dark ? Microsoft.UI.Colors.White : Microsoft.UI.Colors.Black;

                var titleBar = AppWindow.TitleBar;
                titleBar.ButtonForegroundColor = foreground;
                titleBar.ButtonInactiveForegroundColor = foreground;
                titleBar.ButtonHoverBackgroundColor = dark
                    ? Windows.UI.Color.FromArgb(0x20, 0xFF, 0xFF, 0xFF)
                    : Windows.UI.Color.FromArgb(0x20, 0x00, 0x00, 0x00);
                titleBar.ButtonHoverForegroundColor = foreground;
                titleBar.ButtonPressedBackgroundColor = dark
                    ? Windows.UI.Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF)
                    : Windows.UI.Color.FromArgb(0x40, 0x00, 0x00, 0x00);
            }
            catch
            {
                // Purely cosmetic.
            }
        }

        // ------------------------------------------------------------- bindings

        private void InitializeBinding()
        {
            if (AppViewModel == null)
                return;

            disposables.Add(AppViewModel.SettingsViewModel
                .WhenPropertyChanged(nameof(SettingsViewModel.AppTheme))
                .Cast<AppTheme>()
                .StartWith(AppViewModel.SettingsViewModel.AppTheme)
                .Subscribe(SetTheme));

            disposables.Add(AppViewModel.UIViewModel
                .WhenPropertyChanged(nameof(UIViewModel.MainWindowTitle))
                .Cast<string>()
                .StartWith(AppViewModel.UIViewModel.MainWindowTitle)
                .Subscribe(title => Title = title));

            disposables.Add(AppViewModel.SettingsViewModel
                .WhenPropertyChanged(nameof(SettingsViewModel.Topmost))
                .Cast<bool>()
                .StartWith(AppViewModel.SettingsViewModel.Topmost)
                .Subscribe(SetTopmost));

            // Compact mode swaps the caption strip for MenuBar's own drag bars.
            disposables.Add(AppViewModel.SettingsViewModel
                .WhenPropertyChanged(nameof(SettingsViewModel.AppCompactMode))
                .Subscribe(_ => UpdateDragRegions()));

            disposables.Add(AppViewModel.FileViewModel
                .NewWindowCommand.OnExecute
                .Subscribe(path => WindowHelper.OpenNewWindow(new[] { path })));
        }

        private void SetTheme(AppTheme theme)
        {
            if (Content is FrameworkElement root)
            {
                root.RequestedTheme = theme switch
                {
                    AppTheme.Light => ElementTheme.Light,
                    AppTheme.Dark => ElementTheme.Dark,
                    _ => ElementTheme.Default,
                };
            }

            SyncCaptionButtonColors();
        }

        private void SetTopmost(bool topmost)
        {
            try
            {
                if (AppWindow.Presenter is OverlappedPresenter presenter)
                    presenter.IsAlwaysOnTop = topmost;
            }
            catch
            {
                // Ignore
            }
        }

        private void OnActivated(object sender, WindowActivatedEventArgs e)
        {
            try
            {
                var hwnd = Handle;
                if (hwnd != default)
                {
                    WindowService?.RaiseWindowIsActivedChanged(hwnd);
                    WindowService?.RaiseWindowStateChanged(hwnd);
                }
            }
            catch
            {
                // Ignore
            }
        }

        private void OnClosed(object sender, WindowEventArgs e)
        {
            OpenWindows.Remove(this);
            ServiceScope?.Dispose();
            ServiceScope = null;
            RootControl = null;
            Content = null;
            disposables.Dispose();
        }

        /// <summary>
        /// Pins the UI language. An explicit choice in settings wins; otherwise (including
        /// the "default" / follow-system value) Chinese is used.
        /// </summary>
        private void TrySetPrimaryLanguage()
        {
            try
            {
                var settingLanguage = AppViewModel?.SettingsViewModel?.Language;

                if (!string.IsNullOrEmpty(settingLanguage) &&
                    settingLanguage != "default" &&
                    Locale.SupportedLangs.ContainsKey(settingLanguage))
                {
                    ApplicationLanguages.PrimaryLanguageOverride = settingLanguage;
                }
                else
                {
                    ApplicationLanguages.PrimaryLanguageOverride = DefaultLanguage;
                }
            }
            catch
            {
                // Ignore
            }
        }
    }
}
