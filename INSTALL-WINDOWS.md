# Cara Install HPX PIRATE di Windows

Panduan ini untuk **Windows 10 (1809+) / Windows 11, 64-bit**.

> HPX PIRATE adalah aplikasi **Windows desktop (.exe)**, bukan APK Android.

---

## Ringkasan cepat

| Cara | Butuh apa | Cocok untuk |
| --- | --- | --- |
| **A. Installer** (`HPX-PIRATE-Setup.exe`) | Build installer dulu di Windows | Pengguna biasa, paling rapi |
| **B. Portable** (ekstrak `publish`) | .NET 8 Desktop Runtime | Coba cepat / tanpa install |
| **C. Build dari source** | .NET 8 SDK | Developer |

Semua cara butuh **.NET 8 Desktop Runtime (x64)** — cara installnya di bawah.

---

## 0. Prasyarat: .NET 8 Desktop Runtime (x64)

Cek dulu apakah sudah terpasang. Buka **Command Prompt** lalu jalankan:

```cmd
dotnet --list-runtimes
```

Kalau muncul baris `Microsoft.WindowsDesktop.App 8.x.x` → **sudah ada**, lanjut ke cara A/B.
Kalau belum ada, install:

1. Buka https://dotnet.microsoft.com/download/dotnet/8.0
2. Pilih **.NET Desktop Runtime 8.x** → **Windows** → **x64**.
3. Unduh `windowsdesktop-runtime-8.x.x-win-x64.exe`, jalankan, klik **Install**.
4. Tutup dan buka ulang Command Prompt, cek lagi dengan `dotnet --list-runtimes`.

> Hanya **Desktop Runtime** (bukan SDK) yang dibutuhkan untuk menjalankan aplikasi.

---

## Cara A — Pakai Installer (`HPX-PIRATE-Setup.exe`)

Installer ini dihasilkan dari script Inno Setup di repo. Karena file `.exe` installer
harus dikompilasi **di Windows**, langkahnya:

### A.1 Build installer (sekali saja, di Windows)

1. Install **.NET 8 SDK** dan **Inno Setup 6** (https://jrsoftware.org/isdl.php).
2. Buka **PowerShell sebagai Administrator** di folder repo, lalu jalankan:

   ```powershell
   powershell -ExecutionPolicy Bypass -File installer\build-installer.ps1
   ```

3. Hasilnya: **`dist\HPX-PIRATE-Setup.exe`**.

### A.2 Jalankan installer

1. Klik dua kali **`HPX-PIRATE-Setup.exe`**.
2. Klik **Yes** pada prompt UAC (aplikasi butuh hak admin untuk restore point & tuning sistem).
3. Ikuti wizard → **Next** → **Install**.
   - Jika .NET 8 Desktop Runtime belum ada dan kamu menaruh file runtime di
     `installer\redist\`, installer akan memasangnya otomatis.
4. Klik **Finish**. Aplikasi bisa dibuka dari **Start Menu** atau **Desktop**.

### A.3 Uninstall

**Settings → Apps → Installed apps → HPX PIRATE → Uninstall**
(atau dari Start Menu → folder HPX PIRATE → Uninstall).

> Data pengguna di `%APPDATA%\HPX Pirate` sengaja **tidak** dihapus. Hapus manual kalau perlu.

---

## Cara B — Portable (tanpa install)

Cocok kalau kamu hanya punya folder hasil build (`publish/`).

1. **Pastikan .NET 8 Desktop Runtime (x64) terpasang** (lihat bagian 0).
2. Ekstrak **`HPX-PIRATE-publish-win-x64.zip`** ke folder pilihanmu,
   misalnya `C:\HPX-PIRATE\`. Pastikan **semua** file ikut terekstrak
   (termasuk folder `amd64\` dan file-file `.dll`).
3. Klik kanan **`HPXPirate.exe`** → **Run as administrator**.
   (Bisa juga klik dua kali; akan muncul prompt UAC.)
4. Aplikasi langsung terbuka. Tidak ada proses install.

> ⚠️ Jangan memindahkan `HPXPirate.exe` sendirian — ia butuh file `.dll` di folder yang
> sama. Pindahkan **seluruh folder**.
>
> 💡 Agar tidak selalu muncul prompt UAC: klik kanan `HPXPirate.exe` → **Properties** →
> tab **Compatibility** → centang **Run this program as an administrator** → **OK**.

### Membuat shortcut manual

Klik kanan `HPXPirate.exe` → **Send to** → **Desktop (create shortcut)**.

---

## Cara C — Build dari source (developer)

Di Windows dengan **.NET 8 SDK** terpasang:

```powershell
# 1) build
dotnet build src\HPXPirate\HPXPirate.csproj -c Release

# 2) jalankan
dotnet run --project src\HPXPirate\HPXPirate.csproj -c Release

# 3) buat folder siap-distribusi
dotnet publish src\HPXPirate\HPXPirate.csproj -c Release -r win-x64 --self-contained false -o publish
```

Ingin versi **self-contained** (tidak perlu .NET Runtime di komputer target)?

```powershell
dotnet publish src\HPXPirate\HPXPirate.csproj -c Release -r win-x64 ^
  --self-contained true -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true -o publish-selfcontained
```

---

## Pertama kali dibuka

1. Terima prompt **UAC** (Yes).
2. **Performance** → pilih tweak → **Apply**.
   - Sebuah **System Restore Point** dibuat otomatis sebelum perubahan.
   - Setiap perubahan bisa di-**UNDO**.
3. **Monitor** → **START** untuk melihat FPS/frame time (jalankan sambil main game).
4. **Profiles** → simpan profil per-game.
5. **Settings** → aktifkan **Start with Windows** / **Minimize to tray** kalau mau.

---

## Troubleshooting

| Masalah | Solusi |
| --- | --- |
| Muncul pesan butuh **.NET** | Install **.NET 8 Desktop Runtime (x64)** (bagian 0). |
| Aplikasi tidak mau dibuka / langsung tertutup | Jalankan **Run as administrator**; cek log di `%APPDATA%\HPX Pirate\logs\`. |
| Windows SmartScreen memblokir | Klik **More info** → **Run anyway** (app belum ditandatangani digital). |
| FPS tidak muncul | Tekan **START** di Monitor **saat game sedang berjalan**; sebagian game ber-anti-cheat memblokir ETW. |
| Suhu/load CPU-GPU "n/a" | Jalankan sebagai admin; sensor tertentu mungkin butuh driver/versi lebih baru. |
| Ingin balikin semua perubahan | **Optimizer → UNDO ALL**, atau pakai System Restore Windows. |

---

## Lokasi data & log

```
%APPDATA%\HPX Pirate\
├── settings.json
├── logs\
├── profiles\        (dan profiles\backups\)
└── history\
```

Buka cepat: tekan **Win + R**, ketik `%APPDATA%\HPX Pirate`, Enter.
