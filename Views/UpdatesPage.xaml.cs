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
        private const string ReleasesUrl = "https://github.com/RLS-Modding/rls_career_overhaul/releases";
        private readonly UpdateCheckService _updateCheckService = new();

        public UpdatesPage()
        {
            InitializeComponent();
            Loaded += UpdatesPage_Loaded;
        }

        private void UpdatesPage_Loaded(object sender, RoutedEventArgs e)
        {
            var (modVersion, modVersionString) = UpdateCheckService.GetInstalledModVersion();
            if (modVersion != null)
            {
                CurrentVersionText.Text = $"Installed mod version: v{modVersionString ?? modVersion.ToString()}";
                return;
            }
            var (modsFolder, _) = UpdateCheckService.GetExpectedModPathsForDisplay();
            if (string.IsNullOrEmpty(modsFolder))
            {
                CurrentVersionText.Text = "RLS Career Overhaul not detected. BeamNG user folder not found (check BeamNG.drive.ini).";
                return;
            }
            CurrentVersionText.Text = "RLS Career Overhaul not detected.";
        }

        private async void CheckUpdatesButton_Click(object sender, RoutedEventArgs e)
        {
            if (CheckUpdatesButton is not Button btn) return;
            btn.IsEnabled = false;
            try
            {
                var installedVersion = UpdateCheckService.GetInstalledModVersion().Version ?? new Version(0, 0, 0, 0);
                var (tagVersion, htmlUrl) = await _updateCheckService.FetchLatestReleaseAsync().ConfigureAwait(true);
                var updateAvailable = UpdateCheckService.IsUpdateAvailable(installedVersion, tagVersion);
                if (updateAvailable)
                {
                    var result = MessageBox.Show(
                        $"A new version (v{tagVersion}) is available.\n\nOpen the releases page to download?",
                        "Update available",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Information);
                    if (result == MessageBoxResult.Yes)
                        Process.Start(new ProcessStartInfo(htmlUrl) { UseShellExecute = true });
                }
                else
                {
                    MessageBox.Show("You're up to date.", "Check for Updates", MessageBoxButton.OK, MessageBoxImage.Information);
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

        private void ViewPatchNotesButton_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo(ReleasesUrl) { UseShellExecute = true });
        }
    }
}
