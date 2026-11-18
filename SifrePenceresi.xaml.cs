using System.Windows;

namespace DafiumArsiv;

public partial class SifrePenceresi : Window
{
    public string Sifre { get; private set; } = "";

    public SifrePenceresi(bool yanlis)
    {
        InitializeComponent();
        if (yanlis)
        {
            Baslik.Text = "Şifre yanlış";
            Alt.Text = "Tekrar dene. Büyük/küçük harfe dikkat.";
        }
        Loaded += (_, _) => Kutu.Focus();
        MouseLeftButtonDown += (_, _) => { try { DragMove(); } catch { } };
    }

    void Goster_Click(object sender, RoutedEventArgs e)
    {
        if (AcikKutu.Visibility == Visibility.Visible)
        {
            Kutu.Password = AcikKutu.Text;
            AcikKutu.Visibility = Visibility.Collapsed;
            Kutu.Visibility = Visibility.Visible;
            Kutu.Focus();
        }
        else
        {
            AcikKutu.Text = Kutu.Password;
            Kutu.Visibility = Visibility.Collapsed;
            AcikKutu.Visibility = Visibility.Visible;
            AcikKutu.Focus();
            AcikKutu.CaretIndex = AcikKutu.Text.Length;
        }
    }

    void Tamam_Click(object sender, RoutedEventArgs e)
    {
        Sifre = AcikKutu.Visibility == Visibility.Visible ? AcikKutu.Text : Kutu.Password;
        if (Sifre.Length == 0) return;
        DialogResult = true;
    }

    void Vazgec_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
