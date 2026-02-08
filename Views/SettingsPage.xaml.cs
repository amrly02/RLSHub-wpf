using System.Windows;
using System.Windows.Controls;
using RLSHub.Wpf.Services;

namespace RLSHub.Wpf.Views
{
    public partial class SettingsPage : UserControl
    {
        private readonly RlsSettingsStore _settingsStore = new();
        private bool _isLoading;

        public SettingsPage()
        {
            InitializeComponent();
            Loaded += SettingsPage_Loaded;
        }

        private void SettingsPage_Loaded(object sender, RoutedEventArgs e)
        {
            _isLoading = true;
            var settings = _settingsStore.LoadSettings(out _);
            MapDevToggle.IsChecked = settings.MapDevMode;
            NoPoliceToggle.IsChecked = settings.NoPoliceMode;
            NoParkedToggle.IsChecked = settings.NoParkedMode;
            _isLoading = false;
        }

        private void Toggle_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;
            var settings = new RlsSettings(
                MapDevToggle.IsChecked == true,
                NoPoliceToggle.IsChecked == true,
                NoParkedToggle.IsChecked == true);
            if (!_settingsStore.SaveSettings(settings, out var error))
                MessageBox.Show(error ?? "Unable to write settings file.", "Settings save failed", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
