#!/usr/bin/env bash
# Motor testleri için örnek arşivleri üretir (Git Bash).
# Gerekenler: WinRAR'ın Rar.exe'si (RAR örnekleri için), 7-Zip'in 7z.exe'si, python.
# Kullanım: bash testler/ornekleri-olustur.sh <klasor>
#           dotnet run --project testler/MotorTestleri -c Release -- <klasor>
set -e
K="${1:?klasor ver}"
R="${RAR:-/c/Program Files/WinRAR/Rar.exe}"
Z="${SEVENZIP:-/c/Program Files/7-Zip/7z.exe}"
rm -rf "$K"; mkdir -p "$K/kaynak/alt klasör/derin" "$K/arsivler"
cd "$K/kaynak"
echo "Merhaba dünya, şğüöçı İĞÜŞÖÇ" > "türkçe ad ğüş.txt"
head -c 3000000 /dev/urandom > "alt klasör/rastgele.bin"
yes "satır satır tekrar eden metin 1234567890" | head -c 2000000 > "alt klasör/derin/tekrar.txt"
cp "$(dirname "$0")/../gorseller/dafi.png" "alt klasör/dafi.png" 2>/dev/null || head -c 50000 /dev/urandom > "alt klasör/dafi.png"
find . -type f -exec sha256sum {} \; | sort > ../kaynak.sha
A=../arsivler
"$R" a -r -ma5 -idq $A/normal5.rar .
"$R" a -r -ma5 -s -idq $A/solid.rar .
"$R" a -r -ma5 -hpGizli123 -idq $A/basliksifreli.rar .
"$R" a -r -ma5 -pVeri456 -idq $A/verisifreli.rar .
"$R" a -r -ma5 -v1m -idq $A/parcali.rar .
"$Z" a -bso0 -bsp0 -tzip $A/normal.zip .
"$Z" a -bso0 -bsp0 -tzip -mem=AES256 -pZip789 $A/aes.zip .
"$Z" a -bso0 -bsp0 -t7z $A/normal.7z .
"$Z" a -bso0 -bsp0 -t7z -mhe=on -pYedi000 $A/sifreli.7z .
"$Z" a -bso0 -bsp0 -t7z -pYedi111 $A/verisifreli.7z .
tar -czf $A/paket.tar.gz .
gzip -c "türkçe ad ğüş.txt" > $A/tek.txt.gz
"$Z" a -bso0 -bsp0 -txz $A/tek2.txt.xz "türkçe ad ğüş.txt"
# Kötü niyetli yollar (zip slip) — hedef klasörün dışına yazılmamalı
python -c "
import zipfile
z=zipfile.ZipFile(r'../arsivler/kacak.zip','w')
z.writestr('../../KACAK_DOSYA.txt','kotu'); z.writestr('C:/Windows/kotu2.txt','x')
z.writestr('normal/iyi.txt','iyi'); z.writestr('CON.txt','con'); z.close()"
echo "Hazır: $K"
