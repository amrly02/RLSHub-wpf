using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using RLSHub.Wpf.Models;
using RLSHub.Wpf.Services;

namespace RLSHub.Wpf.Views
{
    public partial class CarSwapPage : UserControl
    {
        private const int SortPriceAsc = 0;
        private const int SortPriceDesc = 1;
        private const int SortMileageAsc = 2;
        private const int SortMileageDesc = 3;
        private const int SortModelAsc = 4;
        private const int SortDateDesc = 5;
        private const int SortDateAsc = 6;
        private const int ShowAll = 0;
        private const int ShowFavoritesOnly = 1;
        private const int BridgePort = 8766;

        private readonly CarSwapService _carSwapService = new();
        private readonly BridgeScriptService _bridgeService = new();
        private readonly WatchlistStore _watchlistStore = new();
        private readonly List<CarSwapListing> _allListings = new();
        private readonly ObservableCollection<CarSwapDisplayItem> _displayItems = new();
        private HashSet<string> _watchlistIds = new(StringComparer.OrdinalIgnoreCase);
        private Process? _bridgeProcess;
        private readonly StringBuilder _bridgeOutput = new();
        private DispatcherTimer? _bridgeStatusTimer;
        private bool _bridgeOutputExpanded;

        public CarSwapPage()
        {
            InitializeComponent();
            ListingsItems.ItemsSource = _displayItems;
            Loaded += CarSwapPage_Loaded;
            Unloaded += CarSwapPage_Unloaded;
        }

        private void CarSwapPage_Unloaded(object sender, RoutedEventArgs e)
        {
            _bridgeStatusTimer?.Stop();
            _bridgeStatusTimer = null;
        }

        private async void CarSwapPage_Loaded(object sender, RoutedEventArgs e)
        {
            _watchlistIds = _watchlistStore.Load();
            if (SortCombo.Items.Count == 0)
            {
                SortCombo.Items.Add("Price (low)");
                SortCombo.Items.Add("Price (high)");
                SortCombo.Items.Add("Mileage (low)");
                SortCombo.Items.Add("Mileage (high)");
                SortCombo.Items.Add("Model (A–Z)");
                SortCombo.Items.Add("Date (newest)");
                SortCombo.Items.Add("Date (oldest)");
                SortCombo.SelectedIndex = 0;
            }
            if (ShowFilterCombo.Items.Count == 0)
            {
                ShowFilterCombo.Items.Add("All");
                ShowFilterCombo.Items.Add("Favorites");
                ShowFilterCombo.SelectedIndex = 0;
            }
            await RefreshListingsAsync();
            UpdateBridgeStatusIndicator();
            _bridgeStatusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.5) };
            _bridgeStatusTimer.Tick += BridgeStatusTimer_Tick;
            _bridgeStatusTimer.Start();
        }

        private void BridgeStatusTimer_Tick(object? sender, EventArgs e)
        {
            if (!IsLoaded) return;
            _ = Task.Run(() => IsBridgeListening(BridgePort))
                .ContinueWith(t => Dispatcher.BeginInvoke(() =>
                {
                    if (IsLoaded) UpdateBridgeStatusIndicator(t.Result);
                }), TaskScheduler.Default);
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshListingsAsync();

        private void UpdateBridgeStatusIndicator()
        {
            UpdateBridgeStatusIndicator(IsBridgeListening(BridgePort));
        }

        private void UpdateBridgeStatusIndicator(bool running)
        {
            if (!IsLoaded || BridgeStatusText == null) return;
            if (RunStopBridgeButton != null)
            {
                RunStopBridgeButton.Content = running ? "Stop bridge" : "Run bridge";
            }
            var runningBrush = (Brush)FindResource("BridgeRunningBrush");
            var stoppedBrush = (Brush)FindResource("BridgeStoppedBrush");
            if (BridgeStatusDot != null)
                BridgeStatusDot.Fill = running ? runningBrush : stoppedBrush;
            if (running)
            {
                BridgeStatusText.Text = "Bridge running";
                BridgeStatusText.Foreground = runningBrush;
                var output = _bridgeOutput.ToString().Trim();
                if (output.Length > 0)
                    output = string.Join(" ", output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries));
                BridgeOutputText.Text = output;
                BridgeOutputText.Visibility = _bridgeOutputExpanded && output.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
                BridgeExpandButton.Visibility = Visibility.Visible;
                BridgeExpandButton.Content = _bridgeOutputExpanded ? "\uE70E" : "\uE70D"; // ChevronUp : ChevronDown
                BridgeExpandButton.ToolTip = _bridgeOutputExpanded ? "Hide bridge output" : "Show bridge output";
            }
            else
            {
                if (_bridgeProcess != null && _bridgeProcess.HasExited)
                {
                    _bridgeProcess = null;
                    _bridgeOutput.Clear();
                }
                BridgeStatusText.Text = "Bridge: not running";
                BridgeStatusText.Foreground = stoppedBrush;
                BridgeOutputText.Visibility = Visibility.Collapsed;
                BridgeExpandButton.Visibility = Visibility.Collapsed;
            }
        }

        private void BridgeExpandButton_Click(object sender, RoutedEventArgs e)
        {
            _bridgeOutputExpanded = !_bridgeOutputExpanded;
            UpdateBridgeStatusIndicator();
        }

        private static bool IsBridgeListening(int port)
        {
            try
            {
                using var client = new TcpClient();
                var result = client.BeginConnect("127.0.0.1", port, null, null);
                if (result.AsyncWaitHandle.WaitOne(300))
                {
                    client.EndConnect(result);
                    return true;
                }
            }
            catch { }
            return false;
        }

        private async Task RefreshListingsAsync()
        {
            SetStatus("Loading listings...");
            _allListings.Clear();
            var (listings, error) = await _carSwapService.GetListingsAsync();
            if (!string.IsNullOrWhiteSpace(error))
            {
                SetStatus(error);
                return;
            }
            foreach (var listing in listings)
                _allListings.Add(listing);
            _watchlistIds = _watchlistStore.Load();
            RefreshFilteredListings();
            SetStatus(_allListings.Count == 0 ? "No listings available." : $"{_allListings.Count} listings. Showing {_displayItems.Count}.");
        }

        private void SearchOrFilter_Changed(object sender, RoutedEventArgs e) => RefreshFilteredListings();

        private void RefreshFilteredListings()
        {
            var search = (SearchBox?.Text ?? "").Trim();
            var priceMin = TryParseInt(PriceMinBox?.Text, out var pMin) ? pMin : (int?)null;
            var priceMax = TryParseInt(PriceMaxBox?.Text, out var pMax) ? pMax : (int?)null;
            var showFavoritesOnly = ShowFilterCombo?.SelectedIndex == ShowFavoritesOnly;
            var sortIndex = SortCombo?.SelectedIndex ?? 0;

            var query = _allListings.AsEnumerable();

            if (showFavoritesOnly)
                query = query.Where(l => _watchlistIds.Contains(l.Id));

            if (!string.IsNullOrEmpty(search))
            {
                var s = search.ToLowerInvariant();
                query = query.Where(l =>
                    (l.Title?.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (l.Description?.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (l.VehicleModel?.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (l.SellerName?.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false));
            }

            if (priceMin.HasValue)
                query = query.Where(l => l.Price >= priceMin.Value);
            if (priceMax.HasValue)
                query = query.Where(l => l.Price <= priceMax.Value);

            query = sortIndex switch
            {
                SortPriceAsc => query.OrderBy(l => l.Price),
                SortPriceDesc => query.OrderByDescending(l => l.Price),
                SortMileageAsc => query.OrderBy(l => l.Mileage),
                SortMileageDesc => query.OrderByDescending(l => l.Mileage),
                SortModelAsc => query.OrderBy(l => l.VehicleModel, StringComparer.OrdinalIgnoreCase),
                SortDateDesc => query.OrderByDescending(l => l.CreatedAtUtc ?? DateTime.MinValue),
                SortDateAsc => query.OrderBy(l => l.CreatedAtUtc ?? DateTime.MinValue),
                _ => query.OrderBy(l => l.Price)
            };

            var list = query.ToList();
            _displayItems.Clear();
            foreach (var listing in list)
                _displayItems.Add(new CarSwapDisplayItem(listing, _watchlistIds.Contains(listing.Id)));
            if (StatusText != null && _allListings.Count > 0)
                StatusText.Text = $"{_allListings.Count} listings. Showing {_displayItems.Count}.";
        }

        private static bool TryParseInt(string? text, out int value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(text)) return false;
            return int.TryParse(text.Trim(), out value);
        }

        private void ToggleWatchlist_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not CarSwapDisplayItem item) return;
            var id = item.Listing.Id;
            if (string.IsNullOrEmpty(id)) return;
            if (_watchlistIds.Contains(id))
                _watchlistIds.Remove(id);
            else
                _watchlistIds.Add(id);
            _watchlistStore.Save(_watchlistIds);
            item.IsFavorite = _watchlistIds.Contains(id);
        }

        private void SetStatus(string text) => StatusText.Text = text;

        private async void RunStopBridge_Click(object sender, RoutedEventArgs e)
        {
            if (IsBridgeListening(BridgePort))
            {
                StopBridge();
                return;
            }
            await StartBridgeAsync();
        }

        private void StopBridge()
        {
            BridgeScriptService.KillCurrentBridge();
            _bridgeProcess = null;
            _bridgeOutput.Clear();
            UpdateBridgeStatusIndicator();
        }

        private async Task StartBridgeAsync()
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
                var psi = BridgeScriptService.CreateBridgeProcessStartInfo(path);
                _bridgeProcess = Process.Start(psi);
                if (_bridgeProcess == null)
                {
                    MessageBox.Show("Could not start the bridge process.", "Run bridge", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                BridgeScriptService.RegisterBridgeProcess(_bridgeProcess);
                _bridgeOutput.Clear();
                _ = ReadBridgeOutputAsync(_bridgeProcess);
                UpdateBridgeStatusIndicator();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Run bridge failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task ReadBridgeOutputAsync(Process process)
        {
            var buffer = new char[256];
            void ReadStream(StreamReader reader)
            {
                try
                {
                    int count;
                    while (!process.HasExited && (count = reader.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        var chunk = new string(buffer, 0, count);
                        Dispatcher.Invoke(() =>
                        {
                            _bridgeOutput.Append(chunk);
                            if (_bridgeOutput.Length > 2000)
                                _bridgeOutput.Remove(0, _bridgeOutput.Length - 1500);
                            UpdateBridgeStatusIndicator();
                        });
                    }
                }
                catch { }
            }
            await Task.WhenAll(
                Task.Run(() => ReadStream(process.StandardOutput)),
                Task.Run(() => ReadStream(process.StandardError))
            ).ConfigureAwait(false);
            Dispatcher.Invoke(UpdateBridgeStatusIndicator);
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
