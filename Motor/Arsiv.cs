using System.IO;

namespace DafiumArsiv.Motor;

public sealed class Kayit
{
    public required string Yol { get; init; }
    public long Boyut { get; init; }
    public long Paketli { get; init; } = -1;
    public DateTime? Tarih { get; init; }
    public bool Klasor { get; init; }
    public bool Sifreli { get; init; }
    public bool Baglanti { get; init; }
    public int Sira { get; init; }

    public string Ad => Yol.Contains('/') ? Yol[(Yol.LastIndexOf('/') + 1)..] : Yol;
}

public readonly record struct Ilerleme(long Yazilan, long Toplam, string Dosya, int DosyaNo, int DosyaSayisi);

public sealed class SifreGerekliException(string mesaj, bool yanlis) : Exception(mesaj)
{
    public bool Yanlis { get; } = yanlis;
}

public sealed class ArsivHatasi(string mesaj) : Exception(mesaj);

public abstract class Arsiv : IDisposable
{
    public string Yol { get; }
    public abstract string MotorAdi { get; }
    public string? Sifre { get; set; }

    protected Arsiv(string yol) => Yol = yol;

    public abstract List<Kayit> Listele(CancellationToken iptal = default);

    public abstract void Cikar(IReadOnlyCollection<Kayit>? secili, string hedef, IProgress<Ilerleme>? ilerleme, CancellationToken iptal);

    public abstract byte[]? Oku(Kayit kayit, long sinir, CancellationToken iptal = default);

    public virtual void Dispose() { }

    public static Arsiv Ac(string yol)
    {
        if (!File.Exists(yol)) throw new ArsivHatasi(Dil.T("e_not_found"));
        if (RarMi(yol)) return new UnrarArsiv(IlkCilt(yol));
        if (YediMi(yol) && SevenZipArsiv.Var()) return new SevenZipArsiv(yol);
        return new LibarchiveArsiv(yol);
    }

    static bool RarMi(string yol)
    {
        Span<byte> b = stackalloc byte[7];
        using var f = File.OpenRead(yol);
        int n = f.Read(b);
        return n == 7 && b[0] == 0x52 && b[1] == 0x61 && b[2] == 0x72 && b[3] == 0x21 && b[4] == 0x1A && b[5] == 0x07;
    }

    static bool YediMi(string yol)
    {
        Span<byte> b = stackalloc byte[6];
        using var f = File.OpenRead(yol);
        return f.Read(b) == 6 && b[0] == 0x37 && b[1] == 0x7A && b[2] == 0xBC && b[3] == 0xAF && b[4] == 0x27 && b[5] == 0x1C;
    }

    // opening part3 should still start from part1
    static string IlkCilt(string yol)
    {
        var ad = Path.GetFileName(yol);
        var m = System.Text.RegularExpressions.Regex.Match(ad, @"^(.*\.part)(\d+)(\.rar)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (!m.Success) return yol;
        var bir = m.Groups[1].Value + "1".PadLeft(m.Groups[2].Value.Length, '0') + m.Groups[3].Value;
        var aday = Path.Combine(Path.GetDirectoryName(yol)!, bir);
        return File.Exists(aday) ? aday : yol;
    }
}

public static class Guvenli
{
    static readonly char[] Yasak = Path.GetInvalidFileNameChars();
    static readonly HashSet<string> AyrilmisAdlar = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    public static string Normallestir(string arsivYolu)
    {
        var parcalar = new List<string>();
        foreach (var ham in arsivYolu.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            var p = ham.Trim();
            // zip slip: drop .. and drive letters
            if (p is "." or "..") continue;
            if (p.Length == 2 && p[1] == ':') continue;
            var temiz = new string(p.Select(c => Array.IndexOf(Yasak, c) >= 0 ? '_' : c).ToArray()).TrimEnd('.', ' ');
            if (temiz.Length == 0) continue;
            var kok = temiz.Contains('.') ? temiz[..temiz.IndexOf('.')] : temiz;
            if (AyrilmisAdlar.Contains(kok)) temiz = "_" + temiz;
            parcalar.Add(temiz);
        }
        return string.Join('/', parcalar);
    }

    public static string HedefYol(string hedefKok, string arsivYolu)
    {
        var goreli = Normallestir(arsivYolu);
        if (goreli.Length == 0) throw new ArsivHatasi(Dil.T("e_bad_name", arsivYolu));
        var kok = Path.GetFullPath(hedefKok).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var tam = Path.GetFullPath(Path.Combine(kok, goreli.Replace('/', Path.DirectorySeparatorChar)));
        if (!tam.StartsWith(kok, StringComparison.OrdinalIgnoreCase))
            throw new ArsivHatasi(Dil.T("e_slip", arsivYolu));
        return tam;
    }
}
