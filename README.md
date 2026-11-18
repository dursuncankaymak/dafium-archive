<p align="center">
  <img src="docs/dafi.png" width="120" alt="Dafi">
</p>

<h1 align="center">Dafium Arşiv</h1>

<p align="center">
  RAR, ZIP, 7z ve daha fazlasını açan; hızlı, reklamsız, Türkçe ve <b>güzel</b> bir arşiv yöneticisi.<br>
  <i>A fast, ad-free, good-looking archive manager for Windows — English below.</i>
</p>

<p align="center">
  <img src="docs/ekran-onizleme.png" width="760" alt="Dafium Arşiv ekran görüntüsü">
</p>

## Neler yapıyor?

- **Açar:** RAR (RAR4 ve RAR5), ZIP, 7z, TAR, GZ, XZ, ZST, BZ2, LZ4, ISO, CAB ve fazlası
  - Şifreli arşivler (RAR, AES-256 ZIP, başlığı şifreli 7z dahil)
  - Çok parçalı RAR (`film.part1.rar`, `part2` …) — ortadaki parçayı açsan bile baştan okur
  - Solid arşivler
- **Oluşturur:** ZIP (isteğe bağlı AES-256 şifre), 7z, tar.zst
- **Önizleme:** Arşivi açmadan resim ve yazı dosyalarını görür
- **Sürükle bırak:** Arşivi pencereye bırak → açılır. Dosya/klasör bırak → arşiv yapar. Listeden Gezgin'e sürükle → çıkarır.
- **Sağ tık menüsü** (isteğe bağlı, menüden açılır): *Dafium Arşiv ile aç*, *Buraya çıkar*, *ZIP yap*
- **Güvenli:** Arşivden `..\..\Windows\…` gibi dışarı kaçmaya çalışan dosyalar engellenir (zip slip koruması)
- **Sade:** Bildirim yok, arka planda çalışan bir şey yok, "satın al" penceresi yok

> **RAR oluşturamaz.** RAR biçimi RARLAB'a ait ve sıkıştırıcısı kapalı; 7-Zip de bu yüzden RAR oluşturmaz.

## İndir

[**Releases**](../../releases) sayfasından son sürümün zip'ini indir, bir klasöre çıkar, `DafiumArsiv.exe`'yi çalıştır. Kurulum yok; .NET kurulu olması gerekmez.

Windows SmartScreen "tanınmayan uygulama" uyarısı verirse: **Ek bilgi → Yine de çalıştır** (exe imzasız).

## Motorlar

| Biçim | Motor | Lisans |
|---|---|---|
| RAR | [UnRAR](https://www.rarlab.com) (RARLAB) | UnRAR lisansı (ücretsiz, sadece açma) |
| 7z | [7-Zip](https://www.7-zip.org) (Igor Pavlov) | LGPL 2.1 |
| Diğerleri + oluşturma | [libarchive](https://www.libarchive.org) | BSD |

Hepsi `native/` klasöründe ayrı DLL olarak durur; exe'nin içine gömülmez. Kaynaktan derlemek için: [`scripts/motorlari-derle.ps1`](scripts/motorlari-derle.ps1).

## Derleme

```powershell
# .NET 10 SDK gerekli
dotnet build -c Release
# Dağıtım (tek klasör, .NET gerekmez):
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o yayin
```

Testler (örnek arşivleri üretmek için WinRAR'ın `Rar.exe`'si ve 7-Zip gerekir):

```bash
bash testler/ornekleri-olustur.sh /tmp/dafium-test
dotnet run --project testler/MotorTestleri -c Release -- /tmp/dafium-test
```

## Katkı

İnceleme, hata bildirimi ve PR'lar çok değerli! Bkz. [CONTRIBUTING.md](CONTRIBUTING.md). Özellikle bakılmasını istediğimiz yerler:

- `Motor/UnrarArsiv.cs` ve `Motor/SevenZipArsiv.cs`'teki yerel (native) çağrılar
- `Motor/Arsiv.cs` içindeki `Guvenli` sınıfı (yol temizleme / zip slip)
- Büyük arşivlerde hız ve bellek

## Lisans

Dafium Arşiv'in kendi kodu **MIT** lisanslıdır ([LICENSE](LICENSE)). Motorlar kendi lisanslarıyla gelir: [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt). "Dafium" adı ve "Dafi" maskotu MIT kapsamında değildir.

---

## English

**Dafium Arşiv** is a Windows archive manager (WPF, .NET 10) with a modern dark UI. The UI is in Turkish for now — translations are welcome.

- **Extracts:** RAR4/RAR5 (encrypted, multi-volume, solid), ZIP (incl. AES-256), 7z (incl. encrypted headers), TAR, GZ, XZ, ZST, BZ2, LZ4, ISO, CAB…
- **Creates:** ZIP (optional AES-256), 7z, tar.zst
- Preview images/text without extracting, drag & drop in and out, optional Explorer context menu (per-user, no admin)
- Path traversal ("zip slip") protection on every engine
- No ads, no telemetry, no background processes

Engines: UnRAR (RAR, extraction only), 7-Zip `7z.dll` (7z, LGPL), libarchive (everything else + creation, BSD). Native DLLs live next to the exe so they can be replaced (LGPL). Build them from source with `scripts/motorlari-derle.ps1`.

The app's own code is MIT licensed. Reviews, bug reports and pull requests are very welcome — especially around the native interop and the path-sanitizing code.
