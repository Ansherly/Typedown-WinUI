using Microsoft.UI.Xaml;
using Microsoft.Web.WebView2.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Typedown
{
    public sealed partial class MainWindow : Window
    {
        private static readonly string LogPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Typedown", "startup.log");

        private CoreWebView2 webView;

        public MainWindow()
        {
            InitializeComponent();
            Title = "Typedown";
            RootGrid.Loaded += OnLoaded;
        }

        private static void Log(string message)
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

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            await InitializeEditorAsync();
        }

        // ---------------------------------------------------------------- host bridge
        //
        // Wire protocol (matches Dev\Typedown.Editor\src\services\transport.ts):
        //   editor -> host : {"type":"invoke","id":"invoke_N","name":...,"args":...}
        //                    {"type":"diffmsg","name":...,"args":...}
        //   host  -> editor: {"name":"<invoke id>","args":{"code":0,"data":<payload>}}
        //                    {"name":"<event>","args":<payload>}

        private void OnWebMessageReceived(CoreWebView2 sender, CoreWebView2WebMessageReceivedEventArgs args)
        {
            string raw;
            try
            {
                raw = args.TryGetWebMessageAsString();
            }
            catch (Exception ex)
            {
                Log("webmsg read failed: " + ex.Message);
                return;
            }

            JObject message;
            try
            {
                message = JObject.Parse(raw);
            }
            catch
            {
                return;
            }

            var type = (string)message["type"];
            var name = (string)message["name"];

            if (type == "diffmsg")
            {
                Log($"event {name}");
                return;
            }

            if (type != "invoke")
            {
                return;
            }

            var id = (string)message["id"];
            var payload = message["args"];

            try
            {
                object data = name switch
                {
                    "GetCurrentTheme" => BuildTheme(),
                    "GetSettings" => BuildSettings(),
                    "GetStringResources" => BuildStringResources(payload),
                    "SetClipboard" => true,
                    "ExportCallback" => true,
                    "PrintHTML" => true,
                    "ResizeTable" => JObject.FromObject(new { row = 0, column = 0 }),
                    "LoadImage" => JObject.FromObject(new { url = (string)payload?["url"] }),
                    _ => null
                };

                PostResponse(id, 0, data);
                Log($"invoke {name} -> ok");
            }
            catch (Exception ex)
            {
                PostResponse(id, 1, null, ex.Message);
                Log($"invoke {name} -> error {ex.Message}");
            }
        }

        private void PostResponse(string id, int code, object data, string msg = null)
        {
            var response = new JObject
            {
                ["name"] = id,
                ["args"] = new JObject
                {
                    ["code"] = code,
                    ["data"] = data == null ? JValue.CreateNull() : JToken.FromObject(data),
                    ["msg"] = msg
                }
            };
            webView?.PostWebMessageAsString(response.ToString(Formatting.None));
        }

        private void PostEvent(string name, object args)
        {
            var message = new JObject
            {
                ["name"] = name,
                ["args"] = args == null ? JValue.CreateNull() : JToken.FromObject(args)
            };
            webView?.PostWebMessageAsString(message.ToString(Formatting.None));
        }

        private static object BuildTheme() => new
        {
            theme = "light",
            accentColor = new { r = 0, g = 120, b = 212, a = 255 },
            background = new { R = 255, G = 255, B = 255, A = 255 }
        };

        private static object BuildSettings() => new
        {
            markdown = "# Welcome to Typedown\n\nNative **ARM64** build (WinUI 3).\n",
            basePath = "",
            // Muya editor defaults; the full option set is provided once Typedown.Core ports over.
            autoSave = false,
            editorFontFamily = "Consolas",
            editorFontSize = 16,
            codeFontFamily = "Consolas",
            codeFontSize = 14,
            lineHeight = 1.6,
            wrapCodeBlocks = true,
            trimUnnecessaryCodeBlockEmptyLines = true,
            preferLooseListItem = true,
            bulletListMarker = "-",
            orderListDelimiter = ".",
            tabSize = 4,
            listIndentation = 1,
            frontmatterType = "-",
            sequenceTheme = "hand",
            mermaidTheme = "default",
            vegaTheme = "latimes",
            hideQuickInsertHint = false,
            hideLinkPopup = false,
            autoCheck = false,
            spellcheckEnabled = false,
            spellcheckHideSuggestion = false
        };

        private static object BuildStringResources(JToken payload)
        {
            var result = new JObject();
            if (payload?["names"] is JArray names)
            {
                foreach (var entry in names)
                {
                    result[(string)entry] = (string)entry;
                }
            }
            return result;
        }

        // ---------------------------------------------------------------- startup

        private async Task InitializeEditorAsync()
        {
            Log($"START arch={System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture} base={AppContext.BaseDirectory}");

            try
            {
                var userDataFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Typedown", "WebView2");
                Directory.CreateDirectory(userDataFolder);

                var environment = await CoreWebView2Environment.CreateWithOptionsAsync(null, userDataFolder, null);
                await EditorView.EnsureCoreWebView2Async(environment);

                webView = EditorView.CoreWebView2;
                Log("CoreWebView2 ready");

                var settings = webView.Settings;
                settings.AreBrowserAcceleratorKeysEnabled = false;
                settings.AreDefaultContextMenusEnabled = false;
                settings.AreDevToolsEnabled = false;
                settings.IsStatusBarEnabled = false;
                settings.IsZoomControlEnabled = false;

                webView.NavigationCompleted += (s, a) =>
                    Log($"NavigationCompleted success={a.IsSuccess}");
                webView.WebMessageReceived += (s, a) => OnWebMessageReceived(s, a);

                var indexPath = Path.Combine(AppContext.BaseDirectory, "Resources", "Statics", "index.html");
                if (!File.Exists(indexPath))
                {
                    Log("FAIL index.html missing: " + indexPath);
                    StatusText.Text = "Editor assets missing";
                    return;
                }

                EditorView.Source = new Uri(indexPath);
                StatusText.Text = "Native ARM64 build - editor loading";

                // Capture renderer-side JS failures for diagnosis.
                const string installErrorHook = @"
(function(){
  if (window.__hookInstalled) return 'already';
  window.__hookInstalled = true;
  window.__jsErrors = [];
  window.addEventListener('error', function(e){
    window.__jsErrors.push('ERROR: ' + (e.message || '') + ' @ ' + (e.filename || '') + ':' + (e.lineno || ''));
  });
  window.addEventListener('unhandledrejection', function(e){
    window.__jsErrors.push('REJECT: ' + (e.reason && e.reason.message ? e.reason.message : String(e.reason)));
  });
  return 'installed';
})();";
                EditorView.CoreWebView2.NavigationCompleted += async (s, a) =>
                {
                    try
                    {
                        var hookResult = await webView.ExecuteScriptAsync(installErrorHook);
                        Log("error hook: " + hookResult);
                    }
                    catch (Exception ex) { Log("hook failed: " + ex.Message); }
                };

                await Task.Delay(12000);

                const string probe = @"
(function(){
  var r = document.getElementById('root');
  var out = {
    rootChildren: r ? r.childElementCount : -1,
    rootW: r ? r.clientWidth : -1,
    rootH: r ? r.clientHeight : -1,
    rootHtml: r ? r.innerHTML.substring(0, 300) : 'no root',
    hasEditorDiv: !!document.getElementById('editor'),
    errors: (window.__jsErrors || []).slice(0, 5)
  };
  document.title = JSON.stringify(out);
  return JSON.stringify(out);
})();";
                var result = await webView.ExecuteScriptAsync(probe);
                Log("PROBE " + result);
                StatusText.Text = "Native ARM64 build - editor ready";
            }
            catch (Exception ex)
            {
                Log("EXCEPTION " + ex.GetType().Name + ": " + ex.Message);
                StatusText.Text = "Editor init failed: " + ex.Message;
            }
        }
    }
}
