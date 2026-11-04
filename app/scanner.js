import { chromium, devices } from 'playwright';
import path from 'node:path';

export const DEVICE_PROFILES = {
  desktop: {
    id: 'desktop',
    name: 'Desktop (1920x1080)',
    icon: '[Desktop]',
    viewport: { width: 1440, height: 900 },
    userAgent: 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0.0.0 Safari/537.36',
    isMobile: false,
    hasTouch: false
  },
  mobile: {
    id: 'mobile',
    name: 'Mobile Smartphone (Pixel 7)',
    icon: '[Mobile]',
    ...devices['Pixel 7']
  },
  tablet: {
    id: 'tablet',
    name: 'Tablet (iPad Air / 820x1180)',
    icon: '[Tablet]',
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
    // 1. DETEKSI SEMUA MODEL IKLAN GOOGLE ADS
    //    - Search Text Ads (Top & Bottom)
    //    - Google Maps Ads / Local Pack Sponsored
    //    - Google Shopping Ads / PLA
    //    - Local Services Ads & Extension Ads
    // ==========================================
    const adLinks = Array.from(document.querySelectorAll('a[href*="/aclk?"], a[href*="googleadservices"], a[href*="doubleclick.net"]'));
    const processedAds = new Set();
    let adRank = 1;

    adLinks.forEach(link => {
      // Cari container pembungkus iklan
      const container = link.closest(
        'div.uEierd, div[data-text-ad], div.VkpGBb, div.rllt__details, div[data-cid], div[data-local-pack], div.pla-unit, div.commercial-unit-desktop-top, div[data-pla], div.MjjYud, div.g, div[data-hveid]'
      ) || link.closest('div');
      if (!container || processedAds.has(container)) return;
      processedAds.add(container);

      // Cek apakah iklan ini merupakan Iklan Google Maps / Local Pack
      const isMapAd = Boolean(
        container.closest('div.VkpGBb, div.rllt__details, div[data-local-pack], #lu_map') ||
        container.matches('div.VkpGBb, div.rllt__details, div[data-local-pack], div[data-cid]') ||
        container.querySelector('div[data-cid], a[href*="google.com/maps"], a[href*="maps.google.com"]') ||
        link.href.includes('google.com/maps') ||
        link.href.includes('maps.google.com')
      );

      // Cek apakah iklan ini merupakan Iklan Google Shopping (PLA)
      const isShoppingAd = Boolean(
        container.closest('div.pla-unit, div.commercial-unit-desktop-top, div[data-pla], div[data-attrid="pla_unit"], div.pla-unit-container') ||
        container.matches('div.pla-unit, div[data-pla]') ||
        container.querySelector('.pla-unit-title, .sh-np__product-title, .sh-np__seller-container')
      );

      // Ekstraksi Judul Iklan (dukung h3, role=heading, heading maps, dsb)
      let heading = container.querySelector('h3, [role="heading"], .OSrXXb, .dbg0pd, .fontHeadlineSmall, div.CCgQ5, .pla-unit-title, .sh-np__product-title, h4');
      let adTitle = heading ? heading.textContent.trim() : '';
      if (!adTitle && isMapAd) {
        const mapHeading = container.querySelector('[role="heading"], .OSrXXb, .dbg0pd, .fontHeadlineSmall, span.fontTitleMedium');
        if (mapHeading) adTitle = mapHeading.textContent.trim();
      } else if (!adTitle && isShoppingAd) {
        const shopHeading = container.querySelector('.pla-unit-title, .sh-np__product-title, [role="heading"]');
        if (shopHeading) adTitle = shopHeading.textContent.trim();
      }
      if (!adTitle) {
        const mainAnchor = container.querySelector('a[href*="/aclk?"], a[href*="googleadservices"]');
        if (mainAnchor && mainAnchor.textContent.trim().length > 3) {
          adTitle = mainAnchor.textContent.trim().split('\n')[0].trim();
        }
      }

      // Ekstraksi Nama Pengiklan / Bisnis
      let advertiserName = '';
      const cite = container.querySelector('cite, .x2VHCd, .v5yQqb, span.VuuXrf, [data-dtld], .sh-np__seller-container, .E5ocAb');
      if (cite) {
        advertiserName = cite.textContent.trim();
      }

      const finalUrl = cleanUrl(link.href);
      let domain = extractDomain(finalUrl);

      if (cite && (!domain || domain === 'google.com')) {
        const citeText = cite.textContent.trim().split(/[\s>›]/)[0].trim();
        if (citeText.includes('.') && !citeText.includes('google')) domain = citeText.replace(/^https?:\/\//, '').replace(/^www\./, '');
      }
      if (!domain && isMapAd) {
        domain = 'maps.google.com';
      }

      const snippetEl = container.querySelector('.VwiC3b, .MUxGbd, [data-content-feature="1"], .rllt__wrapped, .wUrVib, .I6TXqe, .hG48Ns, .sh-np__price');
      const snippet = snippetEl ? snippetEl.textContent.trim() : '';

      // Ekstraksi seluruh teks yang ada di dalam kartu iklan untuk pencocokan nama bisnis lengkap
      const fullText = (container.innerText || container.textContent || '').trim();

      const sitelinks = [];
      container.querySelectorAll('.usJj9c a, .bOeZT, a.l, div[data-flt="sitelink"] a').forEach(sl => {
        const txt = sl.textContent.trim();
        if (txt.length > 2 && !sitelinks.includes(txt)) sitelinks.push(txt);
      });

      // VALIDASI KETAT: Jangan tampilkan jika tidak ada judul, URL, maupun teks
      if (!adTitle && !finalUrl && !domain && !fullText) return;

      const isBottom = Boolean(!isMapAd && (container.closest('#bottomads') || container.getBoundingClientRect().top > window.innerHeight / 1.5));
      const adId = adRank++;

      let placement = 'Top Ad (Sponsor Atas)';
      let extra = 'Iklan Google Ads';

      if (isMapAd) {
        placement = 'Iklan Google Maps (Bersponsor)';
        extra = 'Iklan Google Maps Bersponsor';
      } else if (isShoppingAd) {
        placement = 'Iklan Shopping (Bersponsor)';
        extra = 'Iklan Google Shopping Bersponsor';
      } else if (isBottom) {
        placement = 'Bottom Ad (Sponsor Bawah)';
        extra = 'Iklan Google Ads (Bawah)';
      }

      items.push({
        keyword, device: deviceType, overallOrder: overallOrder++, category: 'ADS',
        adId: adId,
        placement: placement, rank: adId,
        title: adTitle || advertiserName || domain || 'Iklan Google',
        businessName: advertiserName || (isMapAd ? adTitle : ''),
        advertiserName: advertiserName,
        domain: domain || (isMapAd ? 'maps.google.com' : 'Iklan'),
        rawHref: link.href,
        url: finalUrl,
        snippet: snippet,
        sitelinks: sitelinks,
        fullText: fullText,
        extra: extra,
        isMapAd: isMapAd,
        isShoppingAd: isShoppingAd,
        isBottom: isBottom
      });
    });

    // Deteksi Tambahan: Iklan Maps Bersponsor di Local Pack yang tidak memiliki link aclk langsung
    const localPackCards = document.querySelectorAll('div[data-local-pack] div.VkpGBb, div.rllt__details');
    localPackCards.forEach(localCard => {
      if (processedAds.has(localCard)) return;
      const cardText = (localCard.innerText || '').toLowerCase();
      const isSponsored = /\b(bersponsor|sponsored|sponsor|iklan)\b/i.test(cardText) ||
        Boolean(localCard.querySelector('[aria-label*="Sponsor" i], [aria-label*="Bersponsor" i], [aria-label*="Sponsored" i], .k8XOCe, .w3Db1'));

      if (isSponsored) {
        processedAds.add(localCard);
        const nameEl = localCard.querySelector('[role="heading"], .OSrXXb, .dbg0pd, .fontHeadlineSmall, span.fontTitleMedium');
        const mapAnchor = localCard.querySelector('a[href*="/aclk?"], a[href*="googleadservices"], a[href*="google.com/maps"], a[href*="maps.google.com"], a[href]');
        const adLinkHref = mapAnchor ? (mapAnchor.getAttribute('href') || mapAnchor.href) : '';
        const finalUrl = cleanUrl(adLinkHref);
        const adId = adRank++;
        const mapTitle = nameEl ? nameEl.textContent.trim() : (extractDomain(finalUrl) || 'Iklan Google Maps');

        items.push({
          keyword, device: deviceType, overallOrder: overallOrder++, category: 'ADS',
          adId: adId,
          placement: 'Iklan Google Maps (Bersponsor)',
          rank: adId,
          title: mapTitle,
          businessName: mapTitle,
          advertiserName: mapTitle,
          domain: 'maps.google.com',
          rawHref: adLinkHref || 'https://maps.google.com',
          url: finalUrl || 'https://maps.google.com',
          snippet: (localCard.innerText || '').replace(/\n+/g, ' • ').substring(0, 160),
          sitelinks: [],
          fullText: localCard.innerText || '',
          extra: 'Iklan Google Maps Bersponsor',
          isMapAd: true,
          isShoppingAd: false,
          isBottom: false
        });
      }
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

    // 3. GOOGLE MAPS (Organik)
    const mapItems = document.querySelectorAll('div.VkpGBb, div.rllt__details, div[data-local-pack], div[data-cid]');
    const seenMapTitles = new Set();
    mapItems.forEach((m) => {
      if (m.querySelector('a[href*="youtube.com"]')) return;
      if (processedAds.has(m)) return;
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

export function isProtectedOwnEntity(ad, blacklistRules = []) {
  if (!Array.isArray(blacklistRules) || blacklistRules.length === 0) return { matched: false };

  // Kumpulkan seluruh data teks dari iklan
  const adTitle = String(ad.title || '').trim().toLowerCase();
  const adDomain = String(ad.domain || '').trim().toLowerCase();
  const adUrl = String(ad.url || '').trim().toLowerCase();
  const adRawHref = String(ad.rawHref || '').trim().toLowerCase();
  const adSnippet = String(ad.snippet || '').trim().toLowerCase();
  const adAdvertiser = String(ad.advertiserName || ad.businessName || '').trim().toLowerCase();
  const adFullText = String(ad.fullText || '').trim().toLowerCase();
  const adSitelinks = Array.isArray(ad.sitelinks) ? ad.sitelinks.join(' ').toLowerCase() : '';

  // Gabungkan semua teks iklan menjadi satu ruang pencarian lengkap
  const allAdText = `${adTitle} ${adDomain} ${adAdvertiser} ${adSnippet} ${adSitelinks} ${adFullText} ${adUrl} ${adRawHref}`.toLowerCase();
  const allAdTextClean = allAdText.replace(/[^a-z0-9]/g, ' ');
  const allAdCompact = allAdText.replace(/[^a-z0-9]/g, '');

  for (const rawRule of blacklistRules) {
    const rule = String(rawRule || '').trim().toLowerCase();
    if (!rule || rule.startsWith('#')) continue;

    // 1. Pencocokan Langsung Substring (Case-Insensitive) di field atau full text
    if (
      adTitle.includes(rule) ||
      adFullText.includes(rule) ||
      adAdvertiser.includes(rule) ||
      adSnippet.includes(rule) ||
      adDomain.includes(rule) ||
      adUrl.includes(rule) ||
      adRawHref.includes(rule) ||
      allAdText.includes(rule)
    ) {
      return { matched: true, rule: rawRule, reason: 'direct-substring' };
    }

    // 2. Pencocokan Alphanumerik Tanpa Tanda Baca (mengabaikan titik, spasi, koma)
    // Contoh: "ANUGRAH SUPPLY MANDIRI" cocok dengan "CV. ANUGRAH SUPPLY MANDIRI" atau "Anugrah Supply Mandiri, PT"
    const ruleCompact = rule.replace(/[^a-z0-9]/g, '');
    if (ruleCompact.length >= 4 && allAdCompact.includes(ruleCompact)) {
      return { matched: true, rule: rawRule, reason: 'compact-match' };
    }

    // 3. Pencocokan Semua Kata Kunci Multi-Kata (jika aturan terdiri dari beberapa kata)
    const ruleWords = rule.split(/[\s,.-]+/).map(w => w.trim()).filter(w => w.length >= 3);
    if (ruleWords.length >= 2) {
      const allWordsFound = ruleWords.every(word => allAdTextClean.includes(word));
      if (allWordsFound) {
        return { matched: true, rule: rawRule, reason: 'token-all-words-matched' };
      }
    }

    // 4. Pencocokan Domain / Hostname (untuk nama domain seperti "mesinprinting.id")
    const ruleDomain = rule.replace(/^https?:\/\//, '').replace(/^www\./, '').split('/')[0];
    if (ruleDomain && ruleDomain.includes('.')) {
      if (
        adDomain === ruleDomain ||
        adDomain.endsWith('.' + ruleDomain) ||
        adUrl.includes(ruleDomain) ||
        adRawHref.includes(ruleDomain) ||
        allAdText.includes(ruleDomain)
      ) {
        return { matched: true, rule: rawRule, reason: 'domain-match' };
      }
    }
  }

  return { matched: false };
}

export function isTargetEntity(ad, targetRules = []) {
  if (!Array.isArray(targetRules) || targetRules.length === 0) return { matched: false };

  const adTitle = String(ad.title || '').trim().toLowerCase();
  const adDomain = String(ad.domain || '').trim().toLowerCase();
  const adUrl = String(ad.url || '').trim().toLowerCase();
  const adRawHref = String(ad.rawHref || '').trim().toLowerCase();
  const adSnippet = String(ad.snippet || '').trim().toLowerCase();
  const adAdvertiser = String(ad.advertiserName || ad.businessName || '').trim().toLowerCase();
  const adFullText = String(ad.fullText || '').trim().toLowerCase();
  const allAdText = `${adTitle} ${adDomain} ${adAdvertiser} ${adSnippet} ${adFullText} ${adUrl} ${adRawHref}`.toLowerCase();
  const allAdTextClean = allAdText.replace(/[^a-z0-9]/g, ' ');
  const allAdCompact = allAdText.replace(/[^a-z0-9]/g, '');

  for (const rawRule of targetRules) {
    const rule = String(rawRule || '').trim().toLowerCase();
    if (!rule || rule.startsWith('#')) continue;

    if (
      adTitle.includes(rule) ||
      adFullText.includes(rule) ||
      adAdvertiser.includes(rule) ||
      adDomain.includes(rule) ||
      adUrl.includes(rule) ||
      allAdText.includes(rule)
    ) {
      return { matched: true, rule: rawRule };
    }

    const ruleCompact = rule.replace(/[^a-z0-9]/g, '');
    if (ruleCompact.length >= 4 && allAdCompact.includes(ruleCompact)) {
      return { matched: true, rule: rawRule };
    }

    const ruleWords = rule.split(/[\s,.-]+/).map(w => w.trim()).filter(w => w.length >= 3);
    if (ruleWords.length >= 2 && ruleWords.every(word => allAdTextClean.includes(word))) {
      return { matched: true, rule: rawRule };
    }

    const ruleDomain = rule.replace(/^https?:\/\//, '').replace(/^www\./, '').split('/')[0];
    if (ruleDomain && ruleDomain.includes('.')) {
      if (
        adDomain === ruleDomain ||
        adDomain.endsWith('.' + ruleDomain) ||
        adUrl.includes(ruleDomain) ||
        allAdText.includes(ruleDomain)
      ) {
        return { matched: true, rule: rawRule };
      }
    }
  }

  return { matched: false };
}

export async function clickDetectedAds(page, adItems = [], options = {}) {
  const ownDomains = (Array.isArray(options.ownDomains) ? options.ownDomains : [])
    .map(s => String(s || '').trim())
    .filter(s => s.length > 0 && !s.startsWith('#'));

  const targetAds = (Array.isArray(options.targetAds) ? options.targetAds : [])
    .map(s => String(s || '').trim())
    .filter(s => s.length > 0 && !s.startsWith('#'));

  const targetOnly = options.targetOnly === true;
  const clickBottomAds = options.clickBottomAds === true;
  const skipMapAds = options.skipMapAds === true;
  const maxClicks = Math.max(1, Number(options.maxClicks) || 3);
  const clickDelayMs = Math.max(500, Number(options.clickDelayMs) || 1000);

  const result = { clicked: [], skipped: [], foundAds: adItems.length };
  const classifiedAds = [];

  for (const ad of adItems) {
    // 1. Cek proteksi domain / nama bisnis sendiri (Blacklist) TERLEBIH DAHULU!
    const ownCheck = isProtectedOwnEntity(ad, ownDomains);
    if (ownCheck.matched) {
      result.skipped.push({
        adId: ad.adId,
        title: ad.title,
        domain: ad.domain,
        matchedDomain: ownCheck.rule,
        placement: ad.placement,
        reason: 'own-domain'
      });
      continue;
    }

    // 2. Cek apakah ini Iklan Google Maps dan opsi lewati iklan Maps aktif
    if (ad.isMapAd && skipMapAds) {
      result.skipped.push({
        adId: ad.adId,
        title: ad.title,
        domain: ad.domain,
        placement: ad.placement,
        reason: 'map-ads-disabled'
      });
      continue;
    }

    // 3. Cek apakah ini iklan target khusus yang ditentukan (Whitelist)
    const targetCheck = isTargetEntity(ad, targetAds);
    const isTarget = targetCheck.matched;
    const matchedTarget = targetCheck.rule;

    // 4. Jika mode target-only aktif dan iklan ini bukan target, lewati
    if (targetOnly && targetAds.length > 0 && !isTarget) {
      result.skipped.push({
        adId: ad.adId,
        title: ad.title,
        domain: ad.domain,
        placement: ad.placement,
        reason: 'not-in-target-list'
      });
      continue;
    }

    // 5. Cek sponsor bawah
    if (ad.isBottom && !clickBottomAds) {
      result.skipped.push({
        adId: ad.adId,
        title: ad.title,
        domain: ad.domain,
        placement: ad.placement,
        reason: 'bottom-ads-disabled'
      });
      continue;
    }

    classifiedAds.push({
      ad,
      isTarget,
      matchedTarget
    });
  }

  // Prioritaskan iklan target di paling atas agar selalu diklik pertama!
  classifiedAds.sort((a, b) => (b.isTarget ? 1 : 0) - (a.isTarget ? 1 : 0));

  let clickedCount = 0;
  for (const item of classifiedAds) {
    const { ad, isTarget, matchedTarget } = item;
    if (clickedCount >= maxClicks) {
      result.skipped.push({
        adId: ad.adId,
        title: ad.title,
        domain: ad.domain,
        placement: ad.placement,
        reason: 'over-max-clicks'
      });
      continue;
    }

    const rawHref = ad.rawHref || ad.url || '';
    try {
      await page.waitForTimeout(clickDelayMs + Math.floor(Math.random() * 500));
      const newPage = await page.context().newPage();
      try {
        await newPage.goto(rawHref, { waitUntil: 'domcontentloaded', timeout: 25000 });
      } catch (navErr) { }
      const dwell = 2500 + Math.floor(Math.random() * 2000);
      await newPage.waitForTimeout(dwell);
      await newPage.close().catch(() => {});

      result.clicked.push({
        adId: ad.adId,
        title: ad.title,
        domain: ad.domain,
        isTarget: isTarget,
        matchedTarget: matchedTarget,
        placement: ad.placement,
        method: 'new-tab',
        dwellMs: dwell
      });
      clickedCount++;
    } catch (err) {
      result.skipped.push({
        adId: ad.adId,
        title: ad.title,
        domain: ad.domain,
        placement: ad.placement,
        reason: 'click-error: ' + err.message
      });
    }
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

    const adItems = items.filter(it => it.category === 'ADS');
    let clickSummary = null;
    if (options.clickAds === true && adItems.length > 0) {
      try {
        clickSummary = await clickDetectedAds(page, adItems, {
          ownDomains: options.ownDomains,
          targetAds: options.targetAds,
          targetOnly: options.targetOnly === true,
          clickBottomAds: options.clickBottomAds === true,
          skipMapAds: options.skipMapAds === true,
          maxClicks: options.maxClicks || 3,
          clickDelayMs: options.clickDelayMs || 1000
        });
      } catch (err) {
        clickSummary = { clicked: [], skipped: [], error: err.message, foundAds: adItems.length };
      }
    }

    items.forEach(it => {
      if (it.category === 'ADS') {
        const foundClicked = clickSummary?.clicked?.find(c => c.adId === it.adId);
        if (foundClicked) {
          it.isClicked = true;
          it.isTarget = !!foundClicked.isTarget;
          it.clickStatus = 'CLICKED';
          it.clickDetails = foundClicked.isTarget
            ? `Target Utama Berhasil Diklik (Target: ${foundClicked.matchedTarget || it.domain})`
            : `Berhasil diklik (${foundClicked.method})`;
          return;
        }
        const foundSkipped = clickSummary?.skipped?.find(s => s.adId === it.adId);
        if (foundSkipped) {
          it.isClicked = false;
          if (foundSkipped.reason === 'own-domain') {
            it.clickStatus = 'SKIPPED_OWN_DOMAIN';
            it.clickDetails = `Dilewati (Milik Sendiri / Terproteksi: ${foundSkipped.matchedDomain || 'Blacklist'})`;
          } else if (foundSkipped.reason === 'map-ads-disabled') {
            it.clickStatus = 'SKIPPED_MAP_DISABLED';
            it.clickDetails = 'Dilewati (Opsi iklan Google Maps dinonaktifkan)';
          } else if (foundSkipped.reason === 'not-in-target-list') {
            it.clickStatus = 'SKIPPED_NOT_TARGET';
            it.clickDetails = 'Dilewati (Bukan target yang di-whitelist)';
          } else if (foundSkipped.reason === 'bottom-ads-disabled') {
            it.clickStatus = 'SKIPPED_BOTTOM_DISABLED';
            it.clickDetails = 'Dilewati (Opsi sponsor bawah nonaktif)';
          } else if (foundSkipped.reason === 'over-max-clicks') {
            it.clickStatus = 'SKIPPED_LIMIT';
            it.clickDetails = 'Dilewati (Batas kuota klik per keyword)';
          } else {
            it.clickStatus = 'SKIPPED';
            it.clickDetails = `Dilewati (${foundSkipped.reason})`;
          }
          return;
        }
        it.isClicked = false;
        it.clickStatus = options.clickAds ? 'SKIPPED' : 'DISABLED';
        it.clickDetails = options.clickAds ? 'Dilewati' : 'Fitur klik iklan nonaktif';
      }
    });

    return { success: true, keyword, device: profile.id, itemsCount: items.length, items, screenshot: screenshotUrl, clickSummary };
  } catch (err) {
    return { success: false, keyword, device: profile.id, error: err.message, items: [] };
  } finally {
    await context.close().catch(() => { });
  }
}