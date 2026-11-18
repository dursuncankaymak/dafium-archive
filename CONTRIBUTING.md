# Katkı rehberi / Contributing

Teşekkürler! Her türlü katkı değerli: kod incelemesi, hata bildirimi, çeviri, test arşivi, fikir.
Thanks! Reviews, bug reports, translations and PRs are all welcome — English is fine too.

## Hata bildirirken / Reporting bugs

- Windows sürümü ve Dafium Arşiv sürümü (menü → Lisanslar ve hakkında)
- Arşivin biçimi (RAR5, ZIP, 7z…), şifreli mi, çok parçalı mı
- Mümkünse sorunu gösteren küçük bir örnek arşiv (kişisel dosya **göndermeyin**)
- Hata kaydı: `%APPDATA%\DafiumArsiv\hata.log`

Güvenlik açığı bulduysanız (ör. hedef klasör dışına yazdıran bir arşiv) lütfen herkese açık issue yerine
GitHub'daki **Security → Report a vulnerability** ile bildirin.

## Kod

- Proje yapısı:
  - `Motor/` — arşiv motorları (UnRAR, 7-Zip, libarchive) ve ortak `Arsiv` sınıfı
  - `MainWindow.*`, `YeniArsivPenceresi.*`, `SifrePenceresi.*` — arayüz (WPF)
  - `Kabuk.cs` — Gezgin sağ tık menüsü (HKCU)
  - `native/` — derlenmiş motorlar, `scripts/motorlari-derle.ps1` ile yeniden üretilir
- Kod içindeki adlar ve yorumlar Türkçe. Yeni kodda da Türkçe tercih edilir ama İngilizce PR da kabul.
- PR göndermeden önce motor testlerini çalıştırın (README → Derleme).
- Bir motora dokunduysanız şifreli, çok parçalı ve "zip slip" testlerinin hâlâ geçtiğinden emin olun.

## Yapay zeka

Bu projenin bir kısmı yapay zeka (Claude) yardımıyla yazıldı ve testlerle doğrulandı. Yapay zekayla
hazırladığınız PR'ları da kabul ediyoruz; lütfen PR açıklamasında belirtin ve kendiniz test edin.
