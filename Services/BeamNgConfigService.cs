using System;
using System.Collections.Generic;
using System.IO;

namespace RLSHub.Wpf.Services
{
    public sealed record BeamNgConfig(string Version, string InstallPath, string UserFolder);

    public static class BeamNgConfigService
    {
        private const string IniFileName = "BeamNG.drive.ini";

        public static bool TryLoad(out BeamNgConfig? config)
        {
            config = null;
            var iniPath = GetIniPath();
            if (!File.Exists(iniPath))
                return false;

            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var line in File.ReadLines(iniPath))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#") || trimmed.StartsWith(";"))
                    continue;
                var separatorIndex = trimmed.IndexOf('=');
                if (separatorIndex <= 0) continue;
                var key = trimmed.Substring(0, separatorIndex).Trim();
                var value = trimmed[(separatorIndex + 1)..].Trim().Trim('"');
                values[key] = value;
            }

            if (!values.TryGetValue("installPath", out var installPath) || !values.TryGetValue("userFolder", out var userFolder))
                return false;

            values.TryGetValue("version", out var version);
            var normalizedUserFolder = NormalizeUserFolder(userFolder);
            config = new BeamNgConfig(version ?? string.Empty, installPath, normalizedUserFolder);
            return true;
        }

        private static string NormalizeUserFolder(string userFolder)
        {
            if (string.IsNullOrWhiteSpace(userFolder)) return userFolder;
            var trimmed = userFolder.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var lastSegment = Path.GetFileName(trimmed);
            if (string.Equals(lastSegment, "current", StringComparison.OrdinalIgnoreCase))
                return trimmed;
            return Path.Combine(trimmed, "current");
        }

        public static string GetIniPath()
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, "BeamNG", IniFileName);
        }
    }
}
