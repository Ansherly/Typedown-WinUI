using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using System;
using System.IO;
using System.Threading.Tasks;
using Typedown.Core.Interfaces;
using Typedown.Core.Models;
using Typedown.Core.Utilities;

namespace Typedown.Services
{
    /// <summary>
    /// HTML to PDF / print conversion built on an offscreen WinUI 3 <see cref="WebView2"/>.
    /// Replaces the XAML-Islands <c>WebViewController</c> based implementation.
    /// Printing uses WebView2's own print UI, so no third-party PDF native library is needed
    /// (the previous PdfiumViewer path was x64-only and could not run on ARM64).
    /// </summary>
    public class FileConverter : IFileConverter
    {
        private static async Task<WebView2> CreateOffscreenWebViewAsync()
        {
            var userDataFolder = Path.Combine(Core.Config.GetLocalFolderPath(), "WebView2");
            Directory.CreateDirectory(userDataFolder);

            var environment = await CoreWebView2Environment.CreateWithOptionsAsync(null, userDataFolder, null);
            var webView = new WebView2();
            await webView.EnsureCoreWebView2Async(environment);
            return webView;
        }

        private static async Task<bool> LoadHtmlAsync(WebView2 webView, string html)
        {
            var tmpFile = Core.Utilities.Common.GetTempFileName(".html");
            try
            {
                await File.WriteAllTextAsync(tmpFile, html);
                var loaded = new TaskCompletionSource<bool>();
                void Handler(object s, CoreWebView2NavigationCompletedEventArgs e)
                {
                    webView.CoreWebView2.NavigationCompleted -= Handler;
                    loaded.TrySetResult(e.IsSuccess);
                }
                webView.CoreWebView2.NavigationCompleted += Handler;
                webView.CoreWebView2.Navigate(new Uri(tmpFile).AbsoluteUri);
                return await loaded.Task;
            }
            finally
            {
                try { File.Delete(tmpFile); } catch { /* Ignore */ }
            }
        }

        public async Task<MemoryStream> HtmlToPdf(string html, CoreWebView2PrintSettings settings = null)
        {
            var webView = await CreateOffscreenWebViewAsync();
            try
            {
                if (!await LoadHtmlAsync(webView, html))
                    throw new InvalidOperationException("Failed to load HTML for PDF export.");

                var tmpPdf = Path.GetTempFileName();
                try
                {
                    await webView.CoreWebView2.PrintToPdfAsync(tmpPdf, settings);
                    return new MemoryStream(await File.ReadAllBytesAsync(tmpPdf));
                }
                finally
                {
                    try { File.Delete(tmpPdf); } catch { /* Ignore */ }
                }
            }
            finally
            {
                webView.Close();
            }
        }

        public async Task<MemoryStream> HtmlToPdf(string html, PdfPrintSettings settings = null)
        {
            settings ??= new PdfPrintSettings();

            var webView = await CreateOffscreenWebViewAsync();
            try
            {
                var printSettings = webView.CoreWebView2.Environment.CreatePrintSettings();
                printSettings.Orientation = (CoreWebView2PrintOrientation)settings.Orientation;
                if (settings.PageSize.HasValue)
                {
                    printSettings.PageWidth = settings.PageSize.Value.Width;
                    printSettings.PageHeight = settings.PageSize.Value.Height;
                }
                if (settings.Margin.HasValue)
                {
                    printSettings.MarginLeft = settings.Margin.Value.Left;
                    printSettings.MarginTop = settings.Margin.Value.Top;
                    printSettings.MarginRight = settings.Margin.Value.Right;
                    printSettings.MarginBottom = settings.Margin.Value.Bottom;
                }
                printSettings.ShouldPrintHeaderAndFooter = settings.ShouldPrintHeaderAndFooter;
                printSettings.HeaderTitle = settings.Header;
                printSettings.FooterUri = settings.Footer;

                if (!await LoadHtmlAsync(webView, html))
                    throw new InvalidOperationException("Failed to load HTML for PDF export.");

                var tmpPdf = Path.GetTempFileName();
                try
                {
                    await webView.CoreWebView2.PrintToPdfAsync(tmpPdf, printSettings);
                    return new MemoryStream(await File.ReadAllBytesAsync(tmpPdf));
                }
                finally
                {
                    try { File.Delete(tmpPdf); } catch { /* Ignore */ }
                }
            }
            finally
            {
                webView.Close();
            }
        }

        /// <summary>
        /// Shows the WebView2 print dialog for the given HTML.
        /// </summary>
        public async Task PrintHtml(string html, string documentName = null)
        {
            var webView = await CreateOffscreenWebViewAsync();
            try
            {
                if (!await LoadHtmlAsync(webView, html))
                    throw new InvalidOperationException("Failed to load HTML for printing.");
                webView.CoreWebView2.ShowPrintUI(CoreWebView2PrintDialogKind.System);
            }
            catch
            {
                webView.Close();
                throw;
            }
        }
    }
}
