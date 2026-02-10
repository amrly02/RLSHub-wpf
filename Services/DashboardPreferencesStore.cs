using System;
using System.IO;
using System.Text.Json;

namespace RLSHub.Wpf.Services
{
    public sealed record DashboardPreferences(
        bool EnableConsole,
        bool AutoRunBridge,
        int RendererIndex,
        DateTime? LastLaunchUtc,
        bool NotifyWhenUpdateAvailable);

    public sealed class DashboardPreferencesStore
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };
        private static string GetFilePath()
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "RLSHub");
            return Path.Combine(dir, "dashboard.json");
        }

        public DashboardPreferences Load()
        {
            var path = GetFilePath();
            if (!File.Exists(path))
                return new DashboardPreferences(false, false, 0, null, true);
            try
            {
                var json = File.ReadAllText(path);
                var node = JsonDocument.Parse(json).RootElement;
                var rendererIndex = 0;
                if (node.TryGetProperty("rendererIndex", out var r) && r.TryGetInt32(out var ri))
                    rendererIndex = Math.Clamp(ri, 0, 1);
                DateTime? lastLaunch = null;
                if (node.TryGetProperty("lastLaunchUtc", out var ll))
                {
                    if (ll.ValueKind == JsonValueKind.String && ll.TryGetDateTime(out var dt))
                        lastLaunch = dt;
                }
                var notifyUpdates = true;
                if (node.TryGetProperty("notifyWhenUpdateAvailable", out var nu))
                    notifyUpdates = nu.ValueKind == JsonValueKind.True;
                return new DashboardPreferences(
                    node.TryGetProperty("enableConsole", out var c) && c.ValueKind == JsonValueKind.True,
                    node.TryGetProperty("autoRunBridge", out var b) && b.ValueKind == JsonValueKind.True,
                    rendererIndex,
                    lastLaunch,
                    notifyUpdates);
            }
            catch
            {
                return new DashboardPreferences(false, false, 0, null, true);
            }
        }

        public void Save(DashboardPreferences prefs)
        {
            var path = GetFilePath();
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(new
            {
                enableConsole = prefs.EnableConsole,
                autoRunBridge = prefs.AutoRunBridge,
                rendererIndex = Math.Clamp(prefs.RendererIndex, 0, 1),
                lastLaunchUtc = prefs.LastLaunchUtc.HasValue ? prefs.LastLaunchUtc.Value.ToString("O") : (object?)null,
                notifyWhenUpdateAvailable = prefs.NotifyWhenUpdateAvailable
            }, JsonOptions);
            File.WriteAllText(path, json);
        }
    }
}
