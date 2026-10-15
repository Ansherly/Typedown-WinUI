using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage.Pickers;

namespace Typedown.Core.Utilities
{
    /// <summary>
    /// Owner-window plumbing for the WinRT pickers.
    ///
    /// In a WinUI 3 desktop app these pickers are not parented to anything until an
    /// <c>IInitializeWithWindow</c> implementation is supplied; without it the call throws.
    /// The previous hand-rolled <c>[ComImport]</c> interface silently failed the cast
    /// (<c>picker as object as IInitializeWithWindow</c> returned null, raising
    /// NullReferenceException), which made Open / Open Recent / Open Folder / Save / Save As /
    /// Import / Export all inert. We now use the C#/WinRT helper shipped in the Windows SDK
    /// projection, whose signature is <c>Initialize(object target, IntPtr hwnd)</c>.
    /// </summary>
    public static class FilePickersExtensions
    {
        /// <summary>
        /// Parents a picker to the given window. Throws a descriptive exception when the
        /// handle is unavailable, so a missing owner is diagnosable instead of a silent no-op.
        /// </summary>
        private static void SetOwnerWindowCore(object picker, nint hWnd)
        {
            if (picker == null)
                throw new ArgumentNullException(nameof(picker));

            if (hWnd == default)
                throw new InvalidOperationException(
                    "No owner window handle is available for the file picker. " +
                    "AppViewModel.MainWindow must be set from MainWindow once the XAML root is loaded.");

            WinRT.Interop.InitializeWithWindow.Initialize(picker, hWnd);
        }

        public static void SetOwnerWindow(this FileOpenPicker picker, nint hWnd)
            => SetOwnerWindowCore(picker, hWnd);

        public static void SetOwnerWindow(this FileSavePicker picker, nint hWnd)
            => SetOwnerWindowCore(picker, hWnd);

        public static void SetOwnerWindow(this FolderPicker picker, nint hWnd)
            => SetOwnerWindowCore(picker, hWnd);

        private static void TrySetOwnerWindow(object picker, nint hWnd, string kind)
        {
            try
            {
                SetOwnerWindowCore(picker, hWnd);
            }
            catch (Exception ex)
            {
                App.Log($"picker owner FAILED: {kind} hwnd=0x{hWnd:X} :: {ex}");
                throw;
            }
        }

        public static async Task<string> PickMarkdownFolderAsync(this nint window)
        {
            var folderPicker = new FolderPicker();
            TrySetOwnerWindow(folderPicker, window, "FolderPicker");
            folderPicker.FileTypeFilter.Add("*");
            var folder = await folderPicker.PickSingleFolderAsync();
            return folder?.Path;
        }

        public static async Task<string> PickMarkdownFileAsync(this nint window)
        {
            var filePicker = new FileOpenPicker();
            foreach (var extension in FileTypeHelper.Markdown)
                filePicker.FileTypeFilter.Add(extension);
            TrySetOwnerWindow(filePicker, window, "FileOpenPicker(markdown)");
            var file = await filePicker.PickSingleFileAsync();
            return file?.Path;
        }

        public static async Task<string> PickImageFileAsync(this nint window)
        {
            var filePicker = new FileOpenPicker();
            foreach (var extension in FileTypeHelper.Image)
                filePicker.FileTypeFilter.Add(extension);
            TrySetOwnerWindow(filePicker, window, "FileOpenPicker(image)");
            var file = await filePicker.PickSingleFileAsync();
            return file?.Path;
        }
    }
}
