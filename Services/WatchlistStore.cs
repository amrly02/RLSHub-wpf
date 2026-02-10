using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace RLSHub.Wpf.Services
{
    public sealed class WatchlistStore
    {
        private static string GetFilePath()
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "RLSHub");
            return Path.Combine(dir, "carswap_watchlist.json");
        }

        public HashSet<string> Load()
        {
            var path = GetFilePath();
            if (!File.Exists(path)) return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var json = File.ReadAllText(path);
                var list = JsonSerializer.Deserialize<List<string>>(json);
                return list != null
                    ? new HashSet<string>(list.Where(id => !string.IsNullOrWhiteSpace(id)), StringComparer.OrdinalIgnoreCase)
                    : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }
        }

        public void Save(HashSet<string> listingIds)
        {
            var path = GetFilePath();
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            var list = listingIds.ToList();
            var json = JsonSerializer.Serialize(list);
            File.WriteAllText(path, json);
        }
    }
}
