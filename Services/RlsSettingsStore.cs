using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace RLSHub.Wpf.Services
{
    public sealed record RlsSettings(bool MapDevMode, bool NoPoliceMode, bool NoParkedMode);

    public sealed class RlsSettingsStore
    {
        private const string SettingsRelativePath = "settings\\RLS\\careerOverhaul.json";

        public bool TryGetSettingsPath(out string path, out string? error)
        {
            error = null;
            path = string.Empty;
            if (!BeamNgConfigService.TryLoad(out var config) || config == null)
            {
                error = "BeamNG.drive.ini not found.";
                return false;
            }
            path = Path.Combine(config.UserFolder, SettingsRelativePath);
            return true;
        }

        public RlsSettings LoadSettings(out string? error)
        {
            error = null;
            if (!TryGetSettingsPath(out var path, out error))
                return new RlsSettings(false, true, false);
            if (!File.Exists(path))
                return new RlsSettings(false, true, false);
            try
            {
                var json = File.ReadAllText(path);
                var node = JsonNode.Parse(json) as JsonObject;
                if (node == null) return new RlsSettings(false, true, false);
                return new RlsSettings(
                    node["mapDevMode"]?.GetValue<bool>() ?? false,
                    node["noPoliceMode"]?.GetValue<bool>() ?? true,
                    node["noParkedMode"]?.GetValue<bool>() ?? false);
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return new RlsSettings(false, true, false);
            }
        }

        public bool SaveSettings(RlsSettings settings, out string? error)
        {
            error = null;
            if (!TryGetSettingsPath(out var path, out error)) return false;
            try
            {
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(directory))
                    Directory.CreateDirectory(directory);
                var node = new JsonObject
                {
                    ["mapDevMode"] = settings.MapDevMode,
                    ["noPoliceMode"] = settings.NoPoliceMode,
                    ["noParkedMode"] = settings.NoParkedMode
                };
                var json = node.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }
    }
}
