# 🔍 Google SERP Multi-Device Scanner (Windows)

Aplikasi desktop native Windows (.exe) untuk memindai (scan) hasil penelusuran **Google Halaman 1** secara massal (hingga 300+ kata kunci) dari berbagai perangkat (**Desktop**, **Mobile**, dan **Tablet**).

Aplikasi ini berdiri sendiri (*standalone*) di folder:
```text
C:\Users\dell\Documents\google-serp-scanner
```

---

## 🚀 Cara Menjalankan Aplikasi di Windows

1. Buka folder aplikasi di Windows Explorer:
   ```text
   C:\Users\dell\Documents\google-serp-scanner
   ```

2. **Double-Click** file aplikasi:
   ```text
   GoogleSerpScanner.exe
   ```

3. Jendela aplikasi Windows akan terbuka, server otomatis berjalan di latar belakang (tanpa jendela command prompt hitam), dan browser Anda akan otomatis membuka dashboard:
   ```text
   http://localhost:4321
   ```

4. Masukkan daftar kata kunci Anda (hingga 300+ baris), pilih perangkat (Desktop / Mobile / Tablet), lalu klik **🚀 Mulai Scan Sekarang**.

---

## 📋 Fitur Utama
1. **Format Executable Native (.exe)**: Berjalan langsung sebagai aplikasi Windows tanpa file batch (.bat).
2. **Input Massal**: Ketik, paste langsung hingga 300+ baris, atau upload file `.txt` / `.csv`.
3. **Multi-Device**: Desktop (1920x1080), Mobile (Pixel 7), & Tablet (iPad Air).
4. **Detail Penandaan Lengkap**:
   - 🌿 **Organik**: Peringkat 1-10+, Judul, Domain, Clean URL, Snippet, Sitelinks.
   - 🟧 **Google Ads**: Sponsor Atas (*Top Ads*) & Sponsor Bawah (*Bottom Ads*), Headline, Domain/Display Link, URL Iklan, Snippet.
   - 🗺️ **Google Maps (Local 3-Pack)**: Nama Toko/Profil Bisnis, Rating Bintang, Jumlah Ulasan, Alamat/Kategori, Nomor Telepon.
   - 🟣 **AI Overview / Featured Snippet**: Jawaban langsung posisi 0 (*Position Zero*) beserta domain sitasi.
   - ❓ **People Also Ask (PAA)**: Pertanyaan-pertanyaan populer Google.
5. **Pencarian Cepat Domain Sendiri**: Ketik nama domain Anda di kolom pencarian (misal: `mesinprinting.id`) untuk langsung melihat di keyword mana saja website Anda muncul di halaman 1.
6. **Ekspor Sekali Klik**:
   - **📊 Excel / CSV**: Format UTF-8 BOM yang otomatis rapi dan langsung terbaca di Microsoft Excel.
   - **📄 JSON**: Format data mentah terstruktur.
7. **Anti-Detection Stealth**: Bypass otomatis `navigator.webdriver` dan delay acak (jitter) agar aman dari blokir Google saat memindai ratusan keyword.
