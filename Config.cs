using System.Text.Json;
using Microsoft.Win32;

namespace CargoDeckScanner;

class Config
{
    public string Url { get; set; } = "https://cargodeck.onrender.com";
    public string Code { get; set; } = "";
    public string Mode { get; set; } = "hotkey";   // hotkey oder auto
    public int Interval { get; set; } = 5;
    public string ScanKey { get; set; } = "ö";
    public string AutoKey { get; set; } = "ä";
    public bool Sounds { get; set; } = true;
    public bool Notify { get; set; } = true;
    public bool Autostart { get; set; } = false;
    public bool StartMinimized { get; set; } = false;

    static string Dir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CargoDeck");
    static string FilePath => Path.Combine(Dir, "scanner.json");
    static readonly JsonSerializerOptions Opt = new() { WriteIndented = true };

    public static Config Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var c = JsonSerializer.Deserialize<Config>(File.ReadAllText(FilePath)) ?? new Config();
                c.Interval = Math.Clamp(c.Interval, 3, 60);
                if (string.IsNullOrEmpty(c.ScanKey)) c.ScanKey = "ö";
                if (string.IsNullOrEmpty(c.AutoKey)) c.AutoKey = "ä";
                return c;
            }
        }
        catch { }
        return new Config();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, Opt));
            SetAutostart(Autostart);
        }
        catch { }
    }

    static void SetAutostart(bool on)
    {
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
            if (k == null) return;
            if (on) k.SetValue("CargoDeckScanner", $"\"{Environment.ProcessPath}\" --tray");
            else if (k.GetValue("CargoDeckScanner") != null) k.DeleteValue("CargoDeckScanner");
        }
        catch { }
    }
}
