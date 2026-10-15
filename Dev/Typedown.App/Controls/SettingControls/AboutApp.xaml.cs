using System;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Typedown.Core.Utilities;
using Windows.ApplicationModel;

namespace Typedown.Core.Controls
{
    public sealed partial class AboutApp : UserControl
    {
        /// <summary>
        /// Third-party components the app ships with or links against, mirroring NOTICE.md.
        /// Kept in code rather than in the .resw files: names and licence identifiers are proper
        /// nouns, so the list stays in step with NOTICE.md instead of being duplicated (and
        /// inevitably drifting) across the 75 localisations.
        /// </summary>
        private static readonly (string Name, string License)[] OpenSourceComponents =
        {
            ("Typedown (upstream, byxiaozhi)", "MIT · Copyright (c) 2022 ZZF"),
            ("Windows App SDK / WinUI 3", "MIT · Microsoft"),
            ("Microsoft WebView2 Runtime", "Microsoft proprietary (redistributable)"),
            (".NET Runtime", "MIT · .NET Foundation"),
            ("Muya (marktext)", "MIT · Markdown engine"),
            ("React / React DOM", "MIT · Meta"),
            ("CodeMirror", "MIT · Marijn Haverbeke"),
            ("KaTeX", "MIT · Khan Academy"),
            ("Mermaid", "MIT · Knut Sveidqvist"),
            ("Newtonsoft.Json", "MIT · James Newton-King"),
            ("System.Reactive (Rx.NET)", "MIT · .NET Foundation"),
            ("Microsoft.Extensions.DependencyInjection", "MIT · .NET Foundation"),
            ("Microsoft.EntityFrameworkCore.Sqlite", "MIT · .NET Foundation"),
            ("PropertyChanged.Fody", "MIT · Fody project"),
        };

        public AboutApp()
        {
            InitializeComponent();
        }

        public static string GetAppVersion()
        {
            if (Config.IsPackaged)
            {
                var version = Package.Current.Id.Version;
                return string.Format("{0}.{1}.{2}.{3}", version.Major, version.Minor, version.Build, version.Revision);
            }
            else
            {
                var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                return string.Format("{0}.{1}.{2}.{3}", version.Major, version.Minor, version.Build, version.Revision) + " (Unpackaged)";
            }
        }

        private async void OpenSourceSoftwareButton_Click(object sender, RoutedEventArgs e)
        {
            var content = new ScrollViewer
            {
                MaxHeight = 480,
                HorizontalScrollMode = ScrollMode.Disabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = BuildOpenSourceList(),
            };

            await AppContentDialog.Create(
                Locale.GetString("About.BasedOnOpenSourceSoftware"),
                content,
                Locale.GetDialogString("Ok")).ShowAsync(XamlRoot);
        }

        /// <summary>
        /// Two even columns, filled top-to-bottom: 14 entries in one column would make the dialog
        /// taller than the window and push the close button out of reach.
        /// </summary>
        private static Grid BuildOpenSourceList()
        {
            var grid = new Grid { ColumnSpacing = 16, RowSpacing = 10 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var rows = (OpenSourceComponents.Length + 1) / 2;
            for (var i = 0; i < rows; i++)
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            for (var i = 0; i < OpenSourceComponents.Length; i++)
            {
                var (name, license) = OpenSourceComponents[i];

                var item = new StackPanel { Spacing = 1 };
                item.Children.Add(new TextBlock
                {
                    Text = name,
                    FontWeight = FontWeights.SemiBold,
                    TextWrapping = TextWrapping.Wrap,
                });
                item.Children.Add(new TextBlock
                {
                    Text = license,
                    Opacity = 0.65,
                    FontSize = 12,
                    TextWrapping = TextWrapping.Wrap,
                });

                Grid.SetColumn(item, i / rows);
                Grid.SetRow(item, i % rows);
                grid.Children.Add(item);
            }

            return grid;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Bindings?.StopTracking();
        }
    }
}
