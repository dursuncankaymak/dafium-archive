using System.Diagnostics;
using System.Security.Cryptography;
using DafiumArsiv.Motor;

var kok = args[0];
var arsivler = Path.Combine(kok, "arsivler");
var beklenen = File.ReadAllLines(Path.Combine(kok, "kaynak.sha"))
    .Select(l => (hash: l[..64], yol: l[66..].TrimStart('.', '/')))
    .ToDictionary(x => x.yol, x => x.hash);
int hata = 0;

void Sonuc(string ad, bool ok, string not = "") { Console.WriteLine($"{(ok ? "GEÇTİ" : "KALDI")}  {ad}  {not}"); if (!ok) hata++; }

Dictionary<string, string> Hashler(string klasor) => Directory.GetFiles(klasor, "*", SearchOption.AllDirectories)
    .ToDictionary(f => Path.GetRelativePath(klasor, f).Replace('\\', '/'), f => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(f))));

void TamTest(string dosya, string? sifre, bool listeSifreIster, bool cikarSifreIster)
{
    var yol = Path.Combine(arsivler, dosya);
    var hedef = Path.Combine(kok, "cikti", Path.GetFileNameWithoutExtension(dosya));
    if (Directory.Exists(hedef)) Directory.Delete(hedef, true);
    try
    {
        using var a = Arsiv.Ac(yol);
        List<Kayit> liste;
        try { liste = a.Listele(); if (listeSifreIster) { Sonuc(dosya + " listele-şifre-ister", false, "şifre istemedi"); } }
        catch (SifreGerekliException s) { Sonuc(dosya + " listele-şifre-ister", listeSifreIster && !s.Yanlis); a.Sifre = sifre; liste = a.Listele(); }
        if (sifre != null)
        {
            using var y = Arsiv.Ac(yol);
            y.Sifre = "yanlis";
            try
            {
                y.Listele();
                var h2 = Path.Combine(kok, "cikti", "yanlis-" + dosya);
                y.Cikar(null, h2, null, default);
                Sonuc(dosya + " yanlış-şifre", false, "hata vermedi");
            }
            catch (SifreGerekliException s) { Sonuc(dosya + " yanlış-şifre", s.Yanlis); }
            catch (Exception ex) { Sonuc(dosya + " yanlış-şifre", false, ex.GetType().Name + ": " + ex.Message); }
        }
        int dosyaSay = liste.Count(k => !k.Klasor);
        Sonuc(dosya + " liste", dosyaSay == beklenen.Count, $"{dosyaSay} dosya, motor {a.MotorAdi}");
        var sw = Stopwatch.StartNew();
        if (cikarSifreIster && a.Sifre == null)
        {
            try { a.Cikar(null, hedef, null, default); Sonuc(dosya + " çıkar-şifre-ister", false); }
            catch (SifreGerekliException) { Sonuc(dosya + " çıkar-şifre-ister", true); a.Sifre = sifre; }
        }
        long son = 0; int bildirim = 0;
        a.Cikar(null, hedef, new SyncProgress(i => { bildirim++; son = i.Yazilan; }), default);
        var h = Hashler(hedef);
        bool ayni = h.Count == beklenen.Count && beklenen.All(b => h.TryGetValue(b.Key, out var x) && x == b.Value);
        Sonuc(dosya + " çıkar+hash", ayni, $"{sw.ElapsedMilliseconds} ms, {bildirim} bildirim, {son} bayt");
        if (!ayni) Console.WriteLine("   bulunan: " + string.Join(", ", h.Keys));
        var t = liste.First(k => k.Yol.EndsWith("tekrar.txt"));
        var veri = a.Oku(t, 10 << 20);
        Sonuc(dosya + " oku", veri != null && Convert.ToHexStringLower(SHA256.HashData(veri)) == beklenen["alt klasör/derin/tekrar.txt"]);
        var sec = liste.Where(k => k.Yol.EndsWith("dafi.png")).ToList();
        var hedef2 = hedef + "-secili";
        a.Cikar(sec, hedef2, null, default);
        var h3 = Hashler(hedef2);
        Sonuc(dosya + " seçili", h3.Count == 1 && h3.ContainsKey("alt klasör/dafi.png"));
    }
    catch (Exception ex) { Sonuc(dosya, false, ex.GetType().Name + ": " + ex.Message); }
}

TamTest("normal5.rar", null, false, false);
TamTest("solid.rar", null, false, false);
TamTest("basliksifreli.rar", "Gizli123", true, true);
TamTest("verisifreli.rar", "Veri456", false, true);
TamTest("parcali.part1.rar", null, false, false);
TamTest("parcali.part3.rar", null, false, false);
TamTest("normal.zip", null, false, false);
TamTest("aes.zip", "Zip789", false, true);
TamTest("normal.7z", null, false, false);
TamTest("sifreli.7z", "Yedi000", true, true);
TamTest("verisifreli.7z", "Yedi111", false, true);
TamTest("paket.tar.gz", null, false, false);

foreach (var tek in new[] { "tek.txt.gz", "tek2.txt.xz" })
{
    try
    {
        using var a = Arsiv.Ac(Path.Combine(arsivler, tek));
        var l = a.Listele();
        var h = Path.Combine(kok, "cikti", tek);
        a.Cikar(null, h, null, default);
        var dosyalar = Directory.GetFiles(h);
        Sonuc(tek, l.Count == 1 && dosyalar.Length == 1 && Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(dosyalar[0]))) == beklenen["türkçe ad ğüş.txt"], $"ad: {l[0].Yol}");
    }
    catch (Exception ex) { Sonuc(tek, false, ex.Message); }
}

try
{
    using var a = Arsiv.Ac(Path.Combine(arsivler, "kacak.zip"));
    var h = Path.Combine(kok, "cikti", "kacak", "ic");
    a.Cikar(null, h, null, default);
    var disari = File.Exists(Path.Combine(kok, "cikti", "KACAK_DOSYA.txt")) || File.Exists(Path.Combine(kok, "KACAK_DOSYA.txt")) || File.Exists(@"C:\Windows\kotu2.txt");
    var icerdekiler = Directory.GetFiles(h, "*", SearchOption.AllDirectories).Select(f => Path.GetRelativePath(h, f)).ToList();
    Sonuc("zip-slip", !disari && icerdekiler.All(f => !f.StartsWith("..")), string.Join(" | ", icerdekiler));
}
catch (Exception ex) { Sonuc("zip-slip", false, ex.Message); }

try
{
    var gercek = Environment.GetEnvironmentVariable("DAFIUM_GERCEK_RAR") ?? "";
    if (File.Exists(gercek)) { using var a = Arsiv.Ac(gercek); var l = a.Listele(); Sonuc("gerçek rar listele", l.Count > 0, $"{l.Count} kayıt"); }
}
catch (Exception ex) { Sonuc("gerçek rar listele", false, ex.Message); }

try
{
    using var a = Arsiv.Ac(Path.Combine(arsivler, "solid.rar"));
    var cts = new CancellationTokenSource();
    cts.Cancel();
    try { a.Cikar(null, Path.Combine(kok, "cikti", "iptal"), null, cts.Token); Sonuc("iptal", false); }
    catch (OperationCanceledException) { Sonuc("iptal", true); }
}
catch (Exception ex) { Sonuc("iptal", false, ex.Message); }

Console.WriteLine(hata == 0 ? "\nHEPSİ GEÇTİ" : $"\n{hata} HATA");
return hata;

sealed class SyncProgress(Action<Ilerleme> a) : IProgress<Ilerleme> { public void Report(Ilerleme v) => a(v); }
