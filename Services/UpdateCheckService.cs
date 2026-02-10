using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace RLSHub.Wpf.Services
{
    public sealed class UpdateCheckService
    {
        private const string GitHubApiBase = "https://api.github.com";
        private const string DefaultUserAgent = "RLSHub-UpdateCheck/1.0";
        private const string ModTagId = "RLSCO24";

        /// <summary>GitHub repo for the RLSHub app (owner/repo).</summary>
        public static (string Owner, string Repo) AppRepo => ("RLS-Modding", "RLSHub");
        private static readonly string[] ModFolderNames = { "rls_career_overhaul", "rls-career-overhaul", "rls career overhaul" };

        private readonly HttpClient _httpClient;
        private readonly string _owner;
        private readonly string _repo;

        public UpdateCheckService(string owner = "RLS-Modding", string repo = "rls_career_overhaul")
        {
            _owner = owner;
            _repo = repo;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", DefaultUserAgent);
        }

        public static (Version? Version, string? VersionString) GetInstalledModVersion()
        {
            if (!BeamNgConfigService.TryLoad(out var config) || config == null)
                return (null, null);
            var modsPath = Path.Combine(config.UserFolder, "mods");
            var unpackedPath = Path.Combine(modsPath, "unpacked");
            foreach (var folderName in ModFolderNames)
            {
                var infoPath = Path.Combine(unpackedPath, folderName, "mod_info", ModTagId, "info.json");
                if (File.Exists(infoPath))
                {
                    var (v, s) = TryReadVersionFromInfoFile(infoPath);
                    if (v != null) return (v, s);
                }
            }
            foreach (var folderName in ModFolderNames)
            {
                var infoPath = Path.Combine(modsPath, folderName, "mod_info", ModTagId, "info.json");
                if (File.Exists(infoPath))
                {
                    var (v, s) = TryReadVersionFromInfoFile(infoPath);
                    if (v != null) return (v, s);
                }
            }
            if (!Directory.Exists(modsPath)) return (null, null);
            foreach (var zipPath in Directory.EnumerateFiles(modsPath, "*.zip"))
            {
                var fileName = Path.GetFileNameWithoutExtension(zipPath);
                if (!IsCareerOverhaulZipName(fileName)) continue;
                var (v, s) = TryReadVersionFromZip(zipPath);
                if (v != null) return (v, s);
            }
            return (null, null);
        }

        public static (string ModsFolder, string[] ExamplePaths) GetExpectedModPathsForDisplay()
        {
            if (!BeamNgConfigService.TryLoad(out var config) || config == null)
                return (string.Empty, Array.Empty<string>());
            var modsPath = Path.Combine(config.UserFolder, "mods");
            var unpackedPath = Path.Combine(modsPath, "unpacked");
            var examples = new List<string>
            {
                Path.Combine(unpackedPath, "rls_career_overhaul", "mod_info", ModTagId, "info.json"),
                Path.Combine(unpackedPath, "rls-career-overhaul", "mod_info", ModTagId, "info.json"),
                Path.Combine(unpackedPath, "rls career overhaul", "mod_info", ModTagId, "info.json"),
                Path.Combine(modsPath, "rls_career_overhaul", "mod_info", ModTagId, "info.json"),
                Path.Combine(modsPath, "rls-career-overhaul", "mod_info", ModTagId, "info.json"),
                Path.Combine(modsPath, "rls career overhaul", "mod_info", ModTagId, "info.json"),
                Path.Combine(modsPath, "rls_career_overhaul_*.zip") + " (zip with mod_info/RLSCO24/info.json inside)"
            };
            return (modsPath, examples.ToArray());
        }

        private static bool IsCareerOverhaulZipName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            var n = name.Replace("-", "_").Replace(" ", "_").ToLowerInvariant();
            return n.StartsWith("rls_career_overhaul", StringComparison.Ordinal);
        }

        private static (Version? Version, string? VersionString) TryReadVersionFromInfoFile(string infoPath)
        {
            try
            {
                var json = File.ReadAllText(infoPath);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                var tagid = root.TryGetProperty("tagid", out var tagProp) ? tagProp.GetString() : null;
                if (string.IsNullOrWhiteSpace(tagid) || !string.Equals(tagid, ModTagId, StringComparison.OrdinalIgnoreCase))
                    return (null, null);
                var versionString = root.TryGetProperty("version_string", out var verProp) ? verProp.GetString() : null;
                if (string.IsNullOrWhiteSpace(versionString)) return (null, null);
                return (Version.Parse(versionString), versionString);
            }
            catch { return (null, null); }
        }

        private static (Version? Version, string? VersionString) TryReadVersionFromZip(string zipPath)
        {
            try
            {
                using var archive = ZipFile.OpenRead(zipPath);
                var entry = archive.Entries.FirstOrDefault(e =>
                {
                    var name = e.FullName.Replace('\\', '/').TrimEnd('/');
                    return name.EndsWith("mod_info/RLSCO24/info.json", StringComparison.OrdinalIgnoreCase);
                });
                if (entry == null) return (null, null);
                using var stream = entry.Open();
                using var reader = new StreamReader(stream);
                var json = reader.ReadToEnd();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                var tagid = root.TryGetProperty("tagid", out var tagProp) ? tagProp.GetString() : null;
                if (string.IsNullOrWhiteSpace(tagid) || !string.Equals(tagid, ModTagId, StringComparison.OrdinalIgnoreCase))
                    return (null, null);
                var versionString = root.TryGetProperty("version_string", out var verProp) ? verProp.GetString() : null;
                if (string.IsNullOrWhiteSpace(versionString)) return (null, null);
                return (Version.Parse(versionString), versionString);
            }
            catch { return (null, null); }
        }

        /// <summary>Gets the current RLSHub app version from the entry assembly.</summary>
        public static Version GetCurrentAppVersion()
        {
            try
            {
                var v = Assembly.GetExecutingAssembly().GetName().Version;
                return v ?? new Version(1, 0, 0, 0);
            }
            catch { return new Version(1, 0, 0, 0); }
        }

        public async Task<(Version TagVersion, string HtmlUrl)> FetchLatestReleaseAsync()
        {
            var url = $"{GitHubApiBase}/repos/{_owner}/{_repo}/releases/latest";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                throw new InvalidOperationException($"Repository or latest release not found (404). https://github.com/{_owner}/{_repo}");
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var tagName = root.GetProperty("tag_name").GetString() ?? "";
            var htmlUrl = root.GetProperty("html_url").GetString() ?? $"https://github.com/{_owner}/{_repo}/releases";
            return (ParseTagToVersion(tagName), htmlUrl);
        }

        public static Version ParseTagToVersion(string tagName)
        {
            if (string.IsNullOrWhiteSpace(tagName)) return new Version(0, 0, 0, 0);
            var s = tagName.Trim();
            if (s.StartsWith("v", StringComparison.OrdinalIgnoreCase)) s = s.Substring(1).Trim();
            if (string.IsNullOrWhiteSpace(s)) return new Version(0, 0, 0, 0);
            try { return Version.Parse(s); } catch { return new Version(0, 0, 0, 0); }
        }

        public static bool IsUpdateAvailable(Version current, Version latest)
        {
            if (latest == null) return false;
            if (current == null) return true;
            return current.CompareTo(latest) < 0;
        }
    }
}
