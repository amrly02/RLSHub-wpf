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

        public HomePage()
        {
            InitializeComponent();
            Loaded += HomePage_Loaded;
        }

        private void HomePage_Loaded(object sender, RoutedEventArgs e)
        {
            if (RendererCombo.Items.Count == 0)
            {
                RendererCombo.Items.Add("Vulkan");
                RendererCombo.Items.Add("DirectX");
                RendererCombo.SelectedIndex = 0;
            }
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

            var workDir = Path.GetDirectoryName(path);
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true,
                    WorkingDirectory = !string.IsNullOrEmpty(workDir) && Directory.Exists(workDir) ? workDir : null,
                    CreateNoWindow = false
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                return (false, "Could not start bridge: " + ex.Message);
            }

            const int bridgePort = 8766;
            const int maxWaitMs = 5000;
            const int stepMs = 200;
            for (var elapsed = 0; elapsed < maxWaitMs; elapsed += stepMs)
            {
                await Task.Delay(stepMs).ConfigureAwait(false);
                if (IsBridgeListening(bridgePort))
                    return (true, null);
            }

            return (false, "Bridge did not start listening on port " + bridgePort + " in time.");
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
