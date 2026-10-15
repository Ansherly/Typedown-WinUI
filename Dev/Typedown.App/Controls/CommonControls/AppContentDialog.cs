using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Typedown.Core.Utilities;
using Windows.Foundation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

namespace Typedown.Core.Controls
{
    public class AppContentDialog : ContentControl
    {
        public static DependencyProperty TitleTemplateProperty { get; } = DependencyProperty.Register(nameof(TitleTemplate), typeof(DataTemplate), typeof(AppContentDialog), null);
        public DataTemplate TitleTemplate { get => (DataTemplate)GetValue(TitleTemplateProperty); set => SetValue(TitleTemplateProperty, value); }

        public static DependencyProperty TitleProperty { get; } = DependencyProperty.Register(nameof(Title), typeof(object), typeof(AppContentDialog), null);
        public object Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

        public static DependencyProperty SecondaryButtonTextProperty { get; } = DependencyProperty.Register(nameof(SecondaryButtonText), typeof(string), typeof(AppContentDialog), null);
        public string SecondaryButtonText { get => (string)GetValue(SecondaryButtonTextProperty); set => SetValue(SecondaryButtonTextProperty, value); }

        public static DependencyProperty SecondaryButtonCommandParameterProperty { get; } = DependencyProperty.Register(nameof(SecondaryButtonCommandParameter), typeof(object), typeof(AppContentDialog), null);
        public object SecondaryButtonCommandParameter { get => GetValue(SecondaryButtonCommandParameterProperty); set => SetValue(SecondaryButtonCommandParameterProperty, value); }

        public static DependencyProperty SecondaryButtonCommandProperty { get; } = DependencyProperty.Register(nameof(SecondaryButtonCommand), typeof(System.Windows.Input.ICommand), typeof(AppContentDialog), null);
        public System.Windows.Input.ICommand SecondaryButtonCommand { get => (System.Windows.Input.ICommand)GetValue(SecondaryButtonCommandProperty); set => SetValue(SecondaryButtonCommandProperty, value); }

        public static DependencyProperty PrimaryButtonTextProperty { get; } = DependencyProperty.Register(nameof(PrimaryButtonText), typeof(string), typeof(AppContentDialog), null);
        public string PrimaryButtonText { get => (string)GetValue(PrimaryButtonTextProperty); set => SetValue(PrimaryButtonTextProperty, value); }

        public static DependencyProperty PrimaryButtonCommandParameterProperty { get; } = DependencyProperty.Register(nameof(PrimaryButtonCommandParameter), typeof(object), typeof(AppContentDialog), null);
        public object PrimaryButtonCommandParameter { get => GetValue(PrimaryButtonCommandParameterProperty); set => SetValue(PrimaryButtonCommandParameterProperty, value); }

        public static DependencyProperty PrimaryButtonCommandProperty { get; } = DependencyProperty.Register(nameof(PrimaryButtonCommand), typeof(System.Windows.Input.ICommand), typeof(AppContentDialog), null);
        public System.Windows.Input.ICommand PrimaryButtonCommand { get => (System.Windows.Input.ICommand)GetValue(PrimaryButtonCommandProperty); set => SetValue(PrimaryButtonCommandProperty, value); }

        public static DependencyProperty IsSecondaryButtonEnabledProperty { get; } = DependencyProperty.Register(nameof(IsSecondaryButtonEnabled), typeof(bool), typeof(AppContentDialog), new(true));
        public bool IsSecondaryButtonEnabled { get => (bool)GetValue(IsSecondaryButtonEnabledProperty); set => SetValue(IsSecondaryButtonEnabledProperty, value); }

        public static DependencyProperty IsPrimaryButtonEnabledProperty { get; } = DependencyProperty.Register(nameof(IsPrimaryButtonEnabled), typeof(bool), typeof(AppContentDialog), new(true));
        public bool IsPrimaryButtonEnabled { get => (bool)GetValue(IsPrimaryButtonEnabledProperty); set => SetValue(IsPrimaryButtonEnabledProperty, value); }

        public static DependencyProperty FullSizeDesiredProperty { get; } = DependencyProperty.Register(nameof(FullSizeDesired), typeof(bool), typeof(AppContentDialog), null);
        public bool FullSizeDesired { get => (bool)GetValue(FullSizeDesiredProperty); set => SetValue(FullSizeDesiredProperty, value); }

        public static DependencyProperty SecondaryButtonStyleProperty { get; } = DependencyProperty.Register(nameof(SecondaryButtonStyle), typeof(Style), typeof(AppContentDialog), null);
        public Style SecondaryButtonStyle { get => (Style)GetValue(SecondaryButtonStyleProperty); set => SetValue(SecondaryButtonStyleProperty, value); }

        public static DependencyProperty PrimaryButtonStyleProperty { get; } = DependencyProperty.Register(nameof(PrimaryButtonStyle), typeof(Style), typeof(AppContentDialog), null);
        public Style PrimaryButtonStyle { get => (Style)GetValue(PrimaryButtonStyleProperty); set => SetValue(PrimaryButtonStyleProperty, value); }

        public static DependencyProperty DefaultButtonProperty { get; } = DependencyProperty.Register(nameof(DefaultButton), typeof(ContentDialogButton), typeof(AppContentDialog), null);
        public ContentDialogButton DefaultButton { get => (ContentDialogButton)GetValue(DefaultButtonProperty); set => SetValue(DefaultButtonProperty, value); }

        public static DependencyProperty CloseButtonTextProperty { get; } = DependencyProperty.Register(nameof(CloseButtonText), typeof(string), typeof(AppContentDialog), null);
        public string CloseButtonText { get => (string)GetValue(CloseButtonTextProperty); set => SetValue(CloseButtonTextProperty, value); }

        public static DependencyProperty CloseButtonStyleProperty { get; } = DependencyProperty.Register(nameof(CloseButtonStyle), typeof(Style), typeof(AppContentDialog), null);
        public Style CloseButtonStyle { get => (Style)GetValue(CloseButtonStyleProperty); set => SetValue(CloseButtonStyleProperty, value); }

        public static DependencyProperty CloseButtonCommandParameterProperty { get; } = DependencyProperty.Register(nameof(CloseButtonCommandParameter), typeof(object), typeof(AppContentDialog), null);
        public object CloseButtonCommandParameter { get => GetValue(CloseButtonCommandParameterProperty); set => SetValue(CloseButtonCommandParameterProperty, value); }

        public static DependencyProperty CloseButtonCommandProperty { get; } = DependencyProperty.Register(nameof(CloseButtonCommand), typeof(System.Windows.Input.ICommand), typeof(AppContentDialog), null);
        public System.Windows.Input.ICommand CloseButtonCommand { get => (System.Windows.Input.ICommand)GetValue(CloseButtonCommandProperty); set => SetValue(CloseButtonCommandProperty, value); }

        public event TypedEventHandler<AppContentDialog, AppContentDialogClosedEventArgs> Closed;

        public event TypedEventHandler<AppContentDialog, AppContentDialogClosingEventArgs> Closing;

        public event TypedEventHandler<AppContentDialog, AppContentDialogOpenedEventArgs> Opened;

        public event TypedEventHandler<AppContentDialog, AppContentDialogButtonClickEventArgs> PrimaryButtonClick;

        public event TypedEventHandler<AppContentDialog, AppContentDialogButtonClickEventArgs> SecondaryButtonClick;

        public event TypedEventHandler<AppContentDialog, AppContentDialogButtonClickEventArgs> CloseButtonClick;

        public Button PrimaryButton => GetTemplateChild("PrimaryButton") as Button;

        public Button SecondaryButton => GetTemplateChild("SecondaryButton") as Button;

        public Button CloseButton => GetTemplateChild("CloseButton") as Button;

        public Grid LayoutRoot => GetTemplateChild("LayoutRoot") as Grid;

        private Rectangle SmokeLayerBackground => GetTemplateChild("SmokeLayerBackground") as Rectangle;

        private Border BackgroundElement => GetTemplateChild("BackgroundElement") as Border;

        private TaskCompletionSource<ContentDialogResult> result;

        private object prevFocusedElement;

        public AppContentDialog()
        {
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            AddHandler(KeyDownEvent, new KeyEventHandler(OnKeyDown), true);
        }

        private void SetButtonState()
        {
            var primaryVisible = !string.IsNullOrEmpty(PrimaryButtonText);
            var secondaryVisible = !string.IsNullOrEmpty(SecondaryButtonText);
            var closeVisible = !string.IsNullOrEmpty(CloseButtonText);
            if (primaryVisible && secondaryVisible && closeVisible)
                VisualStateManager.GoToState(this, "AllVisible", false);
            else if (primaryVisible && secondaryVisible)
                VisualStateManager.GoToState(this, "PrimaryAndSecondaryVisible", false);
            else if (primaryVisible && closeVisible)
                VisualStateManager.GoToState(this, "PrimaryAndCloseVisible", false);
            else if (secondaryVisible && closeVisible)
                VisualStateManager.GoToState(this, "SecondaryAndCloseVisible", false);
            else if (primaryVisible)
                VisualStateManager.GoToState(this, "PrimaryVisible", false);
            else if (secondaryVisible)
                VisualStateManager.GoToState(this, "SecondaryVisible", false);
            else if (closeVisible)
                VisualStateManager.GoToState(this, "CloseVisible", false);
            else
                VisualStateManager.GoToState(this, "NoneVisible", false);
            switch (DefaultButton)
            {
                case ContentDialogButton.Primary:
                    VisualStateManager.GoToState(this, "PrimaryAsDefaultButton", false);
                    break;
                case ContentDialogButton.Secondary:
                    VisualStateManager.GoToState(this, "SecondaryAsDefaultButton", false);
                    break;
                case ContentDialogButton.Close:
                    VisualStateManager.GoToState(this, "CloseAsDefaultButton", false);
                    break;
                default:
                    VisualStateManager.GoToState(this, "NoDefaultButton", false);
                    break;
            }
        }

        private async void SetFocusButton()
        {
            bool success = false;
            prevFocusedElement = FocusManager.GetFocusedElement(XamlRoot);
            for (int i = 0; i < 10 && !success; i++)
            {
                await Task.Delay(100);
                if (DefaultButton == ContentDialogButton.Primary && PrimaryButton.Visibility == Visibility.Visible)
                    success = PrimaryButton.Focus(FocusState.Programmatic);
                else if (DefaultButton == ContentDialogButton.Secondary && SecondaryButton.Visibility == Visibility.Visible)
                    success = SecondaryButton.Focus(FocusState.Programmatic);
                else if (DefaultButton == ContentDialogButton.Close && CloseButton.Visibility == Visibility.Visible)
                    success = CloseButton.Focus(FocusState.Programmatic);
                else if (PrimaryButton.Visibility == Visibility.Visible)
                    success = PrimaryButton.Focus(FocusState.Programmatic);
                else if (SecondaryButton.Visibility == Visibility.Visible)
                    success = SecondaryButton.Focus(FocusState.Programmatic);
                else if (CloseButton.Visibility == Visibility.Visible)
                    success = CloseButton.Focus(FocusState.Programmatic);
                else
                    success = true;
            }
        }

        public void SetShadow()
        {
            var sharedShadow = new ThemeShadow();
            BackgroundElement.Shadow = sharedShadow;
            sharedShadow.Receivers.Add(SmokeLayerBackground);
            BackgroundElement.Translation = new Vector3(0, 0, 128);
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            SetButtonState();
            SetFocusButton();
            SetShadow();
            VisualStateManager.GoToState(this, "DialogShowing", true);
            PrimaryButton.Click += OnPrimaryButtonClick;
            SecondaryButton.Click += OnSecondaryButtonClick;
            CloseButton.Click += OnCloseButtonClick;
            Opened?.Invoke(this, new());
            FocusManager.GettingFocus += OnFocusManagerGettingFocus;
        }

        private async void OnUnloaded(object sender, RoutedEventArgs e)
        {
            VisualStateManager.GoToState(this, "DialogHidden", true);
            PrimaryButton.Click -= OnPrimaryButtonClick;
            SecondaryButton.Click -= OnSecondaryButtonClick;
            CloseButton.Click -= OnCloseButtonClick;
            Closed?.Invoke(this, new(result.Task.Result));
            FocusManager.GettingFocus -= OnFocusManagerGettingFocus;
            if (prevFocusedElement is FrameworkElement ele && ele.IsLoaded)
                await FocusManager.TryFocusAsync(ele, FocusState.Programmatic);
        }

        private void OnFocusManagerGettingFocus(object sender, GettingFocusEventArgs e)
        {
            if (e.NewFocusedElement is FrameworkElement ele)
            {
                if (ele != this && ele.GetAncestor<AppContentDialog>() == null)
                {
                    if (e.Direction == FocusNavigationDirection.Next)
                        e.TrySetNewFocusedElement(FocusManager.FindFirstFocusableElement(this));
                    else if (e.Direction == FocusNavigationDirection.Previous)
                        e.TrySetNewFocusedElement(FocusManager.FindLastFocusableElement(this));
                }
            }
        }

        private void OnPrimaryButtonClick(object sender, RoutedEventArgs e)
        {
            var clickEventArgs = new AppContentDialogButtonClickEventArgs();
            PrimaryButtonClick?.Invoke(this, clickEventArgs);
            if (clickEventArgs.Cancel)
                return;
            SetResult(ContentDialogResult.Primary);
        }

        private void OnSecondaryButtonClick(object sender, RoutedEventArgs e)
        {
            var clickEventArgs = new AppContentDialogButtonClickEventArgs();
            SecondaryButtonClick?.Invoke(this, clickEventArgs);
            if (clickEventArgs.Cancel)
                return;
            SetResult(ContentDialogResult.Secondary);
        }

        private void OnCloseButtonClick(object sender, RoutedEventArgs e)
        {
            var clickEventArgs = new AppContentDialogButtonClickEventArgs();
            CloseButtonClick?.Invoke(this, clickEventArgs);
            if (clickEventArgs.Cancel)
                return;
            SetResult(ContentDialogResult.None);
        }

        private void OnKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (IsLoaded && CloseButton.Visibility == Visibility.Visible && e.Key == Windows.System.VirtualKey.Escape)
                OnCloseButtonClick(this, null);
        }

        private void SetResult(ContentDialogResult result)
        {
            this.result.SetResult(result);
            var closingEventArgs = new AppContentDialogClosingEventArgs(result);
            Closing?.Invoke(this, closingEventArgs);
            if (closingEventArgs.Cancel)
                return;
            Hide();
        }

        public void Hide()
        {
            if (hostPanel != null && IsLoaded)
                hostPanel.Children.Remove(this);
        }

        /// <summary>
        /// The dialog is hosted by hand: it is added as a child of a panel that fills the window.
        /// In the WPF shell that panel was <c>XamlRoot.Content</c> itself, but the WinUI 3 shell
        /// puts a UserControl (<c>RootControl</c>) there, so look one level down for the panel
        /// instead of throwing <see cref="InvalidOperationException"/> for every dialog.
        /// </summary>
        private static Panel GetHostPanel(XamlRoot xamlRoot)
        {
            if (xamlRoot?.Content is Panel panel)
                return panel;

            return FindFirstPanel(xamlRoot?.Content as DependencyObject);
        }

        /// <summary>
        /// A Grid host (the app's root grid) is split into a caption row and a content row. A child
        /// without attached properties lands in the first cell, which would grow the caption's Auto
        /// row and push the whole UI down instead of overlaying it — so span the dialog over every
        /// row and column of the host.
        /// </summary>
        private void StretchAcrossHost(Panel host)
        {
            if (host is not Grid hostGrid)
                return;

            Grid.SetRow(this, 0);
            Grid.SetRowSpan(this, Math.Max(1, hostGrid.RowDefinitions.Count));
            Grid.SetColumn(this, 0);
            Grid.SetColumnSpan(this, Math.Max(1, hostGrid.ColumnDefinitions.Count));
        }

        private static Panel FindFirstPanel(DependencyObject root)
        {
            if (root == null)
                return null;

            var count = VisualTreeHelper.GetChildrenCount(root);
            for (var i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is Panel panel)
                    return panel;

                if (FindFirstPanel(child) is Panel nested)
                    return nested;
            }

            return null;
        }

        /// <summary>Panel this dialog was added to, so <see cref="Hide"/> removes it from the same place.</summary>
        private Panel hostPanel;

        private readonly ConditionalWeakTable<XamlRoot, SemaphoreSlim> showSemaphores = new();

        public async Task<ContentDialogResult> ShowAsync()
        {
            if (IsLoaded)
                throw new InvalidOperationException();

            var grid = GetHostPanel(XamlRoot);
            if (grid == null)
                throw new InvalidOperationException();
            if (!showSemaphores.TryGetValue(XamlRoot, out var semaphore))
                showSemaphores.Add(XamlRoot, semaphore = new(1));
            await semaphore.WaitAsync();
            try
            {
                result = new();
                hostPanel = grid;
                StretchAcrossHost(grid);
                grid.Children.Add(this);
                return await result.Task;
            }
            finally
            {
                semaphore.Release();
            }
        }

        public async Task<ContentDialogResult> ShowAsync(XamlRoot xamlRoot)
        {
            XamlRoot = xamlRoot;
            return await ShowAsync();
        }

        public static AppContentDialog Create()
        {
            return new AppContentDialog();
        }

        public static AppContentDialog Create(object content, string closeButtonText)
        {
            var dialog = Create();
            dialog.Content = content;
            dialog.CloseButtonText = closeButtonText;
            dialog.DefaultButton = ContentDialogButton.Close;
            return dialog;
        }

        public static AppContentDialog Create(string title, object content, string closeButtonText)
        {
            var dialog = Create(content, closeButtonText);
            dialog.Title = title;
            return dialog;
        }

        public static AppContentDialog Create(string title, object content, string closeButtonText, string primaryButtonText)
        {
            var dialog = Create(title, content, closeButtonText);
            dialog.PrimaryButtonText = primaryButtonText;
            dialog.DefaultButton = ContentDialogButton.Primary;
            return dialog;
        }

        public static AppContentDialog Create(string title, object content, string closeButtonText, string primaryButtonText, string secondaryButtonText)
        {
            var dialog = Create(title, content, closeButtonText, primaryButtonText);
            dialog.SecondaryButtonText = secondaryButtonText;
            dialog.DefaultButton = ContentDialogButton.Primary;
            return dialog;
        }
    }

    public class AppContentDialogClosedEventArgs
    {
        public ContentDialogResult Result { get; }

        public AppContentDialogClosedEventArgs(ContentDialogResult result)
        {
            Result = result;
        }
    }

    public class AppContentDialogClosingEventArgs
    {
        public bool Cancel { get; set; } = false;

        public ContentDialogResult Result { get; }

        public AppContentDialogClosingEventArgs(ContentDialogResult result)
        {
            Result = result;
        }
    }

    public class AppContentDialogOpenedEventArgs
    {
    }

    public class AppContentDialogButtonClickEventArgs
    {
        public bool Cancel { get; set; } = false;
    }
}
