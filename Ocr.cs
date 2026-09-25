using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Security.Cryptography;

namespace CargoDeckScanner;

// Texterkennung mit der in Windows eingebauten OCR, alles lokal auf dem PC.
// Liefert das gleiche Format wie der alte ocr.ps1, damit der Server es versteht.
static class Ocr
{
    static OcrEngine _engine;

    static OcrEngine Engine()
    {
        if (_engine != null) return _engine;
        var en = new Windows.Globalization.Language("en-US");
        _engine = OcrEngine.IsLanguageSupported(en) ? OcrEngine.TryCreateFromLanguage(en) : OcrEngine.TryCreateFromUserProfileLanguages();
        if (_engine == null) throw new Exception("Keine Windows Texterkennung gefunden. In Windows unter Sprache eine Sprache mit Texterkennung installieren, zum Beispiel Englisch.");
        return _engine;
    }

    record Pass(string Name, bool Invert, double Cx, double Cw, double Zoom, float Contrast = 1f);

    static readonly Pass[] Plan =
    {
        new("rechts", true, 0.5, 0.5, 2),
        new("links", true, 0.0, 0.5, 2),
        // Zusätzlich mit starkem Kontrast und noch grösser, damit blasse und kleine Zahlen sauber gelesen werden
        new("rechts-k", true, 0.5, 0.5, 3, 1.9f),
        new("links-k", true, 0.0, 0.5, 3, 1.9f),
        new("invert", true, 0.0, 1.0, 1),
        new("normal", false, 0.0, 1.0, 1),
    };

    public static async Task<JsonObject> Run(Bitmap src)
    {
        var engine = Engine();
        var passes = new JsonArray();
        foreach (var p in Plan)
        {
            int sx = (int)(src.Width * p.Cx), sw = (int)(src.Width * p.Cw), sh = src.Height;
            double maxDim = OcrEngine.MaxImageDimension, longest = Math.Max(sw, sh), scale = p.Zoom;
            if (p.Zoom == 1 && longest < 2000) scale = Math.Min(1.6, maxDim / longest);
            if (longest * scale > maxDim) scale = maxDim / longest;
            int w = Math.Max(1, (int)(sw * scale)), h = Math.Max(1, (int)(sh * scale));

            using var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                float k = p.Contrast, a = (p.Invert ? -0.3f : 0.3f) * k, b = (p.Invert ? -0.59f : 0.59f) * k, c = (p.Invert ? -0.11f : 0.11f) * k;
                float o = (p.Invert ? 1f : 0f) * k - (k - 1f) / 2f;
                var cm = new ColorMatrix(new[]
                {
                    new[] { a, a, a, 0f, 0f },
                    new[] { b, b, b, 0f, 0f },
                    new[] { c, c, c, 0f, 0f },
                    new[] { 0f, 0f, 0f, 1f, 0f },
                    new[] { o, o, o, 0f, 1f },
                });
                using var ia = new ImageAttributes();
                ia.SetColorMatrix(cm);
                g.DrawImage(src, new Rectangle(0, 0, w, h), sx, 0, sw, sh, GraphicsUnit.Pixel, ia);
            }

            var sb = ToSoftwareBitmap(bmp);
            var res = await engine.RecognizeAsync(sb);
            sb.Dispose();

            var lines = new JsonArray();
            foreach (var l in res.Lines)
            {
                double x1 = 1e9, y1 = 1e9, x2 = 0, y2 = 0;
                foreach (var wd in l.Words)
                {
                    var r = wd.BoundingRect;
                    x1 = Math.Min(x1, r.X); y1 = Math.Min(y1, r.Y);
                    x2 = Math.Max(x2, r.X + r.Width); y2 = Math.Max(y2, r.Y + r.Height);
                }
                lines.Add(new JsonObject
                {
                    ["t"] = l.Text,
                    ["x"] = (int)(x1 / scale + sx),
                    ["y"] = (int)(y1 / scale),
                    ["w"] = (int)((x2 - x1) / scale),
                    ["h"] = (int)((y2 - y1) / scale),
                });
            }
            passes.Add(new JsonObject { ["name"] = p.Name, ["w"] = src.Width, ["h"] = src.Height, ["lines"] = lines });
        }
        return new JsonObject { ["ok"] = true, ["passes"] = passes };
    }

    static SoftwareBitmap ToSoftwareBitmap(Bitmap bmp)
    {
        var data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            var bytes = new byte[bmp.Width * bmp.Height * 4];
            for (int y = 0; y < bmp.Height; y++)
                Marshal.Copy(data.Scan0 + y * data.Stride, bytes, y * bmp.Width * 4, bmp.Width * 4);
            var buf = CryptographicBuffer.CreateFromByteArray(bytes);
            return SoftwareBitmap.CreateCopyFromBuffer(buf, BitmapPixelFormat.Bgra8, bmp.Width, bmp.Height, BitmapAlphaMode.Premultiplied);
        }
        finally { bmp.UnlockBits(data); }
    }
}
