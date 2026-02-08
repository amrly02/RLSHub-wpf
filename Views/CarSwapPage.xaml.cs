using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using RLSHub.Wpf.Models;
using RLSHub.Wpf.Services;

namespace RLSHub.Wpf.Views
{
    public partial class CarSwapPage : UserControl
    {
        private readonly CarSwapService _carSwapService = new();
        private readonly BridgeScriptService _bridgeService = new();
        private readonly ObservableCollection<CarSwapListing> _listings = new();

        public CarSwapPage()
        {
            InitializeComponent();
            ListingsItems.ItemsSource = _listings;
            Loaded += CarSwapPage_Loaded;
        }

        private async void CarSwapPage_Loaded(object sender, RoutedEventArgs e)
        {
            await RefreshListingsAsync();
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshListingsAsync();

        private async Task RefreshListingsAsync()
        {
            SetStatus("Loading listings...");
            _listings.Clear();
            var (listings, error) = await _carSwapService.GetListingsAsync();
            if (!string.IsNullOrWhiteSpace(error))
            {
                SetStatus(error);
                return;
            }
            foreach (var listing in listings)
                _listings.Add(listing);
            SetStatus(_listings.Count == 0 ? "No listings available." : $"{_listings.Count} listings available.");
        }

        private void SetStatus(string text) => StatusText.Text = text;

        private async void RunBridge_Click(object sender, RoutedEventArgs e)
        {
            var (path, error) = await _bridgeService.EnsureBridgeScriptAsync();
            if (string.IsNullOrWhiteSpace(path))
            {
                MessageBox.Show(error ?? "Unable to locate the bridge executable.", "Bridge unavailable", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!File.Exists(path))
            {
                try
                {
                    await _bridgeService.UpdateFromPackageAsync();
                    path = _bridgeService.GetLocalBridgePath();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Bridge not found. " + ex.Message + "\n\nEnsure the app is run from its install folder so Assets\\Bridge\\bridge.exe is present.", "Run bridge", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                if (!File.Exists(path))
                {
                    MessageBox.Show("The bridge executable could not be copied to:\n" + path, "Bridge missing", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }
            try
            {
                var workDir = Path.GetDirectoryName(path);
                var psi = new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true,
                    WorkingDirectory = !string.IsNullOrEmpty(workDir) && Directory.Exists(workDir) ? workDir : null
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Run bridge failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void UpdateBridge_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await _bridgeService.UpdateFromPackageAsync();
                MessageBox.Show("Bridge updated.", "Update bridge", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (FileNotFoundException ex)
            {
                MessageBox.Show(ex.Message + "\n\nRun the app from its install folder so Assets\\Bridge\\bridge.exe is present.", "Update bridge", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Update bridge failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
