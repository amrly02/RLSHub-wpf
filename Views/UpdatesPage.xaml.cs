using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using RLSHub.Wpf.Services;

namespace RLSHub.Wpf.Views
{
    public partial class UpdatesPage : UserControl
    {
        private const string ModReleasesUrl = "https://github.com/RLS-Modding/rls_career_overhaul/releases";
        private static string AppReleasesUrl => $"https://github.com/{UpdateCheckService.AppRepo.Owner}/{UpdateCheckService.AppRepo.Repo}/releases";

        private readonly UpdateCheckService _modUpdateService = new("RLS-Modding", "rls_career_overhaul");
        private readonly UpdateCheckService _appUpdateService = new(UpdateCheckService.AppRepo.Owner, UpdateCheckService.AppRepo.Repo);
        private readonly DashboardPreferencesStore _prefsStore = new();

        public UpdatesPage()
        {
            InitializeComponent();
            Loaded += UpdatesPage_Loaded;
        }

        private void UpdatesPage_Loaded(object sender, RoutedEventArgs e)
        {
            var appVer = UpdateCheckService.GetCurrentAppVersion();
            AppVersionText.Text = $"Installed: v{UpdateCheckService.FormatVersionDisplay(appVer)}";
            _ = RefreshInstalledPreReleaseLabelAsync(appVer);

            var (modVersion, modVersionString) = UpdateCheckService.GetInstalledModVersion();
            if (modVersion != null)
                ModVersionText.Text = $"Installed: v{modVersionString ?? modVersion.ToString()}";
            else if (string.IsNullOrEmpty(UpdateCheckService.GetExpectedModPathsForDisplay().ModsFolder))
                ModVersionText.Text = "Mod not detected. BeamNG user folder not found.";
            else
                ModVersionText.Text = "RLS Career Overhaul not detected.";

            var prefs = _prefsStore.Load();
            NotifyWhenUpdateCheckBox.IsChecked = prefs.NotifyWhenUpdateAvailable;
        }

        private async void CheckUpdatesButton_Click(object sender, RoutedEventArgs e)
        {
            if (CheckUpdatesButton is not Button btn) return;
            btn.IsEnabled = false;
            AppUpdateStatusText.Visibility = Visibility.Collapsed;
            ModUpdateStatusText.Visibility = Visibility.Collapsed;
            try
            {
                var appCurrent = UpdateCheckService.GetCurrentAppVersion();
                var modInstalled = UpdateCheckService.GetInstalledModVersion().Version ?? new Version(0, 0, 0, 0);

                Version? appLatest = null;
                string? appUrl = null;
                var appLatestIsPrerelease = false;
                try
                {
                    var (tag, htmlUrl, isPrerelease) = await _appUpdateService.FetchLatestReleaseAsync(includePrereleases: true).ConfigureAwait(true);
                    appLatest = tag;
                    appUrl = htmlUrl;
                    appLatestIsPrerelease = isPrerelease;
                }
                catch (Exception ex)
                {
                    AppUpdateStatusText.Text = "Could not check: " + (ex.Message ?? "Unknown error");
                    AppUpdateStatusText.Visibility = Visibility.Visible;
                }

                Version? modLatest = null;
                string? modUrl = null;
                try
                {
                    var (tag, htmlUrl, _) = await _modUpdateService.FetchLatestReleaseAsync(includePrereleases: false).ConfigureAwait(true);
                    modLatest = tag;
                    modUrl = htmlUrl;
                }
                catch (Exception ex)
                {
                    ModUpdateStatusText.Text = "Could not check: " + (ex.Message ?? "Unknown error");
                    ModUpdateStatusText.Visibility = Visibility.Visible;
                }

                if (appLatest != null)
                {
                    var appVerDisplay = UpdateCheckService.FormatVersionDisplay(appLatest);
                    var appUpdateAvailable = UpdateCheckService.IsUpdateAvailable(appCurrent, appLatest);
                    if (appUpdateAvailable)
                    {
                        AppUpdateStatusText.Text = appLatestIsPrerelease
                            ? $"New pre-release v{appVerDisplay} available."
                            : $"New version v{appVerDisplay} available.";
                    }
                    else
                    {
                        AppUpdateStatusText.Text = "App is up to date.";
                        if (appLatestIsPrerelease && appCurrent.CompareTo(appLatest) == 0)
                            AppVersionText.Text = $"Installed: v{UpdateCheckService.FormatVersionDisplay(appCurrent)} (Pre-release)";
                    }
                    AppUpdateStatusText.Visibility = Visibility.Visible;
                    if (appUpdateAvailable && appUrl != null)
                    {
                        var preNote = appLatestIsPrerelease ? " (pre-release)" : "";
                        var open = MessageBox.Show(
                            $"A new RLSHub version (v{appVerDisplay}) is available{preNote}.\n\nOpen the releases page?",
                            appLatestIsPrerelease ? "App pre-release available" : "App update available",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Information);
                        if (open == MessageBoxResult.Yes)
                            Process.Start(new ProcessStartInfo(appUrl) { UseShellExecute = true });
                    }
                }

                if (modLatest != null)
                {
                    var modUpdateAvailable = UpdateCheckService.IsUpdateAvailable(modInstalled, modLatest);
                    ModUpdateStatusText.Text = modUpdateAvailable
                        ? $"New version v{modLatest} available."
                        : "Mod is up to date.";
                    ModUpdateStatusText.Visibility = Visibility.Visible;
                    if (modUpdateAvailable && modUrl != null)
                    {
                        var open = MessageBox.Show(
                            $"A new mod version (v{modLatest}) is available.\n\nOpen the releases page?",
                            "Mod update available",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Information);
                        if (open == MessageBoxResult.Yes)
                            Process.Start(new ProcessStartInfo(modUrl) { UseShellExecute = true });
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                MessageBox.Show("Network error: " + (ex.Message ?? "Unable to reach GitHub."), "Check for Updates", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message ?? "An error occurred.", "Check for Updates", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                btn.IsEnabled = true;
            }
        }

        private async System.Threading.Tasks.Task RefreshInstalledPreReleaseLabelAsync(Version appCurrent)
        {
            try
            {
                var (tag, _, isPrerelease) = await _appUpdateService.FetchLatestReleaseAsync(includePrereleases: true).ConfigureAwait(false);
                if (isPrerelease && appCurrent.CompareTo(tag) == 0)
                    Dispatcher.Invoke(() => AppVersionText.Text = $"Installed: v{UpdateCheckService.FormatVersionDisplay(appCurrent)} (Pre-release)");
            }
            catch { /* ignore */ }
        }

        private void NotifyCheck_Changed(object sender, RoutedEventArgs e)
        {
            var p = _prefsStore.Load();
            _prefsStore.Save(p with { NotifyWhenUpdateAvailable = NotifyWhenUpdateCheckBox.IsChecked == true });
        }

        private void ViewAppReleasesButton_Click(object sender, RoutedEventArgs e)
            => Process.Start(new ProcessStartInfo(AppReleasesUrl) { UseShellExecute = true });

        private void ViewModReleasesButton_Click(object sender, RoutedEventArgs e)
            => Process.Start(new ProcessStartInfo(ModReleasesUrl) { UseShellExecute = true });
    }
}
