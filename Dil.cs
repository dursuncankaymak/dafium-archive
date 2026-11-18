using System.Globalization;
using System.IO;
using System.Windows;

namespace DafiumArsiv;

public static class Dil
{
    public static bool Tr { get; private set; }
    public static CultureInfo Kultur => Tr ? CultureInfo.GetCultureInfo("tr-TR") : CultureInfo.GetCultureInfo("en-US");
    public static event Action? Degisti;

    static string Dosya => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DafiumArsiv", "dil.txt");

    public static void Yukle()
    {
        string? kayitli = null;
        try { if (File.Exists(Dosya)) kayitli = File.ReadAllText(Dosya).Trim(); } catch { }
        // no saved choice yet: follow windows, english for everyone else
        Tr = kayitli is null ? CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "tr" : kayitli == "tr";
        Uygula();
    }

    public static void Sec(bool tr)
    {
        Tr = tr;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Dosya)!);
            File.WriteAllText(Dosya, tr ? "tr" : "en");
        }
        catch { }
        Uygula();
        Degisti?.Invoke();
    }

    // xaml binds to these with {DynamicResource s.key}
    static void Uygula()
    {
        var r = Application.Current?.Resources;
        if (r == null) return;
        foreach (var (k, v) in Metinler) r["s." + k] = Tr ? v.tr : v.en;
    }

    public static string T(string k) => Metinler.TryGetValue(k, out var v) ? (Tr ? v.tr : v.en) : k;
    public static string T(string k, params object[] a) => string.Format(Kultur, T(k), a);

    static readonly Dictionary<string, (string en, string tr)> Metinler = new()
    {
        ["app"] = ("Dafium Archive", "Dafium Arşiv"),
        ["menu"] = ("Menu", "Menü"),
        ["minimize"] = ("Minimize", "Küçült"),
        ["maximize"] = ("Maximize", "Büyüt"),
        ["close"] = ("Close", "Kapat"),
        ["new_archive"] = ("New archive", "Yeni arşiv oluştur"),
        ["shell_add"] = ("Add to right-click menu", "Sağ tık menüsüne ekle"),
        ["shell_remove"] = ("Remove from right-click menu", "Sağ tık menüsünden kaldır"),
        ["about"] = ("Licenses and about", "Lisanslar ve hakkında"),
        ["other_lang"] = ("Türkçe", "English"),

        ["extract"] = ("Extract…", "Çıkar…"),
        ["extract_all"] = ("Extract all…", "Hepsini çıkar…"),
        ["extract_sel"] = ("Extract selected ({0})…", "Seçilenleri çıkar ({0})…"),
        ["extract_here"] = ("Extract here", "Yanına çıkar"),
        ["extract_here_tip"] = ("Extracts into a folder named after the archive, next to it", "Arşivin bulunduğu klasöre, arşivin adıyla bir klasör açıp çıkarır"),
        ["open_other"] = ("Open another", "Başka arşiv aç"),
        ["search"] = ("Search archive", "Arşivde ara"),
        ["search_crumb"] = ("Search: “{0}”", "“{0}” araması"),
        ["up"] = ("Parent folder", "Üst klasör"),
        ["col_name"] = ("Name", "Ad"),
        ["col_size"] = ("Size", "Boyut"),
        ["col_packed"] = ("Packed", "Sıkışmış"),
        ["col_type"] = ("Type", "Tür"),
        ["col_modified"] = ("Modified", "Değiştirilme"),
        ["encrypted"] = ("Encrypted", "Şifreli"),
        ["open_file"] = ("Open file", "Dosyayı aç"),

        ["drop_title"] = ("Drop an archive here", "Arşivi buraya bırak"),
        ["drop_formats"] = ("RAR, ZIP, 7z, TAR, GZ, XZ, ZST, ISO and more · encrypted and multi-part RAR too", "RAR, ZIP, 7z, TAR, GZ, XZ, ZST, ISO ve daha fazlası · şifreli ve çok parçalı RAR dahil"),
        ["open_archive"] = ("Open archive", "Arşiv aç"),
        ["drop_hint"] = ("Drop files or folders to make an archive from them", "Dosya ya da klasör bırakırsan onlardan arşiv yapar"),
        ["drop_overlay"] = ("Drop to open", "Bırak, açayım"),
        ["open_folder"] = ("Open folder", "Klasörü aç"),
        ["cancel"] = ("Cancel", "İptal"),

        ["reading"] = ("Reading archive", "Arşiv okunuyor"),
        ["trying_pw"] = ("Trying password", "Şifre deneniyor"),
        ["extracting"] = ("Extracting", "Çıkarılıyor"),
        ["opening"] = ("Opening", "Açılıyor"),
        ["creating"] = ("Creating archive", "Arşiv oluşturuluyor"),
        ["preparing"] = ("Preparing…", "Hazırlanıyor…"),
        ["cancelling"] = ("Cancelling…", "İptal ediliyor…"),
        ["speed"] = ("{0}/s", "{0}/sn"),
        ["left"] = ("{0} left", "{0} kaldı"),
        ["took"] = ("took {0}", "{0} sürdü"),
        ["n_of_files"] = ("{0} / {1} files", "{0} / {1} dosya"),

        ["pick_archive"] = ("Choose an archive", "Arşiv seç"),
        ["filter_archives"] = ("Archives", "Arşivler"),
        ["filter_all"] = ("All files", "Tüm dosyalar"),
        ["where_extract"] = ("Extract to…", "Nereye çıkarılsın?"),
        ["extracted"] = ("Extracted", "Çıkarılanlar"),

        ["no_engine"] = ("Archive engine not found (archive.dll / unrar.dll). Try downloading the app again.", "Arşiv motoru bulunamadı (archive.dll / unrar.dll). Programı yeniden indirmeyi dene."),
        ["cant_open"] = ("Couldn't open the archive:\n{0}", "Arşiv açılamadı:\n{0}"),
        ["cant_open_file"] = ("Couldn't open the file: {0}", "Dosya açılamadı: {0}"),
        ["cant_extract"] = ("Extraction didn't finish:\n{0}", "Çıkarma tamamlanamadı:\n{0}"),
        ["cant_create"] = ("Couldn't create the archive:\n{0}", "Arşiv oluşturulamadı:\n{0}"),
        ["cant_setting"] = ("Couldn't change the setting: {0}", "Ayar yapılamadı: {0}"),
        ["unexpected"] = ("Something went wrong:\n{0}", "Beklenmeyen bir hata oldu:\n{0}"),

        ["status"] = ("{0} files, {1} folders · {2}", "{0} dosya, {1} klasör · {2}"),
        ["engine"] = ("Engine: {0}", "Motor: {0}"),
        ["pct"] = ("{0}%", "%{0}"),
        ["pct_paren"] = (" ({0}%)", " (%{0})"),
        ["status_enc"] = (" · encrypted", " · şifreli"),
        ["n_selected"] = ("{0} items selected", "{0} öğe seçili"),
        ["n_items"] = (" items", " öğe"),
        ["prev_pick"] = ("Pick a file to preview it here.", "Bir dosya seç, burada önizlemesi çıksın."),
        ["prev_tip"] = ("Use Extract to pull everything out at once.", "Çıkar düğmesiyle hepsini birden çıkarabilirsin."),
        ["prev_loading"] = ("Loading preview…", "Önizleme hazırlanıyor…"),
        ["prev_pw"] = ("Preview needs the password", "Önizleme için şifre gerekiyor"),
        ["prev_fail"] = ("Can't preview this", "Önizleme yapılamadı"),
        ["prev_img_fail"] = ("Can't show this image", "Bu resim gösterilemiyor"),
        ["pixels"] = ("{0} × {1} px", "{0} × {1} piksel"),

        ["shell_removed"] = ("Removed from the right-click menu.", "Sağ tık menüsünden kaldırıldı."),
        ["shell_added"] = ("Done. Right-click an archive for \"Open with Dafium Archive\" and \"Dafium: Extract here\", any file or folder for \"Dafium: Make ZIP\".\n\nOn Windows 11 they're under \"Show more options\".\n\nYour default app hasn't changed. You can pick Dafium Archive from \"Open with\" if you want.",
                          "Eklendi. Arşivlere sağ tıklayınca \"Dafium Arşiv ile aç\" ve \"Dafium: Buraya çıkar\", her dosya ve klasörde \"Dafium: ZIP yap\" çıkacak.\n\nWindows 11'de bunlar \"Diğer seçenekleri göster\" altında.\n\nVarsayılan programın değişmedi; istersen \"Birlikte aç\" listesinden Dafium Arşiv'i seçebilirsin."),
        ["shell_open"] = ("Open with Dafium Archive", "Dafium Arşiv ile aç"),
        ["shell_here"] = ("Dafium: Extract here", "Dafium: Buraya çıkar"),
        ["shell_zip"] = ("Dafium: Make ZIP", "Dafium: ZIP yap"),
        ["shell_type"] = ("Archive", "Arşiv"),
        ["about_text"] = ("Dafium Archive {0}\nOpen source, MIT licensed.\n\nEngines:\n• UnRAR — RARLAB, UnRAR license (extracts RAR, can't create it)\n• 7-Zip — Igor Pavlov, LGPL 2.1\n• libarchive — BSD license\n• zlib, bzip2, xz, zstd, lz4\n\nOpen the license texts?",
                          "Dafium Arşiv {0}\nMIT lisanslı açık kaynak.\n\nKullanılan motorlar:\n• UnRAR — RARLAB, UnRAR lisansı (RAR açar, oluşturmaz)\n• 7-Zip — Igor Pavlov, LGPL 2.1\n• libarchive — BSD lisansı\n• zlib, bzip2, xz, zstd, lz4\n\nLisans metinlerini açmak ister misin?"),

        ["t_folder"] = ("Folder", "Klasör"),
        ["t_image"] = ("Image", "Resim"),
        ["t_video"] = ("Video", "Video"),
        ["t_audio"] = ("Audio", "Ses"),
        ["t_archive"] = ("Archive", "Arşiv"),
        ["t_program"] = ("Program", "Program"),
        ["t_code"] = ("Code", "Kod"),
        ["t_document"] = ("Document", "Belge"),
        ["t_text"] = ("Text", "Metin"),
        ["t_file"] = ("File", "Dosya"),
        ["t_ext_file"] = ("{0} file", "{0} dosyası"),
        ["u_s"] = ("s", "sn"),
        ["u_min"] = ("min", "dk"),
        ["u_h"] = ("h", "sa"),

        ["na_title"] = ("New archive", "Yeni arşiv"),
        ["na_pick"] = ("Pick what to add", "Eklenecekleri seç"),
        ["na_count"] = ("{0} items to add", "{0} öğe eklenecek"),
        ["na_default"] = ("Archive", "Arşiv"),
        ["na_remove"] = ("Remove from list", "Listeden çıkar"),
        ["na_drop"] = ("Drag files or folders here", "Dosya ya da klasörleri buraya sürükle"),
        ["na_add_file"] = ("+ File", "+ Dosya"),
        ["na_add_folder"] = ("+ Folder", "+ Klasör"),
        ["na_name"] = ("Archive name", "Arşiv adı"),
        ["na_where"] = ("Save to", "Nereye"),
        ["na_change"] = ("Change", "Değiştir"),
        ["na_format"] = ("Format", "Biçim"),
        ["na_zip"] = ("Opens everywhere, supports a password", "Her yerde açılır, şifre konabilir"),
        ["na_7z"] = ("Smallest size", "En küçük boyut"),
        ["na_zst"] = ("Fastest, good for backups", "En hızlı, yedekler için"),
        ["na_level"] = ("Compression", "Sıkıştırma"),
        ["na_fast"] = ("Fast", "Hızlı"),
        ["na_normal"] = ("Normal", "Normal"),
        ["na_best"] = ("Best", "En iyi"),
        ["na_pw"] = ("Password (optional, AES-256)", "Şifre (isteğe bağlı, AES-256)"),
        ["na_pw_zip"] = ("Passwords work with ZIP only", "Şifre sadece ZIP'te"),
        ["na_files"] = ("Files to add", "Eklenecek dosyalar"),
        ["na_folders"] = ("Folders to add", "Eklenecek klasörler"),
        ["na_save_where"] = ("Where should the archive go?", "Arşiv nereye kaydedilsin?"),
        ["na_bad_name"] = ("The archive name has a character that can't be used.", "Arşiv adında kullanılamayan bir karakter var."),
        ["na_exists"] = ("{0} already exists. Overwrite it?", "{0} zaten var. Üzerine yazılsın mı?"),
        ["back"] = ("Cancel", "Vazgeç"),
        ["create"] = ("Create", "Oluştur"),

        ["pw_title"] = ("Password", "Şifre"),
        ["pw_locked"] = ("This archive is encrypted", "Bu arşiv şifreli"),
        ["pw_enter"] = ("Enter the password to open it.", "Açmak için şifreyi gir."),
        ["pw_show"] = ("Show password", "Şifreyi göster"),
        ["pw_open"] = ("Open", "Aç"),
        ["pw_wrong"] = ("Wrong password", "Şifre yanlış"),
        ["pw_retry"] = ("Try again. Passwords are case-sensitive.", "Tekrar dene. Büyük/küçük harfe dikkat."),

        ["e_not_found"] = ("File not found.", "Dosya bulunamadı."),
        ["e_bad_name"] = ("Invalid file name: {0}", "Geçersiz dosya adı: {0}"),
        ["e_slip"] = ("Skipped an entry that tried to write outside the target folder: {0}", "Hedef klasörün dışına yazmaya çalışan kayıt atlandı: {0}"),
        ["e_nothing"] = ("Nothing to add.", "Eklenecek dosya bulunamadı."),
        ["e_pw_zip7"] = ("Pick ZIP for a password-protected archive (no 7z encryption yet).", "Şifreli arşiv için ZIP seç (7z şifreleme şimdilik yok)."),
        ["e_pw_zip"] = ("Pick ZIP for a password-protected archive.", "Şifreli arşiv için ZIP seç."),
        ["e_write"] = ("Couldn't write the archive.", "Arşiv yazılamadı."),
        ["e_read"] = ("Couldn't read the archive.", "Arşiv okunamadı."),
        ["e_enc_unsupported"] = ("This archive's encryption isn't supported yet (encrypted 7z).", "Bu arşivin şifreleme türü henüz desteklenmiyor (şifreli 7z)."),
        ["e_7z_init"] = ("Couldn't start the 7-Zip engine.", "7-Zip motoru başlatılamadı."),
        ["e_encrypted"] = ("This archive is encrypted.", "Bu arşiv şifreli."),
        ["e_wrong_pw"] = ("Wrong password.", "Şifre yanlış."),
        ["e_not_7z"] = ("Not a valid 7z archive, or it's damaged.", "Bu dosya geçerli bir 7z arşivi değil ya da bozuk."),
        ["e_7z_open"] = ("Couldn't open 7z (0x{0:X8}).", "7z açılamadı (0x{0:X8})."),
        ["e_extract_fail"] = ("Extraction failed (0x{0:X8}).", "Çıkarma başarısız (0x{0:X8})."),
        ["e_method"] = ("Unsupported compression method in this archive: {0}", "Bu arşivde desteklenmeyen bir sıkıştırma yöntemi var: {0}"),
        ["e_truncated"] = ("Archive is incomplete or only partly downloaded: {0}", "Arşiv eksik ya da yarım inmiş: {0}"),
        ["e_headers"] = ("Archive headers are damaged.", "Arşiv başlıkları bozuk."),
        ["e_corrupt_file"] = ("File came out damaged: {0}", "Dosya bozuk çıktı: {0}"),
        ["e_disk"] = ("Couldn't write to disk: {0}", "Diske yazılamadı: {0}"),
        ["e_missing_part"] = ("A part of the archive is missing: {0}", "Arşivin bir parçası eksik: {0}"),
        ["e_bad_data"] = ("Archive is damaged (data error).", "Arşiv bozuk (veri hatası)."),
        ["e_not_rar"] = ("Not a valid RAR archive.", "Bu dosya geçerli bir RAR arşivi değil."),
        ["e_unknown_rar"] = ("Unknown RAR format.", "Bilinmeyen RAR biçimi."),
        ["e_eopen"] = ("Couldn't open the archive (another program may be using it, or a part is missing).", "Arşiv açılamadı (başka bir program kullanıyor olabilir ya da bir parça eksik)."),
        ["e_ecreate"] = ("Couldn't create the file. Do you have write access to the target folder?", "Dosya oluşturulamadı. Hedef klasöre yazma izni var mı?"),
        ["e_eread"] = ("Error while reading the archive.", "Arşiv okunurken hata oluştu."),
        ["e_ewrite"] = ("Couldn't write to disk. It may be full.", "Diske yazılamadı. Disk dolu olabilir."),
        ["e_largedict"] = ("The archive needs too much memory.", "Arşiv çok büyük bellek istiyor."),
        ["e_rar"] = ("RAR error (code {0}).", "RAR hatası (kod {0})."),
    };
}
