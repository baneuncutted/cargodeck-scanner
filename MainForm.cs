using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace CargoDeckScanner;

class MainForm : Form
{
    // Farben wie auf der Seite
    static readonly Color cBg = Color.FromArgb(0x0c, 0x11, 0x19), cPanel = Color.FromArgb(0x14, 0x1c, 0x28), cPanel2 = Color.FromArgb(0x1a, 0x24, 0x33),
        cLine = Color.FromArgb(0x24, 0x31, 0x42), cText = Color.FromArgb(0xee, 0xf3, 0xf9), cMuted = Color.FromArgb(0x8f, 0x9d, 0xb1),
        cAccent = Color.FromArgb(0x35, 0xd6, 0xcc), cGood = Color.FromArgb(0x4a, 0xde, 0x80), cBad = Color.FromArgb(0xf8, 0x71, 0x71),
        cWarn = Color.FromArgb(0xfb, 0xbf, 0x24), cDark = Color.FromArgb(0x0b, 0x0f, 0x14);

    static readonly Regex TerminalWords = new("COMMODIT|SHOP INVENTOR|LOCAL MARKET|IN DEMAND|YOUR INVENTOR|SHOP QUANTIT", RegexOptions.IgnoreCase);
    static readonly Regex CodeRe = new("^[A-Z2-9]{12}$");
    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };

    readonly Config cfg = Config.Load();
    readonly bool startInTray;
    bool running, busy, wasScan, wasAuto;
    string lastPrint = "";
    DateTime nextAuto = DateTime.MinValue, lastPing = DateTime.MinValue;

    TextBox tUrl, tCode, tScanKey, tAutoKey;
    RadioButton rHot, rAuto;
    NumericUpDown nInt;
    Toggle cSound, cNotify, cAutostart;
    bool loading;
    Button bStart, bNow;
    Label lState;
    Panel dot;
    ListBox log;
    NotifyIcon tray;
    ToolStripMenuItem miAuto;
    System.Windows.Forms.Timer timer;

    float k;
    int S(float v) => (int)Math.Round(v * k);

    public MainForm(bool tray)
    {
        startInTray = tray;
        AutoScaleMode = AutoScaleMode.None;
        k = DeviceDpi / 96f;
        Text = "Cargo Deck Scanner";
        BackColor = cBg; ForeColor = cText;
        Font = new Font("Segoe UI", 9.75f);
        FormBorderStyle = FormBorderStyle.FixedSingle; MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(S(500), S(828));
        using (var s = typeof(MainForm).Assembly.GetManifestResourceStream("CargoDeckScanner.app.ico"))
            if (s != null) Icon = new Icon(s);
        Build();
        LoadForm();
        SetupTray();
        timer = new System.Windows.Forms.Timer { Interval = 50 };
        timer.Tick += Tick;
        timer.Start();
    }

    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int val, int size);
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        int on = 1; try { DwmSetWindowAttribute(Handle, 20, ref on, 4); } catch { }   // dunkle Titelleiste
    }

    // ---------------- Aufbau ----------------
    Label L(Control parent, string text, int x, int y, Color? col = null, float size = 9.75f, bool bold = false, int w = 0, int h = 0)
    {
        var l = new Label { Text = text, Left = S(x), Top = S(y), AutoSize = w == 0, ForeColor = col ?? cText, BackColor = parent.BackColor, Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular) };
        if (w > 0) { l.Width = S(w); l.Height = S(h > 0 ? h : 22); }
        parent.Controls.Add(l); return l;
    }
    TextBox T(Control parent, int x, int y, int w, bool mono = false)
    {
        var t = new TextBox { Left = S(x), Top = S(y), Width = S(w), BackColor = cPanel2, ForeColor = cText, BorderStyle = BorderStyle.FixedSingle, Font = mono ? new Font("Consolas", 12f, FontStyle.Bold) : new Font("Segoe UI", 11f) };
        parent.Controls.Add(t); return t;
    }
    Button B(string text, int x, int y, int w, int h, bool primary)
    {
        var b = new Button { Text = text, Left = S(x), Top = S(y), Width = S(w), Height = S(h), FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 10.5f, FontStyle.Bold), Cursor = Cursors.Hand, UseVisualStyleBackColor = false };
        StyleButton(b, primary);
        Controls.Add(b); return b;
    }
    void StyleButton(Button b, bool primary)
    {
        b.BackColor = primary ? cAccent : cPanel2; b.ForeColor = primary ? cDark : cText;
        b.FlatAppearance.BorderColor = primary ? cAccent : cLine; b.FlatAppearance.BorderSize = 1;
        b.FlatAppearance.MouseOverBackColor = primary ? Color.FromArgb(0x5b, 0xe4, 0xdb) : Color.FromArgb(0x21, 0x2d, 0x3e);
        b.FlatAppearance.MouseDownBackColor = primary ? Color.FromArgb(0x1a, 0xa3, 0x9b) : cLine;
    }
    Toggle Tg(Control parent, string text, int x, int y)
    {
        var t = new Toggle { Text = text, Left = S(x), Top = S(y), BackColor = parent.BackColor, ForeColor = cText, OnColor = cAccent, OffColor = cLine, Font = new Font("Segoe UI", 9.75f) };
        t.Size = t.Measure();
        parent.Controls.Add(t); return t;
    }
    RadioButton R(Control parent, string text, int x, int y)
    {
        var r = new RadioButton { Text = text, Left = S(x), Top = S(y), AutoSize = true, ForeColor = cText, BackColor = parent.BackColor, Cursor = Cursors.Hand };
        parent.Controls.Add(r); return r;
    }
    Panel Card(int y, int h, string title)
    {
        var p = new CardPanel { Left = S(16), Top = S(y), Width = S(468), Height = S(h), BackColor = cPanel, LineColor = cLine, GlowColor = cAccent };
        Controls.Add(p);
        if (title != null) L(p, title.ToUpperInvariant(), 16, 12, cMuted, 8.25f, true);
        return p;
    }

    void Build()
    {
        // Kopf
        var logo = new PictureBox { Left = S(18), Top = S(16), Width = S(36), Height = S(36), SizeMode = PictureBoxSizeMode.Zoom, BackColor = cBg };
        if (Icon != null) logo.Image = new Icon(Icon, 64, 64).ToBitmap();
        Controls.Add(logo);
        var t1 = L(this, "CARGO", 62, 15, cText, 14f, true);
        L(this, "DECK", 62 + (int)(t1.PreferredWidth / k), 15, cAccent, 14f, true);
        L(this, "Scanner für Handelsterminals", 64, 40, cMuted, 9f);

        // Status
        var ps = Card(68, 52, null);
        dot = new Panel { Left = S(16), Top = S(20), Width = S(12), Height = S(12), BackColor = cPanel };
        dot.Paint += (s, e) => { e.Graphics.SmoothingMode = SmoothingMode.AntiAlias; using var b = new SolidBrush((Color)dot.Tag); e.Graphics.FillEllipse(b, 0, 0, dot.Width - 1, dot.Height - 1); };
        dot.Tag = cMuted; ps.Controls.Add(dot);
        lState = L(ps, "Gestoppt", 36, 14, cText, 11f, true, 420, 26);

        // Verbindung
        var pv = Card(132, 158, "Verbindung");
        L(pv, "Adresse der Seite", 16, 36, cMuted, 9f);
        tUrl = T(pv, 16, 56, 436);
        L(pv, "Kopplungscode", 16, 94, cMuted, 9f);
        tCode = T(pv, 16, 114, 190, true); tCode.CharacterCasing = CharacterCasing.Upper; tCode.MaxLength = 12;
        L(pv, "Steht auf der Seite unter Einstellungen, PC Scanner", 218, 114, cMuted, 8.5f, false, 236, 34);

        // Modus
        var pm = Card(302, 116, "Wann scannen");
        rHot = R(pm, "Nur wenn ich die Taste drücke", 16, 36);
        rAuto = R(pm, "Automatisch alle", 16, 66);
        nInt = new NumericUpDown { Left = S(150), Top = S(64), Width = S(56), Minimum = 3, Maximum = 60, Value = 5, BackColor = cPanel2, ForeColor = cText, BorderStyle = BorderStyle.FixedSingle };
        pm.Controls.Add(nInt);
        L(pm, "Sekunden", 212, 67, cText);
        L(pm, "Sendet nur, wenn wirklich ein Terminal zu sehen ist", 34, 90, cMuted, 8.5f);

        // Tasten
        var pk = Card(430, 104, "Tasten");
        L(pk, "Scannen", 16, 42, cText);
        L(pk, "Linke Strg +", 200, 42, cMuted);
        tScanKey = T(pk, 290, 37, 44, true); tScanKey.MaxLength = 1; tScanKey.TextAlign = HorizontalAlignment.Center;
        L(pk, "Automatik an und aus", 16, 74, cText);
        L(pk, "Linke Strg +", 200, 74, cMuted);
        tAutoKey = T(pk, 290, 69, 44, true); tAutoKey.MaxLength = 1; tAutoKey.TextAlign = HorizontalAlignment.Center;

        // Optionen
        var po = Card(546, 110, "Optionen");
        cSound = Tg(po, "Leise Töne", 16, 38);
        cNotify = Tg(po, "Benachrichtigungen", 236, 38);
        cAutostart = Tg(po, "Mit Windows starten", 16, 72);

        // Knöpfe
        bStart = B("Starten", 16, 670, 228, 46, true);
        bNow = B("Jetzt scannen", 256, 670, 228, 46, false);

        // Verlauf
        log = new ListBox { Left = S(16), Top = S(728), Width = S(468), Height = S(84), BackColor = cPanel, ForeColor = cMuted, BorderStyle = BorderStyle.FixedSingle, IntegralHeight = false, Font = new Font("Segoe UI", 8.75f), SelectionMode = SelectionMode.None };
        Controls.Add(log);

        bStart.Click += (s, e) => { if (running) StopScanner(); else StartScanner(); };
        bNow.Click += async (s, e) =>
        {
            if (!running) StartScanner();
            if (!running) return;
            WindowState = FormWindowState.Minimized;
            await Task.Delay(700);
            await Scan(false);
        };
        rAuto.CheckedChanged += (s, e) => { if (loading) return; ReadForm(); if (running) ShowRunning(); if (miAuto != null) miAuto.Checked = rAuto.Checked; };
        nInt.ValueChanged += (s, e) => { if (!loading) ReadForm(); if (running) ShowRunning(); };
        cSound.CheckedChanged += (s, e) => { if (loading) return; ReadForm(); if (cfg.Sounds) Sound.Play(Sound.On); };
        cNotify.CheckedChanged += (s, e) => { if (!loading) ReadForm(); };
        cAutostart.CheckedChanged += (s, e) => { if (!loading) ReadForm(); };
        foreach (var t in new[] { tUrl, tScanKey, tAutoKey }) t.Leave += (s, e) => { if (!loading) ReadForm(); };
        tCode.TextChanged += (s, e) =>
        {
            var clean = Regex.Replace(tCode.Text.ToUpperInvariant(), "[^A-Z2-9]", "");
            if (clean != tCode.Text) { tCode.Text = clean; tCode.SelectionStart = clean.Length; }
        };
    }

    void LoadForm()
    {
        loading = true;
        tUrl.Text = cfg.Url; tCode.Text = cfg.Code;
        rAuto.Checked = cfg.Mode == "auto"; rHot.Checked = !rAuto.Checked;
        nInt.Value = Math.Clamp(cfg.Interval, 3, 60);
        tScanKey.Text = cfg.ScanKey; tAutoKey.Text = cfg.AutoKey;
        cSound.Checked = cfg.Sounds; cNotify.Checked = cfg.Notify; cAutostart.Checked = cfg.Autostart;
        loading = false;
    }

    void ReadForm()
    {
        cfg.Url = tUrl.Text.Trim().TrimEnd('/');
        cfg.Code = tCode.Text.Trim().ToUpperInvariant();
        cfg.Mode = rAuto.Checked ? "auto" : "hotkey";
        cfg.Interval = (int)nInt.Value;
        cfg.ScanKey = string.IsNullOrWhiteSpace(tScanKey.Text) ? "ö" : tScanKey.Text.Trim();
        cfg.AutoKey = string.IsNullOrWhiteSpace(tAutoKey.Text) ? "ä" : tAutoKey.Text.Trim();
        cfg.Sounds = cSound.Checked; cfg.Notify = cNotify.Checked; cfg.Autostart = cAutostart.Checked;
        cfg.Save();
    }

    // ---------------- Tray ----------------
    void SetupTray()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Öffnen", null, (s, e) => ShowWindow());
        menu.Items.Add("Jetzt scannen", null, async (s, e) => { if (!running) StartScanner(); if (running) await Scan(false); });
        miAuto = new ToolStripMenuItem("Automatik", null, (s, e) => { if (rAuto.Checked) rHot.Checked = true; else rAuto.Checked = true; }) { Checked = rAuto.Checked };
        menu.Items.Add(miAuto);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Beenden", null, (s, e) => Close());
        tray = new NotifyIcon { Icon = Icon, Text = "Cargo Deck Scanner", Visible = true, ContextMenuStrip = menu };
        tray.DoubleClick += (s, e) => ShowWindow();
    }

    void ShowWindow() { Show(); WindowState = FormWindowState.Normal; ShowInTaskbar = true; Activate(); }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (WindowState == FormWindowState.Minimized && running) { Hide(); ShowInTaskbar = false; }
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        Log("Bereit");
        if ((startInTray || cfg.Autostart) && CodeRe.IsMatch(cfg.Code) && cfg.Url.StartsWith("http"))
        {
            StartScanner();
            if (startInTray) { WindowState = FormWindowState.Minimized; }
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        ReadForm();
        tray.Visible = false; tray.Dispose();
        base.OnFormClosing(e);
    }

    // ---------------- Zustand ----------------
    void SetState(string text, Color col) { lState.Text = text; dot.Tag = col; dot.Invalidate(); }
    void Log(string text)
    {
        log.Items.Insert(0, DateTime.Now.ToString("HH:mm:ss") + "   " + text);
        while (log.Items.Count > 60) log.Items.RemoveAt(log.Items.Count - 1);
    }
    void Notify(string text)
    {
        if (!cfg.Notify) return;
        try { tray.ShowBalloonTip(2500, "Cargo Deck", text, ToolTipIcon.None); } catch { }
    }
    void Beep(Sound.Kind kind)
    {
        if (cfg.Sounds) Sound.Play(kind);
    }
    void ShowRunning()
    {
        SetState(cfg.Mode == "auto" ? $"Läuft, automatisch alle {cfg.Interval} Sekunden" : $"Läuft, Linke Strg + {cfg.ScanKey} scannt", cGood);
    }

    void StartScanner()
    {
        ReadForm();
        if (!Regex.IsMatch(cfg.Url, "^https?://[^/]+"))
        { MessageBox.Show(this, "Bitte die Adresse der Cargo Deck Seite eintragen, zum Beispiel https://cargodeck.onrender.com", "Cargo Deck Scanner"); return; }
        if (!CodeRe.IsMatch(cfg.Code))
        { MessageBox.Show(this, "Der Kopplungscode hat 12 Zeichen. Du findest ihn auf der Seite unter Einstellungen, PC Scanner.", "Cargo Deck Scanner"); return; }
        running = true; lastPrint = ""; nextAuto = DateTime.Now; lastPing = DateTime.MinValue;
        bStart.Text = "Stoppen"; StyleButton(bStart, false);
        foreach (var t in new[] { tUrl, tCode, tScanKey, tAutoKey }) { t.ReadOnly = true; t.ForeColor = cMuted; t.BackColor = cPanel; }
        ShowRunning(); Log($"Gestartet, Linke Strg + {cfg.ScanKey} scannt"); Beep(Sound.On);
    }

    void StopScanner()
    {
        running = false;
        bStart.Text = "Starten"; StyleButton(bStart, true);
        foreach (var t in new[] { tUrl, tCode, tScanKey, tAutoKey }) { t.ReadOnly = false; t.ForeColor = cText; t.BackColor = cPanel2; }
        SetState("Gestoppt", cMuted); Log("Gestoppt");
    }

    // ---------------- Tasten und Takt ----------------
    [DllImport("user32.dll")] static extern short GetAsyncKeyState(int vKey);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern short VkKeyScan(char ch);
    static bool Down(int vk) => vk > 0 && (GetAsyncKeyState(vk) & 0x8000) != 0;
    static int Vk(string s) => string.IsNullOrEmpty(s) ? 0 : VkKeyScan(s[0]) & 0xFF;

    async void Tick(object sender, EventArgs e)
    {
        if (!running) return;
        try
        {
            bool ctrl = Down(0xA2);
            bool scan = ctrl && Down(Vk(cfg.ScanKey)), auto = ctrl && Down(Vk(cfg.AutoKey));
            bool scanEdge = scan && !wasScan, autoEdge = auto && !wasAuto;
            wasScan = scan; wasAuto = auto;
            if (autoEdge)
            {
                if (rAuto.Checked) { rHot.Checked = true; Log("Automatik aus"); Beep(Sound.Off); Notify("Automatik aus"); }
                else { rAuto.Checked = true; Log("Automatik an"); Beep(Sound.On); Notify($"Automatik an, alle {cfg.Interval} Sekunden"); }
            }
            if (DateTime.Now - lastPing > TimeSpan.FromSeconds(30)) { lastPing = DateTime.Now; _ = Ping(); }
            if (scanEdge) await Scan(false);
            else if (rAuto.Checked && !busy && DateTime.Now >= nextAuto) { nextAuto = DateTime.Now.AddSeconds(cfg.Interval); await Scan(true); }
        }
        catch (Exception ex) { Log("Fehler " + ex.Message); busy = false; }
    }

    async Task Ping()
    {
        try
        {
            using var c = new StringContent(JsonSerializer.Serialize(new { pair = cfg.Code }), Encoding.UTF8, "application/json");
            await Http.PostAsync(cfg.Url + "/api/pair/ping", c);
        }
        catch { }
    }

    // ---------------- Scannen ----------------
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int Left, Top, Right, Bottom; }

    static Bitmap TakeShot()
    {
        GetWindowRect(GetForegroundWindow(), out var r);
        int w = r.Right - r.Left, h = r.Bottom - r.Top;
        if (w < 400 || h < 300) { var b = Screen.PrimaryScreen.Bounds; r.Left = b.X; r.Top = b.Y; w = b.Width; h = b.Height; }
        var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.CopyFromScreen(r.Left, r.Top, 0, 0, new Size(w, h));
        return bmp;
    }

    // Kleiner Fingerabdruck, damit ein unverändertes Bild im Automatik Modus nicht erneut gelesen wird
    static string Print(Bitmap bmp)
    {
        using var small = new Bitmap(40, 24);
        using (var g = Graphics.FromImage(small)) g.DrawImage(bmp, 0, 0, 40, 24);
        var sb = new StringBuilder(960);
        for (int y = 0; y < 24; y++) for (int x = 0; x < 40; x++) { var p = small.GetPixel(x, y); sb.Append((char)(65 + (p.R + p.G + p.B) / 48)); }
        return sb.ToString();
    }

    // Verkleinertes JPG als Nachweis für UEX
    static string Jpeg64(Bitmap bmp)
    {
        double scale = Math.Min(1.0, 1920.0 / bmp.Width);
        int w = (int)(bmp.Width * scale), h = (int)(bmp.Height * scale);
        using var outB = new Bitmap(w, h);
        using (var g = Graphics.FromImage(outB)) { g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.DrawImage(bmp, 0, 0, w, h); }
        var enc = ImageCodecInfo.GetImageEncoders().First(c => c.MimeType == "image/jpeg");
        using var ep = new EncoderParameters(1);
        ep.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 78L);
        using var ms = new MemoryStream();
        outB.Save(ms, enc, ep);
        return Convert.ToBase64String(ms.ToArray());
    }

    async Task Scan(bool auto)
    {
        if (busy) { if (!auto) Log("Noch beschäftigt, einen Moment"); return; }
        busy = true;
        try
        {
            using var bmp = TakeShot();
            if (auto)
            {
                var pr = Print(bmp);
                if (pr == lastPrint) return;
                lastPrint = pr;
            }
            if (!auto) SetState("Lese Terminal…", cWarn);
            var img = Jpeg64(bmp);
            JsonObject ocr;
            try { ocr = await Task.Run(() => Ocr.Run(bmp)); }
            catch (Exception ex) { Log("Texterkennung Fehler " + ex.Message); if (!auto) SetState("Texterkennung ging nicht", cBad); return; }

            if (auto)
            {
                var txt = string.Join(" ", ocr["passes"].AsArray().SelectMany(p => p["lines"].AsArray().Select(l => (string)l["t"])));
                if (!TerminalWords.IsMatch(txt)) return;
            }

            var body = new JsonObject { ["pair"] = cfg.Code, ["auto"] = auto, ["ocr"] = ocr, ["image"] = img };
            HttpResponseMessage resp;
            string raw;
            try
            {
                using var content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
                resp = await Http.PostAsync(cfg.Url + "/api/pair/scan", content);
                raw = await resp.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                Log("Senden fehlgeschlagen " + ex.Message); SetState("Seite nicht erreichbar, Adresse prüfen", cBad);
                if (!auto) { Beep(Sound.Error); Notify("Senden fehlgeschlagen, ist die Adresse richtig?"); }
                return;
            }

            JsonNode r = null; try { r = JsonNode.Parse(raw); } catch { }
            int rows = 0; try { rows = (int?)r?["rows"] ?? 0; } catch { }
            if (rows > 0)
            {
                string st = ""; try { st = ((string)r["station"] ?? "").Split(" > ").Last(); } catch { }
                Log($"{rows} Preise erkannt {st}".Trim());
                SetState($"Letzter Scan {DateTime.Now:HH:mm}, {rows} Preise", cGood);
                Beep(Sound.Success); Notify($"{rows} Preise erkannt {st}, schau auf die Seite".Replace("  ", " "));
            }
            else if (!auto)
            {
                string n = "Kein Terminal erkannt";
                try { n = (string)r?["error"] ?? (string)r?["note"] ?? n; } catch { }
                if (!resp.IsSuccessStatusCode && r?["error"] == null) n = $"Seite antwortet mit Fehler {(int)resp.StatusCode}";
                Log(n); SetState(n, cWarn); Beep(Sound.Error);
            }
        }
        finally
        {
            busy = false;
            if (running && !auto && lState.Text.StartsWith("Lese")) ShowRunning();
        }
    }
}
