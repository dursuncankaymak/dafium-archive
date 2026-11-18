using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DafiumArsiv.Motor;
using Microsoft.Win32;

namespace DafiumArsiv;

public sealed record KaynakOge(string Yol, string Ad, string Simge, Brush Renk);

public partial class YeniArsivPenceresi : Window
{
    readonly ObservableCollection<KaynakOge> _kaynaklar = [];
    bool _adElle;

    public IReadOnlyList<string> Secilenler => _kaynaklar.Select(k => k.Yol).ToList();
    public string Hedef => Path.Combine(KonumKutu.Text, AdKutu.Text.Trim() + ArsivOlusturucu.Uzanti(SecilenBicim));
    public ArsivBicimi SecilenBicim => YediSec.IsChecked == true ? ArsivBicimi.YediZ : ZstSec.IsChecked == true ? ArsivBicimi.TarZst : ArsivBicimi.Zip;
    public Seviye SecilenSeviye => HizliSec.IsChecked == true ? Seviye.Hizli : EnIyiSec.IsChecked == true ? Seviye.EnIyi : Seviye.Normal;
    public string? Sifre => SecilenBicim == ArsivBicimi.Zip && SifreKutu.Password.Length > 0 ? SifreKutu.Password : null;

    public YeniArsivPenceresi(IEnumerable<string> baslangic)
    {
        InitializeComponent();
        Kaynaklar.ItemsSource = _kaynaklar;
        _kaynaklar.CollectionChanged += (_, _) => Guncelle();
        AdKutu.TextChanged += (_, _) => { if (AdKutu.IsKeyboardFocused) _adElle = true; };
        foreach (var y in baslangic) Ekle(y);
        if (KonumKutu.Text.Length == 0) KonumKutu.Text = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        MouseLeftButtonDown += (_, e) => { if (e.OriginalSource is Border or StackPanel) try { DragMove(); } catch { } };
        DragOver += (_, e) => { e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; };
        Drop += (_, e) => { if (e.Data.GetData(DataFormats.FileDrop) is string[] d) foreach (var y in d) Ekle(y); };
        Guncelle();
        Bicim_Checked(this, new RoutedEventArgs());
        Loaded += (_, _) => AdKutu.Focus();
    }

    void Ekle(string yol)
    {
        if (_kaynaklar.Any(k => string.Equals(k.Yol, yol, StringComparison.OrdinalIgnoreCase))) return;
        bool klasor = Directory.Exists(yol);
        if (!klasor && !File.Exists(yol)) return;
        var tur = klasor ? Turler.Klasor : Turler.Bul(yol);
        _kaynaklar.Add(new KaynakOge(yol, Path.GetFileName(yol.TrimEnd('\\')), tur.Simge, tur.Renk));
        if (_kaynaklar.Count == 1)
        {
            KonumKutu.Text = Path.GetDirectoryName(yol.TrimEnd('\\')) ?? KonumKutu.Text;
        }
    }

    void Guncelle()
    {
        BosYazi.Visibility = _kaynaklar.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        Ozet.Text = _kaynaklar.Count == 0 ? Dil.T("na_pick") : Dil.T("na_count", _kaynaklar.Count);
        if (!_adElle)
        {
            AdKutu.Text = _kaynaklar.Count switch
            {
                0 => Dil.T("na_default"),
                1 => Directory.Exists(_kaynaklar[0].Yol) ? _kaynaklar[0].Ad : Path.GetFileNameWithoutExtension(_kaynaklar[0].Ad),
                _ => Path.GetFileName(KonumKutu.Text.TrimEnd('\\')) is { Length: > 0 } k ? k : Dil.T("na_default"),
            };
        }
        OlusturDugme.IsEnabled = _kaynaklar.Count > 0;
    }

    void Bicim_Checked(object sender, RoutedEventArgs e)
    {
        // fires during InitializeComponent before these exist
        if (UzantiYazi == null || SifreCerceve == null || SifreBaslik == null) return;
        UzantiYazi.Text = ArsivOlusturucu.Uzanti(SecilenBicim);
        bool zip = SecilenBicim == ArsivBicimi.Zip;
        SifreCerceve.IsEnabled = zip;
        SifreCerceve.Opacity = zip ? 1 : 0.4;
        SifreBaslik.Text = Dil.T(zip ? "na_pw" : "na_pw_zip");
    }

    void Cikar_Click(object sender, RoutedEventArgs e)
    {
        if (((Button)sender).Tag is KaynakOge k) _kaynaklar.Remove(k);
    }

    void DosyaEkle_Click(object sender, RoutedEventArgs e)
    {
        var d = new OpenFileDialog { Multiselect = true, Title = Dil.T("na_files") };
        if (d.ShowDialog(this) == true) foreach (var f in d.FileNames) Ekle(f);
    }

    void KlasorEkle_Click(object sender, RoutedEventArgs e)
    {
        var d = new OpenFolderDialog { Multiselect = true, Title = Dil.T("na_folders") };
        if (d.ShowDialog(this) == true) foreach (var f in d.FolderNames) Ekle(f);
    }

    void Konum_Click(object sender, RoutedEventArgs e)
    {
        var d = new OpenFolderDialog { Title = Dil.T("na_save_where"), InitialDirectory = KonumKutu.Text };
        if (d.ShowDialog(this) == true) KonumKutu.Text = d.FolderName;
    }

    void Olustur_Click(object sender, RoutedEventArgs e)
    {
        var ad = AdKutu.Text.Trim();
        if (ad.Length == 0 || ad.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            MessageBox.Show(this, Dil.T("na_bad_name"), Dil.T("app"));
            return;
        }
        if (File.Exists(Hedef) && MessageBox.Show(this, Dil.T("na_exists", Path.GetFileName(Hedef)), Dil.T("app"),
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        DialogResult = true;
    }

    void Vazgec_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
