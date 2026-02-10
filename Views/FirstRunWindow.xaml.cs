using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using RLSHub.Wpf.Services;

namespace RLSHub.Wpf.Views
{
    public partial class FirstRunWindow : Window
    {
        private readonly FirstRunService _firstRun = new();
        private readonly BridgeScriptService _bridgeService = new();

        public FirstRunWindow()
        {
            InitializeComponent();
            Loaded += FirstRunWindow_Loaded;
        }

        private void FirstRunWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (BeamNgConfigService.TryLoad(out var config) && config != null)
            {
                var exePath = Path.Combine(config.InstallPath, "Bin64", "BeamNG.drive.x64.exe");
                if (File.Exists(exePath))
                    BeamNgStatusText.Text = $"BeamNG: Found at {config.InstallPath}";
                else
                    BeamNgStatusText.Text = $"BeamNG install path found but executable not at Bin64\\BeamNG.drive.x64.exe.";
            }
            else
                BeamNgStatusText.Text = "BeamNG: Not detected. Install BeamNG and run it once so the config is created (see BeamNG.drive.ini in LocalAppData).";
        }

        private void SetUpLater_Click(object sender, RoutedEventArgs e)
        {
            _firstRun.CompleteFirstRun();
            Close();
        }

        private async void RunBridge_Click(object sender, RoutedEventArgs e)
        {
            if (RunBridgeButton is Button btn) btn.IsEnabled = false;
            try
            {
                var (path, error) = await _bridgeService.EnsureBridgeScriptAsync();
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                {
                    try
                    {
                        await _bridgeService.UpdateFromPackageAsync();
                        path = _bridgeService.GetLocalBridgePath();
                    }
                    catch { }
                }
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    var workDir = Path.GetDirectoryName(path);
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = path,
                        UseShellExecute = true,
                        WorkingDirectory = !string.IsNullOrEmpty(workDir) && Directory.Exists(workDir) ? workDir : null
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message ?? "Could not start bridge.", "Bridge", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                if (RunBridgeButton is Button b) b.IsEnabled = true;
            }
            _firstRun.CompleteFirstRun();
            Close();
        }
    }
}
