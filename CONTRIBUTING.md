# Contributing

Thanks! Every kind of help counts: code review, bug reports, translations, test archives, ideas.
Issues and PRs in English or Turkish are both fine.

## Reporting bugs

- Windows version and Dafium Archive version (⋮ menu → Licenses and about)
- Archive format (RAR5, ZIP, 7z…), whether it's encrypted or multi-part
- If you can, a small sample archive that shows the problem (please **don't** send personal files)
- Error log: `%APPDATA%\DafiumArsiv\hata.log`

Found a security issue (e.g. an archive that writes outside the target folder)? Please report it through
GitHub's **Security → Report a vulnerability** instead of a public issue.

## Code

- Layout:
  - `Motor/` archive engines (UnRAR, 7-Zip, libarchive) and the shared `Arsiv` base class
  - `MainWindow.*`, `YeniArsivPenceresi.*`, `SifrePenceresi.*` the UI (WPF)
  - `Dil.cs` all UI strings, English and Turkish
  - `Kabuk.cs` Explorer context menu (HKCU)
  - `native/` prebuilt engines, rebuilt with `scripts/motorlari-derle.ps1`
- Identifiers are in Turkish. New code can follow that or use English, either is fine.
- Run the engine tests before sending a PR (README → Building).
- If you touch an engine, make sure the encrypted, multi-part and zip slip tests still pass.

## Translations

Add your language as a new column in `Dil.cs` and a way to pick it in the ⋮ menu. Keep strings short, the buttons aren't very wide.

## AI

Parts of this project were written with help from AI (Claude) and verified with tests. AI-assisted PRs
are welcome too; just mention it in the PR description and test it yourself.

---

## Türkçe

Her türlü katkı değerli: inceleme, hata bildirimi, çeviri, test arşivi, fikir. Hata bildirirken Windows ve
uygulama sürümünü, arşivin biçimini ve mümkünse küçük bir örnek arşivi ekleyin (kişisel dosya göndermeyin).
Hata kaydı `%APPDATA%\DafiumArsiv\hata.log` içinde. Güvenlik açıklarını herkese açık issue yerine
**Security → Report a vulnerability** ile bildirin. PR göndermeden önce motor testlerini çalıştırın.
