import http from 'node:http';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { createBrowser, scanKeywordOnDevice, DEVICE_PROFILES } from './scanner.js';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const HOST = process.env.HOST || '0.0.0.0';
const ACCESS_HOST = process.env.ACCESS_HOST || '127.0.0.1';
const PORT = process.env.PORT || 4321;
const PUBLIC_DIR = path.join(__dirname, 'public');
const SCREENSHOTS_DIR = path.join(PUBLIC_DIR, 'screenshots');
if (!fs.existsSync(SCREENSHOTS_DIR)) {
  fs.mkdirSync(SCREENSHOTS_DIR, { recursive: true });
}

const DEFAULT_OWN_DOMAINS = [
  'mesinprinting.id',
  'anugrahsupplymandiri.com',
  'anugrahsupplymandiri.co.id'
];

// Global Scanning Session State
let scanSession = {
  status: 'idle',
  shouldStop: false,
  keywords: [],
  devices: ['desktop'],
  delaySeconds: 3,
  headless: true,
  proxy: null,

  // Click-Ads options
  clickAds: false,
  ownDomains: [...DEFAULT_OWN_DOMAINS],
  targetAds: [],
  targetOnly: false,
  clickBottomAds: false,
  maxClicksPerKeyword: 3,
  clickSummary: { totalClicked: 0, totalSkipped: 0, details: [] },

  currentIndex: 0,
  currentKeyword: '',
  currentDevice: '',
  totalTasks: 0,
  completedTasks: 0,
  results: [],
  logs: [],
  startedAt: null,
  completedAt: null
};

// Connected SSE clients
const sseClients = new Set();

function broadcastSSE(eventType, data) {
  const message = `event: ${eventType}\ndata: ${JSON.stringify(data)}\n\n`;
  for (const res of sseClients) {
    try {
      res.write(message);
    } catch (e) {
      sseClients.delete(res);
    }
  }
}

function addLog(message, type = 'info') {
  const logEntry = {
    id: Date.now() + '-' + Math.random().toString(36).substr(2, 4),
    timestamp: new Date().toLocaleTimeString('id-ID'),
    message,
    type
  };
  scanSession.logs.unshift(logEntry);
  if (scanSession.logs.length > 300) scanSession.logs.pop();
  broadcastSSE('log', logEntry);
}

// Background scan runner
async function runScanSession() {
  scanSession.status = 'running';
  scanSession.shouldStop = false;
  scanSession.startedAt = new Date().toISOString();
  scanSession.completedAt = null;
  scanSession.completedTasks = 0;

  const tasks = [];
  for (const kw of scanSession.keywords) {
    for (const dev of scanSession.devices) {
      tasks.push({ keyword: kw, device: dev });
    }
  }
  scanSession.totalTasks = tasks.length;

  addLog(
    `Memulai pemindaian: ${scanSession.keywords.length} kata kunci di ${scanSession.devices.length} jenis perangkat (${tasks.length} kombinasi)...`,
    'info'
  );
  if (scanSession.clickAds) {
    addLog(
      `🖱️ Mode Klik Iklan AKTIF — maks ${scanSession.maxClicksPerKeyword} klik/keyword. Own-domain dilewati: ${scanSession.ownDomains.join(', ')}`,
      'warning'
    );
  }
  broadcastSSE('status', getSessionStatus());

  let browser = null;
  try {
    addLog(`Membuka browser Google Chrome / MS Edge (Headless: ${scanSession.headless})...`, 'info');
    if (scanSession.proxy) {
      const sanitizedProxy = scanSession.proxy.replace(/:[^:@]*@/, ':***@');
      addLog(`🌐 Menggunakan Proxy Server: ${sanitizedProxy}`, 'info');
    }
    browser = await createBrowser(scanSession.headless, scanSession.proxy);
    addLog(`Browser berhasil diluncurkan dengan Anti-Detection Stealth bypass.`, 'success');

    let consecutiveCaptchaErrors = 0;
    for (let i = 0; i < tasks.length; i++) {
      if (scanSession.shouldStop) {
        addLog(`Pemindaian dihentikan oleh pengguna.`, 'warning');
        scanSession.status = 'stopped';
        break;
      }

      const task = tasks[i];
      scanSession.currentIndex = i + 1;
      scanSession.currentKeyword = task.keyword;
      scanSession.currentDevice = task.device;

      const devName = DEVICE_PROFILES[task.device]?.name || task.device;
      addLog(`[${i + 1}/${tasks.length}] Memindai: "${task.keyword}" [${devName}]...`, 'scan');
      broadcastSSE('progress', {
        currentIndex: i + 1,
        totalTasks: tasks.length,
        currentKeyword: task.keyword,
        currentDevice: task.device
      });

      const res = await scanKeywordOnDevice(browser, task.keyword, task.device, {
        locale: 'id-ID',
        screenshotsDir: SCREENSHOTS_DIR,
        captureScreenshot: true,
        clickAds: scanSession.clickAds === true,
        ownDomains: scanSession.ownDomains,
        targetAds: scanSession.targetAds,
        targetOnly: scanSession.targetOnly === true,
        clickBottomAds: scanSession.clickBottomAds === true,
        maxClicks: scanSession.maxClicksPerKeyword || 3,
        clickDelayMs: 1000
      });

      if (res.success) {
        const adsCount = res.items.filter(x => x.category === 'ADS').length;
        const orgCount = res.items.filter(x => x.category === 'ORGANIC').length;
        const mapCount = res.items.filter(x => x.category === 'MAPS').length;

        scanSession.results.push(...res.items);

        if (res.items.length === 0) {
          addLog(`⚠️ Peringatan: Tidak ada item diekstrak dari "${task.keyword}" (${task.device}). Kemungkinan Google memblokir atau layout berubah.`, 'warning');
        }

        // LOGGING IKLAN YANG JELAS
        if (res.clickSummary) {
          const c = res.clickSummary.clicked?.length || 0;
          const s = res.clickSummary.skipped?.length || 0;
          const f = res.clickSummary.foundAds || 0;
          scanSession.clickSummary.totalClicked += c;
          scanSession.clickSummary.totalSkipped += s;

          if (c > 0 || s > 0) {
            scanSession.clickSummary.details.push({
              keyword: task.keyword, device: task.device,
              clicked: res.clickSummary.clicked, skipped: res.clickSummary.skipped
            });
          }

          if (scanSession.clickAds) {
            if (f > 0) {
              addLog(`🎯 [IKLAN DITEMUKAN] "${task.keyword}" (${task.device}): ${f} iklan terdeteksi.`, 'info');
              for (const sk of (res.clickSummary.skipped || [])) {
                if (sk.reason === 'own-domain') {
                  addLog(`🛡️ [DILEWATI - DOMAIN SENDIRI] Iklan #${sk.adId} "${sk.title}" (${sk.domain}) TIDAK diklik demi melindungi budget sendiri (Domain: ${sk.matchedDomain || 'terproteksi'}).`, 'warning');
                } else if (sk.reason === 'not-in-target-list') {
                  addLog(`ℹ️ [DILEWATI - BUKAN TARGET] Iklan #${sk.adId} "${sk.title}" (${sk.domain}) dilewati karena bukan target yang di-whitelist.`, 'info');
                } else if (sk.reason === 'bottom-ads-disabled') {
                  addLog(`ℹ️ [DILEWATI - SPONSOR BAWAH] Iklan #${sk.adId} "${sk.title}" (${sk.domain}) dilewati karena opsi 'Klik iklan bawah' nonaktif.`, 'info');
                } else if (sk.reason === 'over-max-clicks') {
                  addLog(`ℹ️ [DILEWATI - KUOTA KLIK] Iklan #${sk.adId} "${sk.title}" (${sk.domain}) dilewati karena kuota maks (${scanSession.maxClicksPerKeyword} klik/kw) sudah terpenuhi.`, 'info');
                } else {
                  addLog(`ℹ️ [DILEWATI] Iklan #${sk.adId} "${sk.title}" (${sk.domain}): ${sk.reason}`, 'info');
                }
              }
              for (const cl of (res.clickSummary.clicked || [])) {
                if (cl.isTarget) {
                  addLog(`🎯 [KLIK TARGET UTAMA] Iklan Target #${cl.adId} "${cl.title}" (${cl.domain}) [Target: ${cl.matchedTarget}] BERHASIL DIKLIK via ${cl.method} (${Math.round((cl.dwellMs || 2500)/1000)}s)!`, 'success');
                } else {
                  addLog(`🖱️ [BERHASIL DIKLIK] Iklan Kompetitor #${cl.adId} "${cl.title}" (${cl.domain}) berhasil dibuka via ${cl.method} (${Math.round((cl.dwellMs || 2500)/1000)}s).`, 'success');
                }
              }
            } else {
              addLog(`ℹ️ [IKLAN] "${task.keyword}" (${task.device}): Tidak ada iklan yang tampil di halaman ini.`, 'debug');
            }
          } else if (adsCount > 0) {
            addLog(`ℹ️ [IKLAN TERDETEKSI] "${task.keyword}" (${task.device}): Ditemukan ${adsCount} iklan di Google, namun opsi 'Klik Iklan' sedang nonaktif.`, 'info');
          }
        }

        if (res.items.length > 0) {
          addLog(`✔ Selesai: "${task.keyword}" (${task.device}) -> ${orgCount} Organik, ${adsCount} Iklan, ${mapCount} Maps`, 'success');
        }

        consecutiveCaptchaErrors = 0;
        broadcastSSE('new_items', {
          keyword: task.keyword, device: task.device, items: res.items
        });
      } else {
        // Tangkap pesan error dari scanner (misal: CAPTCHA)
        addLog(`✖ Gagal pada "${task.keyword}" (${task.device}): ${res.error}`, 'error');
        if (res.error && res.error.includes('BLOKIR_CAPTCHA')) {
          consecutiveCaptchaErrors++;
          if (consecutiveCaptchaErrors >= 2) {
            addLog(`⛔ PENGHENTIAN KEAMANAN OTOMATIS: Terdeteksi blokir CAPTCHA Google berturut-turut! Pemindaian otomatis dihentikan agar IP Anda tidak di-blacklist oleh Google. Harap ganti koneksi IP (misal: mode pesawat HP / restart modem).`, 'error');
            scanSession.shouldStop = true;
          }
        }
      }

      scanSession.completedTasks = i + 1;
      broadcastSSE('status', getSessionStatus());

      if (i < tasks.length - 1 && !scanSession.shouldStop) {
        const baseDelay = (scanSession.delaySeconds || 3) * 1000;
        const jitter = Math.floor(Math.random() * 1500);
        const totalDelay = baseDelay + jitter;
        addLog(`Menunggu ${Math.round(totalDelay / 100) / 10} detik jeda keamanan anti-spam Google...`, 'debug');
        await new Promise(r => setTimeout(r, totalDelay));
      }
    }

    if (!scanSession.shouldStop) {
      scanSession.status = 'completed';
      scanSession.completedAt = new Date().toISOString();
      addLog(
        `🎉 Pemindaian seluruh kata kunci selesai! Total ${scanSession.results.length} item tersimpan.` +
        (scanSession.clickAds
          ? ` (Klik iklan: ${scanSession.clickSummary.totalClicked} berhasil, ${scanSession.clickSummary.totalSkipped} dilewati)`
          : ''),
        'success'
      );
    }
  } catch (err) {
    addLog(`Terjadi kesalahan kritis saat memindai: ${err.message}`, 'error');
    scanSession.status = 'error';
  } finally {
    if (browser) {
      await browser.close().catch(() => { });
      addLog(`Browser ditutup.`, 'info');
    }
    broadcastSSE('status', getSessionStatus());
  }
}

function getSessionStatus() {
  const ads = scanSession.results.filter(r => r.category === 'ADS').length;
  const organic = scanSession.results.filter(r => r.category === 'ORGANIC').length;
  const maps = scanSession.results.filter(r => r.category === 'MAPS').length;
  const ai = scanSession.results.filter(r => r.category === 'AI_OVERVIEW').length;
  const paa = scanSession.results.filter(r => r.category === 'PAA').length;

  return {
    status: scanSession.status,
    totalTasks: scanSession.totalTasks,
    completedTasks: scanSession.completedTasks,
    percent: scanSession.totalTasks > 0
      ? Math.round((scanSession.completedTasks / scanSession.totalTasks) * 100)
      : 0,
    currentKeyword: scanSession.currentKeyword,
    currentDevice: scanSession.currentDevice,
    totalResults: scanSession.results.length,
    counts: { ads, organic, maps, ai, paa },
    keywordsCount: scanSession.keywords.length,
    devices: scanSession.devices,
    startedAt: scanSession.startedAt,
    completedAt: scanSession.completedAt,
    clickInfo: {
      enabled: scanSession.clickAds === true,
      ownDomains: scanSession.ownDomains,
      targetAds: scanSession.targetAds,
      targetOnly: scanSession.targetOnly,
      clickBottomAds: scanSession.clickBottomAds,
      maxClicksPerKeyword: scanSession.maxClicksPerKeyword,
      totalClicked: scanSession.clickSummary.totalClicked,
      totalSkipped: scanSession.clickSummary.totalSkipped
    }
  };
}

// Convert dataset to clean CSV with Excel-friendly UTF-8 BOM
function generateCSV(items) {
  const headers = [
    'No',
    'Kata Kunci',
    'Perangkat',
    'Kategori',
    'Posisi / Placement',
    'Peringkat (Rank)',
    'Judul (Title)',
    'Domain',
    'URL Target',
    'Status Klik Iklan',
    'Detail Klik Iklan',
    'Deskripsi / Snippet / Jawaban AI',
    'Sitelinks / Sumber Sitasi AI',
    'Catatan / Penanda',
    'Bukti Screenshot URL'
  ];

  const escapeCsv = (str) => {
    if (str === null || str === undefined) return '""';
    const s = String(str).replace(/"/g, '""').replace(/\r?\n/g, ' ');
    return `"${s}"`;
  };

  const rows = items.map((it, idx) => [
    idx + 1,
    escapeCsv(it.keyword),
    escapeCsv(it.device),
    escapeCsv(it.category),
    escapeCsv(it.placement),
    it.rank !== undefined ? it.rank : '',
    escapeCsv(it.title),
    escapeCsv(it.domain),
    escapeCsv(it.url),
    escapeCsv(it.category === 'ADS' ? (it.isClicked ? 'DIKLIK OTOMATIS' : (it.clickStatus || 'TIDAK DIKLIK')) : '-'),
    escapeCsv(it.clickDetails || '-'),
    escapeCsv(it.snippet),
    escapeCsv(Array.isArray(it.sitelinks) ? it.sitelinks.join(' | ') : ''),
    escapeCsv(it.extra),
    escapeCsv(it.screenshot ? `http://localhost:${PORT}${it.screenshot}` : '')
  ].join(','));

  return '\uFEFF' + [headers.join(','), ...rows].join('\r\n');
}

// MIME Types map
const MIME_TYPES = {
  '.html': 'text/html; charset=utf-8',
  '.css': 'text/css; charset=utf-8',
  '.js': 'application/javascript; charset=utf-8',
  '.json': 'application/json; charset=utf-8',
  '.png': 'image/png',
  '.jpg': 'image/jpeg',
  '.jpeg': 'image/jpeg',
  '.svg': 'image/svg+xml',
  '.ico': 'image/x-icon'
};

const server = http.createServer(async (req, res) => {
  const parsedUrl = new URL(req.url, `http://${req.headers.host}`);
  const pathname = parsedUrl.pathname;

  res.setHeader('Access-Control-Allow-Origin', '*');
  res.setHeader('Access-Control-Allow-Methods', 'GET, POST, OPTIONS');
  res.setHeader('Access-Control-Allow-Headers', 'Content-Type');

  if (req.method === 'OPTIONS') {
    res.writeHead(204);
    res.end();
    return;
  }

  // SSE Stream
  if (pathname === '/api/scan/stream') {
    res.writeHead(200, {
      'Content-Type': 'text/event-stream',
      'Cache-Control': 'no-cache',
      'Connection': 'keep-alive'
    });
    res.write(`event: init\ndata: ${JSON.stringify({ status: getSessionStatus(), logs: scanSession.logs.slice(0, 50) })}\n\n`);
    sseClients.add(res);

    req.on('close', () => {
      sseClients.delete(res);
    });
    return;
  }

  // API: Get Status
  if (pathname === '/api/scan/status' && req.method === 'GET') {
    res.writeHead(200, { 'Content-Type': 'application/json' });
    res.end(JSON.stringify(getSessionStatus()));
    return;
  }

  // API: Get Results
  if (pathname === '/api/scan/results' && req.method === 'GET') {
    const category = parsedUrl.searchParams.get('category');
    const device = parsedUrl.searchParams.get('device');
    const search = (parsedUrl.searchParams.get('search') || '').toLowerCase();

    let filtered = scanSession.results;
    if (category && category !== 'ALL') {
      filtered = filtered.filter(x => x.category === category);
    }
    if (device && device !== 'ALL') {
      filtered = filtered.filter(x => x.device === device);
    }
    if (search) {
      filtered = filtered.filter(x =>
        (x.title && x.title.toLowerCase().includes(search)) ||
        (x.domain && x.domain.toLowerCase().includes(search)) ||
        (x.keyword && x.keyword.toLowerCase().includes(search)) ||
        (x.url && x.url.toLowerCase().includes(search))
      );
    }

    res.writeHead(200, { 'Content-Type': 'application/json' });
    res.end(JSON.stringify({
      total: filtered.length,
      allTotal: scanSession.results.length,
      results: filtered
    }));
    return;
  }

  // API: Start Scan
  if (pathname === '/api/scan/start' && req.method === 'POST') {
    let body = '';
    req.on('data', chunk => { body += chunk; });
    req.on('end', () => {
      try {
        const payload = JSON.parse(body || '{}');
        const rawKeywords = payload.keywords || [];
        const cleanedKeywords = rawKeywords
          .map(k => String(k).trim())
          .filter(k => k.length > 0);

        if (cleanedKeywords.length === 0) {
          res.writeHead(400, { 'Content-Type': 'application/json' });
          res.end(JSON.stringify({ error: 'Daftar kata kunci tidak boleh kosong!' }));
          return;
        }

        if (scanSession.status === 'running') {
          res.writeHead(409, { 'Content-Type': 'application/json' });
          res.end(JSON.stringify({ error: 'Pemindaian sedang berjalan. Harap tunggu atau klik Berhenti terlebih dahulu.' }));
          return;
        }

        scanSession.keywords = cleanedKeywords;
        scanSession.devices = Array.isArray(payload.devices) && payload.devices.length > 0
          ? payload.devices
          : ['desktop'];
        scanSession.delaySeconds = Number(payload.delaySeconds) || 3;
        scanSession.headless = payload.headless !== false;
        scanSession.proxy = payload.proxy && typeof payload.proxy === 'string' && payload.proxy.trim().length > 0
          ? payload.proxy.trim()
          : null;

        // Opsi klik iklan (default MATI demi keamanan)
        scanSession.clickAds = payload.clickAds === true;
        scanSession.ownDomains = Array.isArray(payload.ownDomains) && payload.ownDomains.length
          ? payload.ownDomains
            .map(d => String(d).trim().toLowerCase().replace(/^https?:\/\//, '').replace(/^www\./, '').split('/')[0])
            .filter(Boolean)
          : [...DEFAULT_OWN_DOMAINS];
        scanSession.targetAds = Array.isArray(payload.targetAds)
          ? payload.targetAds
            .map(d => String(d).trim().toLowerCase().replace(/^https?:\/\//, '').replace(/^www\./, '').split('/')[0])
            .filter(Boolean)
          : [];
        scanSession.targetOnly = payload.targetOnly === true;
        scanSession.clickBottomAds = payload.clickBottomAds === true;
        scanSession.maxClicksPerKeyword = Math.max(1, Math.min(10, Number(payload.maxClicksPerKeyword) || 3));
        scanSession.clickSummary = { totalClicked: 0, totalSkipped: 0, details: [] };

        if (payload.clearPrevious !== false) {
          scanSession.results = [];
          scanSession.logs = [];
        }

        runScanSession();

        res.writeHead(200, { 'Content-Type': 'application/json' });
        res.end(JSON.stringify({
          message: 'Pemindaian dimulai!',
          keywordsCount: cleanedKeywords.length,
          devices: scanSession.devices,
          clickAds: scanSession.clickAds,
          ownDomains: scanSession.ownDomains
        }));
      } catch (err) {
        res.writeHead(500, { 'Content-Type': 'application/json' });
        res.end(JSON.stringify({ error: err.message }));
      }
    });
    return;
  }

  // API: Stop Scan
  if (pathname === '/api/scan/stop' && req.method === 'POST') {
    scanSession.shouldStop = true;
    addLog('Perintah stop diterima, menghentikan antrean setelah keyword saat ini...', 'warning');
    res.writeHead(200, { 'Content-Type': 'application/json' });
    res.end(JSON.stringify({ message: 'Menghentikan pemindaian...' }));
    return;
  }

  // API: Clear Results
  if (pathname === '/api/scan/clear' && req.method === 'POST') {
    if (scanSession.status === 'running') {
      res.writeHead(400, { 'Content-Type': 'application/json' });
      res.end(JSON.stringify({ error: 'Tidak dapat menghapus data saat pemindaian masih berjalan!' }));
      return;
    }
    scanSession.results = [];
    scanSession.logs = [];
    scanSession.completedTasks = 0;
    scanSession.totalTasks = 0;
    scanSession.status = 'idle';
    scanSession.clickSummary = { totalClicked: 0, totalSkipped: 0, details: [] };
    broadcastSSE('status', getSessionStatus());
    res.writeHead(200, { 'Content-Type': 'application/json' });
    res.end(JSON.stringify({ message: 'Hasil dan log berhasil dibersihkan.' }));
    return;
  }

  // API: Click Ads Report (audit)
  if (pathname === '/api/scan/click-report' && req.method === 'GET') {
    res.writeHead(200, { 'Content-Type': 'application/json' });
    res.end(JSON.stringify({
      enabled: scanSession.clickAds === true,
      ownDomains: scanSession.ownDomains,
      clickBottomAds: scanSession.clickBottomAds,
      maxClicksPerKeyword: scanSession.maxClicksPerKeyword,
      summary: scanSession.clickSummary
    }));
    return;
  }

  // Export: CSV
  if (pathname === '/api/scan/export/csv' && req.method === 'GET') {
    const csvContent = generateCSV(scanSession.results);
    const filename = `google-serp-scan-${new Date().toISOString().slice(0, 10)}.csv`;
    res.writeHead(200, {
      'Content-Type': 'text/csv; charset=utf-8',
      'Content-Disposition': `attachment; filename="${filename}"`
    });
    res.end(csvContent);
    return;
  }

  // Export: JSON
  if (pathname === '/api/scan/export/json' && req.method === 'GET') {
    const filename = `google-serp-scan-${new Date().toISOString().slice(0, 10)}.json`;
    res.writeHead(200, {
      'Content-Type': 'application/json; charset=utf-8',
      'Content-Disposition': `attachment; filename="${filename}"`
    });
    res.end(JSON.stringify(scanSession.results, null, 2));
    return;
  }

  // Static File Serving
  let filePath = path.join(PUBLIC_DIR, pathname === '/' ? 'index.html' : pathname);
  const ext = path.extname(filePath).toLowerCase();
  const contentType = MIME_TYPES[ext] || 'application/octet-stream';

  fs.readFile(filePath, (err, content) => {
    if (err) {
      if (err.code === 'ENOENT') {
        res.writeHead(404, { 'Content-Type': 'text/plain; charset=utf-8' });
        res.end('404 Not Found');
      } else {
        res.writeHead(500, { 'Content-Type': 'text/plain; charset=utf-8' });
        res.end(`Server Error: ${err.code}`);
      }
    } else {
      res.writeHead(200, { 'Content-Type': contentType, 'Cache-Control': 'no-cache, no-store, must-revalidate' });
      res.end(content);
    }
  });
});

server.listen(PORT, HOST, () => {
  const displayHost = ACCESS_HOST || (HOST === '0.0.0.0' ? '127.0.0.1' : HOST);
  console.log(`=======================================================`);
  console.log(`Google SERP Scanner siap berjalan di Windows!`);
  console.log(`Buka di browser: http://${displayHost}:${PORT}`);
  console.log(`Bisa juga via IPv6: http://[::1]:${PORT}`);
  console.log(`Fitur klik iklan: ${scanSession.clickAds ? 'AKTIF' : 'nonaktif (default)'}`);
  console.log(`=======================================================`);
});