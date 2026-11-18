using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using DafiumArsiv.Motor;

namespace DafiumArsiv;

/// <summary>Listede görünen satır: arşivdeki bir dosya ya da (gerçek veya yoldan türetilen) klasör.</summary>
public sealed class Oge
{
    public required string Ad { get; init; }
    public required string Yol { get; init; }
    public bool Klasor { get; init; }
    public Kayit? Kayit { get; set; }
    public long Boyut { get; set; }
    public long Paketli { get; set; } = -1;
    public DateTime? Tarih { get; set; }
    public bool Sifreli { get; set; }
    public List<Oge> Cocuklar { get; } = [];

    public Tur TurBilgisi => Klasor ? Turler.Klasor : Turler.Bul(Ad);
    public string Simge => TurBilgisi.Simge;
    public Brush SimgeRenk => TurBilgisi.Renk;
    public Brush SimgeZemin => TurBilgisi.Zemin;
    public string Tur => TurBilgisi.Ad;
    public string BoyutYazi => Klasor && Boyut == 0 ? "" : Bicim.Boyut(Boyut);
    public string PaketliYazi => Paketli < 0 ? "" : Bicim.Boyut(Paketli);
    public string TarihYazi => Tarih?.ToString("dd.MM.yyyy HH:mm") ?? "";
    public FontWeight Kalinlik => Klasor ? FontWeights.SemiBold : FontWeights.Normal;
    public Visibility SifreGorunum => Sifreli ? Visibility.Visible : Visibility.Collapsed;

    // Ekran okuyucular ve UI Automation satırı adıyla görsün
    public override string ToString() => Ad;
}

public sealed class KonumParcasi
{
    public required string Ad { get; init; }
    public required string Yol { get; init; }
    public Visibility AyracGorunum { get; init; }
}

public sealed record Tur(string Ad, string Simge, Brush Renk, Brush Zemin, string Grup);

public static class Turler
{
    static Brush F(string hex, byte alfa = 255)
    {
        var c = (Color)ColorConverter.ConvertFromString(hex);
        c.A = alfa;
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }

    static Tur T(string ad, string simge, string renk, string grup) => new(ad, simge, F(renk), F(renk, 38), grup);

    public static readonly Tur Klasor = T("Klasör", "", "#FFD166", "klasor");
    static readonly Tur Resim = T("Resim", "", "#4BE3B0", "resim");
    static readonly Tur Video = T("Video", "", "#FF7AA8", "video");
    static readonly Tur Ses = T("Ses", "", "#3EE7FF", "ses");
    static readonly Tur ArsivT = T("Arşiv", "", "#B7A6FF", "arsiv");
    static readonly Tur Program = T("Program", "", "#FF9A3D", "program");
    static readonly Tur Kod = T("Kod", "", "#9FD8FF", "kod");
    static readonly Tur Belge = T("Belge", "", "#FF8A96", "belge");
    static readonly Tur Yazi = T("Metin", "", "#DCD8EF", "yazi");

    static readonly Dictionary<string, Tur> Uzanti = new(StringComparer.OrdinalIgnoreCase);

    static Turler()
    {
        void E(Tur t, params string[] u) { foreach (var x in u) Uzanti[x] = t; }
        E(Resim, "png", "jpg", "jpeg", "gif", "bmp", "webp", "ico", "tif", "tiff", "svg", "heic", "avif", "psd", "dds", "tga");
        E(Video, "mp4", "mkv", "avi", "mov", "webm", "wmv", "flv", "m4v", "ts");
        E(Ses, "mp3", "wav", "flac", "ogg", "m4a", "aac", "opus", "wma");
        E(ArsivT, "rar", "zip", "7z", "tar", "gz", "tgz", "xz", "zst", "bz2", "iso", "cab", "lz4", "001");
        E(Program, "exe", "msi", "dll", "sys", "bat", "cmd", "ps1", "apk", "jar");
        E(Kod, "cs", "js", "ts", "tsx", "jsx", "py", "java", "c", "cpp", "h", "hpp", "rs", "go", "php", "html", "css", "xml", "xaml", "json", "yml", "yaml", "sh", "lua", "kt", "dart", "sql");
        E(Belge, "pdf", "doc", "docx", "xls", "xlsx", "ppt", "pptx", "odt", "rtf", "epub");
        E(Yazi, "txt", "md", "log", "ini", "cfg", "conf", "csv", "nfo", "srt", "properties", "toml");
    }

    public static Tur Bul(string ad)
    {
        var u = Path.GetExtension(ad).TrimStart('.');
        if (u.Length > 0 && Uzanti.TryGetValue(u, out var t)) return t;
        return new Tur(u.Length > 0 ? u.ToUpperInvariant() + " dosyası" : "Dosya", "", F("#9A94BF"), F("#9A94BF", 38), "diger");
    }

    public static bool ResimMi(string ad) => Bul(ad).Grup == "resim" && !ad.EndsWith(".svg", StringComparison.OrdinalIgnoreCase)
        && !ad.EndsWith(".psd", StringComparison.OrdinalIgnoreCase) && !ad.EndsWith(".heic", StringComparison.OrdinalIgnoreCase)
        && !ad.EndsWith(".avif", StringComparison.OrdinalIgnoreCase) && !ad.EndsWith(".dds", StringComparison.OrdinalIgnoreCase)
        && !ad.EndsWith(".tga", StringComparison.OrdinalIgnoreCase);
    public static bool YaziMi(string ad) => Bul(ad).Grup is "yazi" or "kod";
}

public static class Bicim
{
    static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

    public static string Boyut(long b)
    {
        if (b < 1024) return b + " B";
        string[] b2 = ["KB", "MB", "GB", "TB"];
        double d = b;
        int i = -1;
        do { d /= 1024; i++; } while (d >= 1024 && i < b2.Length - 1);
        return d.ToString(d >= 100 ? "0" : d >= 10 ? "0.#" : "0.##", Tr) + " " + b2[i];
    }

    public static string Sure(double saniye)
    {
        if (double.IsNaN(saniye) || double.IsInfinity(saniye) || saniye < 0) return "";
        if (saniye < 60) return Math.Ceiling(saniye) + " sn";
        if (saniye < 3600) return (int)(saniye / 60) + " dk " + (int)(saniye % 60) + " sn";
        return (int)(saniye / 3600) + " sa " + (int)(saniye % 3600 / 60) + " dk";
    }

    public static string Sayi(long n) => n.ToString("N0", Tr);
}
