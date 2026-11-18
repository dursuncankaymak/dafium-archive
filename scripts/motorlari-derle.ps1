# Dafium Arşiv — yerel motorları (native/*.dll) kaynaktan derler.
# Rebuilds the native engines from source. Requirements: Visual Studio 2026 (C++ workload), git.
#
#   archive.dll  libarchive 3.8.x + zlib, bzip2, xz, zstd, lz4 (static CRT)   via vcpkg
#   unrar.dll    RARLAB UnRAR 7.3.x (static CRT)                              via vcpkg
#   7z.dll       7-Zip 26.05 Format7zF bundle                                 via nmake
#
# Kullanım / usage:  powershell -ExecutionPolicy Bypass -File scripts\motorlari-derle.ps1 -Is D:\dafium-insa
param([string]$Is = "$env:TEMP\dafium-arsiv-insa")

$ErrorActionPreference = 'Stop'
$proje = Split-Path $PSScriptRoot -Parent
New-Item -ItemType Directory -Force "$Is\triplets" | Out-Null

# 1) vcpkg
if (-not (Test-Path "$Is\vcpkg\vcpkg.exe")) {
    git clone --depth 1 https://github.com/microsoft/vcpkg "$Is\vcpkg"
    & "$Is\vcpkg\bootstrap-vcpkg.bat" -disableMetrics
}

# 2) Özel triplet: her şey statik CRT, sadece libarchive ve unrar DLL olarak
@'
set(VCPKG_TARGET_ARCHITECTURE x64)
set(VCPKG_CRT_LINKAGE static)
set(VCPKG_LIBRARY_LINKAGE static)
set(VCPKG_BUILD_TYPE release)
if(PORT STREQUAL "libarchive" OR PORT STREQUAL "unrar")
  set(VCPKG_LIBRARY_LINKAGE dynamic)
endif()
'@ | Set-Content -Encoding ascii "$Is\triplets\x64-windows-arsivst.cmake"

& "$Is\vcpkg\vcpkg.exe" install 'libarchive[core,bzip2,lz4,lzma,zstd]' unrar `
    --triplet x64-windows-arsivst --overlay-triplets "$Is\triplets" --x-install-root "$Is\kurulu"
Copy-Item "$Is\kurulu\x64-windows-arsivst\bin\archive.dll", "$Is\kurulu\x64-windows-arsivst\bin\unrar.dll" "$proje\native\" -Force

# 3) 7-Zip (resmi kaynak, github.com/ip7z/7zip)
$yedi = "$Is\7zip"
if (-not (Test-Path $yedi)) { git clone --depth 1 --branch 26.05 https://github.com/ip7z/7zip $yedi }
# Yeni MSVC sürümlerinde -Wall uyarıları -WX ile hataya dönmesin
(Get-Content "$yedi\CPP\Build.mak") -replace ' -WX -EHsc', ' -EHsc' | Set-Content "$yedi\CPP\Build.mak"
$vs = & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -property installationPath
cmd /c "`"$vs\VC\Auxiliary\Build\vcvars64.bat`" && cd /d `"$yedi\CPP\7zip\Bundles\Format7zF`" && nmake /nologo PLATFORM=x64"
Copy-Item "$yedi\CPP\7zip\Bundles\Format7zF\x64\7z.dll" "$proje\native\" -Force

Write-Host "Bitti: native\archive.dll, native\unrar.dll, native\7z.dll"
