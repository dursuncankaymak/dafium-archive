using System.IO;
using System.Runtime.InteropServices;

namespace DafiumArsiv.Motor;

public sealed unsafe class SevenZipArsiv : Arsiv
{
    public SevenZipArsiv(string yol) : base(yol) { }
    public override string MotorAdi => "7-Zip";

    static readonly Guid Clsid7z = new("23170F69-40C1-278A-1000-000110070000");
    static readonly Guid IidInArchive = new("23170F69-40C1-278A-0000-000600600000");

    const uint kpidPath = 3, kpidIsDir = 6, kpidSize = 7, kpidPackSize = 8, kpidMTime = 12, kpidEncrypted = 15;
    const int S_OK = 0, S_FALSE = 1, E_ABORT = unchecked((int)0x80004004);
    const int kOK = 0, kUnsupportedMethod = 1, kDataError = 2, kCRCError = 3, kUnexpectedEnd = 5, kHeadersError = 8, kWrongPassword = 9;

    static IntPtr _dll;
    static delegate* unmanaged<Guid*, Guid*, IntPtr*, int> _createObject;

    public static bool Var() => File.Exists(Path.Combine(AppContext.BaseDirectory, "7z.dll"));

    static IInArchive Yeni()
    {
        if (_createObject == null)
        {
            _dll = NativeLibrary.Load(Path.Combine(AppContext.BaseDirectory, "7z.dll"));
            _createObject = (delegate* unmanaged<Guid*, Guid*, IntPtr*, int>)NativeLibrary.GetExport(_dll, "CreateObject");
        }
        Guid c = Clsid7z, i = IidInArchive;
        IntPtr p;
        int hr = _createObject(&c, &i, &p);
        if (hr != S_OK || p == IntPtr.Zero) throw new ArsivHatasi(Dil.T("e_7z_init"));
        try { return (IInArchive)Marshal.GetObjectForIUnknown(p); }
        finally { Marshal.Release(p); }
    }

    sealed class Acik : IDisposable
    {
        public required IInArchive A;
        public required DosyaAkisi Akis;
        public required AcmaGeriCagri Cagri;
        public void Dispose()
        {
            try { A.Close(); } catch { }
            Marshal.FinalReleaseComObject(A);
            Akis.Dispose();
        }
    }

    Acik Ac(CancellationToken iptal)
    {
        var a = Yeni();
        var akis = new DosyaAkisi(Yol);
        var cagri = new AcmaGeriCagri { Sifre = Sifre, Iptal = iptal };
        ulong maks = 1 << 20;
        int hr = a.Open(akis, &maks, cagri);
        if (hr != S_OK)
        {
            try { a.Close(); } catch { }
            Marshal.FinalReleaseComObject(a);
            akis.Dispose();
            iptal.ThrowIfCancellationRequested();
            if (cagri.SifreIstendi)
                throw new SifreGerekliException(Dil.T(string.IsNullOrEmpty(Sifre) ? "e_encrypted" : "e_wrong_pw"), !string.IsNullOrEmpty(Sifre));
            throw new ArsivHatasi(hr == S_FALSE ? Dil.T("e_not_7z") : Dil.T("e_7z_open", hr));
        }
        return new Acik { A = a, Akis = akis, Cagri = cagri };
    }

    static PropVariant Ozellik(IInArchive a, uint i, uint id)
    {
        PropVariant v = default;
        a.GetProperty(i, id, &v);
        return v;
    }

    List<Kayit> Oku(IInArchive a)
    {
        uint n;
        a.GetNumberOfItems(&n);
        var liste = new List<Kayit>((int)n);
        for (uint i = 0; i < n; i++)
        {
            var yol = Ozellik(a, i, kpidPath);
            var klasor = Ozellik(a, i, kpidIsDir);
            var boyut = Ozellik(a, i, kpidSize);
            var paket = Ozellik(a, i, kpidPackSize);
            var zaman = Ozellik(a, i, kpidMTime);
            var sifre = Ozellik(a, i, kpidEncrypted);
            liste.Add(new Kayit
            {
                Yol = (yol.Metin() ?? Path.GetFileNameWithoutExtension(Yol)).Replace('\\', '/').TrimEnd('/'),
                Klasor = klasor.Mantik(),
                Boyut = (long)boyut.Sayi(),
                Paketli = paket.Tur == 0 ? -1 : (long)paket.Sayi(),
                Tarih = zaman.Zaman(),
                Sifreli = sifre.Mantik(),
                Sira = (int)i,
            });
            yol.Temizle(); zaman.Temizle();
        }
        return liste;
    }

    public override List<Kayit> Listele(CancellationToken iptal = default)
    {
        using var o = Ac(iptal);
        return Oku(o.A);
    }

    public override void Cikar(IReadOnlyCollection<Kayit>? secili, string hedef, IProgress<Ilerleme>? ilerleme, CancellationToken iptal)
    {
        using var o = Ac(iptal);
        var tumu = Oku(o.A);
        var istenen = secili == null ? tumu : tumu.Where(k => secili.Any(s => s.Sira == k.Sira)).ToList();
        Directory.CreateDirectory(hedef);
        var cagri = new CikarmaGeriCagri(this, istenen, hedef, ilerleme, iptal);
        var indeksler = istenen.Select(k => (uint)k.Sira).OrderBy(x => x).ToArray();
        int hr;
        fixed (uint* p = indeksler)
            hr = secili == null ? o.A.Extract(null, uint.MaxValue, 0, cagri) : o.A.Extract(p, (uint)indeksler.Length, 0, cagri);
        cagri.Kapat();
        iptal.ThrowIfCancellationRequested();
        if (cagri.Hata != null) throw cagri.Hata;
        if (hr != S_OK) throw new ArsivHatasi(Dil.T("e_extract_fail", hr));
    }

    public override byte[]? Oku(Kayit kayit, long sinir, CancellationToken iptal = default)
    {
        if (kayit.Klasor || kayit.Boyut > sinir) return null;
        using var o = Ac(iptal);
        var cagri = new CikarmaGeriCagri(this, [kayit], null, null, iptal) { Bellek = new MemoryStream(), Sinir = sinir };
        uint i = (uint)kayit.Sira;
        o.A.Extract(&i, 1, 0, cagri);
        if (cagri.SinirAsildi) return null;
        if (cagri.Hata != null) throw cagri.Hata;
        return cagri.Bellek.ToArray();
    }

    [ComVisible(true)]
    public sealed class AcmaGeriCagri : IArchiveOpenCallback, ICryptoGetTextPassword
    {
        public string? Sifre;
        public bool SifreIstendi;
        public CancellationToken Iptal;
        public int SetTotal(ulong* dosyalar, ulong* baytlar) => S_OK;
        public int SetCompleted(ulong* dosyalar, ulong* baytlar) => Iptal.IsCancellationRequested ? E_ABORT : S_OK;
        public int CryptoGetTextPassword(out IntPtr sifre)
        {
            SifreIstendi = true;
            if (string.IsNullOrEmpty(Sifre)) { sifre = IntPtr.Zero; return E_ABORT; }
            sifre = Marshal.StringToBSTR(Sifre);
            return S_OK;
        }
    }

    [ComVisible(true)]
    public sealed class CikarmaGeriCagri : IArchiveExtractCallback, ICryptoGetTextPassword
    {
        readonly SevenZipArsiv _s;
        readonly Dictionary<uint, Kayit> _kayitlar;
        readonly string? _hedef;
        readonly IProgress<Ilerleme>? _ilerleme;
        readonly CancellationToken _iptal;
        readonly long _toplam;
        readonly int _dosyaSayisi;
        int _dosyaNo;
        long _bitenler, _son;
        Kayit? _suanki;
        CiktiAkisi? _akis;
        string? _gecici, _yol;
        public Exception? Hata;
        public MemoryStream? Bellek;
        public long Sinir;
        public bool SinirAsildi;

        internal CikarmaGeriCagri(SevenZipArsiv s, List<Kayit> kayitlar, string? hedef, IProgress<Ilerleme>? ilerleme, CancellationToken iptal)
        {
            _s = s;
            _kayitlar = kayitlar.ToDictionary(k => (uint)k.Sira);
            _hedef = hedef;
            _ilerleme = ilerleme;
            _iptal = iptal;
            _toplam = kayitlar.Sum(k => k.Boyut);
            _dosyaSayisi = kayitlar.Count(k => !k.Klasor);
        }

        public int SetTotal(ulong toplam) => S_OK;
        public int SetCompleted(ulong* tamam) => _iptal.IsCancellationRequested ? E_ABORT : S_OK;

        public int GetStream(uint indeks, out ISequentialOutStream? cikis, int kip)
        {
            cikis = null;
            if (_iptal.IsCancellationRequested) return E_ABORT;
            if (!_kayitlar.TryGetValue(indeks, out var k)) return S_OK;
            // after a wrong password 7z keeps going in test mode, still blame the right entry
            _suanki = k;
            if (kip != 0) return S_OK; // 0 = kExtract
            if (Bellek != null)
            {
                if (k.Klasor) return S_OK;
                _akis = new CiktiAkisi(Bellek, this);
                cikis = _akis;
                return S_OK;
            }
            if (k.Baglanti) return S_OK;
            try { _yol = Guvenli.HedefYol(_hedef!, k.Yol); }
            catch (ArsivHatasi) { return S_OK; }
            if (k.Klasor) { Directory.CreateDirectory(_yol); return S_OK; }
            _dosyaNo++;
            Directory.CreateDirectory(Path.GetDirectoryName(_yol)!);
            _gecici = _yol + ".dafium-yarim";
            _akis = new CiktiAkisi(new FileStream(_gecici, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 16), this);
            cikis = _akis;
            return S_OK;
        }

        long _sonBildirim;
        public int PrepareOperation(int kip) => S_OK;

        public int SetOperationResult(int sonuc)
        {
            var akis = _akis;
            _akis = null;
            akis?.Kapat();
            var k = _suanki;
            _suanki = null;
            if (k == null) return S_OK;
            if (sonuc != kOK)
            {
                if (_gecici != null) try { File.Delete(_gecici); } catch { }
                _gecici = null;
                if (SinirAsildi) return E_ABORT;
                Hata ??= sonuc switch
                {
                    kWrongPassword => new SifreGerekliException(Dil.T("e_wrong_pw"), true),
                    kDataError or kCRCError when k.Sifreli => new SifreGerekliException(Dil.T("e_wrong_pw"), !string.IsNullOrEmpty(_s.Sifre)),
                    kUnsupportedMethod => new ArsivHatasi(Dil.T("e_method", k.Ad)),
                    kUnexpectedEnd => new ArsivHatasi(Dil.T("e_truncated", k.Ad)),
                    kHeadersError => new ArsivHatasi(Dil.T("e_headers")),
                    _ => new ArsivHatasi(Dil.T("e_corrupt_file", k.Ad)),
                };
                return E_ABORT;
            }
            if (_gecici != null && _yol != null)
            {
                File.Move(_gecici, _yol, true);
                if (k.Tarih is { } t) try { File.SetLastWriteTime(_yol, t); } catch { }
            }
            _gecici = null;
            if (!k.Klasor) _bitenler += k.Boyut;
            _son = 0;
            Bildir(k, true);
            return S_OK;
        }

        public void Bildir(Kayit k, bool zorla)
        {
            if (_ilerleme == null) return;
            if (!zorla && Environment.TickCount64 - _sonBildirim < 100) return;
            _sonBildirim = Environment.TickCount64;
            _ilerleme.Report(new Ilerleme(_bitenler + _son, _toplam, k.Yol, _dosyaNo, _dosyaSayisi));
        }

        public void YazildiBildir(long n)
        {
            _son += n;
            if (_suanki != null) Bildir(_suanki, false);
        }

        public void Kapat()
        {
            _akis?.Kapat();
            if (_gecici != null) try { File.Delete(_gecici); } catch { }
        }

        public int CryptoGetTextPassword(out IntPtr sifre)
        {
            if (string.IsNullOrEmpty(_s.Sifre))
            {
                Hata ??= new SifreGerekliException(Dil.T("e_encrypted"), false);
                sifre = IntPtr.Zero;
                return E_ABORT;
            }
            sifre = Marshal.StringToBSTR(_s.Sifre);
            return S_OK;
        }
    }

    [ComVisible(true)]
    public sealed class CiktiAkisi(Stream hedef, CikarmaGeriCagri cagri) : ISequentialOutStream
    {
        bool _kapali;
        public int Write(byte* veri, uint boyut, uint* yazilan)
        {
            try
            {
                hedef.Write(new ReadOnlySpan<byte>(veri, (int)boyut));
                if (yazilan != null) *yazilan = boyut;
                if (cagri.Bellek != null && cagri.Bellek.Length > cagri.Sinir) { cagri.SinirAsildi = true; return E_ABORT; }
                cagri.YazildiBildir(boyut);
                return S_OK;
            }
            catch (Exception ex)
            {
                cagri.Hata ??= new ArsivHatasi(Dil.T("e_disk", ex.Message));
                return E_ABORT;
            }
        }
        public void Kapat()
        {
            if (_kapali) return;
            _kapali = true;
            if (hedef is FileStream) hedef.Dispose();
        }
    }

    [ComVisible(true)]
    public sealed class DosyaAkisi(string yol) : IInStream, IDisposable
    {
        readonly FileStream _f = new(yol, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 1 << 16);
        public int Read(byte* veri, uint boyut, uint* okunan)
        {
            int n = _f.Read(new Span<byte>(veri, (int)boyut));
            if (okunan != null) *okunan = (uint)n;
            return S_OK;
        }
        public int Seek(long konum, uint koken, ulong* yeni)
        {
            long p = _f.Seek(konum, (SeekOrigin)koken);
            if (yeni != null) *yeni = (ulong)p;
            return S_OK;
        }
        public void Dispose() => _f.Dispose();
    }

    [ComImport, Guid("23170F69-40C1-278A-0000-000600600000"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IInArchive
    {
        [PreserveSig] int Open(IInStream akis, ulong* maksBaslangic, IArchiveOpenCallback cagri);
        [PreserveSig] int Close();
        [PreserveSig] int GetNumberOfItems(uint* sayi);
        [PreserveSig] int GetProperty(uint indeks, uint ozellik, PropVariant* deger);
        [PreserveSig] int Extract(uint* indeksler, uint sayi, int test, IArchiveExtractCallback cagri);
        [PreserveSig] int GetArchiveProperty(uint ozellik, PropVariant* deger);
        [PreserveSig] int GetNumberOfProperties(uint* sayi);
        [PreserveSig] int GetPropertyInfo(uint indeks, IntPtr* ad, uint* ozellik, ushort* tur);
        [PreserveSig] int GetNumberOfArchiveProperties(uint* sayi);
        [PreserveSig] int GetArchivePropertyInfo(uint indeks, IntPtr* ad, uint* ozellik, ushort* tur);
    }

    [ComImport, Guid("23170F69-40C1-278A-0000-000300030000"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IInStream
    {
        [PreserveSig] int Read(byte* veri, uint boyut, uint* okunan);
        [PreserveSig] int Seek(long konum, uint koken, ulong* yeni);
    }

    [ComImport, Guid("23170F69-40C1-278A-0000-000300020000"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface ISequentialOutStream
    {
        [PreserveSig] int Write(byte* veri, uint boyut, uint* yazilan);
    }

    [ComImport, Guid("23170F69-40C1-278A-0000-000600100000"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IArchiveOpenCallback
    {
        [PreserveSig] int SetTotal(ulong* dosyalar, ulong* baytlar);
        [PreserveSig] int SetCompleted(ulong* dosyalar, ulong* baytlar);
    }

    [ComImport, Guid("23170F69-40C1-278A-0000-000600200000"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IArchiveExtractCallback
    {
        [PreserveSig] int SetTotal(ulong toplam);
        [PreserveSig] int SetCompleted(ulong* tamam);
        [PreserveSig] int GetStream(uint indeks, out ISequentialOutStream? cikis, int kip);
        [PreserveSig] int PrepareOperation(int kip);
        [PreserveSig] int SetOperationResult(int sonuc);
    }

    [ComImport, Guid("23170F69-40C1-278A-0000-000500100000"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface ICryptoGetTextPassword
    {
        [PreserveSig] int CryptoGetTextPassword(out IntPtr sifre);
    }

    [StructLayout(LayoutKind.Explicit, Size = 24)]
    public struct PropVariant
    {
        [FieldOffset(0)] public ushort Tur;
        [FieldOffset(8)] public long Uzun;
        [FieldOffset(8)] public IntPtr Isaret;
        [FieldOffset(8)] public short Kisa;

        const ushort VT_BOOL = 11, VT_BSTR = 8, VT_UI4 = 19, VT_UI8 = 21, VT_FILETIME = 64, VT_I8 = 20, VT_UI2 = 18;

        public string? Metin() => Tur == VT_BSTR && Isaret != IntPtr.Zero ? Marshal.PtrToStringBSTR(Isaret) : null;
        public bool Mantik() => Tur == VT_BOOL && Kisa != 0;
        public ulong Sayi() => Tur switch { VT_UI8 or VT_I8 => (ulong)Uzun, VT_UI4 => (uint)Uzun, VT_UI2 => (ushort)Uzun, _ => 0 };
        public DateTime? Zaman()
        {
            if (Tur != VT_FILETIME || Uzun <= 0) return null;
            try { return DateTime.FromFileTime(Uzun); } catch { return null; }
        }
        public void Temizle() { if (Tur == VT_BSTR && Isaret != IntPtr.Zero) Marshal.FreeBSTR(Isaret); Tur = 0; }
    }
}
