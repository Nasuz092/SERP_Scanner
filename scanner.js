import { chromium, devices } from 'playwright';
import path from 'node:path';

export const DEVICE_PROFILES = {
  desktop: {
    id: 'desktop',
    name: 'Desktop (1920x1080)',
    icon: '🖥️',
    viewport: { width: 1440, height: 900 },
    userAgent: 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0.0.0 Safari/537.36',
    isMobile: false,
    hasTouch: false
  },
  mobile: {
    id: 'mobile',
    name: 'Mobile Smartphone (Pixel 7)',
    icon: '📱',
    ...devices['Pixel 7']
  },
  tablet: {
    id: 'tablet',
    name: 'Tablet (iPad Air / 820x1180)',
    icon: '📟',
    viewport: { width: 820, height: 1180 },
    userAgent: 'Mozilla/5.0 (iPad; CPU OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1',
    isMobile: true,
    hasTouch: true
  }
};

export async function createBrowser(headless = true, proxy = null) {
  const commonArgs = [
    '--disable-blink-features=AutomationControlled',
    '--no-sandbox',
    '--disable-infobars',
    '--disable-dev-shm-usage',
    '--disable-features=IsolateOrigins,site-per-process'
  ];

  const launchOptions = { headless, args: commonArgs };
  if (proxy && typeof proxy === 'string' && proxy.trim().length > 0) {
    launchOptions.proxy = { server: proxy.trim() };
  } else if (proxy && proxy.server) {
    launchOptions.proxy = proxy;
  }

  try {
    return await chromium.launch({ channel: 'chrome', ...launchOptions });
  } catch (err) {
    return await chromium.launch({ channel: 'msedge', ...launchOptions });
  }
}

export async function extractSerpData(page, keyword, deviceType) {
  return await page.evaluate(({ keyword, deviceType }) => {
    const items = [];
    let overallOrder = 1;

    function cleanUrl(rawUrl) {
      if (!rawUrl) return '';
      try {
        let absoluteUrl = rawUrl.startsWith('/') ? 'https://www.google.com' + rawUrl : rawUrl;
        const u = new URL(absoluteUrl);

        // 1. Ekstraksi URL Asli dari Google Ads (adurl)
        if (u.searchParams.has('adurl')) {
          return decodeURIComponent(u.searchParams.get('adurl'));
        }
        if (u.searchParams.has('q') && u.pathname.includes('/aclk')) {
          return decodeURIComponent(u.searchParams.get('q'));
        }

        // 2. Ekstraksi Redirect Organik
        if (u.hostname.includes('google.com') && (u.pathname === '/url' || u.pathname === '/goto')) {
          const target = u.searchParams.get('q') || u.searchParams.get('url');
          if (target && (target.startsWith('http://') || target.startsWith('https://'))) {
            return decodeURIComponent(target);
          }
        }
        return absoluteUrl;
      } catch (e) {
        return rawUrl;
      }
    }

    function extractDomain(urlStr) {
      if (!urlStr) return '';
      try {
        const u = new URL(urlStr.startsWith('http') ? urlStr : 'https://' + urlStr);
        return u.hostname.replace(/^www\./, '');
      } catch (e) {
        return '';
      }
    }

    // ==========================================
    // 1. DETEKSI IKLAN (Hanya berdasarkan Link Valid)
    // ==========================================
    const adLinks = Array.from(document.querySelectorAll('a[href*="/aclk?"], a[href*="googleadservices"], a[href*="doubleclick.net"]'));
    const processedAds = new Set();
    let adRank = 1;

    adLinks.forEach(link => {
      // Cari container pembungkus iklan
      const container = link.closest('div.MjjYud, div.g, div[data-hveid], div.uEierd, div[data-text-ad]');
      if (!container || processedAds.has(container)) return;
      processedAds.add(container);

      // Pastikan bukan Maps
      if (container.querySelector('div[data-cid], .VkpGBb')) return;

      let heading = container.querySelector('h3');
      const cite = container.querySelector('cite, .x2VHCd, .v5yQqb, span.VuuXrf');
      const snippet = container.querySelector('.VwiC3b, .MUxGbd, [data-content-feature="1"]');

      const finalUrl = cleanUrl(link.href);
      let domain = extractDomain(finalUrl);

      if (cite) {
        const citeText = cite.textContent.trim().split(/[\s>›]/)[0].trim();
        if (citeText.includes('.') && !citeText.includes('google')) domain = citeText.replace(/^www\./, '');
      }

      // VALIDASI KETAT: Jangan tampilkan jika tidak ada judul / URL
      if (!heading && !finalUrl && !domain) return;

      const placement = (container.closest('#bottomads') || container.getBoundingClientRect().top > window.innerHeight / 1.5)
        ? 'Bottom Ad (Sponsor Bawah)' : 'Top Ad (Sponsor Atas)';

      items.push({
        keyword, device: deviceType, overallOrder: overallOrder++, category: 'ADS',
        placement: placement, rank: adRank++,
        title: heading ? heading.textContent.trim() : (domain || 'Iklan Google'),
        domain: domain || 'Iklan',
        url: finalUrl, // Jika kosong, UI akan handle
        snippet: snippet ? snippet.textContent.trim() : '',
        sitelinks: [],
        extra: 'Iklan Google Ads'
      });
    });

    // 2. AI OVERVIEW
    const aiOverview = document.querySelector('div[data-attrid="wa:/description"], div.LGOjhe, div.wDYxhc[data-attrid], div[aria-label*="AI Overview"], div[aria-label*="Ringkasan AI"], div.CKgc1d, div.qRuFed, div.FkX2oe');
    if (aiOverview) {
      const sourceTitle = aiOverview.querySelector('h3, [role="heading"]');
      const title = sourceTitle ? sourceTitle.textContent.trim() : 'Google AI Overview / Ringkasan Cerdas';
      const textNodes = Array.from(aiOverview.querySelectorAll('p, li, div.hgKElc, div.b1vJ2c, div.VwiC3b, div.YrbPuc'))
        .map(el => el.textContent.trim())
        .filter(t => t.length > 15 && !t.includes('Tampilkan lainnya') && !t.includes('Beri masukan'));
      let fullSnippet = textNodes.join('\n\n') || (aiOverview.innerText || '').replace(/Tampilkan lainnya/gi, '').trim();

      const citations = [];
      aiOverview.querySelectorAll('a[href]').forEach(a => {
        const finalU = cleanUrl(a.getAttribute('href') || a.href);
        let dom = extractDomain(finalU);
        if (dom && dom !== 'google.com' && !citations.some(c => c.domain === dom)) {
          citations.push({ name: a.textContent.trim() || dom, domain: dom, url: finalU });
        }
      });

      items.push({
        keyword, device: deviceType, overallOrder: overallOrder++, category: 'AI_OVERVIEW',
        placement: 'AI Overview / Posisi 0', rank: 0, title: title,
        domain: citations[0] ? citations[0].domain : 'google.com',
        url: citations[0] ? citations[0].url : '',
        snippet: fullSnippet || 'Ringkasan jawaban otomatis Google AI.',
        sitelinks: citations.map(c => `${c.name} (${c.domain})`),
        extra: citations.length ? `Disitir dari ${citations.length} sumber web` : 'Hasil Jawaban Langsung'
      });
    }

    // 3. GOOGLE MAPS
    const mapItems = document.querySelectorAll('div.VkpGBb, div.rllt__details, div[data-local-pack], div[data-cid]');
    const seenMapTitles = new Set();
    mapItems.forEach((m) => {
      if (m.querySelector('a[href*="youtube.com"]')) return;
      const nameEl = m.querySelector('[role="heading"], .OSrXXb, .dbg0pd, .fontHeadlineSmall');
      const ratingEl = m.querySelector('.Y0A0hc, span[aria-label*="bintang"], .yi40Hd');
      const descEl = m.querySelector('.rllt__wrapped, .wUrVib, .I6TXqe, .hG48Ns');
      const mapLink = m.querySelector('a[href*="google.com/maps"], a[href*="maps.google.com"]');

      if (nameEl && !seenMapTitles.has(nameEl.textContent.trim())) {
        const title = nameEl.textContent.trim();
        seenMapTitles.add(title);
        items.push({
          keyword, device: deviceType, overallOrder: overallOrder++, category: 'MAPS', placement: 'Google Maps', rank: seenMapTitles.size,
          title: title, domain: 'maps.google.com', url: mapLink ? mapLink.href : 'https://maps.google.com',
          snippet: [ratingEl ? `⭐ ${ratingEl.textContent.trim()}` : '', descEl ? descEl.textContent.trim() : ''].filter(Boolean).join(' • '),
          sitelinks: [], extra: 'Profil Bisnis Google Maps'
        });
      }
    });

    // 4. PEOPLE ALSO ASK
    const paaCards = document.querySelectorAll('div[related-question-pair], div.cbphWd, div.k27K1e');
    if (paaCards.length > 0) {
      const questions = [];
      paaCards.forEach(c => { if (c.textContent.trim().length > 6) questions.push(c.textContent.trim()); });
      items.push({
        keyword, device: deviceType, overallOrder: overallOrder++, category: 'PAA', placement: 'People Also Ask', rank: 1,
        title: 'Orang Lain Juga Bertanya', domain: 'google.com', url: '', snippet: questions.slice(0, 5).join(' | '),
        sitelinks: questions.slice(0, 6), extra: `${questions.length} pertanyaan`
      });
    }

    // 5. ORGANIC RESULTS
    let organicRank = 1;
    const seenOrganicUrls = new Set();
    const seenTitles = new Set();

    let organicCards = Array.from(document.querySelectorAll('#rso .MjjYud, #rso div.g, div.MjjYud, div.g'));
    if (organicCards.length === 0) {
      organicCards = Array.from(document.querySelectorAll('#rso h3')).map(h => h.closest('div')).filter(Boolean);
    }

    organicCards.forEach(card => {
      if (card.closest('#tads, #bottomads, div.VkpGBb, div[data-local-pack], div[related-question-pair]')) return;
      // Abaikan jika card ini terdeteksi sebagai Ads valid
      if (card.querySelector('a[href*="/aclk?"], [data-text-ad]')) return;

      const h3 = card.querySelector('h3');
      if (!h3 || !h3.textContent.trim() || seenTitles.has(h3.textContent.trim())) return;

      let anchor = h3.closest('a') || card.querySelector('a[href]');
      if (!anchor) return;

      const rawUrl = anchor.getAttribute('href') || anchor.href || '';
      if (!rawUrl || rawUrl.includes('google.com/search') || rawUrl.includes('google.com/policies') || rawUrl.startsWith('javascript:')) return;

      const finalUrl = cleanUrl(rawUrl);
      if (seenOrganicUrls.has(finalUrl)) return;

      let domain = extractDomain(finalUrl);
      const cite = card.querySelector('cite, .VuuXrf, span.OBQrM, [data-domain]');
      if (cite) {
        const citeText = cite.textContent.trim().split(/[\s>›•]/)[0].trim();
        if (citeText && !citeText.toLowerCase().includes('google') && !citeText.toLowerCase().includes('goto')) {
          domain = citeText.replace(/^https?:\/\//, '').replace(/^www\./, '');
        }
      }

      seenOrganicUrls.add(finalUrl);
      seenTitles.add(h3.textContent.trim());

      const snippet = card.querySelector('div.VwiC3b, div[data-sncf="1"], span.aCOpRe');
      const sitelinks = [];
      card.querySelectorAll('.usJj9c a, .bOeZT, a.l').forEach(sl => { if (sl.textContent.trim().length > 2) sitelinks.push(sl.textContent.trim()); });

      items.push({
        keyword, device: deviceType, overallOrder: overallOrder++, category: 'ORGANIC', placement: 'Organic Listing', rank: organicRank++,
        title: h3.textContent.trim(), domain: domain || 'Web',
        url: finalUrl,
        snippet: snippet ? snippet.textContent.trim() : '',
        sitelinks: sitelinks.slice(0, 4), extra: sitelinks.length ? 'Rich Snippet' : 'Hasil Organik'
      });
    });

    return items;
  }, { keyword, deviceType });
}

function normalizeDomain(d) {
  return String(d || '').trim().toLowerCase().replace(/^https?:\/\//, '').replace(/^www\./, '').split('/')[0];
}

export async function clickDetectedAds(page, options = {}) {
  const ownDomains = (Array.isArray(options.ownDomains) && options.ownDomains.length ? options.ownDomains : ['mesinprinting.id']).map(normalizeDomain).filter(Boolean);
  const clickBottomAds = options.clickBottomAds === true;
  const maxClicks = Math.max(1, Number(options.maxClicks) || 1);
  const clickDelayMs = Math.max(500, Number(options.clickDelayMs) || 1000);

  const result = { clicked: [], skipped: [], foundAds: 0 };

  const adAnchors = await page.evaluate((ownDomains) => {
    function isOwn(text) { return ownDomains.some(d => text.toLowerCase().includes(d)); }
    const results = [];
    document.querySelectorAll('a[href*="/aclk?"], a[href*="googleadservices"]').forEach(a => {
      let cite = '';
      const container = a.closest('div.MjjYud, div.g, div[data-hveid], #tads > div, #bottomads > div');
      if (container) {
        const citeEl = container.querySelector('cite');
        if (citeEl) cite = citeEl.innerText;
      }
      const href = a.href || '';
      const text = a.innerText || '';
      const isBottom = a.closest('#bottomads') !== null || a.getBoundingClientRect().top > window.innerHeight / 1.5;

      a.setAttribute('data-serp-ad-click', 'true');
      results.push({
        title: text.trim().substring(0, 100) || 'Iklan',
        cite: cite.trim(), href: href, isBottom: isBottom,
        isOwn: isOwn(text) || isOwn(href) || isOwn(cite)
      });
    });
    return results;
  }, ownDomains);

  result.foundAds = adAnchors.length;
  const handles = await page.$$('[data-serp-ad-click="true"]');
  const eligible = [];

  for (let i = 0; i < handles.length; i++) {
    const info = adAnchors[i];
    if (!info) continue;
    const data = { handle: handles[i], ...info };

    if (info.isOwn) {
      result.skipped.push({ placement: info.isBottom ? 'Bottom' : 'Top', title: info.title, cite: info.cite, reason: 'own-domain' });
    } else if (info.isBottom && !clickBottomAds) {
      result.skipped.push({ placement: 'Bottom', title: info.title, cite: info.cite, reason: 'bottom-ads-disabled' });
    } else {
      eligible.push(data);
    }
  }

  const toClick = eligible.slice(0, maxClicks);

  for (const cand of toClick) {
    try {
      await page.waitForTimeout(clickDelayMs + Math.floor(Math.random() * 700));
      let popup = null;
      try {
        const popupPromise = page.context().waitForEvent('page', { timeout: 8000 }).catch(() => null);
        await cand.handle.click({ modifiers: ['Control'] });
        popup = await popupPromise;
      } catch (e) {
        try { await cand.handle.click(); } catch (e2) { throw new Error('click failed: ' + e2.message); }
      }

      if (popup) {
        await popup.waitForLoadState('domcontentloaded', { timeout: 15000 }).catch(() => { });
        const dwell = 2000 + Math.floor(Math.random() * 2000);
        await popup.waitForTimeout(dwell);
        await popup.close().catch(() => { });
        result.clicked.push({ placement: cand.isBottom ? 'Bottom' : 'Top', title: cand.title, cite: cand.cite, method: 'new-tab', dwellMs: dwell });
      } else {
        await page.waitForLoadState('domcontentloaded', { timeout: 15000 }).catch(() => { });
        const dwell = 2000 + Math.floor(Math.random() * 2000);
        await page.waitForTimeout(dwell);
        result.clicked.push({ placement: cand.isBottom ? 'Bottom' : 'Top', title: cand.title, cite: cand.cite, method: 'same-tab', dwellMs: dwell });
        await page.goBack({ waitUntil: 'domcontentloaded', timeout: 15000 }).catch(() => { });
        await page.waitForTimeout(1000);
      }
    } catch (err) {
      result.skipped.push({ placement: 'Ad', title: cand.title, cite: cand.cite, reason: `click-error` });
    }
  }

  for (const cand of eligible.slice(maxClicks)) {
    result.skipped.push({ placement: 'Ad', title: cand.title, cite: cand.cite, reason: 'over-max-clicks' });
  }

  return result;
}

export async function scanKeywordOnDevice(browser, keyword, deviceKey, options = {}) {
  const profile = DEVICE_PROFILES[deviceKey] || DEVICE_PROFILES.desktop;
  const context = await browser.newContext({
    userAgent: profile.userAgent, viewport: profile.viewport,
    isMobile: profile.isMobile || false, hasTouch: profile.hasTouch || false, locale: options.locale || 'id-ID'
  });

  const page = await context.newPage();
  await page.addInitScript(() => { Object.defineProperty(navigator, 'webdriver', { get: () => undefined }); });

  try {
    const searchUrl = `https://www.google.com/search?q=${encodeURIComponent(keyword)}&hl=id&gl=id`;
    await page.goto(searchUrl, { waitUntil: 'domcontentloaded', timeout: 30000 });

    if (page.url().includes('/sorry/') || (await page.evaluate(() => document.body.innerText)).toLowerCase().includes('recaptcha')) {
      throw new Error('BLOKIR_CAPTCHA: IP terdeteksi robot oleh Google.');
    }

    try {
      const consentBtn = await page.locator('button:has-text("Setuju"), button:has-text("Accept all")').first();
      if (await consentBtn.isVisible({ timeout: 2000 })) await consentBtn.click();
    } catch (e) { }

    await page.waitForSelector('#rso, #search, div.g, div.MjjYud', { timeout: 10000 }).catch(() => { });
    await page.waitForTimeout(2000);

    await page.evaluate(async () => {
      await new Promise((resolve) => {
        let totalHeight = 0, distance = 400;
        let timer = setInterval(() => {
          let scrollHeight = document.body.scrollHeight;
          window.scrollBy(0, distance); totalHeight += distance;
          if (totalHeight >= scrollHeight - window.innerHeight) { clearInterval(timer); window.scrollTo(0, 0); resolve(); }
        }, 100);
      });
    });
    await page.waitForTimeout(1000);

    let screenshotUrl = null;
    if (options.captureScreenshot !== false && options.screenshotsDir) {
      try {
        const safeKw = keyword.replace(/[^a-zA-Z0-9_]/g, '_').substring(0, 30);
        const fileName = `serp_${profile.id}_${safeKw}_${Date.now()}.jpg`;
        const filePath = path.join(options.screenshotsDir, fileName);
        await page.screenshot({ path: filePath, type: 'jpeg', quality: 80, fullPage: true });
        screenshotUrl = `/screenshots/${fileName}`;
      } catch (err) { }
    }

    const items = await extractSerpData(page, keyword, profile.id);
    items.forEach(it => { it.screenshot = screenshotUrl; });

    let clickSummary = null;
    if (options.clickAds === true && items.length > 0) {
      try {
        clickSummary = await clickDetectedAds(page, {
          ownDomains: options.ownDomains,
          clickBottomAds: options.clickBottomAds === true,
          maxClicks: options.maxClicks || 1,
          clickDelayMs: options.clickDelayMs || 1000
        });
      } catch (err) {
        clickSummary = { clicked: [], skipped: [], error: err.message, foundAds: 0 };
      }
    }

    items.forEach(it => {
      if (it.category === 'ADS') {
        if (clickSummary && clickSummary.clicked.length > 0) {
          const matchClicked = clickSummary.clicked.find(c => (c.title && it.title.includes(c.title)) || (c.cite && it.domain.includes(c.cite)));
          if (matchClicked) {
            it.isClicked = true; it.clickStatus = 'CLICKED'; it.clickDetails = `Berhasil diklik (${matchClicked.method})`; return;
          }
        }
        if (clickSummary && clickSummary.skipped.length > 0) {
          const matchSkipped = clickSummary.skipped.find(s => (s.title && it.title.includes(s.title)) || (s.cite && it.domain.includes(s.cite)));
          if (matchSkipped) {
            it.isClicked = false; it.clickStatus = matchSkipped.reason === 'own-domain' ? 'SKIPPED_OWN_DOMAIN' : 'SKIPPED';
            it.clickDetails = matchSkipped.reason === 'own-domain' ? `Dilewati (Domain Sendiri)` : `Dilewati (${matchSkipped.reason})`; return;
          }
        }
        it.isClicked = false; it.clickStatus = 'SKIPPED_LIMIT'; it.clickDetails = `Dilewati (Limit klik)`;
      }
    });

    return { success: true, keyword, device: profile.id, itemsCount: items.length, items, screenshot: screenshotUrl, clickSummary };
  } catch (err) {
    return { success: false, keyword, device: profile.id, error: err.message, items: [] };
  } finally {
    await context.close().catch(() => { });
  }
}