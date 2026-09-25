using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;

namespace CargoDeckScanner;

// Erkennt, ob die App aus dem Microsoft Store (als Paket) läuft, und kümmert sich dann um Autostart und Neustart
static class Packaged
{
    static bool? _is;
    public const string StartupId = "CargoDeckScannerStartup";
    public const string Alias = "cargodeckscanner.exe";

    public static bool IsPackaged
    {
        get
        {
            if (_is.HasValue) return _is.Value;
            try { _is = Package.Current?.Id != null; } catch { _is = false; }
            return _is.Value;
        }
    }

    public static bool StartedByWindows()
    {
        if (!IsPackaged) return false;
        try { return AppInstance.GetActivatedEventArgs()?.Kind == ActivationKind.StartupTask; } catch { return false; }
    }

    public static async void SetStartup(bool on)
    {
        try
        {
            var t = await StartupTask.GetAsync(StartupId);
            if (on) { if (t.State == StartupTaskState.Disabled) await t.RequestEnableAsync(); }
            else if (t.State == StartupTaskState.Enabled) t.Disable();
        }
        catch { }
    }
}
