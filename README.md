# Google SERP Multi-Device Scanner

Aplikasi desktop native Windows untuk pemindaian, analisis peringkat, dan audit halaman pertama Google Search Engine Results Page (SERP) secara massal lintas perangkat (Desktop, Mobile, dan Tablet).

---

## Ringkasan Proyek

Google SERP Multi-Device Scanner dirancang untuk kebutuhan riset SEO profesional, pemantauan posisi organik, intelijen kompetitor Google Ads, serta pemetaan profil bisnis Google Maps Local Pack. Aplikasi berjalan sebagai file executable mandiri (`GoogleSerpScanner.exe`) yang secara otomatis mengelola server internal di latar belakang tanpa memunculkan jendela konsol hitam.

---

## Struktur Direktori

Seluruh komponen sistem telah distrukturkan secara rapi untuk memisahkan file eksekusi, konfigurasi pengguna, dan modul inti aplikasi:

```text
SERP_Scanner/
|
|-- GoogleSerpScanner.exe        Program utama aplikasi Windows (disertai icon resmi)
|
|-- setup - DO NOT DELETE/       Direktori konfigurasi teks eksternal pengguna
|   |-- keywords.txt             Daftar kata kunci default yang dimuat otomatis
|   |-- blacklist.txt            Daftar domain sendiri untuk proteksi klik iklan
|   |-- config.txt               Parameter operasional bawaan (jeda, browser, klik)
|   `-- BACA_SAYA.txt            Petunjuk format pengisian file konfigurasi
|
|-- app/                         Direktori modul inti sistem dan backend server
|   |-- server.js                Server HTTP dan API endpoint Express
|   |-- scanner.js               Engine otomasi crawling SERP berbasis Playwright
|   |-- package.json             Konfigurasi dependensi Node.js
|   |-- app.ico                  Asset icon aplikasi Windows
|   |-- GoogleSerpScanner.cs     Source code launcher C# WinForms dengan WebView2
|   |-- lib/                     Pustaka Microsoft Edge WebView2 runtime (.NET & native)
|   |-- node_modules/            Pustaka dependensi eksternal
|   `-- public/                  Antarmuka pengguna (HTML5, Tailwind CSS, JavaScript)
|       |-- index.html
|       |-- style.css
|       |-- app.js
|       `-- icons/
|
`-- README.md                    Dokumentasi teknis sistem
```

---

## Fitur Utama

### 1. Multi-Device Emulation
Mendukung pemindaian serentak maupun individual dengan resolusi dan user agent otentik:
- **Desktop**: Resolusi 1920 x 1080 (Chromium Desktop).
- **Mobile**: Emulasi Google Pixel / Safari iOS dengan touch viewport dinamis.
- **Tablet**: Emulasi Apple iPad Air dengan resolusi 1280 x 800.

### 2. Deteksi Entitas SERP Lengkap
- **Hasil Organik**: Peringkat (#1 hingga #10+), judul halaman, domain bersih, URL target, meta deskripsi, dan sitelinks.
- **Google Ads**: Deteksi iklan sponsor bagian atas (Top Ads) dan bawah (Bottom Ads), headline, display link, final target URL, dan ekstensi snippet.
- **Google Maps Local Pack**: Profil bisnis 3-Pack, nama usaha, rating bintang, jumlah ulasan, kategori, alamat fisik, dan nomor telepon.
- **AI Overview & Featured Snippet**: Ekstraksi ringkasan jawaban posisi nol beserta domain rujukan.
- **People Also Ask (PAA)**: Pertanyaan turunan populer yang disajikan oleh Google.

### 3. Filter Metrik Interaktif
Lima panel ringkasan metrik di bagian atas tabel berfungsi langsung sebagai tombol filter kategori:
- **Total Item**: Menampilkan seluruh data yang ditemukan.
- **Organik**: Memfilter hanya peringkat organik halaman 1.
- **Google Ads**: Memfilter iklan sponsor Google Ads.
- **Google Maps**: Memfilter entitas bisnis lokal Maps Pack.
- **AI & PAA**: Memfilter jawaban AI Overview dan People Also Ask.

### 4. Ekspor Data Terpisah
Menu unduhan kustom dengan animasi transisi yang menyediakan 3 format ekspor:
- **Microsoft Excel (.XLS)**: File spreadsheet native dengan format tabel rapi.
- **CSV Spreadsheet (.CSV)**: File teks nilai terpisah koma berstandar UTF-8 BOM.
- **Data JSON (.JSON)**: Struktur data mentah terstruktur untuk integrasi database atau pipeline data.

### 5. Pengujian Klik Iklan dan Proteksi Blacklist Multi-Model
- Fitur simulasi klik iklan kompetitor untuk keperluan Quality Assurance (QA).
- Deteksi komprehensif seluruh model iklan Google: Iklan Pencarian (Top/Bottom Ads), Iklan Google Maps (Local 3-Pack Sponsored, Promoted Map Pins), Iklan Google Shopping (PLA), dan Iklan Ekstensi Lokasi/Bisnis.
- Sistem proteksi blacklist tingkat lanjut via file `setup - DO NOT DELETE/blacklist.txt`: mendukung daftar nama domain web sekaligus NAMA BISNIS / BRAND (misal: `mesinprinting.id`, `ANUGRAH SUPPLY MANDIRI`).
- Algoritma pencocokan multi-lapis (pencocokan substring, token kata kunci, alphanumerik tanpa tanda baca, serta pengecekan teks kartu iklan secara menyeluruh) untuk memastikan iklan dan profil Google Maps milik Anda tidak akan pernah terklik secara otomatis.
- Opsi lewati semua Iklan Google Maps bagi pengguna yang hanya ingin menargetkan iklan peramban web reguler.
- Opsi whitelist khusus untuk menargetkan hanya domain iklan tertentu yang diinginkan.
- Batasan maksimal klik per kata kunci yang dapat dikonfigurasi secara presisi.

### 6. Anti-Detection Stealth Engine
- Bypass otomatis objek `navigator.webdriver`.
- Penerapan jitter jeda acak di antara setiap permintaan kata kunci guna meminimalkan risiko blokir bot atau CAPTCHA.
- Dukungan proxy kustom (format HTTP / SOCKS5 dengan otentikasi username dan password).

### 7. Antarmuka Kustom Modern
- Tipografi konsisten menggunakan Google Font **Poppins**.
- Kontrol form kustom tanpa elemen bawaan browser yang kaku: tombol stepper tambah/kurang numerik dan kotak centang beranimasi khusus.
- Terminal aktivitas real-time dengan status proses interaktif dan indikator persentase pemindaian.

---

## Petunjuk Pengoperasian

### Metode 1: Menggunakan Aplikasi Executable (Disarankan)

1. Pastikan Node.js telah terinstal pada sistem Windows Anda.
2. Buka folder direktori `SERP_Scanner`.
3. Jalankan file:
   ```text
   GoogleSerpScanner.exe
   ```
4. Jendela aplikasi Windows desktop akan terbuka seketika dengan dashboard yang tersemat langsung secara native (didukung oleh engine Microsoft WebView2), tanpa perlu membuka tab peramban eksternal.
5. Server Node.js dimulai di latar belakang. Pengguna dapat langsung memasukkan kata kunci pada formulir input dan klik **Mulai Scan Sekarang**.
6. Bilah navigasi atas menyediakan tombol aksi cepat: **Folder Data**, **Restart Server**, **Log Terminal** (untuk melihat aktivitas backend), serta opsi **Browser Luar** jika ingin membuka di browser eksternal.

### Metode 2: Mode Pengembang (Node.js CLI)

Jika ingin menjalankan aplikasi secara manual melalui command prompt atau PowerShell:

1. Buka terminal di dalam subdirektori `app/`:
   ```bash
   cd app
   ```
2. Jalankan perintah instalasi dependensi (hanya pertama kali):
   ```bash
   npm install
   ```
3. Mulai server:
   ```bash
   node server.js
   ```
4. Buka peramban pada `http://127.0.0.1:4321`.

---

## Panduan Konfigurasi Eksternal

Folder `setup - DO NOT DELETE` memuat file-file konfigurasi yang dapat disunting menggunakan Notepad atau teks editor lainnya:

### 1. `keywords.txt`
Daftar kata kunci awal yang akan langsung terisi pada form input setiap kali tombol "Muat keywords.txt" ditekan.
- Format: Masukkan satu kata kunci per baris.

### 2. `blacklist.txt`
Daftar domain website dan nama bisnis/brand sendiri yang wajib dilindungi dari aksi klik otomatis.
- Format: Masukkan domain atau nama bisnis satu per baris atau pisahkan dengan tanda koma (contoh: `mesinprinting.id`, `ANUGRAH SUPPLY MANDIRI`).
- Proteksi berlaku untuk seluruh model iklan Google (Iklan Pencarian Web, Iklan Google Maps Local Pack, Iklan Shopping, maupun Ekstensi Lokasi).

### 3. `config.txt`
Pengaturan preferensi awal saat server dinyalakan:
- `DELAY_SECONDS`: Jeda default antar pencarian dalam detik (1 sampai 15).
- `HEADLESS`: Nilai `true` untuk mode latar belakang, atau `false` untuk menampilkan peramban secara visual.
- `SKIP_MAP_ADS`: Nilai `true` untuk otomatis melewati seluruh iklan Google Maps, atau `false` untuk mengizinkan klik pada iklan Maps kompetitor.
- `TARGET_ADS`: Daftar domain atau nama iklan kompetitor yang wajib diklik (whitelist).

---

## Kompilasi Ulang Executable Windows

Jika Anda melakukan perubahan pada launcher C# (`app/GoogleSerpScanner.cs`), kompilasi ulang dapat dilakukan langsung menggunakan C# compiler bawaan Windows tanpa memerlukan instalasi Visual Studio:

```powershell
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /target:winexe /win32manifest:app\app.manifest /win32icon:app\app.ico /out:GoogleSerpScanner.exe /reference:System.Windows.Forms.dll,System.Drawing.dll,app\lib\Microsoft.Web.WebView2.WinForms.dll,app\lib\Microsoft.Web.WebView2.Core.dll app\GoogleSerpScanner.cs
```

---

## Persyaratan Sistem

- **Sistem Operasi**: Windows 10 atau Windows 11 (64-bit).
- **Runtime Lingkungan**: Node.js versi 18 LTS atau yang lebih baru.
- **Framework Sistem**: Microsoft .NET Framework 4.5+ (terpasang secara standar di Windows 10/11).
- **Peramban Engine**: Playwright Chromium (diunduh otomatis saat instalasi awal).

---

## Lisensi

Hak Cipta (c) 2026. Seluruh hak cipta dilindungi undang-undang. Dirilis di bawah Lisensi MIT.

