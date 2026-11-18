using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using DafiumArsiv.Motor;
using Microsoft.Win32;

namespace DafiumArsiv;

public partial class MainWindow : Window
{
    Arsiv? _arsiv;
    List<Kayit> _kayitlar = [];
    readonly Dictionary<string, Oge> _klasorler = new(StringComparer.Ordinal);
    string _konum = "";
    string _siraAlan = "Ad";
    bool _siraTers;
    CancellationTokenSource? _islemIptal;
    CancellationTokenSource? _onizlemeIptal;
    string? _sonHedef;
    bool _kapatBitince;
    Point _basNokta;
    bool _surukleniyor;

    const long OnizlemeResimSiniri = 40L << 20;
    const long OnizlemeYaziSiniri = 512L << 10;

    public MainWindow(string? acilacak, string? yanina = null, IReadOnlyList<string>? ziple = null)
    {
        InitializeComponent();
        MetinleriYenile();
        Dil.Degisti += MetinleriYenile;
        StateChanged += (_, _) => DurumDegisti();
        DragEnter += Surukle_Gir;
        DragOver += Surukle_Gir;
        DragLeave += (_, _) => SurukleKatman.Visibility = Visibility.Collapsed;
        Drop += Birak;
        Loaded += async (_, _) =>
        {
            Canlandir();
            if (yanina != null)
            {
                _kapatBitince = true;
                await AcAsync(yanina);
                if (_arsiv != null) await CikarAsync(null, YaninaHedef(_arsiv.Yol), sessiz: false);
                else Close();
            }
            else if (ziple is { Count: > 0 })
            {
                _kapatBitince = true;
                await HizliZipAsync(ziple);
            }
            else if (acilacak != null) await AcAsync(acilacak);
        };
    }

    void DurumDegisti()
    {
        Kok.Margin = WindowState == WindowState.Maximized ? new Thickness(7) : new Thickness(0);
        BuyukDugme.Content = WindowState == WindowState.Maximized ? "\uE923" : "\uE922";
    }

    void Kucult_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    void Buyut_Click(object sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    void Kapat_Click(object sender, RoutedEventArgs e) => Close();

    void Canlandir()
    {
        var kay = new DoubleAnimation(0, -10, TimeSpan.FromSeconds(2.4))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
        };
        DafiKay.BeginAnimation(TranslateTransform.YProperty, kay);
        DonenAci.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(0, 360, TimeSpan.FromSeconds(1.1)) { RepeatBehavior = RepeatBehavior.Forever });
        Isik2.BeginAnimation(OpacityProperty, new DoubleAnimation(0.25, 0.55, TimeSpan.FromSeconds(6)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever });
    }

    protected override void OnClosed(EventArgs e)
    {
        _islemIptal?.Cancel();
        _onizlemeIptal?.Cancel();
        _arsiv?.Dispose();
        GeciciTemizle();
        base.OnClosed(e);
    }

    void Surukle_Gir(object sender, DragEventArgs e)
    {
        bool dosya = e.Data.GetDataPresent(DataFormats.FileDrop) && IlerlemeKatman.Visibility != Visibility.Visible && !_surukleniyor;
        e.Effects = dosya ? DragDropEffects.Copy : DragDropEffects.None;
        SurukleKatman.Visibility = dosya ? Visibility.Visible : Visibility.Collapsed;
        e.Handled = true;
    }

    async void Birak(object sender, DragEventArgs e)
    {
        SurukleKatman.Visibility = Visibility.Collapsed;
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] { Length: > 0 } dosyalar) return;
        if (dosyalar.Length == 1 && File.Exists(dosyalar[0]) && Kabuk.ArsivUzantisi(dosyalar[0])) await AcAsync(dosyalar[0]);
        else await YeniArsivAsync(dosyalar);
    }

    async void Ac_Click(object sender, RoutedEventArgs e)
    {
        var d = new OpenFileDialog
        {
            Title = Dil.T("pick_archive"),
            Filter = Dil.T("filter_archives") + "|*.rar;*.zip;*.7z;*.tar;*.gz;*.tgz;*.xz;*.txz;*.zst;*.bz2;*.tbz2;*.lz4;*.iso;*.cab;*.001;*.jar;*.apk|" + Dil.T("filter_all") + "|*.*",
        };
        if (d.ShowDialog(this) == true) await AcAsync(d.FileName);
    }

    async Task AcAsync(string yol)
    {
        Yukleniyor(true, Dil.T("reading"));
        try
        {
            _onizlemeIptal?.Cancel();
            _arsiv?.Dispose();
            _arsiv = null;
            var arsiv = Arsiv.Ac(yol);
            string? sifre = null;
            bool yanlis = false;
            while (true)
            {
                arsiv.Sifre = sifre;
                try
                {
                    _kayitlar = await Task.Run(() => arsiv.Listele());
                    break;
                }
                catch (SifreGerekliException s)
                {
                    yanlis = s.Yanlis || yanlis;
                    Yukleniyor(false);
                    sifre = SifreSor(sifre != null);
                    if (sifre == null) { arsiv.Dispose(); return; }
                    Yukleniyor(true, Dil.T("trying_pw"));
                }
            }
            _arsiv = arsiv;
            AgacKur();
            _konum = "";
            Arama.Text = "";
            ArsivAdi.Text = Path.GetFileName(yol);
            ArsivCipi.Visibility = Visibility.Visible;
            Title = Path.GetFileName(yol) + " · " + Dil.T("app");
            BosEkran.Visibility = Visibility.Collapsed;
            AracCubugu.Visibility = KonumCubugu.Visibility = Icerik.Visibility = Visibility.Visible;
            MotorYazi.Text = Dil.T("engine", arsiv.MotorAdi);
            DurumYaz();
            Goster();
            OnizlemeSifirla();
        }
        catch (DllNotFoundException)
        {
            Uyar(Dil.T("no_engine"));
        }
        catch (Exception ex) when (ex is ArsivHatasi or IOException or UnauthorizedAccessException)
        {
            Uyar(Dil.T("cant_open", ex.Message));
        }
        finally
        {
            Yukleniyor(false);
        }
    }

    string? SifreSor(bool yanlis)
    {
        var p = new SifrePenceresi(yanlis) { Owner = this };
        return p.ShowDialog() == true ? p.Sifre : null;
    }

    void Uyar(string mesaj) => MessageBox.Show(this, mesaj, Dil.T("app"), MessageBoxButton.OK, MessageBoxImage.Warning);

    void Yukleniyor(bool acik, string yazi = "")
    {
        YukleniyorYazi.Text = yazi;
        YukleniyorKatman.Visibility = acik ? Visibility.Visible : Visibility.Collapsed;
    }

    void AgacKur()
    {
        _klasorler.Clear();
        var kok = new Oge { Ad = "", Yol = "", Klasor = true };
        _klasorler[""] = kok;

        Oge KlasorAl(string yol)
        {
            if (_klasorler.TryGetValue(yol, out var k)) return k;
            int i = yol.LastIndexOf('/');
            var ust = KlasorAl(i < 0 ? "" : yol[..i]);
            k = new Oge { Ad = i < 0 ? yol : yol[(i + 1)..], Yol = yol, Klasor = true };
            ust.Cocuklar.Add(k);
            _klasorler[yol] = k;
            return k;
        }

        foreach (var kayit in _kayitlar)
        {
            var yol = kayit.Yol.Trim('/');
            if (yol.Length == 0) continue;
            if (kayit.Klasor)
            {
                var k = KlasorAl(yol);
                k.Kayit = kayit;
                k.Tarih = kayit.Tarih;
                continue;
            }
            int i = yol.LastIndexOf('/');
            var ust = KlasorAl(i < 0 ? "" : yol[..i]);
            ust.Cocuklar.Add(new Oge
            {
                Ad = i < 0 ? yol : yol[(i + 1)..],
                Yol = yol,
                Kayit = kayit,
                Boyut = kayit.Boyut,
                Paketli = kayit.Paketli,
                Tarih = kayit.Tarih,
                Sifreli = kayit.Sifreli,
            });
        }

        foreach (var k in _klasorler.Values.OrderByDescending(k => k.Yol.Length))
        {
            if (k.Yol.Length == 0) continue;
            long b = 0, p = 0;
            bool pVar = true;
            foreach (var c in k.Cocuklar) { b += c.Boyut; if (c.Paketli < 0) pVar = false; else p += c.Paketli; }
            k.Boyut = b;
            k.Paketli = pVar && k.Cocuklar.Count > 0 ? p : -1;
            k.Sifreli = k.Cocuklar.Any(c => c.Sifreli);
        }
    }

    void Goster()
    {
        var arama = Arama.Text.Trim();
        IEnumerable<Oge> ogeler;
        if (arama.Length > 0)
        {
            ogeler = _klasorler.Values.Where(k => k.Yol.Length > 0)
                .Concat(_klasorler.Values.SelectMany(k => k.Cocuklar.Where(c => !c.Klasor)))
                .Where(o => o.Ad.Contains(arama, StringComparison.CurrentCultureIgnoreCase));
        }
        else
        {
            ogeler = _klasorler.TryGetValue(_konum, out var k) ? k.Cocuklar : [];
        }

        Func<Oge, object> anahtar = _siraAlan switch
        {
            "Boyut" => o => o.Boyut,
            "Paketli" => o => o.Paketli,
            "Tur" => o => o.Tur,
            "Tarih" => o => o.Tarih ?? DateTime.MinValue,
            _ => o => o.Ad,
        };
        var sirali = ogeler.OrderByDescending(o => o.Klasor);
        sirali = _siraAlan == "Ad"
            ? (_siraTers ? sirali.ThenByDescending(o => o.Ad, StringComparer.CurrentCultureIgnoreCase) : sirali.ThenBy(o => o.Ad, StringComparer.CurrentCultureIgnoreCase))
            : (_siraTers ? sirali.ThenByDescending(anahtar) : sirali.ThenBy(anahtar));
        Liste.ItemsSource = sirali.ToList();

        var parcalar = new List<KonumParcasi> { new() { Ad = _arsiv != null ? Path.GetFileName(_arsiv.Yol) : "", Yol = "", AyracGorunum = Visibility.Collapsed } };
        if (arama.Length > 0)
            parcalar.Add(new KonumParcasi { Ad = Dil.T("search_crumb", arama), Yol = _konum, AyracGorunum = Visibility.Visible });
        else if (_konum.Length > 0)
        {
            var birikim = "";
            foreach (var p in _konum.Split('/'))
            {
                birikim = birikim.Length == 0 ? p : birikim + "/" + p;
                parcalar.Add(new KonumParcasi { Ad = p, Yol = birikim, AyracGorunum = Visibility.Visible });
            }
        }
        Konum.ItemsSource = parcalar;
        YukariDugme.IsEnabled = _konum.Length > 0 || arama.Length > 0;
        SecimDegisti();
    }

    void Git(string yol)
    {
        _konum = yol;
        if (Arama.Text.Length > 0) Arama.Text = "";
        else Goster();
        if (Liste.Items.Count > 0) Liste.ScrollIntoView(Liste.Items[0]);
    }

    void Konum_Click(object sender, RoutedEventArgs e) => Git((string)((Button)sender).Tag);

    void Yukari_Click(object sender, RoutedEventArgs e)
    {
        if (Arama.Text.Length > 0) { Arama.Text = ""; return; }
        int i = _konum.LastIndexOf('/');
        Git(i < 0 ? "" : _konum[..i]);
    }

    void Arama_TextChanged(object sender, TextChangedEventArgs e)
    {
        AramaIpucu.Visibility = Arama.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (_arsiv != null) Goster();
    }

    void Baslik_Click(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not GridViewColumnHeader { Column: not null } b) return;
        var alan = b.Column == SutunBoyut ? "Boyut" : b.Column == SutunPaketli ? "Paketli" : b.Column == SutunTur ? "Tur" : b.Column == SutunTarih ? "Tarih" : "Ad";
        _siraTers = _siraAlan == alan && !_siraTers;
        _siraAlan = alan;
        Goster();
    }

    void DurumYaz()
    {
        int dosya = _kayitlar.Count(k => !k.Klasor);
        int klasor = _klasorler.Count - 1;
        long boyut = _kayitlar.Sum(k => k.Boyut);
        var s = Dil.T("status", Bicim.Sayi(dosya), Bicim.Sayi(klasor), Bicim.Boyut(boyut));
        long arsivBoyut = _arsiv != null && File.Exists(_arsiv.Yol) ? new FileInfo(_arsiv.Yol).Length : 0;
        if (arsivBoyut > 0 && boyut > 0)
            s += $" → {Bicim.Boyut(arsivBoyut)}" + Dil.T("pct_paren", Math.Round(100.0 * arsivBoyut / boyut));
        if (_kayitlar.Any(k => k.Sifreli)) s += Dil.T("status_enc");
        Durum.Text = s;
    }

    void Liste_SelectionChanged(object sender, SelectionChangedEventArgs e) => SecimDegisti();

    void SecimDegisti()
    {
        int n = Liste.SelectedItems.Count;
        CikarYazi.Text = n == 0 ? Dil.T("extract_all") : Dil.T("extract_sel", n);
        if (n == 1 && Liste.SelectedItem is Oge o) _ = OnizleAsync(o);
        else OnizlemeSifirla(n > 1 ? Dil.T("n_selected", n) : null);
    }

    void OnizlemeSifirla(string? baslik = null)
    {
        _onizlemeIptal?.Cancel();
        OnizlemeResim.Source = null;
        OnizlemeResim.Visibility = Visibility.Collapsed;
        OnizlemeYazi.Visibility = Visibility.Collapsed;
        OnizlemeYazi.Text = "";
        OnizlemeSimge.Visibility = Visibility.Visible;
        OnizlemeSimge.Text = "\uE7B8";
        OnizlemeSimge.Foreground = (Brush)FindResource("Vurgu2");
        OnizlemeDurum.Text = "";
        DosyaAcDugme.Visibility = Visibility.Collapsed;
        if (_arsiv == null) { OnizlemeAd.Text = ""; OnizlemeBilgi.Text = ""; return; }
        OnizlemeAd.Text = baslik ?? Path.GetFileName(_arsiv.Yol);
        OnizlemeBilgi.Text = Dil.T(baslik == null ? "prev_pick" : "prev_tip");
    }

    async Task OnizleAsync(Oge o)
    {
        OnizlemeSifirla();
        var iptal = _onizlemeIptal = new CancellationTokenSource();
        OnizlemeAd.Text = o.Ad;
        OnizlemeSimge.Text = o.Simge;
        OnizlemeSimge.Foreground = o.SimgeRenk;
        var bilgi = new StringBuilder(o.Tur);
        if (o.Boyut > 0 || !o.Klasor) bilgi.Append(" · ").Append(Bicim.Boyut(o.Boyut));
        if (o.Klasor) bilgi.Append(" · ").Append(Bicim.Sayi(o.Cocuklar.Count)).Append(Dil.T("n_items"));
        if (o.Tarih is { } t) bilgi.Append('\n').Append(t.ToString("f", Dil.Kultur));
        if (o.Sifreli) bilgi.Append('\n').Append(Dil.T("encrypted"));
        OnizlemeBilgi.Text = bilgi.ToString();
        if (o.Klasor || o.Kayit == null || _arsiv == null) return;
        DosyaAcDugme.Visibility = Visibility.Visible;

        bool resim = Turler.ResimMi(o.Ad) && o.Boyut <= OnizlemeResimSiniri;
        bool yazi = (Turler.YaziMi(o.Ad) || (Turler.Bul(o.Ad).Grup == "diger" && o.Boyut <= 64 << 10)) && o.Boyut <= OnizlemeYaziSiniri;
        if (!resim && !yazi) return;

        // debounce, don't load every item while scrolling with arrows
        try { await Task.Delay(140, iptal.Token); } catch (TaskCanceledException) { return; }
        OnizlemeDurum.Text = Dil.T("prev_loading");
        var arsiv = _arsiv;
        var kayit = o.Kayit;
        byte[]? veri;
        try
        {
            veri = await Task.Run(() => arsiv.Oku(kayit, resim ? OnizlemeResimSiniri : OnizlemeYaziSiniri, iptal.Token), iptal.Token);
        }
        catch (OperationCanceledException) { return; }
        catch (SifreGerekliException) { if (!iptal.IsCancellationRequested) OnizlemeDurum.Text = Dil.T("prev_pw"); return; }
        catch (Exception) { if (!iptal.IsCancellationRequested) OnizlemeDurum.Text = Dil.T("prev_fail"); return; }
        if (iptal.IsCancellationRequested) return;
        OnizlemeDurum.Text = "";
        if (veri == null) return;

        if (resim)
        {
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.DecodePixelWidth = 560;
                bmp.StreamSource = new MemoryStream(veri);
                bmp.EndInit();
                bmp.Freeze();
                OnizlemeResim.Source = bmp;
                OnizlemeResim.Visibility = Visibility.Visible;
                OnizlemeSimge.Visibility = Visibility.Collapsed;
                if (bmp.PixelWidth > 0) OnizlemeBilgi.Text += $"\n{GercekBoyut(veri)}";
            }
            catch { OnizlemeDurum.Text = Dil.T("prev_img_fail"); }
        }
        else if (MetinMi(veri))
        {
            OnizlemeYazi.Text = Coz(veri);
            OnizlemeYazi.Visibility = Visibility.Visible;
            OnizlemeSimge.Visibility = Visibility.Collapsed;
        }
    }

    static string GercekBoyut(byte[] veri)
    {
        try
        {
            var d = BitmapDecoder.Create(new MemoryStream(veri), BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
            var f = d.Frames[0];
            return Dil.T("pixels", f.PixelWidth, f.PixelHeight);
        }
        catch { return ""; }
    }

    static bool MetinMi(byte[] v)
    {
        int n = Math.Min(v.Length, 8192), kotu = 0;
        for (int i = 0; i < n; i++)
        {
            byte b = v[i];
            if (b == 0) return false;
            if (b < 9 || (b > 13 && b < 32)) kotu++;
        }
        return kotu <= n / 50;
    }

    static string Coz(byte[] v)
    {
        try { return new UTF8Encoding(false, true).GetString(v); }
        catch (DecoderFallbackException)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding(1254).GetString(v); // old turkish txt files (windows-1254)
        }
    }

    async void Liste_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject d && ItemsControl.ContainerFromElement(Liste, d) is ListViewItem { Content: Oge o })
            await AcOge(o);
    }

    async void Liste_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Liste.SelectedItem is Oge o) { e.Handled = true; await AcOge(o); }
        else if (e.Key == Key.Back) { e.Handled = true; Yukari_Click(sender, e); }
    }

    async Task AcOge(Oge o)
    {
        if (o.Klasor) { Git(o.Yol); return; }
        await DosyayiAcAsync(o);
    }

    async void DosyaAc_Click(object sender, RoutedEventArgs e)
    {
        if (Liste.SelectedItem is Oge { Klasor: false } o) await DosyayiAcAsync(o);
    }

    static string GeciciKok => Path.Combine(Path.GetTempPath(), "DafiumArsiv");

    async Task DosyayiAcAsync(Oge o)
    {
        if (o.Kayit == null || _arsiv == null) return;
        var hedef = Path.Combine(GeciciKok, Guid.NewGuid().ToString("N")[..8]);
        if (await CikarAsync([o.Kayit], hedef, sessiz: true))
        {
            var yol = Guvenli.HedefYol(hedef, o.Kayit.Yol);
            try { Process.Start(new ProcessStartInfo(yol) { UseShellExecute = true }); }
            catch (Exception ex) { Uyar(Dil.T("cant_open_file", ex.Message)); }
        }
    }

    static void GeciciTemizle()
    {
        try
        {
            if (!Directory.Exists(GeciciKok)) return;
            foreach (var d in Directory.GetDirectories(GeciciKok))
                // leftovers from older runs
                if (Directory.GetLastWriteTime(d) < DateTime.Now.AddDays(-1))
                    try { Directory.Delete(d, true); } catch { }
        }
        catch { }
    }

    List<Kayit>? SeciliKayitlar()
    {
        if (Liste.SelectedItems.Count == 0) return null;
        var sonuc = new Dictionary<int, Kayit>();
        foreach (Oge o in Liste.SelectedItems)
        {
            if (!o.Klasor) { if (o.Kayit != null) sonuc[o.Kayit.Sira] = o.Kayit; continue; }
            var on = o.Yol + "/";
            foreach (var k in _kayitlar)
                if (k.Yol == o.Yol || k.Yol.StartsWith(on, StringComparison.Ordinal)) sonuc[k.Sira] = k;
        }
        return sonuc.Values.OrderBy(k => k.Sira).ToList();
    }

    async void Cikar_Click(object sender, RoutedEventArgs e)
    {
        if (_arsiv == null) return;
        var d = new OpenFolderDialog
        {
            Title = Dil.T("where_extract"),
            InitialDirectory = Path.GetDirectoryName(_arsiv.Yol),
        };
        if (d.ShowDialog(this) != true) return;
        await CikarAsync(SeciliKayitlar(), d.FolderName, sessiz: false);
    }

    async void Buraya_Click(object sender, RoutedEventArgs e)
    {
        if (_arsiv == null) return;
        await CikarAsync(SeciliKayitlar(), YaninaHedef(_arsiv.Yol), sessiz: false);
    }

    static string YaninaHedef(string arsivYolu)
    {
        var ad = Path.GetFileName(arsivYolu);
        ad = System.Text.RegularExpressions.Regex.Replace(ad, @"(\.part\d+)?\.(rar|zip|7z|tar|tgz|txz|tbz2|gz|xz|zst|bz2|lz4|iso|cab|001)$", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        ad = System.Text.RegularExpressions.Regex.Replace(ad, @"\.tar$", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (ad.Length == 0) ad = Dil.T("extracted");
        return Path.Combine(Path.GetDirectoryName(arsivYolu)!, ad);
    }

    async Task<bool> CikarAsync(List<Kayit>? secili, string hedef, bool sessiz)
    {
        if (_arsiv == null) return false;
        var arsiv = _arsiv;
        _islemIptal = new CancellationTokenSource();
        var iptal = _islemIptal.Token;
        IlerlemeBaslat(Dil.T(sessiz ? "opening" : "extracting"));
        var saat = Stopwatch.StartNew();
        var ilerleme = new Progress<Ilerleme>(i => IlerlemeGuncelle(i, saat.Elapsed.TotalSeconds));
        try
        {
            while (true)
            {
                try
                {
                    await Task.Run(() => arsiv.Cikar(secili, hedef, ilerleme, iptal), iptal);
                    break;
                }
                catch (SifreGerekliException s)
                {
                    var yeni = SifreSor(s.Yanlis);
                    if (yeni == null) { IlerlemeKapat(); return false; }
                    arsiv.Sifre = yeni;
                    saat.Restart();
                }
            }
            _sonHedef = hedef;
            if (sessiz) { IlerlemeKapat(); return true; }
            IlerlemeBitti(saat.Elapsed.TotalSeconds);
            return true;
        }
        catch (OperationCanceledException)
        {
            IlerlemeKapat();
            if (_kapatBitince) Close();
            return false;
        }
        catch (Exception ex) when (ex is ArsivHatasi or IOException or UnauthorizedAccessException)
        {
            IlerlemeKapat();
            Uyar(Dil.T("cant_extract", ex.Message));
            if (_kapatBitince) Close();
            return false;
        }
    }

    async void YeniArsiv_Click(object sender, RoutedEventArgs e) => await YeniArsivAsync([]);

    async Task YeniArsivAsync(IEnumerable<string> baslangic)
    {
        var p = new YeniArsivPenceresi(baslangic) { Owner = this };
        if (p.ShowDialog() != true) return;
        await OlusturAsync(p.Secilenler, p.Hedef, p.SecilenBicim, p.SecilenSeviye, p.Sifre);
    }

    async Task HizliZipAsync(IReadOnlyList<string> yollar)
    {
        var ilk = yollar[0].TrimEnd('\\');
        var klasor = Path.GetDirectoryName(ilk)!;
        var ad = Directory.Exists(ilk) ? Path.GetFileName(ilk) : Path.GetFileNameWithoutExtension(ilk);
        var hedef = Path.Combine(klasor, ad + ".zip");
        for (int i = 2; File.Exists(hedef); i++) hedef = Path.Combine(klasor, $"{ad} ({i}).zip");
        await OlusturAsync(yollar, hedef, ArsivBicimi.Zip, Seviye.Normal, null);
    }

    async Task OlusturAsync(IReadOnlyList<string> kaynaklar, string hedef, ArsivBicimi bicim, Seviye seviye, string? sifre)
    {
        _islemIptal = new CancellationTokenSource();
        var iptal = _islemIptal.Token;
        IlerlemeBaslat(Dil.T("creating"));
        var saat = Stopwatch.StartNew();
        var ilerleme = new Progress<Ilerleme>(i => IlerlemeGuncelle(i, saat.Elapsed.TotalSeconds));
        try
        {
            await Task.Run(() => ArsivOlusturucu.Olustur(kaynaklar, hedef, bicim, seviye, sifre, ilerleme, iptal), iptal);
            _sonHedef = hedef;
            IlerlemeBitti(saat.Elapsed.TotalSeconds);
            IlerlemeHiz.Text = Bicim.Boyut(new FileInfo(hedef).Length) + " · " + IlerlemeHiz.Text;
        }
        catch (OperationCanceledException)
        {
            IlerlemeKapat();
            if (_kapatBitince) Close();
        }
        catch (Exception ex) when (ex is ArsivHatasi or IOException or UnauthorizedAccessException or DllNotFoundException)
        {
            IlerlemeKapat();
            Uyar(Dil.T("cant_create", ex.Message));
            if (_kapatBitince) Close();
        }
    }

    void Menu_Click(object sender, RoutedEventArgs e)
    {
        KabukMenu.Header = Dil.T(Kabuk.KuruluMu() ? "shell_remove" : "shell_add");
        UcNokta.PlacementTarget = MenuDugme;
        UcNokta.IsOpen = true;
    }

    void Kabuk_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (Kabuk.KuruluMu())
            {
                Kabuk.Kaldir();
                MessageBox.Show(this, Dil.T("shell_removed"), Dil.T("app"));
            }
            else
            {
                Kabuk.Kur();
                MessageBox.Show(this, Dil.T("shell_added"), Dil.T("app"));
            }
        }
        catch (Exception ex) { Uyar(Dil.T("cant_setting", ex.Message)); }
    }

    void MetinleriYenile()
    {
        SutunAd.Header = Dil.T("col_name");
        SutunBoyut.Header = Dil.T("col_size");
        SutunPaketli.Header = Dil.T("col_packed");
        SutunTur.Header = Dil.T("col_type");
        SutunTarih.Header = Dil.T("col_modified");
        if (_arsiv == null) return;
        Title = Path.GetFileName(_arsiv.Yol) + " · " + Dil.T("app");
        MotorYazi.Text = Dil.T("engine", _arsiv.MotorAdi);
        Goster();
        DurumYaz();
        SecimDegisti();
    }

    void Dil_Click(object sender, RoutedEventArgs e)
    {
        Dil.Sec(!Dil.Tr);
        // menu labels live in the registry, rewrite them in the new language
        try { if (Kabuk.KuruluMu()) Kabuk.Kur(); } catch { }
    }

    void Hakkinda_Click(object sender, RoutedEventArgs e)
    {
        var surum = typeof(MainWindow).Assembly.GetName().Version?.ToString(3);
        var r = MessageBox.Show(this,
            Dil.T("about_text", surum ?? ""),
            Dil.T("app"), MessageBoxButton.YesNo, MessageBoxImage.Information);
        if (r != MessageBoxResult.Yes) return;
        var dosya = Path.Combine(AppContext.BaseDirectory, "THIRD-PARTY-NOTICES.txt");
        if (File.Exists(dosya)) Process.Start(new ProcessStartInfo(dosya) { UseShellExecute = true });
        else Process.Start(new ProcessStartInfo("https://github.com/dursuncankaymak/dafium-archive") { UseShellExecute = true });
    }

    void Liste_FareBas(object sender, MouseButtonEventArgs e) => _basNokta = e.GetPosition(null);

    async void Liste_FareKay(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _surukleniyor || _arsiv == null || Liste.SelectedItems.Count == 0) return;
        var fark = e.GetPosition(null) - _basNokta;
        if (Math.Abs(fark.X) < SystemParameters.MinimumHorizontalDragDistance * 2 && Math.Abs(fark.Y) < SystemParameters.MinimumVerticalDragDistance * 2) return;
        if (e.OriginalSource is DependencyObject d && ItemsControl.ContainerFromElement(Liste, d) is not ListViewItem) return;

        _surukleniyor = true;
        try
        {
            var secilen = Liste.SelectedItems.Cast<Oge>().ToList();
            var kayitlar = SeciliKayitlar();
            var gecici = Path.Combine(GeciciKok, "s" + Guid.NewGuid().ToString("N")[..8]);
            if (!await CikarAsync(kayitlar, gecici, sessiz: true)) return;
            var yollar = secilen.Select(o => Guvenli.HedefYol(gecici, o.Yol)).Where(y => File.Exists(y) || Directory.Exists(y)).ToArray();
            if (yollar.Length == 0) return;
            var veri = new DataObject(DataFormats.FileDrop, yollar);
            DragDrop.DoDragDrop(Liste, veri, DragDropEffects.Copy | DragDropEffects.Move);
        }
        finally { _surukleniyor = false; }
    }

    void IlerlemeBaslat(string baslik)
    {
        IlerlemeBaslik.Text = baslik;
        IlerlemeDosya.Text = Dil.T("preparing");
        IlerlemeDolgu.Width = 0;
        IlerlemeYuzde.Text = Dil.T("pct", 0);
        IlerlemeHiz.Text = "";
        IlerlemeSayac.Text = "";
        IptalDugme.Content = Dil.T("cancel");
        KlasoruAcDugme.Visibility = Visibility.Collapsed;
        IlerlemeKatman.Visibility = Visibility.Visible;
    }

    void IlerlemeGuncelle(Ilerleme i, double gecen)
    {
        if (IlerlemeKatman.Visibility != Visibility.Visible) return;
        double oran = i.Toplam > 0 ? Math.Clamp((double)i.Yazilan / i.Toplam, 0, 1) : 0;
        var genislik = ((FrameworkElement)IlerlemeDolgu.Parent).ActualWidth * oran;
        IlerlemeDolgu.BeginAnimation(WidthProperty, new DoubleAnimation(genislik, TimeSpan.FromMilliseconds(180)));
        IlerlemeYuzde.Text = Dil.T("pct", (int)(oran * 100));
        IlerlemeDosya.Text = i.Dosya;
        if (gecen > 0.3)
        {
            double hiz = i.Yazilan / gecen;
            var kalan = hiz > 0 && i.Toplam > i.Yazilan ? (i.Toplam - i.Yazilan) / hiz : 0;
            IlerlemeHiz.Text = Dil.T("speed", Bicim.Boyut((long)hiz)) + (kalan > 0.5 ? " · " + Dil.T("left", Bicim.Sure(kalan)) : "");
        }
        IlerlemeSayac.Text = Dil.T("n_of_files", Bicim.Sayi(i.DosyaNo), Bicim.Sayi(i.DosyaSayisi));
    }

    void IlerlemeBitti(double sure)
    {
        IlerlemeBaslik.Text = "Bitti";
        IlerlemeDosya.Text = _sonHedef;
        IlerlemeDolgu.BeginAnimation(WidthProperty, new DoubleAnimation(((FrameworkElement)IlerlemeDolgu.Parent).ActualWidth, TimeSpan.FromMilliseconds(250)));
        IlerlemeYuzde.Text = Dil.T("pct", 100);
        IlerlemeHiz.Text = Dil.T("took", Bicim.Sure(Math.Max(sure, 0.1)));
        IptalDugme.Content = "Kapat";
        KlasoruAcDugme.Visibility = Visibility.Visible;
        KlasoruAcDugme.Focus();
    }

    void IlerlemeKapat()
    {
        IlerlemeDolgu.BeginAnimation(WidthProperty, null);
        IlerlemeKatman.Visibility = Visibility.Collapsed;
    }

    void Iptal_Click(object sender, RoutedEventArgs e)
    {
        if (IptalDugme.Content as string == "Kapat") { IlerlemeKapat(); if (_kapatBitince) Close(); return; }
        _islemIptal?.Cancel();
        IlerlemeDosya.Text = Dil.T("cancelling");
    }

    void KlasoruAc_Click(object sender, RoutedEventArgs e)
    {
        if (_sonHedef != null && Directory.Exists(_sonHedef))
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{_sonHedef}\"") { UseShellExecute = true });
        else if (_sonHedef != null && File.Exists(_sonHedef))
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{_sonHedef}\"") { UseShellExecute = true });
        IlerlemeKapat();
        if (_kapatBitince) Close();
    }
}
