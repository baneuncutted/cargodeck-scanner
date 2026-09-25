namespace CargoDeckScanner;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        using var mutex = new Mutex(true, "CargoDeckScanner_single", out bool first);
        if (!first)
        {
            MessageBox.Show("Der Cargo Deck Scanner läuft schon. Du findest ihn unten rechts bei den Symbolen neben der Uhr.", "Cargo Deck Scanner");
            return;
        }
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm(args.Contains("--tray")));
    }
}
