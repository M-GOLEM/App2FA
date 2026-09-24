using ZXing;
using ZXing.Windows.Compatibility;

namespace Secure2FA.Core;

record ParsedOtp(string Issuer, string Label, string Secret);

static class QrEngine
{
    public static string? DecodeFromBitmap(Bitmap bmp)
    {
        try
        {
            var reader = new BarcodeReader { AutoRotate = true, TryInverted = true, Options = { PossibleFormats = new[] { BarcodeFormat.QR_CODE } } };
            var result = reader.Decode(bmp);
            return result?.Text;
        }
        catch { return null; }
    }

    public static string? DecodeFromFile(string path)
    {
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var img = Image.FromStream(fs);
            using var bmp = new Bitmap(img);
            return DecodeFromBitmap(bmp);
        }
        catch { return null; }
    }

    public static string? DecodeFromClipboardImage()
    {
        try
        {
            if (!Clipboard.ContainsImage()) return null;
            using var img = Clipboard.GetImage(); if (img == null) return null;
            using var bmp = new Bitmap(img);
            return DecodeFromBitmap(bmp);
        }
        catch { return null; }
    }

    public static Bitmap CaptureScreenSelection(Rectangle rect)
    {
        var bmp = new Bitmap(rect.Width, rect.Height);
        using (var g = Graphics.FromImage(bmp)) g.CopyFromScreen(rect.Location, Point.Empty, rect.Size);
        return bmp;
    }

    public static ParsedOtp? ParseOtpAuth(string text)
    {
        try
        {
            text = text.Trim();
            if (text.StartsWith("otpauth://", StringComparison.OrdinalIgnoreCase))
            {
                var uri = new Uri(text);
                var qdict = ParseQuery(uri.Query);
                qdict.TryGetValue("secret", out var secret); secret ??= "";
                if (string.IsNullOrWhiteSpace(secret)) return null;
                secret =  ", "").ToUpperInvariant();
                if (!TotpEngine.IsValidSecret(secret)) return null;
                qdict.TryGetValue("issuer", out var issuer); issuer ??= "";
                var labelPart = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'));
                string label = "", iss2 = "";
                if (labelPart.Contains(":"))
                {
                    var sp = labelPart.Split(':', 2);
                    iss2 = sp[0]; label = sp[1];
                }
                else label = labelPart;
                if (string.IsNullOrWhiteSpace(issuer)) issuer = iss2;
                if (string.IsNullOrWhiteSpace(issuer)) issuer = "Unknown";
                if (string.IsNullOrWhiteSpace(label)) label = "default";
                return new ParsedOtp(issuer.Trim(), label.Trim(), secret);
            }
            else
            {
                var s = text.Trim().Replace(" ", "").ToUpperInvariant();
                if (TotpEngine.IsValidSecret(s) && s.Length >= 16) return new ParsedOtp("Imported", "QR-Scan", s);
            }
        }
        catch { }
        return null;
    }

    public static ParsedOtp? DecodeAndParse(string imagePath)
    {
        var txt = DecodeFromFile(imagePath);
        if (txt == null) return null;
        return ParseOtpAuth(txt);
    }

    public static ParsedOtp? DecodeAndParse(Bitmap bmp)
    {
        var txt = DecodeFromBitmap(bmp);
        if (txt == null) return null;
        return ParseOtpAuth(txt);
    }

    static Dictionary<string, string> ParseQuery(string q)
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(q)) return d;
        q = q.TrimStart('?');
        foreach (var kv in q.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var idx = kv.IndexOf('=');
            if (idx < 0) d[Uri.UnescapeDataString(kv)] = "";
            else d[Uri.UnescapeDataString(kv[..idx])] = Uri.UnescapeDataString(kv[(idx + 1)..]);
        }
        return d;
    }
}
