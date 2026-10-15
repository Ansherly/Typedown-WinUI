using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using System;
using Typedown.Core.ViewModels;

namespace Typedown.Utilities
{
    /// <summary>
    /// Theme payload handed to the web editor. Named <c>ThemeHelper</c> (not <c>Common</c>)
    /// to avoid colliding with <see cref="Typedown.Core.Utilities.Common"/>.
    /// </summary>
    public static class ThemeHelper
    {
        /// <summary>
        /// Builds the <c>{ theme, accentColor, background }</c> object the editor's
        /// <c>services/theme.ts</c> expects.
        /// </summary>
        public static object GetCurrentTheme(this IServiceProvider provider)
        {
            var ui = provider.GetService<UIViewModel>();
            var settings = provider.GetService<SettingsViewModel>();
            var isDarkMode = ui?.ActualTheme == ElementTheme.Dark;

            byte r = 0, g = 120, b = 212, a = 255;
            try
            {
                var uiSettings = new Windows.UI.ViewManagement.UISettings();
                var accent = uiSettings.GetColorValue(Windows.UI.ViewManagement.UIColorType.Accent);
                r = accent.R; g = accent.G; b = accent.B; a = accent.A;
            }
            catch
            {
                // Fall back to the default accent colour.
            }

            var accentColor = new { r, g, b, a };
            var useTransparent = (settings?.UseMicaEffect ?? false) && (settings?.UseEditorMicaEffect ?? false);
            var background = useTransparent
                ? new { R = 0, G = 0, B = 0, A = 0 }
                : isDarkMode
                    ? new { R = 0x28, G = 0x28, B = 0x28, A = 0xFF }
                    : new { R = 0xF9, G = 0xF9, B = 0xF9, A = 0xFF };

            return new { theme = isDarkMode ? "Dark" : "Light", accentColor, background };
        }

        public static bool GetUseLightTheme() =>
            Application.Current.RequestedTheme == ApplicationTheme.Light;
    }
}
