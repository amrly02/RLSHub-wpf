using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace RLSHub.Wpf.Services
{
    public sealed class BridgeScriptService
    {
        private const string BridgeFolderName = "Bridge";
        private const string BridgeFileName = "bridge.exe";

        private static Process? _currentBridgeProcess;

        /// <summary>Register the bridge process so it can be killed when the app exits.</summary>
        public static void RegisterBridgeProcess(Process process)
        {
            _currentBridgeProcess = process;
        }

        /// <summary>Creates ProcessStartInfo for the bridge: no window, redirected output. Do not set Environment so the child gets default inheritance and the bridge exe is not confused by extra vars.</summary>
        public static ProcessStartInfo CreateBridgeProcessStartInfo(string bridgeExePath)
        {
            var workDir = Path.GetDirectoryName(bridgeExePath);
            return new ProcessStartInfo
            {
                FileName = bridgeExePath,
                WorkingDirectory = !string.IsNullOrEmpty(workDir) && Directory.Exists(workDir) ? workDir : null,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
        }

        /// <summary>Kill the bridge process if we started it. Call on app exit.</summary>
        public static void KillCurrentBridge()
        {
            try
            {
                if (_currentBridgeProcess == null) return;
                if (!_currentBridgeProcess.HasExited)
                    _currentBridgeProcess.Kill(entireProcessTree: true);
            }
            catch { }
            finally
            {
                _currentBridgeProcess = null;
            }
        }

        public string GetLocalBridgePath()
        {
            var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RLSHub", BridgeFolderName);
            return Path.Combine(root, BridgeFileName);
        }

        public async Task<(string? Path, string? Error)> EnsureBridgeScriptAsync()
        {
            try
            {
                var destination = GetLocalBridgePath();
                var source = GetPackagedBridgePath();
                if (File.Exists(destination))
                    return (destination, null);
                if (!File.Exists(source))
                    return (null, "Bridge executable not found in the app. Click 'Update bridge' first (run from app folder so Assets\\Bridge\\bridge.exe is present).");
                await UpdateFromPackageAsync();
                if (!File.Exists(destination))
                    return (null, "Bridge executable could not be copied to: " + destination);
                return (destination, null);
            }
            catch (Exception ex)
            {
                return (null, ex.Message);
            }
        }

        public async Task UpdateFromPackageAsync()
        {
            var source = GetPackagedBridgePath();
            if (!File.Exists(source))
                throw new FileNotFoundException("Packaged bridge executable not found.", source);
            var destination = GetLocalBridgePath();
            var directory = Path.GetDirectoryName(destination);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);
            await using var sourceStream = File.OpenRead(source);
            await using var destinationStream = File.Create(destination);
            await sourceStream.CopyToAsync(destinationStream).ConfigureAwait(false);
        }

        private static string GetPackagedBridgePath()
        {
            return Path.Combine(AppContext.BaseDirectory, "Assets", "Bridge", BridgeFileName);
        }
    }
}
