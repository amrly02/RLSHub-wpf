using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using RLSHub.Wpf.Services;

namespace RLSHub.Wpf.Views
{
    public partial class HomePage : UserControl
    {
        private readonly BridgeScriptService _bridgeService = new();
        private readonly DashboardPreferencesStore _dashboardPrefs = new();
        private bool _ignoreCheckChanges;
        private System.Windows.Threading.DispatcherTimer? _launchStatusTimer;

        public HomePage()
        {
            InitializeComponent();
            Loaded += HomePage_Loaded;
            Unloaded += HomePage_Unloaded;
        }

        private void HomePage_Unloaded(object sender, RoutedEventArgs e)
        {
            _launchStatusTimer?.Stop();
            _launchStatusTimer = null;
        }

        private void HomePage_Loaded(object sender, RoutedEventArgs e)
        {
            if (RendererCombo.Items.Count == 0)
            {
                RendererCombo.Items.Add("Vulkan");
                RendererCombo.Items.Add("DirectX");
            }

            var prefs = _dashboardPrefs.Load();
            _ignoreCheckChanges = true;
            try
            {
                RendererCombo.SelectedIndex = Math.Clamp(prefs.RendererIndex, 0, 1);
                ConsoleCheckBox.IsChecked = prefs.EnableConsole;
                AutoBridgeCheckBox.IsChecked = prefs.AutoRunBridge;
            }
            finally
            {
                _ignoreCheckChanges = false;
            }

            RendererCombo.SelectionChanged += DashboardSelection_Changed;
            ConsoleCheckBox.Checked += DashboardCheck_Changed;
            ConsoleCheckBox.Unchecked += DashboardCheck_Changed;
            AutoBridgeCheckBox.Checked += DashboardCheck_Changed;
            AutoBridgeCheckBox.Unchecked += DashboardCheck_Changed;

            RefreshLaunchStatus();
            _launchStatusTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(8),
                IsEnabled = true
            };
            _launchStatusTimer.Tick += (_, _) => RefreshLaunchStatus();
            _launchStatusTimer.Start();
        }

        private void DashboardSelection_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_ignoreCheckChanges || RendererCombo.SelectedIndex < 0) return;
            SaveDashboardPreferences();
        }

        private void DashboardCheck_Changed(object sender, RoutedEventArgs e)
        {
            if (_ignoreCheckChanges) return;
            SaveDashboardPreferences();
        }

        private void SaveDashboardPreferences()
        {
            var p = _dashboardPrefs.Load();
            _dashboardPrefs.Save(p with
            {
                EnableConsole = ConsoleCheckBox.IsChecked == true,
                AutoRunBridge = AutoBridgeCheckBox.IsChecked == true,
                RendererIndex = Math.Clamp(RendererCombo.SelectedIndex, 0, 1)
            });
        }

        private void SaveLastLaunchUtc()
        {
            var p = _dashboardPrefs.Load();
            _dashboardPrefs.Save(p with { LastLaunchUtc = DateTime.UtcNow });
        }

        private void RefreshLaunchStatus()
        {
            var running = IsBeamNgRunning();
            BeamNgStatusText.Text = running ? "BeamNG: Running" : "BeamNG: Not running";
            if (BeamNgStatusDot != null)
                BeamNgStatusDot.Fill = (System.Windows.Media.Brush)FindResource(running ? "BridgeRunningBrush" : "BridgeStoppedBrush");
            var prefs = _dashboardPrefs.Load();
            if (prefs.LastLaunchUtc.HasValue)
            {
                var ago = DateTime.UtcNow - prefs.LastLaunchUtc.Value;
                LastLaunchText.Text = ago.TotalMinutes < 1 ? "Last launched: just now"
                    : ago.TotalMinutes < 60 ? $"Last launched: {(int)ago.TotalMinutes} min ago"
                    : ago.TotalHours < 24 ? $"Last launched: {(int)ago.TotalHours} hr ago"
                    : $"Last launched: {(int)ago.TotalDays} day(s) ago";
                LastLaunchText.Visibility = Visibility.Visible;
            }
            else
                LastLaunchText.Visibility = Visibility.Collapsed;
        }

        private static bool IsBeamNgRunning()
        {
            try
            {
                return System.Diagnostics.Process.GetProcessesByName("BeamNG.drive.x64").Length > 0;
            }
            catch { return false; }
        }

        private async void LaunchButton_Click(object sender, RoutedEventArgs e)
        {
            if (!BeamNgConfigService.TryLoad(out var config) || config == null)
            {
                MessageBox.Show("Ensure BeamNG is installed and the ini file exists in your LocalAppData folder.", "BeamNG.drive.ini not found", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var exePath = Path.Combine(config.InstallPath, "Bin64", "BeamNG.drive.x64.exe");
            if (!File.Exists(exePath))
            {
                MessageBox.Show($"Could not find BeamNG.drive.exe in {config.InstallPath}", "BeamNG executable not found", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (LaunchButton is Button btn)
                btn.IsEnabled = false;

            try
            {
                if (AutoBridgeCheckBox.IsChecked == true)
                {
                    var (bridgeOk, bridgeError) = await RunBridgeBeforeLaunchAsync().ConfigureAwait(true);
                    if (!bridgeOk)
                    {
                        MessageBox.Show(bridgeError ?? "CarSwap bridge could not be started. Launch aborted.", "Bridge before launch", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }

                var rendererArg = RendererCombo.SelectedIndex == 1 ? "-gfx dx11" : "-gfx vk";
                var args = rendererArg;
                if (ConsoleCheckBox.IsChecked == true)
                    args += " -console";
                Process.Start(new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = args,
                    WorkingDirectory = Directory.Exists(config.InstallPath) ? config.InstallPath : null,
                    UseShellExecute = true
                });
                SaveLastLaunchUtc();
                RefreshLaunchStatus();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Launch failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (LaunchButton is Button b)
                    b.IsEnabled = true;
            }
        }

        /// <summary>
        /// Updates bridge from package, runs it, and verifies it is listening before returning.
        /// Returns (true, null) on success, (false, errorMessage) on failure.
        /// </summary>
        private async Task<(bool Success, string? Error)> RunBridgeBeforeLaunchAsync()
        {
            try
            {
                await _bridgeService.UpdateFromPackageAsync().ConfigureAwait(false);
            }
            catch (FileNotFoundException ex)
            {
                return (false, ex.Message + "\n\nRun the app from its install folder so Assets\\Bridge\\bridge.exe is present.");
            }
            catch (Exception ex)
            {
                return (false, "Bridge update failed: " + ex.Message);
            }

            var path = _bridgeService.GetLocalBridgePath();
            if (!File.Exists(path))
                return (false, "Bridge executable not found at: " + path);

            try
            {
                var psi = BridgeScriptService.CreateBridgeProcessStartInfo(path);
                var process = Process.Start(psi);
                if (process == null)
                    return (false, "Could not start bridge process.");
                BridgeScriptService.RegisterBridgeProcess(process);

                const int bridgePort = 8766;
                const int maxWaitMs = 15000;
                const int stepMs = 250;
                for (var elapsed = 0; elapsed < maxWaitMs; elapsed += stepMs)
                {
                    await Task.Delay(stepMs).ConfigureAwait(false);
                    if (IsBridgeListening(bridgePort))
                    {
                        _ = DiscardBridgeOutputAsync(process);
                        return (true, null);
                    }
                    if (process.HasExited)
                        break;
                }

                var errDetail = "";
                try
                {
                    if (process.HasExited)
                        errDetail = process.StandardError.ReadToEnd().Trim();
                }
                catch { /* ignore */ }
                if (!string.IsNullOrEmpty(errDetail))
                    errDetail = " Bridge output: " + errDetail;
                return (false, "Bridge did not start listening on port " + bridgePort + " in time." + errDetail);
            }
            catch (Exception ex)
            {
                return (false, "Could not start bridge: " + ex.Message);
            }
        }

        /// <summary>Reads and discards bridge stdout/stderr so the process does not block on full pipes.</summary>
        private static async Task DiscardBridgeOutputAsync(Process process)
        {
            var buffer = new char[256];
            void Drain(StreamReader reader)
            {
                try
                {
                    while (!process.HasExited && reader.Read(buffer, 0, buffer.Length) > 0) { }
                }
                catch { /* ignore */ }
            }
            await Task.WhenAll(
                Task.Run(() => Drain(process.StandardOutput)),
                Task.Run(() => Drain(process.StandardError))
            ).ConfigureAwait(false);
        }

        private static bool IsBridgeListening(int port)
        {
            try
            {
                using var client = new TcpClient();
                var result = client.BeginConnect("127.0.0.1", port, null, null);
                if (result.AsyncWaitHandle.WaitOne(500))
                {
                    client.EndConnect(result);
                    return true;
                }
            }
            catch { }
            return false;
        }

        private void OpenUserFolderButton_Click(object sender, RoutedEventArgs e)
        {
            if (!BeamNgConfigService.TryLoad(out var config) || config == null)
            {
                MessageBox.Show("Ensure BeamNG is installed and the ini file exists in your LocalAppData folder.", "BeamNG.drive.ini not found", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!Directory.Exists(config.UserFolder))
            {
                MessageBox.Show(config.UserFolder, "User folder not found", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            Process.Start(new ProcessStartInfo { FileName = "explorer.exe", Arguments = config.UserFolder, UseShellExecute = true });
        }
    }
}
