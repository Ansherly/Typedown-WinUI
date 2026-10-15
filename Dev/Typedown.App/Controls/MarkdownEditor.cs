using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Path = System.IO.Path;
using Config = Typedown.Core.Config;
using AppContentDialog = Typedown.Core.Controls.AppContentDialog;
using Microsoft.Web.WebView2.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PropertyChanged;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Typedown.Core.Interfaces;
using Typedown.Core.Services;
using Typedown.Core.Utilities;
using Typedown.Core.ViewModels;
using Typedown.Utilities;
using Typedown.Services;
using Windows.Foundation;

namespace Typedown.Controls
{
    /// <summary>
    /// WinUI 3 markdown editor host: an in-process WebView2 running the Muya/React
    /// front-end built from Dev\Typedown.Editor, plus the host-side message bridge.
    /// Replaces the XAML-Islands based WebViewController used by the WPF shell.
    /// </summary>
    public partial class MarkdownEditor : UserControl, IMarkdownEditor, IDisposable, INotifyPropertyChanged
    {
        public CoreWebView2 CoreWebView2 { get; private set; }

        public EventCenter EventCenter => ServiceProvider.GetService<EventCenter>();

        public RemoteInvoke RemoteInvoke => ServiceProvider.GetService<RemoteInvoke>();

        public AppViewModel AppViewModel => ServiceProvider.GetService<AppViewModel>();

        public Transport Transport => ServiceProvider.GetService<Transport>();

        public bool IsEditorLoadFailed { get; set; }

        public bool IsEditorLoaded { get; set; }

        public IServiceProvider ServiceProvider { get; }

        private readonly Rectangle dummyRectangle = new();

        // Canvas gives children zero desired size, so the WebView2 lives in a stretching
        // Grid and the dummy rectangle floats above it as a hit-test-transparent overlay
        // (used to capture the viewport while scrolling).
        private readonly Grid layoutRoot = new();

        private readonly Canvas overlay = new();

        private readonly CompositeDisposable disposables = new();

        private WebView2 webView;

        public MarkdownEditor(IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;
            Loaded += OnLoaded;

            dummyRectangle.IsHitTestVisible = false;
            dummyRectangle.Fill = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
            overlay.IsHitTestVisible = false;
            overlay.Children.Add(dummyRectangle);

            layoutRoot.Children.Add(overlay);
            Content = layoutRoot;
            IsTabStop = true;

            disposables.Add(RemoteInvoke.Handle("ContentLoaded", OnContentLoaded));
            disposables.Add(RemoteInvoke.Handle<string>("UnhandledException", OnUnhandledException));
            disposables.Add(RemoteInvoke.Handle("GetCurrentTheme", () => ServiceProvider.GetCurrentTheme()));
            disposables.Add(RemoteInvoke.Handle<string>("OpenNewWindow", OpenNewWindow));

            disposables.Add(AppViewModel.UIViewModel.WhenPropertyChanged(nameof(UIViewModel.ActualTheme))
                .Merge(AppViewModel.SettingsViewModel.WhenPropertyChanged(nameof(SettingsViewModel.UseEditorMicaEffect)))
                .Merge(AppViewModel.SettingsViewModel.WhenPropertyChanged(nameof(SettingsViewModel.UseMicaEffect)))
                .Subscribe(_ => OnThemeChanged()));
        }

        private static readonly HashSet<ulong> reported = new();

        private void OnUnhandledException(string error)
        {
            CoreWebView2?.Reload();
            try
            {
                var hash = Core.Utilities.Common.SimpleHash(error);
                if (!reported.Contains(hash))
                {
                    reported.Add(hash);
                    Log.Report("WebViewUnhandledException", error);
                }
            }
            catch
            {
                // Ignore
            }
        }

        [SuppressPropertyChangedWarnings]
        private void OnThemeChanged()
        {
            _ = DispatcherQueue.TryEnqueue(() => PostMessage("ThemeChanged", ServiceProvider.GetCurrentTheme()));
        }

        private void OnContentLoaded()
        {
            Opacity = 1;
            IsEditorLoaded = true;
        }

        protected override void OnPointerPressed(PointerRoutedEventArgs e)
        {
            base.OnPointerPressed(e);
            Focus(FocusState.Pointer);
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            Opacity = 0;
            IsEditorLoaded = false;
            IsEditorLoadFailed = false;

            if (webView != null)
                return;

            try
            {
                webView = new WebView2
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch
                };
                layoutRoot.Children.Insert(0, webView);

                var userDataFolder = Path.Combine(Config.GetLocalFolderPath(), "WebView2");
                Directory.CreateDirectory(userDataFolder);

                var options = new CoreWebView2EnvironmentOptions();
                var environment = await CoreWebView2Environment.CreateWithOptionsAsync(
                    null, userDataFolder, options);

                await webView.EnsureCoreWebView2Async(environment);

                if (disposables.IsDisposed)
                {
                    IsEditorLoadFailed = true;
                    return;
                }

                CoreWebView2 = webView.CoreWebView2;
                OnCoreWebView2Initialized();
                LoadStaticResources();
            }
            catch (Exception ex)
            {
                Log.Report("MarkdownEditorInit", ex.ToString());
                IsEditorLoadFailed = true;
            }
        }

        private void OnCoreWebView2Initialized()
        {
            var settings = CoreWebView2.Settings;
            settings.AreBrowserAcceleratorKeysEnabled = false;
            settings.AreDefaultContextMenusEnabled = false;
            settings.AreDefaultScriptDialogsEnabled = false;
            settings.AreDevToolsEnabled = false;
            settings.IsBuiltInErrorPageEnabled = false;
            settings.IsGeneralAutofillEnabled = false;
            settings.IsPinchZoomEnabled = true;
            settings.IsStatusBarEnabled = false;
            settings.IsSwipeNavigationEnabled = false;
            settings.IsZoomControlEnabled = false;

            CoreWebView2.ScriptDialogOpening += OnScriptDialogOpening;
            CoreWebView2.WebMessageReceived += OnWebMessageReceived;
            CoreWebView2.NewWindowRequested += OnNewWindowRequested;
        }

        private void LoadStaticResources()
        {
            var staticsFolder = Path.Combine(AppContext.BaseDirectory, "Resources", "Statics");
            var indexPath = Path.Combine(staticsFolder, "index.html");
            if (!File.Exists(indexPath))
            {
                IsEditorLoadFailed = true;
                return;
            }
            CoreWebView2.Navigate(new Uri(indexPath).AbsoluteUri);
        }

        private async void OnScriptDialogOpening(object sender, CoreWebView2ScriptDialogOpeningEventArgs args)
        {
            try
            {
                await AppContentDialog.Create("Message", args.Message, "Ok").ShowAsync();
            }
            catch
            {
                // Ignore
            }
        }

        public bool PostMessage(string name, object args)
        {
            try
            {
                var message = new JObject
                {
                    ["name"] = name,
                    ["args"] = args == null ? JValue.CreateNull() : JToken.FromObject(args, JsonSerializer.Create(Config.EditorJsonSerializerSettings))
                };
                CoreWebView2?.PostWebMessageAsString(message.ToString(Formatting.None));
                return true;
            }
            catch
            {
                return false;
            }
        }

        // ------------------------------------------------------------------ bridge

        /// <summary>
        /// Every renderer frame flows through <see cref="Transport.EmitWebViewMessage"/>, which
        /// implements the invoke/response protocol and raises Rx events for diffmsg frames.
        /// </summary>
        private void OnWebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            string raw;
            try
            {
                raw = e.TryGetWebMessageAsString();
            }
            catch
            {
                return;
            }

            try
            {
                Transport.EmitWebViewMessage(this, raw);
            }
            catch (Exception ex)
            {
                Log.Report("WebMessageDispatch", ex.ToString());
            }
        }

        private void OnNewWindowRequested(object sender, CoreWebView2NewWindowRequestedEventArgs args)
        {
            args.Handled = true;
            try
            {
                Core.Utilities.Common.OpenUrl(args.Uri);
            }
            catch
            {
                // Ignore
            }
        }

        private void OpenNewWindow(string uri)
        {
            try
            {
                Core.Utilities.Common.OpenUrl(uri);
            }
            catch
            {
                // Ignore
            }
        }

        public void Dispose()
        {
            try
            {
                webView = null;
            }
            catch
            {
                // Ignore
            }
            webView = null;
            CoreWebView2 = null;
            layoutRoot?.Children.Clear();
            disposables.Dispose();
        }

        public Rectangle GetDummyRectangle(Rect rect)
        {
            dummyRectangle.Width = rect.Width;
            dummyRectangle.Height = rect.Height;
            Canvas.SetLeft(dummyRectangle, rect.Left);
            Canvas.SetTop(dummyRectangle, rect.Top);
            return dummyRectangle;
        }

        public Rectangle MoveDummyRectangle(Point offset)
        {
            Canvas.SetLeft(dummyRectangle, Canvas.GetLeft(dummyRectangle) + offset.X);
            Canvas.SetTop(dummyRectangle, Canvas.GetTop(dummyRectangle) + offset.Y);
            return dummyRectangle;
        }
    }
}
