using System.Drawing;

namespace CargoDeckScanner;

static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        // Prüfmodus, liest ein Bild und schreibt das Ergebnis der Texterkennung in eine Datei
        if (args.Length >= 3 && args[0] == "--ocr-test")
        {
            try
            {
                using var bmp = new Bitmap(args[1]);
                var res = Ocr.Run(bmp).GetAwaiter().GetResult();
                File.WriteAllText(args[2], res.ToJsonString());
                return 0;
            }
            catch (Exception ex) { File.WriteAllText(args[2], "{\"ok\":false,\"error\":" + System.Text.Json.JsonSerializer.Serialize(ex.ToString()) + "}"); return 1; }
        }

        using var mutex = new Mutex(true, "CargoDeckScanner_single", out bool first);
        if (!first)
        {
            MessageBox.Show("Der Cargo Deck Scanner läuft schon. Du findest ihn unten rechts bei den Symbolen neben der Uhr.", "Cargo Deck Scanner");
            return 0;
        }
        ApplicationConfiguration.Initialize();
        var form = new MainForm(args.Contains("--tray"));
        // Prüfmodus, speichert ein Bild vom Fenster und beendet sich
        int ui = Array.IndexOf(args, "--ui-shot");
        if (ui >= 0 && ui + 1 < args.Length)
        {
            var path = args[ui + 1];
            form.Shown += async (s, e) =>
            {
                await Task.Delay(800);
                using var b = new Bitmap(form.Width, form.Height);
                form.DrawToBitmap(b, new Rectangle(0, 0, form.Width, form.Height));
                b.Save(path);
                form.Close();
            };
        }
        Application.Run(form);
        return 0;
    }
}
