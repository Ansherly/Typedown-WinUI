using System.IO;
using System.Threading.Tasks;
using Typedown.Core.Models;

namespace Typedown.Core.Interfaces
{
    public interface IFileConverter
    {
        Task<MemoryStream> HtmlToPdf(string html, PdfPrintSettings settings = null);

        /// <summary>
        /// Shows the print dialog for the given HTML (WinUI 3 uses WebView2's own print UI
        /// instead of the x64-only PdfiumViewer print path).
        /// </summary>
        Task PrintHtml(string html, string documentName = null);
    }
}
