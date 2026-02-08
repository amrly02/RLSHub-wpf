using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace RLSHub.Wpf.Views
{
    public partial class AboutPage : UserControl
    {
        public AboutPage()
        {
            InitializeComponent();
        }

        private void OpenLink(string url)
        {
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
            catch { }
        }

        private void OpenRacelessPatreon_Click(object sender, RoutedEventArgs e) => OpenLink("https://www.patreon.com/cw/RacelessRLS");
        private void OpenVehicleLabsPatreon_Click(object sender, RoutedEventArgs e) => OpenLink("https://www.patreon.com/cw/RLSVehicleLabs");
        private void OpenDiscord_Click(object sender, RoutedEventArgs e) => OpenLink("https://discord.com/invite/pbZ5eJNh9F");
        private void OpenGitHub_Click(object sender, RoutedEventArgs e) => OpenLink("https://github.com/RLS-Modding");
    }
}
