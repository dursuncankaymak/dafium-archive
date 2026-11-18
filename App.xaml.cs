using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace DafiumArsiv;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Dil.Yukle();
        DispatcherUnhandledException += Beklenmeyen;
        string? yanina = null;
        var ziple = new List<string>();
        var digerleri = new List<string>();
        for (int i = 0; i < e.Args.Length; i++)
        {
            var a = e.Args[i];
            if (a == "--yanina" && i + 1 < e.Args.Length) yanina = e.Args[++i];
            else if (a == "--zip") { while (i + 1 < e.Args.Length && !e.Args[i + 1].StartsWith("--")) ziple.Add(e.Args[++i]); }
            else if (!a.StartsWith("--")) digerleri.Add(a);
        }
        var dosya = digerleri.FirstOrDefault(File.Exists);
        var pencere = new MainWindow(dosya, yanina != null && File.Exists(yanina) ? yanina : null,
            ziple.Where(y => File.Exists(y) || Directory.Exists(y)).ToList());
        MainWindow = pencere;
        pencere.Show();
    }

    static void Beklenmeyen(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        try
        {
            var klasor = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DafiumArsiv");
            Directory.CreateDirectory(klasor);
            File.AppendAllText(Path.Combine(klasor, "hata.log"), $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\n{e.Exception}\n\n");
        }
        catch { }
        MessageBox.Show(Dil.T("unexpected", e.Exception.Message), Dil.T("app"), MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
