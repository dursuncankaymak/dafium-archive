using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DafiumArsiv.Motor;

/// <summary>RAR (1.5–7.x, RAR5 dahil) için RARLAB'ın ücretsiz UnRAR kütüphanesi (unrar.dll).
/// Şifreli, çok parçalı ve solid arşivleri destekler. Sadece açar; RAR oluşturamaz.</summary>
public sealed unsafe class UnrarArsiv : Arsiv
{
    public UnrarArsiv(string yol) : base(yol) { }
    public override string MotorAdi => "UnRAR";

    const int ERAR_SUCCESS = 0, ERAR_END_ARCHIVE = 10, ERAR_BAD_DATA = 12, ERAR_BAD_ARCHIVE = 13, ERAR_UNKNOWN_FORMAT = 14,
        ERAR_EOPEN = 15, ERAR_ECREATE = 16, ERAR_EREAD = 18, ERAR_EWRITE = 19, ERAR_MISSING_PASSWORD = 22, ERAR_BAD_PASSWORD = 24, ERAR_LARGE_DICT = 25;
    const int OM_LIST = 0, OM_EXTRACT = 1;
    const int RAR_SKIP = 0, RAR_TEST = 1, RAR_EXTRACT = 2;
    const uint RHDF_ENCRYPTED = 0x04, RHDF_DIRECTORY = 0x20;
    const uint UZUN_AD = 32768;

    /// <summary>Geri çağrının (callback) durumu; GCHandle ile UnRAR'a UserData olarak verilir.</summary>
    sealed class Baglam
    {
        public string? Sifre;
        public bool SifreIstendi;
        public string? EksikCilt;
        public CancellationToken Iptal;
        public Action<long>? Veri;
        public MemoryStream? Yakala;
        public long Sinir;
        public bool SinirAsildi;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    static int GeriCagri(uint mesaj, nint kullanici, nint p1, nint p2)
    {
        var b = (Baglam)GCHandle.FromIntPtr(kullanici).Target!;
        try
        {
            switch (mesaj)
            {
                case 1: // UCM_PROCESSDATA
                    if (b.Iptal.IsCancellationRequested) return -1;
                    if (b.Yakala != null)
                    {
                        if (b.Yakala.Length + p2 > b.Sinir) { b.SinirAsildi = true; return -1; }
                        b.Yakala.Write(new ReadOnlySpan<byte>((void*)p1, (int)p2));
                    }
                    b.Veri?.Invoke(p2);
                    return 1;
                case 4: // UCM_NEEDPASSWORDW
                    b.SifreIstendi = true;
                    if (string.IsNullOrEmpty(b.Sifre)) return -1;
                    var hedef = new Span<char>((void*)p1, (int)p2);
                    hedef.Clear();
                    b.Sifre.AsSpan(0, Math.Min(b.Sifre.Length, hedef.Length - 1)).CopyTo(hedef);
                    return 1;
                case 2: // UCM_NEEDPASSWORD (ANSI) — W sürümü kullanılıyor
                    return -1;
                case 3: // UCM_CHANGEVOLUMEW: P2 = 0 (bulunamadı, sor) / 1 (bilgi)
                    if (p2 == 0) { b.EksikCilt = Marshal.PtrToStringUni(p1); return -1; }
                    return 1;
                case 0: // UCM_CHANGEVOLUME (ANSI)
                    return p2 == 0 ? -1 : 1;
                case 5: // UCM_LARGEDICT: büyük sözlüklü arşive izin ver
                    return 1;
            }
        }
        catch { return -1; }
        return 1;
    }

    sealed class Oturum : IDisposable
    {
        public IntPtr H;
        public GCHandle Tutamac;
        public required Baglam B;
        public void Dispose()
        {
            if (H != IntPtr.Zero) { N.RARCloseArchive(H); H = IntPtr.Zero; }
            if (Tutamac.IsAllocated) Tutamac.Free();
        }
    }

    Oturum Ac(int kip, Baglam b)
    {
        var o = new Oturum { B = b, Tutamac = GCHandle.Alloc(b) };
        fixed (char* ad = Yol)
        {
            var veri = new OpenEx
            {
                ArcNameW = (IntPtr)ad,
                OpenMode = (uint)kip,
                Callback = (IntPtr)(delegate* unmanaged[Stdcall]<uint, nint, nint, nint, int>)&GeriCagri,
                UserData = GCHandle.ToIntPtr(o.Tutamac),
            };
            o.H = N.RAROpenArchiveEx(&veri);
            if (o.H == IntPtr.Zero || veri.OpenResult != ERAR_SUCCESS)
            {
                var kod = (int)veri.OpenResult;
                o.Dispose();
                throw HataNesnesi(kod, b, false);
            }
        }
        return o;
    }

    Exception HataNesnesi(int kod, Baglam b, bool sifreliKayit)
    {
        if (kod == ERAR_MISSING_PASSWORD || (b.SifreIstendi && string.IsNullOrEmpty(b.Sifre)))
            return new SifreGerekliException("Bu arşiv şifreli.", false);
        if (kod == ERAR_BAD_PASSWORD || (kod == ERAR_BAD_DATA && (sifreliKayit || b.SifreIstendi)))
            return new SifreGerekliException("Şifre yanlış.", true);
        if (b.EksikCilt != null)
            return new ArsivHatasi("Arşivin bir parçası eksik: " + Path.GetFileName(b.EksikCilt));
        return new ArsivHatasi(kod switch
        {
            ERAR_BAD_DATA => "Arşiv bozuk (veri hatası).",
            ERAR_BAD_ARCHIVE => "Bu dosya geçerli bir RAR arşivi değil.",
            ERAR_UNKNOWN_FORMAT => "Bilinmeyen RAR biçimi.",
            ERAR_EOPEN => "Arşiv açılamadı (başka bir program kullanıyor olabilir ya da bir parça eksik).",
            ERAR_ECREATE => "Dosya oluşturulamadı. Hedef klasöre yazma izni var mı?",
            ERAR_EREAD => "Arşiv okunurken hata oluştu.",
            ERAR_EWRITE => "Diske yazılamadı. Disk dolu olabilir.",
            ERAR_LARGE_DICT => "Arşiv çok büyük bellek istiyor.",
            _ => "RAR hatası (kod " + kod + ").",
        });
    }

    sealed class Baslik
    {
        public HeaderEx* P;
        public char* UzunAd;
        public Baslik()
        {
            P = (HeaderEx*)NativeMemory.AllocZeroed((nuint)sizeof(HeaderEx));
            UzunAd = (char*)NativeMemory.AllocZeroed(UZUN_AD * 2);
        }
        public void Hazirla()
        {
            NativeMemory.Clear(P, (nuint)sizeof(HeaderEx));
            P->FileNameEx = (IntPtr)UzunAd;
            P->FileNameExSize = UZUN_AD;
        }
        public string Ad
        {
            get
            {
                var s = P->FileNameEx != IntPtr.Zero && UzunAd[0] != 0 ? new string(UzunAd) : new string(P->FileNameW);
                return s.Replace('\\', '/').TrimEnd('/');
            }
        }
        public void Birak() { NativeMemory.Free(P); NativeMemory.Free(UzunAd); }
    }

    static DateTime? Zaman(HeaderEx* h)
    {
        long ft = ((long)h->MtimeHigh << 32) | h->MtimeLow;
        if (ft <= 0) return null;
        try { return DateTime.FromFileTime(ft); } catch { return null; }
    }

    public override List<Kayit> Listele(CancellationToken iptal = default)
    {
        var b = new Baglam { Sifre = Sifre, Iptal = iptal };
        var liste = new List<Kayit>();
        var bas = new Baslik();
        try
        {
            using var o = Ac(OM_LIST, b);
            int sira = 0;
            while (true)
            {
                iptal.ThrowIfCancellationRequested();
                bas.Hazirla();
                int r = N.RARReadHeaderEx(o.H, bas.P);
                if (r == ERAR_END_ARCHIVE) break;
                if (r != ERAR_SUCCESS) throw HataNesnesi(r, b, false);
                var h = bas.P;
                liste.Add(new Kayit
                {
                    Yol = bas.Ad,
                    Boyut = ((long)h->UnpSizeHigh << 32) | h->UnpSize,
                    Paketli = ((long)h->PackSizeHigh << 32) | h->PackSize,
                    Tarih = Zaman(h),
                    Klasor = (h->Flags & RHDF_DIRECTORY) != 0,
                    Sifreli = (h->Flags & RHDF_ENCRYPTED) != 0,
                    Baglanti = h->RedirType != 0,
                    Sira = sira++,
                });
                r = N.RARProcessFileW(o.H, RAR_SKIP, null, null);
                if (r != ERAR_SUCCESS) throw HataNesnesi(r, b, false);
            }
        }
        finally { bas.Birak(); }
        return liste;
    }

    public override void Cikar(IReadOnlyCollection<Kayit>? secili, string hedef, IProgress<Ilerleme>? ilerleme, CancellationToken iptal)
    {
        var tumu = secili ?? Listele(iptal);
        var istenen = secili == null ? null : new HashSet<string>(secili.Select(k => k.Yol), StringComparer.Ordinal);
        long toplam = tumu.Sum(k => k.Boyut);
        int dosyaSayisi = tumu.Count(k => !k.Klasor);
        long yazilan = 0;
        int dosyaNo = 0;
        string suanki = "";
        long son = 0;

        var b = new Baglam { Sifre = Sifre, Iptal = iptal };
        b.Veri = n =>
        {
            yazilan += n;
            if (ilerleme != null && Environment.TickCount64 - son > 100)
            {
                son = Environment.TickCount64;
                ilerleme.Report(new Ilerleme(yazilan, toplam, suanki, dosyaNo, dosyaSayisi));
            }
        };

        Directory.CreateDirectory(hedef);
        var bas = new Baslik();
        try
        {
            using var o = Ac(OM_EXTRACT, b);
            while (true)
            {
                iptal.ThrowIfCancellationRequested();
                bas.Hazirla();
                int r = N.RARReadHeaderEx(o.H, bas.P);
                if (r == ERAR_END_ARCHIVE) break;
                if (r != ERAR_SUCCESS) throw HataNesnesi(r, b, false);
                var h = bas.P;
                var ad = bas.Ad;
                bool klasor = (h->Flags & RHDF_DIRECTORY) != 0;
                bool sifreli = (h->Flags & RHDF_ENCRYPTED) != 0;

                bool al = (istenen == null || istenen.Contains(ad)) && h->RedirType == 0;
                string? yol = null;
                if (al) { try { yol = Guvenli.HedefYol(hedef, ad); } catch (ArsivHatasi) { al = false; } }

                if (!al || klasor)
                {
                    if (al && klasor) Directory.CreateDirectory(yol!);
                    r = N.RARProcessFileW(o.H, RAR_SKIP, null, null);
                    if (r != ERAR_SUCCESS) throw HataNesnesi(r, b, sifreli);
                    continue;
                }

                dosyaNo++;
                suanki = ad;
                Directory.CreateDirectory(Path.GetDirectoryName(yol)!);
                var gecici = yol + ".dafium-yarim";
                fixed (char* g = gecici)
                    r = N.RARProcessFileW(o.H, RAR_EXTRACT, null, g);
                if (r != ERAR_SUCCESS)
                {
                    try { File.Delete(gecici); } catch { }
                    iptal.ThrowIfCancellationRequested();
                    throw HataNesnesi(r, b, sifreli);
                }
                File.Move(gecici, yol!, true);
                ilerleme?.Report(new Ilerleme(yazilan, toplam, ad, dosyaNo, dosyaSayisi));
            }
        }
        finally { bas.Birak(); }
    }

    public override byte[]? Oku(Kayit kayit, long sinir, CancellationToken iptal = default)
    {
        if (kayit.Klasor || kayit.Boyut > sinir) return null;
        var b = new Baglam { Sifre = Sifre, Iptal = iptal };
        var bas = new Baslik();
        try
        {
            using var o = Ac(OM_EXTRACT, b);
            while (true)
            {
                iptal.ThrowIfCancellationRequested();
                bas.Hazirla();
                int r = N.RARReadHeaderEx(o.H, bas.P);
                if (r == ERAR_END_ARCHIVE) return null;
                if (r != ERAR_SUCCESS) throw HataNesnesi(r, b, false);
                if (bas.Ad != kayit.Yol)
                {
                    r = N.RARProcessFileW(o.H, RAR_SKIP, null, null);
                    if (r != ERAR_SUCCESS) throw HataNesnesi(r, b, false);
                    continue;
                }
                b.Yakala = new MemoryStream();
                b.Sinir = sinir;
                r = N.RARProcessFileW(o.H, RAR_TEST, null, null);
                if (b.SinirAsildi) return null;
                if (r != ERAR_SUCCESS) throw HataNesnesi(r, b, (bas.P->Flags & RHDF_ENCRYPTED) != 0);
                return b.Yakala.ToArray();
            }
        }
        finally { bas.Birak(); }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)] // dll.hpp: #pragma pack(push, 1)
    struct OpenEx
    {
        public IntPtr ArcName;
        public IntPtr ArcNameW;
        public uint OpenMode;
        public uint OpenResult;
        public IntPtr CmtBuf;
        public uint CmtBufSize, CmtSize, CmtState, Flags;
        public IntPtr Callback;
        public IntPtr UserData;
        public uint OpFlags;
        public IntPtr CmtBufW;
        public IntPtr MarkOfTheWeb;
        public uint MaxDiskWrite, MaxAllocate, MaxRuntime;
        public fixed uint Reserved[20];
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)] // dll.hpp: #pragma pack(push, 1)
    struct HeaderEx
    {
        public fixed byte ArcName[1024];
        public fixed char ArcNameW[1024];
        public fixed byte FileName[1024];
        public fixed char FileNameW[1024];
        public uint Flags, PackSize, PackSizeHigh, UnpSize, UnpSizeHigh, HostOS, FileCRC, FileTime, UnpVer, Method, FileAttr;
        public IntPtr CmtBuf;
        public uint CmtBufSize, CmtSize, CmtState, DictSize, HashType;
        public fixed byte Hash[32];
        public uint RedirType;
        public IntPtr RedirName;
        public uint RedirNameSize, DirTarget, MtimeLow, MtimeHigh, CtimeLow, CtimeHigh, AtimeLow, AtimeHigh;
        public IntPtr ArcNameEx;
        public uint ArcNameExSize;
        public IntPtr FileNameEx;
        public uint FileNameExSize;
        public fixed uint Reserved[982];
    }

    static class N
    {
        const string D = "unrar.dll";
        [DllImport(D)] public static extern IntPtr RAROpenArchiveEx(OpenEx* veri);
        [DllImport(D)] public static extern int RARCloseArchive(IntPtr h);
        [DllImport(D)] public static extern int RARReadHeaderEx(IntPtr h, HeaderEx* baslik);
        [DllImport(D)] public static extern int RARProcessFileW(IntPtr h, int islem, char* hedefKlasor, char* hedefAd);
    }
}
