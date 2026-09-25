using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace CargoDeckScanner;

// Schalter wie auf der Seite, gut sichtbar an und aus
class Toggle : Control
{
    bool _checked, _hover;
    public Color OnColor { get; set; } = Color.Teal;
    public Color OffColor { get; set; } = Color.Gray;
    public event EventHandler CheckedChanged;

    public Toggle()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        Cursor = Cursors.Hand; TabStop = true;
    }

    public bool Checked
    {
        get => _checked;
        set { if (_checked == value) return; _checked = value; Invalidate(); CheckedChanged?.Invoke(this, EventArgs.Empty); }
    }

    float K => DeviceDpi / 96f;

    public Size Measure()
    {
        var ts = TextRenderer.MeasureText(Text, Font);
        return new Size((int)(46 * K) + ts.Width + 4, Math.Max((int)(26 * K), ts.Height + 6));
    }

    protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Invalidate(); }
    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnClick(EventArgs e) { Focus(); Checked = !Checked; base.OnClick(e); }
    protected override void OnKeyDown(KeyEventArgs e) { if (e.KeyCode == Keys.Space) Checked = !Checked; base.OnKeyDown(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.Clear(BackColor);
        float k = K, w = 38 * k, h = 20 * k, y = (Height - h) / 2f;
        using (var path = Pill(new RectangleF(1, y, w, h)))
        {
            using var b = new SolidBrush(_checked ? OnColor : (_hover ? ControlPaint.Light(OffColor, .3f) : OffColor));
            g.FillPath(b, path);
            if (Focused) { using var p = new Pen(Color.FromArgb(120, OnColor), 1.5f * k); g.DrawPath(p, path); }
        }
        float d = h - 6 * k, x = _checked ? 1 + w - d - 3 * k : 1 + 3 * k;
        using (var kb = new SolidBrush(_checked ? Color.FromArgb(0x0b, 0x0f, 0x14) : Color.FromArgb(0xee, 0xf3, 0xf9)))
            g.FillEllipse(kb, x, y + 3 * k, d, d);
        TextRenderer.DrawText(g, Text, Font, new Rectangle((int)(w + 10 * k), 0, Width - (int)(w + 10 * k), Height), ForeColor, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
    }

    static GraphicsPath Pill(RectangleF r)
    {
        var p = new GraphicsPath(); float d = r.Height;
        p.AddArc(r.X, r.Y, d, d, 90, 180); p.AddArc(r.Right - d, r.Y, d, d, 270, 180); p.CloseFigure(); return p;
    }
}

// Karte mit Rahmen und leuchtender Oberkante
class CardPanel : Panel
{
    public Color LineColor { get; set; } = Color.Gray;
    public Color GlowColor { get; set; } = Color.Teal;
    public CardPanel() { SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw, true); }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var pen = new Pen(LineColor);
        e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        using var br = new LinearGradientBrush(new Rectangle(0, 0, Width, 2), Color.Transparent, GlowColor, 0f)
        {
            InterpolationColors = new ColorBlend { Colors = new[] { Color.Transparent, Color.FromArgb(200, GlowColor), Color.Transparent }, Positions = new[] { 0f, .5f, 1f } }
        };
        e.Graphics.FillRectangle(br, 0, 0, Width, 1);
    }
}

// Namen und Codes für Tasten
static class KeyNames
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern short VkKeyScan(char ch);
    [DllImport("user32.dll")] static extern uint MapVirtualKey(uint code, uint type);
    public static int FromChar(string s) { if (string.IsNullOrEmpty(s)) return 0; var v = VkKeyScan(s[0]); return v == -1 ? 0 : v & 0xFF; }
    public static string Name(int vk)
    {
        if (vk <= 0) return "?";
        try
        {
            var k = (Keys)vk;
            if ((k >= Keys.A && k <= Keys.Z) || (k >= Keys.D0 && k <= Keys.D9) || k >= Keys.Oem1)
            {
                var ch = (char)(MapVirtualKey((uint)vk, 2) & 0x7FFF);
                if (ch > ' ') return char.ToUpper(ch).ToString();
            }
            if (k >= Keys.NumPad0 && k <= Keys.NumPad9) return "Num " + (vk - (int)Keys.NumPad0);
            return k switch { Keys.Next => "Bild ab", Keys.PageUp => "Bild auf", Keys.Insert => "Einfg", Keys.Delete => "Entf", Keys.Home => "Pos1", Keys.End => "Ende", Keys.Space => "Leertaste", _ => k.ToString() };
        }
        catch { return "Taste " + vk; }
    }
}

// Knopf, der die nächste gedrückte Taste aufnimmt
class KeyBox : Button
{
    int _vk; bool _capture;
    public event EventHandler Changed;
    public int Vk { get => _vk; set { _vk = value; if (!_capture) Text = KeyNames.Name(value); } }
    public KeyBox() { Click += (s, e) => { _capture = true; Text = "Taste…"; Focus(); }; LostFocus += (s, e) => { if (_capture) { _capture = false; Text = KeyNames.Name(_vk); } }; }
    protected override bool IsInputKey(Keys keyData) => _capture || base.IsInputKey(keyData);
    protected override bool ProcessDialogKey(Keys keyData) => _capture ? true : base.ProcessDialogKey(keyData);
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (!_capture) { base.OnKeyDown(e); return; }
        e.Handled = true; e.SuppressKeyPress = true;
        var k = e.KeyCode;
        if (k is Keys.ControlKey or Keys.ShiftKey or Keys.Menu or Keys.LControlKey or Keys.RControlKey or Keys.LWin or Keys.RWin) return;
        _capture = false;
        if (k != Keys.Escape) { _vk = (int)k; Changed?.Invoke(this, EventArgs.Empty); }
        Text = KeyNames.Name(_vk);
    }
}
