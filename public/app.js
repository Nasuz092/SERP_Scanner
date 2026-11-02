// Google SERP Scanner Client Script
(function () {
  let allResults = [];
  let currentFilter = 'ALL';
  let searchTerm = '';
  let timerInterval = null;
  let startTime = null;
  let isScanning = false;

  // DOM Elements
  const keywordsInput = document.getElementById('keywordsInput');
  const keywordCountBadge = document.getElementById('keywordCountBadge');
  const btnPresetMesin = document.getElementById('btnPresetMesin');
  const btnClearInput = document.getElementById('btnClearInput');
  const fileImport = document.getElementById('fileImport');

  const devDesktop = document.getElementById('devDesktop');
  const devMobile = document.getElementById('devMobile');
  const devTablet = document.getElementById('devTablet');
  const delayInput = document.getElementById('delayInput');
  const headlessCheckbox = document.getElementById('headlessCheckbox');

  // Proxy controls
  const useProxyCheckbox = document.getElementById('useProxyCheckbox');
  const proxyOptions = document.getElementById('proxyOptions');
  const proxyServerInput = document.getElementById('proxyServerInput');

  // Click Ads controls
  const clickAdsCheckbox = document.getElementById('clickAdsCheckbox');
  const clickAdsOptions = document.getElementById('clickAdsOptions');
  const ownDomainsInput = document.getElementById('ownDomainsInput');
  const targetAdsInput = document.getElementById('targetAdsInput');
  const targetOnlyCheckbox = document.getElementById('targetOnlyCheckbox');
  const clickBottomAdsCheckbox = document.getElementById('clickBottomAdsCheckbox');
  const maxClicksInput = document.getElementById('maxClicksInput');
  const clickInfoBadge = document.getElementById('clickInfoBadge');

  const btnStartScan = document.getElementById('btnStartScan');
  const btnStopScan = document.getElementById('btnStopScan');
  const btnClearData = document.getElementById('btnClearData');

  const logContainer = document.getElementById('logContainer');
  const logCounter = document.getElementById('logCounter');
  const sessionStatusPill = document.getElementById('sessionStatusPill');
  const sessionStatusText = document.getElementById('sessionStatusText');

  const progressBar = document.getElementById('progressBar');
  const progressPercent = document.getElementById('progressPercent');
  const progressDetail = document.getElementById('progressDetail');
  const currentTaskLabel = document.getElementById('currentTaskLabel');
  const currentStatusIndicator = document.getElementById('currentStatusIndicator');
  const timeElapsed = document.getElementById('timeElapsed');

  // Metrics
  const metricTotal = document.getElementById('metricTotal');
  const metricOrganic = document.getElementById('metricOrganic');
  const metricAds = document.getElementById('metricAds');
  const metricMaps = document.getElementById('metricMaps');
  const metricAi = document.getElementById('metricAi');

  // Category counts
  const catCountAll = document.getElementById('catCountAll');
  const catCountOrganic = document.getElementById('catCountOrganic');
  const catCountAds = document.getElementById('catCountAds');
  const catCountMaps = document.getElementById('catCountMaps');
  const catCountAi = document.getElementById('catCountAi');

  const searchFilter = document.getElementById('searchFilter');
  const resultsTableBody = document.getElementById('resultsTableBody');
  const tableSummary = document.getElementById('tableSummary');
  const catPills = document.querySelectorAll('.cat-pill');

  // === Proxy: toggle sub-panel visibility ===
  function toggleProxyOptions() {
    if (!useProxyCheckbox || !proxyOptions) return;
    if (useProxyCheckbox.checked) {
      proxyOptions.classList.remove('hidden');
    } else {
      proxyOptions.classList.add('hidden');
    }
  }
  if (useProxyCheckbox) {
    useProxyCheckbox.addEventListener('change', toggleProxyOptions);
  }

  // === Click Ads: toggle sub-panel visibility ===
  function toggleClickAdsOptions() {
    if (!clickAdsCheckbox || !clickAdsOptions) return;
    if (clickAdsCheckbox.checked) {
      clickAdsOptions.classList.remove('hidden'); clickAdsOptions.style.display = 'block';
    } else {
      clickAdsOptions.classList.add('hidden'); clickAdsOptions.style.display = 'none';
    }
  }
  if (clickAdsCheckbox) {
    clickAdsCheckbox.addEventListener('change', toggleClickAdsOptions);
  }

  // Parse ownDomains input into array
  function getOwnDomains() {
    if (!ownDomainsInput) return [];
    return ownDomainsInput.value
      .split(',')
      .map(s => s.trim().toLowerCase().replace(/^https?:\/\//, '').replace(/^www\./, '').replace(/\/.*$/, ''))
      .filter(Boolean);
  }

  // Parse targetAds input into array
  function getTargetAds() {
    if (!targetAdsInput) return [];
    return targetAdsInput.value
      .split(',')
      .map(s => s.trim().toLowerCase().replace(/^https?:\/\//, '').replace(/^www\./, '').replace(/\/.*$/, ''))
      .filter(Boolean);
  }

  // Update click info badge
  function updateClickInfoBadge(clickInfo) {
    if (!clickInfoBadge) return;
    if (!clickInfo || !clickInfo.enabled) {
      clickInfoBadge.classList.add('hidden');
      return;
    }
    clickInfoBadge.classList.remove('hidden');
    clickInfoBadge.textContent = `🖱️ ${clickInfo.totalClicked || 0} klik / ${clickInfo.totalSkipped || 0} dilewati`;
  }

  // Helper: Count keywords in textarea
  function updateKeywordCount() {
    const text = keywordsInput.value.trim();
    const count = text ? text.split('\n').filter(k => k.trim().length > 0).length : 0;
    keywordCountBadge.textContent = `${count} keyword`;
  }

  keywordsInput.addEventListener('input', updateKeywordCount);

  // Preset Keywords: Mesin Printing Industry
  btnPresetMesin.addEventListener('click', () => {
    const samples = [
      'mesin digital printing outdoor',
      'mesin digital printing indoor',
      'jual printer dtf a3',
      'mesin printing uv flatbed',
      'harga mesin digital printing',
      'distributor mesin digital printing jakarta',
      'mesin digital printing konica minolta',
      'anugrah supply mandiri',
      'mesin percetakan banner spanduk',
      'mesin digital printing tekstil sublim'
    ];
    keywordsInput.value = samples.join('\n');
    updateKeywordCount();
  });

  // Clear Input
  btnClearInput.addEventListener('click', () => {
    keywordsInput.value = '';
    updateKeywordCount();
  });

  // Import from TXT or CSV
  fileImport.addEventListener('change', (e) => {
    const file = e.target.files[0];
    if (!file) return;

    const reader = new FileReader();
    reader.onload = (event) => {
      const content = event.target.result;
      const lines = content.split(/\r?\n/)
        .map(l => l.replace(/^[",']|[",']$/g, '').trim())
        .filter(l => l.length > 0 && !l.toLowerCase().startsWith('keyword') && !l.toLowerCase().startsWith('kata kunci'));

      if (lines.length > 0) {
        keywordsInput.value = lines.join('\n');
        updateKeywordCount();
        appendLog(`Berhasil mengimpor ${lines.length} kata kunci dari file: ${file.name}`, 'success');
      }
    };
    reader.readAsText(file);
    e.target.value = '';
  });

  // Get selected devices
  function getSelectedDevices() {
    const devices = [];
    if (devDesktop.checked) devices.push('desktop');
    if (devMobile.checked) devices.push('mobile');
    if (devTablet.checked) devices.push('tablet');
    return devices;
  }

  // Append log to terminal box
  let logTotal = 0;
  function appendLog(message, type = 'info') {
    const logItem = document.createElement('div');
    const time = new Date().toLocaleTimeString('id-ID');

    let colorClass = 'text-slate-300';
    let prefix = '•';
    if (type === 'success') {
      colorClass = 'text-emerald-400 font-semibold';
      prefix = '✔';
    } else if (type === 'scan') {
      colorClass = 'text-blue-400';
      prefix = '🔍';
    } else if (type === 'warning') {
      colorClass = 'text-amber-400';
      prefix = '⚠';
    } else if (type === 'error') {
      colorClass = 'text-rose-400 font-bold';
      prefix = '✖';
    } else if (type === 'debug') {
      colorClass = 'text-slate-500';
      prefix = '⏱';
    }

    logItem.className = `flex items-start gap-1.5 ${colorClass}`;
    logItem.innerHTML = `
      <span class="text-slate-500 select-none text-[10px] shrink-0 mt-0.5">${time}</span>
      <span class="select-none shrink-0">${prefix}</span>
      <span class="break-all">${escapeHtml(message)}</span>
    `;

    logContainer.prepend(logItem);
    logTotal++;
    logCounter.textContent = `${logTotal} logs`;

    // Limit log display to 200 items in DOM
    if (logContainer.children.length > 200) {
      logContainer.removeChild(logContainer.lastChild);
    }
  }

  // Update session status indicator
  function updateSessionUI(statusData) {
    const { status, totalTasks, completedTasks, percent, currentKeyword, currentDevice, counts, totalResults, clickInfo } = statusData;

    progressBar.style.width = `${percent || 0}%`;
    progressPercent.textContent = `${percent || 0}%`;
    progressDetail.textContent = `${completedTasks || 0} dari ${totalTasks || 0} tugas selesai`;

    if (currentKeyword) {
      const devIcon = currentDevice === 'mobile' ? '📱 Mobile' : (currentDevice === 'tablet' ? '📟 Tablet' : '🖥️ Desktop');
      currentTaskLabel.textContent = `Sedang memindai: "${currentKeyword}" (${devIcon})`;
    } else {
      currentTaskLabel.textContent = status === 'completed' ? 'Semua pemindaian selesai!' : '-';
    }

    // Update click info badge
    updateClickInfoBadge(clickInfo);

    // Status text & buttons
    if (status === 'running') {
      isScanning = true;
      btnStartScan.disabled = true;
      btnStartScan.classList.add('opacity-50', 'cursor-not-allowed');
      btnStopScan.disabled = false;
      sessionStatusText.textContent = 'Memindai...';
      sessionStatusPill.className = 'flex items-center space-x-2 px-3 py-1.5 rounded-full text-xs font-semibold bg-blue-950/60 border border-blue-500/40 text-blue-300 status-pulse';
      currentStatusIndicator.textContent = 'Proses Berjalan';
      currentStatusIndicator.className = 'text-xs font-semibold px-2.5 py-0.5 rounded-full bg-blue-500/20 text-blue-300 border border-blue-500/30';

      if (!startTime) {
        startTime = Date.now();
        startTimer();
      }
    } else if (status === 'completed') {
      isScanning = false;
      btnStartScan.disabled = false;
      btnStartScan.classList.remove('opacity-50', 'cursor-not-allowed');
      btnStopScan.disabled = true;
      sessionStatusText.textContent = 'Selesai';
      sessionStatusPill.className = 'flex items-center space-x-2 px-3 py-1.5 rounded-full text-xs font-semibold bg-emerald-950/60 border border-emerald-500/40 text-emerald-300';
      currentStatusIndicator.textContent = 'Selesai';
      currentStatusIndicator.className = 'text-xs font-semibold px-2.5 py-0.5 rounded-full bg-emerald-500/20 text-emerald-300 border border-emerald-500/30';
      stopTimer();
    } else if (status === 'stopped') {
      isScanning = false;
      btnStartScan.disabled = false;
      btnStartScan.classList.remove('opacity-50', 'cursor-not-allowed');
      btnStopScan.disabled = true;
      sessionStatusText.textContent = 'Dihentikan';
      sessionStatusPill.className = 'flex items-center space-x-2 px-3 py-1.5 rounded-full text-xs font-semibold bg-amber-950/60 border border-amber-500/40 text-amber-300';
      currentStatusIndicator.textContent = 'Dihentikan';
      currentStatusIndicator.className = 'text-xs font-semibold px-2.5 py-0.5 rounded-full bg-amber-500/20 text-amber-300 border border-amber-500/30';
      stopTimer();
    } else {
      isScanning = false;
      btnStartScan.disabled = false;
      btnStartScan.classList.remove('opacity-50', 'cursor-not-allowed');
      btnStopScan.disabled = true;
      sessionStatusText.textContent = 'Siap (Idle)';
      sessionStatusPill.className = 'flex items-center space-x-2 px-3 py-1.5 rounded-full text-xs font-semibold bg-slate-800 border border-slate-700 text-slate-300';
      currentStatusIndicator.textContent = 'Menunggu Input';
      currentStatusIndicator.className = 'text-xs font-semibold px-2.5 py-0.5 rounded-full bg-slate-700 text-slate-300';
      stopTimer();
    }

    // Update Metrics
    if (counts) {
      metricTotal.textContent = totalResults || 0;
      metricOrganic.textContent = counts.organic || 0;
      metricAds.textContent = counts.ads || 0;
      metricMaps.textContent = counts.maps || 0;
      metricAi.textContent = (counts.ai || 0) + (counts.paa || 0);

      catCountAll.textContent = totalResults || 0;
      catCountOrganic.textContent = counts.organic || 0;
      catCountAds.textContent = counts.ads || 0;
      catCountMaps.textContent = counts.maps || 0;
      catCountAi.textContent = (counts.ai || 0) + (counts.paa || 0);
    }
  }

  // Timer runner
  function startTimer() {
    stopTimer();
    timerInterval = setInterval(() => {
      if (!startTime) return;
      const elapsedMs = Date.now() - startTime;
      const totalSec = Math.floor(elapsedMs / 1000);
      const min = String(Math.floor(totalSec / 60)).padStart(2, '0');
      const sec = String(totalSec % 60).padStart(2, '0');
      timeElapsed.textContent = `Waktu: ${min}:${sec}`;
    }, 1000);
  }

  function stopTimer() {
    if (timerInterval) {
      clearInterval(timerInterval);
      timerInterval = null;
    }
  }

  function renderAdClickStatus(item) {
    if (item.category !== 'ADS') return '';
    if (item.isClicked) {
      const isTarget = item.isTarget || (item.clickDetails && item.clickDetails.includes('Target'));
      return `
        <div class="mt-2 pt-1.5 border-t border-slate-700/60">
          <div class="p-2 rounded-lg ${isTarget ? 'bg-amber-950/80 border border-amber-500 text-amber-200' : 'bg-emerald-950/70 border border-emerald-600/70 text-emerald-300'}">
            <div class="flex items-center gap-1.5 font-bold text-[11px]">
              <span>${isTarget ? '🎯 IKLAN TARGET UTAMA BERHASIL DIKLIK' : '🖱️ IKLAN KOMPETITOR BERHASIL DIKLIK'}</span>
            </div>
            <span class="text-[10px] ${isTarget ? 'text-amber-300' : 'text-emerald-400'} block mt-0.5">${escapeHtml(item.clickDetails || '')}</span>
          </div>
        </div>
      `;
    }
    if (item.clickStatus === 'SKIPPED_OWN_DOMAIN') {
      return `
        <div class="mt-2 pt-1.5 border-t border-slate-700/60">
          <div class="p-2 rounded-lg bg-blue-950/70 border border-blue-600/70 text-blue-300">
            <div class="flex items-center gap-1.5 font-bold text-[11px]">
              <span>🛡️ DILEWATI (DOMAIN SENDIRI)</span>
            </div>
            <span class="text-[10px] text-blue-400 block mt-0.5">${escapeHtml(item.clickDetails || '')}</span>
          </div>
        </div>
      `;
    }
    if (item.clickStatus === 'SKIPPED_BOTTOM_DISABLED') {
      return `
        <div class="mt-2 pt-1.5 border-t border-slate-700/60">
          <div class="p-2 rounded-lg bg-amber-950/40 border border-amber-700/50 text-amber-300">
            <div class="flex items-center gap-1.5 font-semibold text-[11px]">
              <span>ℹ️ Sponsor Bawah (Belum Diklik)</span>
            </div>
            <span class="text-[10px] text-amber-400/80 block mt-0.5">Iklan ini dilewati karena opsi 'Klik iklan bawah' belum dicentang di panel kiri.</span>
          </div>
        </div>
      `;
    }
    if (item.clickStatus === 'SKIPPED_LIMIT') {
      return `
        <div class="mt-2 pt-1.5 border-t border-slate-700/60">
          <div class="p-1.5 rounded bg-slate-800 border border-slate-700 text-slate-400 text-[10px]">
            <span>⚠️ Dilewati (melebihi batas maksimal klik per keyword)</span>
          </div>
        </div>
      `;
    }
    return `
      <div class="mt-2 pt-1.5 border-t border-slate-700/60">
        <div class="text-[10px] text-slate-500 font-mono mt-1">
          <span>⚪ Mode klik iklan dinonaktifkan</span>
        </div>
      </div>
    `;
  }

  // Render Table Rows
  function renderTable() {
    let filtered = allResults;

    // Filter by Category
    if (currentFilter === 'ORGANIC') {
      filtered = filtered.filter(x => x.category === 'ORGANIC');
    } else if (currentFilter === 'ADS') {
      filtered = filtered.filter(x => x.category === 'ADS');
    } else if (currentFilter === 'MAPS') {
      filtered = filtered.filter(x => x.category === 'MAPS');
    } else if (currentFilter === 'AI_OVERVIEW') {
      filtered = filtered.filter(x => x.category === 'AI_OVERVIEW' || x.category === 'PAA');
    }

    // Filter by Search Query
    if (searchTerm) {
      const q = searchTerm.toLowerCase();
      filtered = filtered.filter(x =>
        (x.title && x.title.toLowerCase().includes(q)) ||
        (x.domain && x.domain.toLowerCase().includes(q)) ||
        (x.keyword && x.keyword.toLowerCase().includes(q)) ||
        (x.url && x.url.toLowerCase().includes(q)) ||
        (x.snippet && x.snippet.toLowerCase().includes(q))
      );
    }

    tableSummary.textContent = `Menampilkan ${filtered.length} dari ${allResults.length} hasil`;

    if (filtered.length === 0) {
      resultsTableBody.innerHTML = `
        <tr>
          <td colspan="6" class="py-12 text-center text-slate-400">
            <span class="text-3xl block mb-2">🔎</span>
            <p class="font-medium text-slate-300">Tidak ada hasil yang cocok dengan filter "${escapeHtml(searchTerm || currentFilter)}"</p>
          </td>
        </tr>
      `;
      return;
    }

    const rowsHtml = filtered.map((item, idx) => {
      // Category Badge styling
      let catBadgeClass = 'badge-organic';
      let catIcon = '🌿';
      let catText = 'Organik';

      if (item.category === 'ADS') {
        catBadgeClass = 'badge-ads';
        catIcon = '🟧';
        catText = 'Google Ads';
      } else if (item.category === 'MAPS') {
        catBadgeClass = 'badge-maps';
        catIcon = '🗺️';
        catText = 'Maps Pack';
      } else if (item.category === 'AI_OVERVIEW') {
        catBadgeClass = 'badge-ai';
        catIcon = '🟣';
        catText = 'AI Overview';
      } else if (item.category === 'PAA') {
        catBadgeClass = 'badge-paa';
        catIcon = '❓';
        catText = 'PAA';
      }

      // Device badge
      let devBadgeClass = 'badge-desktop';
      let devIcon = '🖥️';
      if (item.device === 'mobile') {
        devBadgeClass = 'badge-mobile';
        devIcon = '📱';
      } else if (item.device === 'tablet') {
        devBadgeClass = 'badge-tablet';
        devIcon = '📟';
      }

      // Sitelinks chips
      let sitelinksHtml = '';
      if (Array.isArray(item.sitelinks) && item.sitelinks.length > 0) {
        sitelinksHtml = `
          <div class="flex items-center gap-1.5 flex-wrap mt-2">
            ${item.sitelinks.map(sl => `<span class="px-2 py-0.5 rounded bg-slate-900 border border-slate-700/80 text-[10px] text-slate-300 font-mono">🔗 ${escapeHtml(sl)}</span>`).join('')}
          </div>
        `;
      }

      // Highlight domain if matches search
      const isDomainMatch = searchTerm && item.domain && item.domain.toLowerCase().includes(searchTerm.toLowerCase());
      const domainDisplay = item.domain ? `
        <span class="inline-flex items-center gap-1 font-mono text-[11px] ${isDomainMatch ? 'text-amber-300 bg-amber-950/60 px-1 rounded font-bold' : 'text-blue-400'}">
          🌐 ${escapeHtml(item.domain)}
        </span>
      ` : '';

      // PERBAIKAN URL: Jangan pernah membuat URL google.com/search palsu. 
      // Jika url kosong atau tidak valid, render sebagai teks biasa (bukan link).
      const safeUrl = item.url && item.url !== '#' && item.url.startsWith('http') ? item.url : null;

      const titleHtml = safeUrl
        ? `<a href="${escapeHtml(safeUrl)}" target="_blank" rel="noopener noreferrer" class="font-semibold text-slate-100 hover:text-blue-400 transition text-sm leading-snug line-clamp-2">${escapeHtml(item.title || '(Tanpa Judul)')}</a>`
        : `<span class="font-semibold text-slate-100 text-sm leading-snug line-clamp-2">${escapeHtml(item.title || '(Tanpa Judul)')}</span>`;

      return `
        <tr class="hover:bg-slate-700/30 transition">
          <td class="py-3 px-3.5 text-center text-slate-500 font-mono">${idx + 1}</td>
          
          <!-- Category -->
          <td class="py-3 px-3.5">
            <span class="inline-flex items-center gap-1 px-2.5 py-1 rounded-md text-[11px] font-semibold ${catBadgeClass}">
              <span>${catIcon}</span>
              <span>${catText}</span>
            </span>
            <span class="block text-[10px] text-slate-400 mt-1">${escapeHtml(item.placement || '')}</span>
          </td>

          <!-- Device -->
          <td class="py-3 px-3.5">
            <span class="inline-flex items-center gap-1 px-2 py-0.5 rounded text-[11px] font-medium ${devBadgeClass}">
              <span>${devIcon}</span>
              <span class="capitalize">${escapeHtml(item.device || '')}</span>
            </span>
          </td>

          <!-- Keyword -->
          <td class="py-3 px-3.5">
            <span class="font-medium text-slate-200 block">${escapeHtml(item.keyword || '')}</span>
            ${item.screenshot ? `
              <button type="button" data-ss="${escapeHtml(item.screenshot)}" data-kw="${escapeHtml(item.keyword || '')}" data-dev="${escapeHtml(item.device || '')}" class="btn-view-ss inline-flex items-center gap-1 mt-1.5 px-2 py-0.5 rounded text-[10px] font-semibold bg-blue-950/90 hover:bg-blue-900 border border-blue-700/60 text-blue-300 transition shadow-sm cursor-pointer">
                <span>📷 Bukti SS</span>
              </button>
            ` : ''}
          </td>

          <!-- Title, Domain & Snippet -->
          <td class="py-3 px-3.5 max-w-md">
            <div class="space-y-1">
              ${titleHtml}
              <div>${domainDisplay}</div>
              
              ${item.category === 'AI_OVERVIEW' ? `
                <div class="p-3 rounded-xl bg-purple-950/40 border border-purple-700/50 mt-1.5 space-y-2">
                  <div class="text-[11px] font-bold text-purple-300 flex items-center gap-1.5">
                    <span>🤖 Ringkasan Lengkap Jawaban AI Google:</span>
                  </div>
                  <p class="text-[11px] text-purple-100 whitespace-pre-line leading-relaxed font-sans">
                    ${escapeHtml(item.snippet || '')}
                  </p>
                  ${sitelinksHtml}
                </div>
              ` : `
                <p class="text-[11px] text-slate-400 line-clamp-3 leading-relaxed">
                  ${escapeHtml(item.snippet || '')}
                </p>
                ${sitelinksHtml}
              `}

              ${renderAdClickStatus(item)}
            </div>
          </td>

          <!-- Rank -->
          <td class="py-3 px-3.5 text-center">
            <span class="inline-block px-2.5 py-1 rounded-lg bg-slate-900 border border-slate-700 font-mono font-bold text-sm text-slate-200">
              #${item.rank !== undefined ? item.rank : '-'}
            </span>
          </td>
        </tr>
      `;
    }).join('');

    resultsTableBody.innerHTML = rowsHtml;
  }

  // Filter Pills click listener
  catPills.forEach(pill => {
    pill.addEventListener('click', () => {
      catPills.forEach(p => p.classList.remove('active'));
      pill.classList.add('active');
      currentFilter = pill.getAttribute('data-filter') || 'ALL';
      renderTable();
    });
  });

  // Search filter listener
  searchFilter.addEventListener('input', (e) => {
    searchTerm = e.target.value.trim();
    renderTable();
  });

  // Start Scan Click
  btnStartScan.addEventListener('click', async () => {
    const rawText = keywordsInput.value.trim();
    if (!rawText) {
      alert('Harap masukkan setidaknya satu kata kunci!');
      keywordsInput.focus();
      return;
    }

    const keywords = rawText.split('\n')
      .map(k => k.trim())
      .filter(k => k.length > 0);

    const devices = getSelectedDevices();
    if (devices.length === 0) {
      alert('Harap pilih minimal 1 perangkat (Desktop, Mobile, atau Tablet)!');
      return;
    }

    const delaySeconds = Number(delayInput.value) || 3;
    const headless = headlessCheckbox.checked;
    const proxy = useProxyCheckbox && useProxyCheckbox.checked && proxyServerInput
      ? proxyServerInput.value.trim()
      : null;

    // Click ads options
    const clickAds = clickAdsCheckbox && clickAdsCheckbox.checked;
    const ownDomains = getOwnDomains();
    const targetAds = getTargetAds();
    const targetOnly = targetOnlyCheckbox && targetOnlyCheckbox.checked;
    const clickBottomAds = clickBottomAdsCheckbox && clickBottomAdsCheckbox.checked;
    const maxClicksPerKeyword = Math.max(1, Math.min(10, Number(maxClicksInput && maxClicksInput.value) || 3));

    if (clickAds) {
      const confirmed = confirm(
        '⚠️ PERINGATAN KLIK IKLAN\n\n' +
        'Fitur ini akan MENGKLIK iklan Google Ads yang terdeteksi di halaman hasil pencarian.\n\n' +
        'Iklan dari domain berikut akan DILEWATI otomatis:\n' +
        (ownDomains.length ? ownDomains.map(d => '  • ' + d).join('\n') : '  (tidak ada — semua iklan akan diklik!)') + '\n\n' +
        'Mengklik iklan pihak lain bisa melanggar ToS Google Ads. Gunakan hanya untuk riset/QA iklan Anda sendiri.\n\n' +
        'Lanjutkan?'
      );
      if (!confirmed) return;
    }

    startTime = Date.now();
    startTimer();
    allResults = [];
    renderTable();

    try {
      const resp = await fetch('/api/scan/start', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          keywords,
          devices,
          delaySeconds,
          headless,
          proxy,
          clearPrevious: true,
          clickAds,
          ownDomains,
          targetAds,
          targetOnly,
          clickBottomAds,
          maxClicksPerKeyword
        })
      });

      const res = await resp.json();
      if (!resp.ok) {
        alert(res.error || 'Gagal memulai scan');
        stopTimer();
      } else {
        appendLog(`🚀 Memulai scan ${keywords.length} kata kunci di ${devices.join(', ')}...`, 'success');
        if (clickAds) {
          appendLog(`🖱️ Mode Klik Iklan AKTIF — own-domain dilewati: ${ownDomains.join(', ') || '(tidak ada)'}`, 'warning');
        }
      }
    } catch (err) {
      alert('Koneksi gagal ke server scanner: ' + err.message);
      stopTimer();
    }
  });

  // Stop Scan Click
  btnStopScan.addEventListener('click', async () => {
    try {
      btnStopScan.disabled = true;
      const resp = await fetch('/api/scan/stop', { method: 'POST' });
      const res = await resp.json();
      appendLog(res.message || 'Menghentikan pemindaian...', 'warning');
    } catch (err) {
      alert('Gagal menghentikan scan: ' + err.message);
    }
  });

  // Clear Data Click
  btnClearData.addEventListener('click', async () => {
    if (isScanning) {
      alert('Tidak bisa mereset data saat pemindaian sedang berjalan!');
      return;
    }
    if (!confirm('Yakin ingin mereset seluruh data hasil pemindaian dan log?')) return;

    try {
      await fetch('/api/scan/clear', { method: 'POST' });
      allResults = [];
      logContainer.innerHTML = '<div class="text-slate-500 italic">Data telah dibersihkan. Siap untuk scan baru.</div>';
      logTotal = 0;
      logCounter.textContent = '0 logs';
      renderTable();
      updateSessionUI({
        status: 'idle',
        totalTasks: 0,
        completedTasks: 0,
        percent: 0,
        currentKeyword: '',
        currentDevice: '',
        totalResults: 0,
        counts: { ads: 0, organic: 0, maps: 0, ai: 0, paa: 0 },
        clickInfo: { enabled: false, totalClicked: 0, totalSkipped: 0 }
      });
    } catch (err) {
      alert('Gagal mereset data: ' + err.message);
    }
  });

  // SSE Real-time updates listener
  function setupEventSource() {
    const eventSource = new EventSource('/api/scan/stream');

    eventSource.addEventListener('init', (e) => {
      const data = JSON.parse(e.data);
      if (data.status) updateSessionUI(data.status);
    });

    eventSource.addEventListener('status', (e) => {
      const statusData = JSON.parse(e.data);
      updateSessionUI(statusData);
    });

    eventSource.addEventListener('progress', (e) => {
      const prog = JSON.parse(e.data);
      const devIcon = prog.currentDevice === 'mobile' ? '📱 Mobile' : (prog.currentDevice === 'tablet' ? '📟 Tablet' : '🖥️ Desktop');
      currentTaskLabel.textContent = `[${prog.currentIndex}/${prog.totalTasks}] "${prog.currentKeyword}" (${devIcon})`;
    });

    eventSource.addEventListener('log', (e) => {
      const log = JSON.parse(e.data);
      appendLog(log.message, log.type);
    });

    eventSource.addEventListener('new_items', (e) => {
      const payload = JSON.parse(e.data);
      if (Array.isArray(payload.items)) {
        allResults.push(...payload.items);
        renderTable();
      }
    });

    eventSource.onerror = () => {
      // Reconnection handled automatically by EventSource
    };
  }

  // Load Initial Status
  async function loadInitialStatus() {
    try {
      const resp = await fetch('/api/scan/status');
      const data = await resp.json();
      updateSessionUI(data);

      const resResp = await fetch('/api/scan/results');
      const resData = await resResp.json();
      if (Array.isArray(resData.results)) {
        allResults = resData.results;
        renderTable();
      }

      // Sinkronkan checkbox & options dengan status server
      if (data.clickInfo && data.clickInfo.enabled) {
        if (clickAdsCheckbox) clickAdsCheckbox.checked = true;
        if (clickAdsOptions) clickAdsOptions.classList.remove('hidden');
        if (Array.isArray(data.clickInfo.ownDomains) && ownDomainsInput) {
          ownDomainsInput.value = data.clickInfo.ownDomains.join(', ');
        }
        if (Array.isArray(data.clickInfo.targetAds) && targetAdsInput) {
          targetAdsInput.value = data.clickInfo.targetAds.join(', ');
        }
        if (targetOnlyCheckbox && data.clickInfo.targetOnly !== undefined) {
          targetOnlyCheckbox.checked = !!data.clickInfo.targetOnly;
        }
        if (clickBottomAdsCheckbox) clickBottomAdsCheckbox.checked = !!data.clickInfo.clickBottomAds;
        if (maxClicksInput && data.clickInfo.maxClicksPerKeyword) {
          maxClicksInput.value = data.clickInfo.maxClicksPerKeyword;
        }
      }
    } catch (e) {
      console.error(e);
    }
  }

  function escapeHtml(str) {
    if (!str) return '';
    return String(str)
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;')
      .replace(/'/g, '&#039;');
  }

  // === Screenshot Preview Modal Logic ===
  const screenshotModal = document.getElementById('screenshotModal');
  const modalScreenshotImg = document.getElementById('modalScreenshotImg');
  const modalDeviceBadge = document.getElementById('modalDeviceBadge');
  const modalKeywordLabel = document.getElementById('modalKeywordLabel');
  const modalDownloadBtn = document.getElementById('modalDownloadBtn');
  const modalOpenTabBtn = document.getElementById('modalOpenTabBtn');
  const btnCloseModal = document.getElementById('btnCloseModal');
  const modalCloseBtnBottom = document.getElementById('modalCloseBtnBottom');

  function openScreenshotModal(url, keyword, device) {
    if (!screenshotModal || !modalScreenshotImg) return;
    modalScreenshotImg.src = url;
    modalDownloadBtn.href = url;
    const safeKw = (keyword || 'serp').replace(/[^a-zA-Z0-9]/g, '_');
    modalDownloadBtn.setAttribute('download', `SERP_${device || 'desktop'}_${safeKw}.jpg`);
    modalOpenTabBtn.href = url;
    modalDeviceBadge.textContent = device ? device.toUpperCase() : 'DESKTOP';
    modalKeywordLabel.textContent = `Kata kunci: "${keyword || ''}"`;
    screenshotModal.classList.remove('hidden');
  }

  function closeScreenshotModal() {
    if (!screenshotModal) return;
    screenshotModal.classList.add('hidden');
    modalScreenshotImg.src = '';
  }

  if (btnCloseModal) btnCloseModal.addEventListener('click', closeScreenshotModal);
  if (modalCloseBtnBottom) modalCloseBtnBottom.addEventListener('click', closeScreenshotModal);
  if (screenshotModal) {
    screenshotModal.addEventListener('click', (e) => {
      if (e.target === screenshotModal) closeScreenshotModal();
    });
  }
  document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape') closeScreenshotModal();
  });

  // Delegate click for .btn-view-ss in table
  if (resultsTableBody) {
    resultsTableBody.addEventListener('click', (e) => {
      const btn = e.target.closest('.btn-view-ss');
      if (btn) {
        const ss = btn.getAttribute('data-ss');
        const kw = btn.getAttribute('data-kw');
        const dev = btn.getAttribute('data-dev');
        if (ss) openScreenshotModal(ss, kw, dev);
      }
    });
  }

  // Init
  updateKeywordCount();
  toggleClickAdsOptions();
  loadInitialStatus();
  setupEventSource();
})();