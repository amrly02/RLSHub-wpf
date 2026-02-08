using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Animation;
using RLSHub.Wpf.Views;

namespace RLSHub.Wpf
{
    public partial class MainWindow : Window
    {
        private static readonly TimeSpan TransitionDuration = TimeSpan.FromMilliseconds(220);
        private static readonly TimeSpan UnderlineTransitionDuration = TimeSpan.FromMilliseconds(200);
        private readonly ToggleButton[] _navButtons;
        private ToggleButton? _pendingIndicatorButton;

        public MainWindow()
        {
            InitializeComponent();
            _navButtons = new[] { NavDashboard, NavCarSwap, NavUpdates, NavSettings, NavAbout };
            SetNavSelection(NavDashboard);
            UpdateHeader("dashboard");
            NavigateTo(new HomePage(), animate: false);
            Loaded += (_, _) => Dispatcher.BeginInvoke((Action)(() => UpdateNavIndicator(NavDashboard, animate: false)), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private void Nav_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not ToggleButton btn || btn.Tag is not string tag)
                return;
            SetNavSelection(btn);
            _pendingIndicatorButton = btn;
            UpdateHeader(tag);
            UserControl? page = tag switch
            {
                "dashboard" => new HomePage(),
                "carswap" => new CarSwapPage(),
                "updates" => new UpdatesPage(),
                "settings" => new SettingsPage(),
                "about" => new AboutPage(),
                _ => null
            };
            if (page != null)
                NavigateTo(page, animate: true);
        }

        private void SetNavSelection(ToggleButton selected)
        {
            foreach (var b in _navButtons)
                b.IsChecked = b == selected;
        }

        private void UpdateNavIndicator(ToggleButton selectedButton, bool animate)
        {
            if (NavIndicatorStrip == null || NavIndicator == null || NavBarBorder == null)
                return;

            // Force layout so ActualWidth and positions are up to date
            NavBarBorder.UpdateLayout();

            // Use nav bar as common reference so coordinates are consistent after content changes
            var buttonInBar = selectedButton.TranslatePoint(new Point(0, 0), NavBarBorder);
            var stripInBar = NavIndicatorStrip.TranslatePoint(new Point(0, 0), NavBarBorder);
            double left = buttonInBar.X - stripInBar.X;
            double width = selectedButton.ActualWidth;

            if (width <= 0)
                return;

            if (animate)
            {
                var currentMargin = NavIndicator.Margin;
                var currentWidth = NavIndicator.Width;
                if (double.IsNaN(currentWidth) || currentWidth <= 0)
                    currentWidth = width;

                var marginAnim = new ThicknessAnimation(
                    currentMargin,
                    new Thickness(left, 0, 0, 0),
                    UnderlineTransitionDuration)
                {
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };
                var widthAnim = new DoubleAnimation(
                    currentWidth,
                    width,
                    UnderlineTransitionDuration)
                {
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };

                Storyboard.SetTarget(marginAnim, NavIndicator);
                Storyboard.SetTargetProperty(marginAnim, new System.Windows.PropertyPath(Border.MarginProperty));
                Storyboard.SetTarget(widthAnim, NavIndicator);
                Storyboard.SetTargetProperty(widthAnim, new System.Windows.PropertyPath(FrameworkElement.WidthProperty));

                var sb = new Storyboard();
                sb.Children.Add(marginAnim);
                sb.Children.Add(widthAnim);
                sb.Begin();
            }
            else
            {
                NavIndicator.Margin = new Thickness(left, 0, 0, 0);
                NavIndicator.Width = width;
            }
        }

        private void NavigateTo(UserControl page, bool animate)
        {
            if (animate)
            {
                page.Opacity = 0;
                page.RenderTransform = new System.Windows.Media.TranslateTransform(24, 0);
                page.RenderTransformOrigin = new Point(0, 0);
                page.Loaded += OnPageLoaded;
            }
            ContentFrame.Content = page;
        }

        private void OnPageLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement page)
                return;
            page.Loaded -= OnPageLoaded;

            // Update sliding underline after new content has laid out (fixes shift/glitch)
            if (_pendingIndicatorButton != null)
            {
                var btn = _pendingIndicatorButton;
                _pendingIndicatorButton = null;
                Dispatcher.BeginInvoke((Action)(() => UpdateNavIndicator(btn, animate: true)), System.Windows.Threading.DispatcherPriority.Loaded);
            }

            var opacityAnim = new DoubleAnimation(0, 1, TransitionDuration)
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            var translateAnim = new DoubleAnimation(24, 0, TransitionDuration)
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };

            Storyboard.SetTarget(opacityAnim, page);
            Storyboard.SetTargetProperty(opacityAnim, new System.Windows.PropertyPath(UIElement.OpacityProperty));
            Storyboard.SetTarget(translateAnim, page);
            Storyboard.SetTargetProperty(translateAnim, new System.Windows.PropertyPath("(UIElement.RenderTransform).(TranslateTransform.X)"));

            var sb = new Storyboard();
            sb.Children.Add(opacityAnim);
            sb.Children.Add(translateAnim);
            sb.Begin();
        }

        private void UpdateHeader(string? tag)
        {
            switch (tag)
            {
                case "carswap":
                    HeaderTitle.Text = "CarSwap";
                    HeaderSubtitle.Text = "Live marketplace listings and bridge tools.";
                    break;
                case "updates":
                    HeaderTitle.Text = "Updates";
                    HeaderSubtitle.Text = "Release tracking and patch notes.";
                    break;
                case "settings":
                    HeaderTitle.Text = "Settings Manager";
                    HeaderSubtitle.Text = "Tune the overhaul to your playstyle.";
                    break;
                case "about":
                    HeaderTitle.Text = "About RLS Hub";
                    HeaderSubtitle.Text = "Companion app for RLS Career Overhaul.";
                    break;
                default:
                    HeaderTitle.Text = "Dashboard";
                    HeaderSubtitle.Text = "RLS Career Overhaul command center.";
                    break;
            }
        }
    }
}
