using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using RLSHub.Wpf.Services;
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
            Loaded += (_, _) =>
            {
                Dispatcher.BeginInvoke((Action)(() => UpdateNavIndicator(NavDashboard, animate: false)), System.Windows.Threading.DispatcherPriority.Loaded);
                StartFallingLogosWithRandomPositions();
                _ = CheckUpdatesForBadgeAsync();
                if (new FirstRunService().IsFirstRun())
                {
                    var firstRun = new FirstRunWindow { Owner = this };
                    firstRun.ShowDialog();
                }
            };
            Closed += (_, _) => BridgeScriptService.KillCurrentBridge();
        }

        private async System.Threading.Tasks.Task CheckUpdatesForBadgeAsync()
        {
            try
            {
                var prefs = new DashboardPreferencesStore().Load();
                if (!prefs.NotifyWhenUpdateAvailable) return;

                var appService = new UpdateCheckService(UpdateCheckService.AppRepo.Owner, UpdateCheckService.AppRepo.Repo);
                var modService = new UpdateCheckService("RLS-Modding", "rls_career_overhaul");
                var appCurrent = UpdateCheckService.GetCurrentAppVersion();
                var modCurrent = UpdateCheckService.GetInstalledModVersion().Version ?? new Version(0, 0, 0, 0);

                var appTask = appService.FetchLatestReleaseAsync(includePrereleases: true);
                var modTask = modService.FetchLatestReleaseAsync(includePrereleases: false);

                var appUpdate = false;
                var modUpdate = false;
                try
                {
                    var (tag, _, _) = await appTask.ConfigureAwait(false);
                    appUpdate = UpdateCheckService.IsUpdateAvailable(appCurrent, tag);
                }
                catch { /* ignore */ }
                try
                {
                    var (tag, _, _) = await modTask.ConfigureAwait(false);
                    modUpdate = UpdateCheckService.IsUpdateAvailable(modCurrent, tag);
                }
                catch { /* ignore */ }

                if (appUpdate || modUpdate)
                    Dispatcher.Invoke(() => UpdatesBadge.Visibility = Visibility.Visible);
            }
            catch { /* ignore */ }
        }

        private static readonly Random _random = new Random();

        private void StartFallingLogosWithRandomPositions()
        {
            if (FallingLogosCanvas?.Children == null) return;
            const double fallTo = 850;
            var logosPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Logos");

            foreach (UIElement child in FallingLogosCanvas.Children)
            {
                if (child is not FrameworkElement viewbox) continue;

                if (child is System.Windows.Controls.Image img && viewbox.Tag is string tag && tag.StartsWith("rls-logo-", StringComparison.OrdinalIgnoreCase))
                {
                    var sourcePath = Path.Combine(logosPath, tag + ".png");
                    if (File.Exists(sourcePath))
                    {
                        try
                        {
                            img.Source = new BitmapImage(new Uri(sourcePath, UriKind.Absolute));
                        }
                        catch { /* ignore load errors */ }
                    }
                }

                // Random starting height (0–700) so they don't all start in a line
                double startTop = _random.Next(0, 701);
                Canvas.SetTop(viewbox, startTop);

                double durationSeconds = 26 + _random.Next(0, 12);
                var firstFall = new DoubleAnimation(startTop, fallTo, TimeSpan.FromSeconds(durationSeconds));
                Storyboard.SetTarget(firstFall, viewbox);
                Storyboard.SetTargetProperty(firstFall, new System.Windows.PropertyPath("(Canvas.Top)"));

                var sb = new Storyboard();
                sb.Children.Add(firstFall);
                sb.Completed += (_, _) => StartRepeatFall(viewbox, fallTo);
                sb.Begin();
            }
        }

        private void StartRepeatFall(FrameworkElement viewbox, double fallTo)
        {
            // Random reset height above view (-350 to -50) so they reappear at different heights
            double resetTop = -50 - _random.Next(0, 301);
            Canvas.SetTop(viewbox, resetTop);

            // New random duration each loop so they stay out of sync
            double durationSeconds = 24 + _random.Next(0, 16);
            var repeatFall = new DoubleAnimation(resetTop, fallTo, TimeSpan.FromSeconds(durationSeconds))
            {
                // Stagger start (0–18 sec) so they don't all drop from top at once
                BeginTime = TimeSpan.FromSeconds(_random.Next(0, 19))
            };
            Storyboard.SetTarget(repeatFall, viewbox);
            Storyboard.SetTargetProperty(repeatFall, new System.Windows.PropertyPath("(Canvas.Top)"));
            var repeatSb = new Storyboard();
            repeatSb.Children.Add(repeatFall);
            repeatSb.Completed += (_, _) => StartRepeatFall(viewbox, fallTo);
            repeatSb.Begin();
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
