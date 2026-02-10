using System;
using System.IO;

namespace RLSHub.Wpf.Services
{
    public sealed class FirstRunService
    {
        private static string GetMarkerPath()
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "RLSHub");
            return Path.Combine(dir, "firstRunDone");
        }

        public bool IsFirstRun()
        {
            return !File.Exists(GetMarkerPath());
        }

        public void CompleteFirstRun()
        {
            var path = GetMarkerPath();
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(path, DateTime.UtcNow.ToString("O"));
        }
    }
}
