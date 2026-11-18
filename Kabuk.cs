using System.IO;
using Microsoft.Win32;

namespace DafiumArsiv;

public static class Kabuk
{
    const string Kok = @"Software\Classes";
    const string ProgId = "DafiumArsiv.Arsiv";
    static readonly string[] Uzantilar = [".rar", ".zip", ".7z", ".tar", ".gz", ".tgz", ".xz", ".txz", ".zst", ".bz2", ".tbz2", ".lz4", ".iso", ".cab", ".001"];

    static string Exe => Environment.ProcessPath!;

    public static bool KuruluMu()
    {
        using var k = Registry.CurrentUser.OpenSubKey($@"{Kok}\{ProgId}\shell\open\command");
        return k?.GetValue("") is string s && s.Contains(Exe, StringComparison.OrdinalIgnoreCase);
    }

    public static void Kur()
    {
        var exe = Exe;
        var simge = $"\"{exe}\",0";

        using (var p = Registry.CurrentUser.CreateSubKey($@"{Kok}\{ProgId}"))
        {
            p.SetValue("", Dil.T("shell_type"));
            p.SetValue("FriendlyTypeName", Dil.T("shell_type"));
            using (var i = p.CreateSubKey("DefaultIcon")) i.SetValue("", simge);
            using (var c = p.CreateSubKey(@"shell\open\command")) c.SetValue("", $"\"{exe}\" \"%1\"");
        }
        foreach (var u in Uzantilar)
        {
            // only adds to "open with", never touches the default app
            using (var o = Registry.CurrentUser.CreateSubKey($@"{Kok}\{u}\OpenWithProgids")) o.SetValue(ProgId, Array.Empty<byte>(), RegistryValueKind.None);
            Ogesi($@"{Kok}\SystemFileAssociations\{u}\shell\DafiumArsiv.Ac", Dil.T("shell_open"), simge, $"\"{exe}\" \"%1\"");
            Ogesi($@"{Kok}\SystemFileAssociations\{u}\shell\DafiumArsiv.Yanina", Dil.T("shell_here"), simge, $"\"{exe}\" --yanina \"%1\"");
        }
        Ogesi($@"{Kok}\*\shell\DafiumArsiv.Zip", Dil.T("shell_zip"), simge, $"\"{exe}\" --zip \"%1\"");
        Ogesi($@"{Kok}\Directory\shell\DafiumArsiv.Zip", Dil.T("shell_zip"), simge, $"\"{exe}\" --zip \"%1\"");
        Yenile();
    }

    public static void Kaldir()
    {
        void Sil(string yol) { try { Registry.CurrentUser.DeleteSubKeyTree(yol, false); } catch { } }
        Sil($@"{Kok}\{ProgId}");
        foreach (var u in Uzantilar)
        {
            try { using var o = Registry.CurrentUser.OpenSubKey($@"{Kok}\{u}\OpenWithProgids", true); o?.DeleteValue(ProgId, false); } catch { }
            Sil($@"{Kok}\SystemFileAssociations\{u}\shell\DafiumArsiv.Ac");
            Sil($@"{Kok}\SystemFileAssociations\{u}\shell\DafiumArsiv.Yanina");
        }
        Sil($@"{Kok}\*\shell\DafiumArsiv.Zip");
        Sil($@"{Kok}\Directory\shell\DafiumArsiv.Zip");
        Yenile();
    }

    static void Ogesi(string yol, string yazi, string simge, string komut)
    {
        using var k = Registry.CurrentUser.CreateSubKey(yol);
        k.SetValue("MUIVerb", yazi);
        k.SetValue("Icon", simge);
        using var c = k.CreateSubKey("command");
        c.SetValue("", komut);
    }

    [System.Runtime.InteropServices.DllImport("shell32.dll")]
    static extern void SHChangeNotify(int olay, uint bayrak, IntPtr a, IntPtr b);
    static void Yenile() => SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero); // SHCNE_ASSOCCHANGED

    public static bool ArsivUzantisi(string yol)
    {
        var ad = Path.GetFileName(yol).ToLowerInvariant();
        return Uzantilar.Any(u => ad.EndsWith(u)) || System.Text.RegularExpressions.Regex.IsMatch(ad, @"\.(r\d\d|\d{3}|jar|apk)$");
    }
}
