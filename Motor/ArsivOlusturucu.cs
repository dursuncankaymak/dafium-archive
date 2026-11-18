using System.IO;
using System.Runtime.InteropServices;

namespace DafiumArsiv.Motor;

public enum ArsivBicimi { Zip, YediZ, TarZst }
public enum Seviye { Hizli, Normal, EnIyi }

public static unsafe class ArsivOlusturucu
{
    public static string Uzanti(ArsivBicimi b) => b switch { ArsivBicimi.Zip => ".zip", ArsivBicimi.YediZ => ".7z", _ => ".tar.zst" };

    public static void Olustur(IReadOnlyList<string> kaynaklar, string hedef, ArsivBicimi bicim, Seviye seviye, string? sifre,
        IProgress<Ilerleme>? ilerleme, CancellationToken iptal)
    {
        var ogeler = new List<(string disk, string ad, bool klasor)>();
        foreach (var k in kaynaklar)
        {
            var tam = Path.GetFullPath(k);
            if (Directory.Exists(tam))
            {
                var kok = Path.GetDirectoryName(tam.TrimEnd(Path.DirectorySeparatorChar)) ?? tam;
                ogeler.Add((tam, Goreli(kok, tam), true));
                foreach (var d in Directory.EnumerateFileSystemEntries(tam, "*", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint }))
                    ogeler.Add((d, Goreli(kok, d), Directory.Exists(d)));
            }
            else if (File.Exists(tam)) ogeler.Add((tam, Path.GetFileName(tam), false));
        }
        var hedefTam = Path.GetFullPath(hedef);
        // don't pack the archive into itself
        ogeler.RemoveAll(o => string.Equals(o.disk, hedefTam, StringComparison.OrdinalIgnoreCase));
        if (ogeler.Count == 0) throw new ArsivHatasi(Dil.T("e_nothing"));

        long toplam = ogeler.Where(o => !o.klasor).Sum(o => { try { return new FileInfo(o.disk).Length; } catch { return 0L; } });
        int dosyaSayisi = ogeler.Count(o => !o.klasor);

        var gecici = hedefTam + ".dafium-yarim";
        var a = N.archive_write_new();
        try
        {
            Kur(a, bicim, seviye, sifre);
            if (N.archive_write_open_filename_w(a, gecici) != 0) throw new ArsivHatasi(Hata(a));

            var tampon = new byte[1 << 20];
            long yazilan = 0, son = 0;
            int no = 0;
            foreach (var (disk, ad, klasor) in ogeler)
            {
                iptal.ThrowIfCancellationRequested();
                var e = N.archive_entry_new();
                try
                {
                    var bilgi = klasor ? (FileSystemInfo)new DirectoryInfo(disk) : new FileInfo(disk);
                    N.archive_entry_copy_pathname_w(e, ad + (klasor ? "/" : ""));
                    N.archive_entry_set_filetype(e, klasor ? 0x4000u : 0x8000u);
                    N.archive_entry_set_perm(e, klasor ? 0x1EDu : 0x1A4u); // 0755 / 0644
                    N.archive_entry_set_mtime(e, new DateTimeOffset(bilgi.LastWriteTimeUtc).ToUnixTimeSeconds(), 0);
                    long boy = klasor ? 0 : ((FileInfo)bilgi).Length;
                    N.archive_entry_set_size(e, boy);
                    if (N.archive_write_header(a, e) < -20) throw new ArsivHatasi(Hata(a));
                    if (klasor) continue;
                    no++;
                    using var f = new FileStream(disk, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 1 << 16, FileOptions.SequentialScan);
                    int n;
                    fixed (byte* p = tampon)
                    {
                        while ((n = f.Read(tampon, 0, tampon.Length)) > 0)
                        {
                            iptal.ThrowIfCancellationRequested();
                            if (N.archive_write_data(a, p, n) < 0) throw new ArsivHatasi(Hata(a));
                            yazilan += n;
                            if (ilerleme != null && Environment.TickCount64 - son > 100)
                            {
                                son = Environment.TickCount64;
                                ilerleme.Report(new Ilerleme(yazilan, toplam, ad, no, dosyaSayisi));
                            }
                        }
                    }
                }
                finally { N.archive_entry_free(e); }
            }
            if (N.archive_write_close(a) != 0) throw new ArsivHatasi(Hata(a));
            N.archive_write_free(a);
            a = IntPtr.Zero;
            File.Move(gecici, hedefTam, true);
            ilerleme?.Report(new Ilerleme(toplam, toplam, Path.GetFileName(hedefTam), dosyaSayisi, dosyaSayisi));
        }
        catch
        {
            if (a != IntPtr.Zero) { N.archive_write_free(a); a = IntPtr.Zero; }
            try { File.Delete(gecici); } catch { }
            throw;
        }
        finally
        {
            if (a != IntPtr.Zero) N.archive_write_free(a);
        }
    }

    static string Goreli(string kok, string yol) => Path.GetRelativePath(kok, yol).Replace('\\', '/');

    static void Kur(IntPtr a, ArsivBicimi bicim, Seviye seviye, string? sifre)
    {
        switch (bicim)
        {
            case ArsivBicimi.Zip:
                N.archive_write_set_format_zip(a);
                Secenek(a, "zip:compression=deflate");
                Secenek(a, "zip:compression-level=" + (seviye switch { Seviye.Hizli => 1, Seviye.Normal => 6, _ => 9 }));
                if (!string.IsNullOrEmpty(sifre))
                {
                    Secenek(a, "zip:encryption=aes256");
                    if (N.archive_write_set_passphrase(a, sifre) != 0) throw new ArsivHatasi(Hata(a));
                }
                // utf-8 flag so 7-zip / explorer show turkish names right
                Secenek(a, "zip:hdrcharset=UTF-8");
                break;
            case ArsivBicimi.YediZ:
                if (!string.IsNullOrEmpty(sifre)) throw new ArsivHatasi(Dil.T("e_pw_zip7"));
                N.archive_write_set_format_7zip(a);
                Secenek(a, "7zip:compression=lzma2");
                Secenek(a, "7zip:compression-level=" + (seviye switch { Seviye.Hizli => 1, Seviye.Normal => 5, _ => 9 }));
                break;
            default:
                if (!string.IsNullOrEmpty(sifre)) throw new ArsivHatasi(Dil.T("e_pw_zip"));
                N.archive_write_set_format_pax_restricted(a);
                N.archive_write_add_filter_zstd(a);
                Secenek(a, "zstd:compression-level=" + (seviye switch { Seviye.Hizli => 3, Seviye.Normal => 9, _ => 19 }));
                Secenek(a, "zstd:threads=" + Math.Max(1, Environment.ProcessorCount / 2));
                break;
        }
    }

    static void Secenek(IntPtr a, string s)
    {
        int r = N.archive_write_set_options(a, s);
        if (r < -20) throw new ArsivHatasi($"{s}: {Hata(a)}");
    }

    static string Hata(IntPtr a)
    {
        var p = N.archive_error_string(a);
        return p == IntPtr.Zero ? Dil.T("e_write") : Marshal.PtrToStringUTF8(p) ?? Dil.T("e_write");
    }

    static class N
    {
        const string D = "archive.dll";
        [DllImport(D)] public static extern IntPtr archive_write_new();
        [DllImport(D)] public static extern int archive_write_free(IntPtr a);
        [DllImport(D)] public static extern int archive_write_close(IntPtr a);
        [DllImport(D)] public static extern int archive_write_set_format_zip(IntPtr a);
        [DllImport(D)] public static extern int archive_write_set_format_7zip(IntPtr a);
        [DllImport(D)] public static extern int archive_write_set_format_pax_restricted(IntPtr a);
        [DllImport(D)] public static extern int archive_write_add_filter_zstd(IntPtr a);
        [DllImport(D)] public static extern int archive_write_set_options(IntPtr a, [MarshalAs(UnmanagedType.LPUTF8Str)] string s);
        [DllImport(D)] public static extern int archive_write_set_passphrase(IntPtr a, [MarshalAs(UnmanagedType.LPUTF8Str)] string p);
        [DllImport(D)] public static extern int archive_write_open_filename_w(IntPtr a, [MarshalAs(UnmanagedType.LPWStr)] string ad);
        [DllImport(D)] public static extern int archive_write_header(IntPtr a, IntPtr e);
        [DllImport(D)] public static extern nint archive_write_data(IntPtr a, byte* veri, nint boyut);
        [DllImport(D)] public static extern IntPtr archive_error_string(IntPtr a);
        [DllImport(D)] public static extern IntPtr archive_entry_new();
        [DllImport(D)] public static extern void archive_entry_free(IntPtr e);
        [DllImport(D)] public static extern void archive_entry_copy_pathname_w(IntPtr e, [MarshalAs(UnmanagedType.LPWStr)] string ad);
        [DllImport(D)] public static extern void archive_entry_set_filetype(IntPtr e, uint tur);
        [DllImport(D)] public static extern void archive_entry_set_perm(IntPtr e, uint izin);
        [DllImport(D)] public static extern void archive_entry_set_size(IntPtr e, long boyut);
        [DllImport(D)] public static extern void archive_entry_set_mtime(IntPtr e, long sn, long ns);
    }
}
