using System.IO;
using System.Runtime.InteropServices;

namespace DafiumArsiv.Motor;

/// <summary>ZIP, 7z, TAR, GZ, XZ, ZST, BZ2, ISO, CAB … için libarchive (archive.dll).</summary>
public sealed unsafe class LibarchiveArsiv : Arsiv
{
    public LibarchiveArsiv(string yol) : base(yol) { }
    public override string MotorAdi => "libarchive";

    // Tek dosyalık sıkıştırmalar (dosya.txt.gz) "raw" biçimiyle okunur; ilk denemede anlaşılır.
    bool _ham;

    const int OK = 0, EOF = 1, WARN = -20, FAILED = -25, FATAL = -30;
    const uint IFMT = 0xF000, IFREG = 0x8000, IFDIR = 0x4000, IFLNK = 0xA000;

    sealed class Okuyucu : IDisposable
    {
        public IntPtr A;
        public void Dispose() { if (A != IntPtr.Zero) { N.archive_read_free(A); A = IntPtr.Zero; } }
    }

    Okuyucu Baslat()
    {
        var o = new Okuyucu { A = N.archive_read_new() };
        N.archive_read_support_filter_all(o.A);
        if (_ham) N.archive_read_support_format_raw(o.A);
        else N.archive_read_support_format_all(o.A);
        // Günümüz tar arşivlerinde adlar UTF-8; yoksa Windows'ta Türkçe harfler bozuk çıkıyor.
        // (ZIP'e dokunulmaz: libarchive UTF-8 bayrağı olmayan ZIP'lerde zaten OEM kod sayfasını kullanıyor.)
        N.archive_read_set_options(o.A, "tar:hdrcharset=UTF-8");
        if (!string.IsNullOrEmpty(Sifre)) N.archive_read_add_passphrase(o.A, Sifre);
        if (N.archive_read_open_filename_w(o.A, Yol, 1 << 20) != OK)
        {
            var h = Hata(o.A);
            o.Dispose();
            throw new ArsivHatasi(Turkcele(h));
        }
        return o;
    }

    static string Hata(IntPtr a)
    {
        var p = N.archive_error_string(a);
        return p == IntPtr.Zero ? "Arşiv okunamadı." : Marshal.PtrToStringUTF8(p) ?? "Arşiv okunamadı.";
    }

    static bool SifreHatasi(string h) => !Desteksiz(h) && (h.Contains("assphrase", StringComparison.OrdinalIgnoreCase) || h.Contains("encrypt", StringComparison.OrdinalIgnoreCase) || h.Contains("decrypt", StringComparison.OrdinalIgnoreCase));

    /// <summary>libarchive bazı şifreleme türlerini (ör. şifreli 7z) hiç açamıyor; şifre sormak sonsuz döngü olur.</summary>
    static bool Desteksiz(string h) => h.Contains("not supported", StringComparison.OrdinalIgnoreCase) || h.Contains("unsupported", StringComparison.OrdinalIgnoreCase);

    static string Turkcele(string h) => Desteksiz(h) && h.Contains("encrypt", StringComparison.OrdinalIgnoreCase)
        ? "Bu arşivin şifreleme türü henüz desteklenmiyor (şifreli 7z)."
        : h;

    string KayitYolu(IntPtr e)
    {
        var w = N.archive_entry_pathname_w(e);
        var s = w != IntPtr.Zero ? Marshal.PtrToStringUni(w) : null;
        if (s == null)
        {
            var u = N.archive_entry_pathname_utf8(e);
            s = u != IntPtr.Zero ? Marshal.PtrToStringUTF8(u) : null;
        }
        if (_ham || string.IsNullOrEmpty(s) || s == "data")
            s = Path.GetFileNameWithoutExtension(Yol); // "notlar.txt.gz" -> "notlar.txt"
        return s!.Replace('\\', '/').TrimEnd('/');
    }

    public override List<Kayit> Listele(CancellationToken iptal = default)
    {
        try { return ListeleIc(iptal); }
        catch (ArsivHatasi h) when (!_ham && h.Message.Contains("Unrecognized archive format", StringComparison.OrdinalIgnoreCase))
        {
            _ham = true; // .gz/.xz/.zst/.bz2 tek dosya
            return ListeleIc(iptal);
        }
    }

    List<Kayit> ListeleIc(CancellationToken iptal)
    {
        var liste = new List<Kayit>();
        using var o = Baslat();
        int sira = 0;
        while (true)
        {
            iptal.ThrowIfCancellationRequested();
            int r = N.archive_read_next_header(o.A, out var e);
            if (r == EOF) break;
            if (r < WARN)
            {
                var h = Hata(o.A);
                if (SifreHatasi(h)) throw new SifreGerekliException(h, !string.IsNullOrEmpty(Sifre));
                throw new ArsivHatasi(Turkcele(h));
            }
            uint tur = N.archive_entry_filetype(e) & IFMT;
            long mt = N.archive_entry_mtime_is_set(e) != 0 ? N.archive_entry_mtime(e) : 0;
            liste.Add(new Kayit
            {
                Yol = KayitYolu(e),
                Boyut = N.archive_entry_size_is_set(e) != 0 ? N.archive_entry_size(e) : 0,
                Tarih = mt > 0 ? DateTimeOffset.FromUnixTimeSeconds(mt).LocalDateTime : null,
                Klasor = tur == IFDIR,
                Baglanti = tur == IFLNK || N.archive_entry_hardlink_w(e) != IntPtr.Zero,
                Sifreli = N.archive_entry_is_encrypted(e) != 0,
                Sira = sira++,
            });
            // Başlıkları okumak için veriyi açmaya gerek yok; ama bazı biçimlerde atlama da okuma demek.
            N.archive_read_data_skip(o.A);
        }
        return liste;
    }

    public override void Cikar(IReadOnlyCollection<Kayit>? secili, string hedef, IProgress<Ilerleme>? ilerleme, CancellationToken iptal)
    {
        var istenen = secili == null ? null : new HashSet<int>(secili.Select(k => k.Sira));
        long toplam = secili?.Sum(k => k.Boyut) ?? 0;
        int dosyaSayisi = secili?.Count(k => !k.Klasor) ?? 0;
        if (secili == null)
        {
            var hepsi = Listele(iptal);
            toplam = hepsi.Sum(k => k.Boyut);
            dosyaSayisi = hepsi.Count(k => !k.Klasor);
        }

        Directory.CreateDirectory(hedef);
        var tampon = new byte[1 << 20];
        long yazilan = 0;
        int dosyaNo = 0, sira = 0;
        var sonBildirim = Environment.TickCount64;
        var tarihler = new List<(string yol, DateTime t)>();

        using var o = Baslat();
        while (true)
        {
            iptal.ThrowIfCancellationRequested();
            int r = N.archive_read_next_header(o.A, out var e);
            if (r == EOF) break;
            if (r < WARN) throw HataNesnesi(o.A);
            int buSira = sira++;
            if (istenen != null && !istenen.Contains(buSira)) { N.archive_read_data_skip(o.A); continue; }

            uint tur = N.archive_entry_filetype(e) & IFMT;
            var ad = KayitYolu(e);
            if (tur == IFLNK || N.archive_entry_hardlink_w(e) != IntPtr.Zero) { N.archive_read_data_skip(o.A); continue; }

            string yol;
            try { yol = Guvenli.HedefYol(hedef, ad); }
            catch (ArsivHatasi) { N.archive_read_data_skip(o.A); continue; }

            long mt = N.archive_entry_mtime_is_set(e) != 0 ? N.archive_entry_mtime(e) : 0;
            if (tur == IFDIR)
            {
                Directory.CreateDirectory(yol);
                if (mt > 0) tarihler.Add((yol, DateTimeOffset.FromUnixTimeSeconds(mt).LocalDateTime));
                continue;
            }

            dosyaNo++;
            Directory.CreateDirectory(Path.GetDirectoryName(yol)!);
            var gecici = yol + ".dafium-yarim";
            try
            {
                using (var f = new FileStream(gecici, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 16, FileOptions.SequentialScan))
                {
                    fixed (byte* p = tampon)
                    {
                        while (true)
                        {
                            iptal.ThrowIfCancellationRequested();
                            nint n = N.archive_read_data(o.A, p, tampon.Length);
                            if (n == 0) break;
                            if (n < 0) throw HataNesnesi(o.A);
                            f.Write(tampon, 0, (int)n);
                            yazilan += n;
                            if (ilerleme != null && Environment.TickCount64 - sonBildirim > 100)
                            {
                                sonBildirim = Environment.TickCount64;
                                ilerleme.Report(new Ilerleme(yazilan, toplam, ad, dosyaNo, dosyaSayisi));
                            }
                        }
                    }
                }
                File.Move(gecici, yol, true);
                if (mt > 0) try { File.SetLastWriteTime(yol, DateTimeOffset.FromUnixTimeSeconds(mt).LocalDateTime); } catch { }
            }
            catch
            {
                try { File.Delete(gecici); } catch { }
                throw;
            }
            ilerleme?.Report(new Ilerleme(yazilan, toplam, ad, dosyaNo, dosyaSayisi));
        }
        foreach (var (y, t) in tarihler) try { Directory.SetLastWriteTime(y, t); } catch { }
    }

    Exception HataNesnesi(IntPtr a)
    {
        var h = Hata(a);
        return SifreHatasi(h) ? new SifreGerekliException(h, !string.IsNullOrEmpty(Sifre)) : new ArsivHatasi(Turkcele(h));
    }

    public override byte[]? Oku(Kayit kayit, long sinir, CancellationToken iptal = default)
    {
        if (kayit.Klasor || kayit.Boyut > sinir) return null;
        using var o = Baslat();
        int sira = 0;
        while (true)
        {
            iptal.ThrowIfCancellationRequested();
            int r = N.archive_read_next_header(o.A, out _);
            if (r == EOF) return null;
            if (r < WARN) throw HataNesnesi(o.A);
            if (sira++ != kayit.Sira) { N.archive_read_data_skip(o.A); continue; }
            using var ms = new MemoryStream();
            var tampon = new byte[1 << 16];
            fixed (byte* p = tampon)
            {
                while (true)
                {
                    nint n = N.archive_read_data(o.A, p, tampon.Length);
                    if (n == 0) break;
                    if (n < 0) throw HataNesnesi(o.A);
                    ms.Write(tampon, 0, (int)n);
                    if (ms.Length > sinir) return null;
                }
            }
            return ms.ToArray();
        }
    }

    static class N
    {
        const string D = "archive.dll";
        [DllImport(D)] public static extern IntPtr archive_read_new();
        [DllImport(D)] public static extern int archive_read_free(IntPtr a);
        [DllImport(D)] public static extern int archive_read_support_filter_all(IntPtr a);
        [DllImport(D)] public static extern int archive_read_support_format_all(IntPtr a);
        [DllImport(D)] public static extern int archive_read_support_format_raw(IntPtr a);
        [DllImport(D)] public static extern int archive_read_set_options(IntPtr a, [MarshalAs(UnmanagedType.LPUTF8Str)] string s);
        [DllImport(D)] public static extern int archive_read_add_passphrase(IntPtr a, [MarshalAs(UnmanagedType.LPUTF8Str)] string p);
        [DllImport(D)] public static extern int archive_read_open_filename_w(IntPtr a, [MarshalAs(UnmanagedType.LPWStr)] string ad, nint blok);
        [DllImport(D)] public static extern int archive_read_next_header(IntPtr a, out IntPtr e);
        [DllImport(D)] public static extern nint archive_read_data(IntPtr a, byte* buf, nint boyut);
        [DllImport(D)] public static extern int archive_read_data_skip(IntPtr a);
        [DllImport(D)] public static extern IntPtr archive_error_string(IntPtr a);
        [DllImport(D)] public static extern IntPtr archive_entry_pathname_w(IntPtr e);
        [DllImport(D)] public static extern IntPtr archive_entry_pathname_utf8(IntPtr e);
        [DllImport(D)] public static extern IntPtr archive_entry_hardlink_w(IntPtr e);
        [DllImport(D)] public static extern long archive_entry_size(IntPtr e);
        [DllImport(D)] public static extern int archive_entry_size_is_set(IntPtr e);
        [DllImport(D)] public static extern long archive_entry_mtime(IntPtr e);
        [DllImport(D)] public static extern int archive_entry_mtime_is_set(IntPtr e);
        [DllImport(D)] public static extern uint archive_entry_filetype(IntPtr e);
        [DllImport(D)] public static extern int archive_entry_is_encrypted(IntPtr e);
    }
}
