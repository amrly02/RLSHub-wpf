using System.Windows;
using RLSHub.Wpf.Native;
using RLSHub.Wpf.Services;

namespace RLSHub.Wpf
{
    public partial class App : Application
    {
        public App()
        {
            // So that when the app exits (close, Alt+F4, crash), the bridge process is killed too.
            WindowsJobObject.EnsureCurrentProcessInKillOnCloseJob();
            Exit += (_, _) => BridgeScriptService.KillCurrentBridge();
        }
    }
}
