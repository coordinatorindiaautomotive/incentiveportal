/**
 * ThessBuddy Sales Analytics Dashboard JS Module
 * Preserves exact same UI / ApexCharts layouts and animations.
 * Performs zero business calculations on the browser — delegates all to REST APIs.
 */

window.Dashboard = (function() {
    // ── PALETTES & CONSTANTS ──
    const PALETTE = [
        '#1b63f2', '#ff9f43', '#ff4d4f', '#00d2d3', '#00b074',
        '#f1c40f', '#9b59b6', '#ff9da7', '#e67e22', '#95a5a6'
    ];
    const LGRAD = ['#1b63f2','#3b82f6','#60a5fa','#93c5fd','#c084fc','#a78bfa','#00b074','#14b8a6','#2dd4bf','#0ea5e9','#38bdf8','#0284c7'];
    
    const PC = {};
    const CC = { 'M': '#1b63f2', 'AA': '#ff9f43', 'AG': '#00b074', 'T': '#f1c40f' };
    const DC = { 'AW': '#1b63f2', 'MW': '#8b5cf6', 'RO': '#ff9f43' };
    const QC = { 'Q1': '#1b63f2', 'Q2': '#ff9f43', 'Q3': '#00b074', 'Q4': '#ff4d4f' };

    // ── STATE ──
    const State = {
        met: 'Net_Sales',
        qs: new Set(),
        ms: new Set(),
        ps: new Set(),
        cats: new Set(),
        cons: new Set(),
        ds: new Set(),
        ls: new Set(),
        cmp: 'none'
    };

    const Charts = {};
    let debounceTimer = null;
    let currentAbortController = null;

    // Initialize UI and bindings
    function init() {
        // Build PC palette maps dynamically from available server-rendered array
        if (window.DB_PARTY_TYPES) {
            window.DB_PARTY_TYPES.forEach((p, i) => {
                PC[p.toUpperCase()] = PALETTE[i % PALETTE.length];
            });
        }

        initChips();
        bindThemeChange();
        go();
    }

    function initChips() {
        const qs = ['Q1', 'Q2', 'Q3', 'Q4'];
        const ps = window.DB_PARTY_TYPES || [];
        const cs = window.DB_CATEGORIES || [];

        // Consignees, Sub-Types, Locations from model
        const cos = window.DB_CONSIGNEES || [];
        const ds = window.DB_DEALER_SUBTYPES || [];
        const ls = window.DB_LOCATIONS || [];

        mkChips('qChips', qs, 'qs');
        mkMonthChips();
        mkChips('pChips', ps, 'ps', PC);
        mkChips('catChips', cs, 'cats', CC);
        mkChips('conChips', cos, 'cons');
        mkChips('dChips', ds, 'ds', DC);
        mkChips('lChips', ls, 'ls');
    }

    function mkChips(id, vals, key, cm) {
        const c = document.getElementById(id);
        if (!c) return;
        c.innerHTML = '';
        
        const a = document.createElement('div');
        a.className = 'chip all' + (State[key].size === 0 ? ' on' : '');
        a.textContent = 'All';
        a.onclick = () => {
            State[key] = new Set();
            refChips(id, vals, key, cm);
            go();
        };
        c.appendChild(a);

        vals.forEach(v => {
            const ch = document.createElement('div');
            ch.className = 'chip' + (State[key].has(v) ? ' on' : '');
            ch.textContent = v;
            if (cm && State[key].has(v)) {
                ch.style.boxShadow = `0 0 7px ${h2r(cm[v.toUpperCase()] || cm[v] || '#3b82f6', .4)}`;
            }
            ch.onclick = () => {
                if (State[key].has(v)) {
                    State[key].delete(v);
                } else {
                    State[key].add(v);
                }
                refChips(id, vals, key, cm);
                go();
            };
            c.appendChild(ch);
        });
    }

    function refChips(id, vals, key, cm) {
        const root = document.getElementById(id);
        if (!root) return;
        const chs = root.querySelectorAll('.chip');
        if (!chs.length) return;
        chs[0].className = 'chip all' + (State[key].size === 0 ? ' on' : '');
        vals.forEach((v, i) => {
            if (chs[i + 1]) {
                const isActive = State[key].has(v);
                chs[i + 1].className = 'chip' + (isActive ? ' on' : '');
                chs[i + 1].style.boxShadow = cm && isActive ? `0 0 7px ${h2r(cm[v.toUpperCase()] || cm[v] || '#3b82f6', .4)}` : '';
            }
        });
    }

    function mkMonthChips() {
        const c = document.getElementById('mChips');
        if (!c) return;
        c.innerHTML = '';
        const months = ["Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec", "Jan", "Feb", "Mar"];
        months.forEach(mo => {
            const ch = document.createElement('div');
            ch.className = 'mchip' + (State.ms.has(mo) ? ' on' : '');
            ch.textContent = mo;
            ch.title = mo;
            ch.onclick = () => {
                if (State.ms.has(mo)) {
                    State.ms.delete(mo);
                } else {
                    State.ms.add(mo);
                }
                mkMonthChips();
                go();
            };
            c.appendChild(ch);
        });
    }

    function setMetric(m, el) {
        State.met = m;
        document.querySelectorAll('.mtab').forEach(t => t.classList.remove('on'));
        if (el) el.classList.add('on');
        go();
    }

    function resetAll() {
        State.met = 'Net_Sales';
        State.qs = new Set();
        State.ms = new Set();
        State.ps = new Set();
        State.cats = new Set();
        State.cons = new Set();
        State.ds = new Set();
        State.ls = new Set();
        State.cmp = 'none';

        const cmpSel = document.getElementById('cmpSel');
        if (cmpSel) cmpSel.value = 'none';

        const trendView = document.getElementById('trendView');
        if (trendView) trendView.value = 'total';

        const yrSel = document.getElementById('yrSel');
        if (yrSel) yrSel.value = 'current';

        document.querySelectorAll('.mtab').forEach(t => {
            if (t.textContent.includes('Sales')) t.classList.add('on');
            else t.classList.remove('on');
        });

        initChips();
        go();
    }

    function go() {
        const cmpSel = document.getElementById('cmpSel');
        if (cmpSel) State.cmp = cmpSel.value;

        updateAfcBadges();
        updateHdrSubtitle();

        // Fire lazy API loaders for each component
        loadKPIs();
        loadTrend();
        loadPartyMix();
        loadQuarterSummary();
        loadConsigneeMix();
        loadDealerSubTypeMixAPI();
        renderDealerSales();
    }

    function getQueryString() {
        const yrSel = document.getElementById('yrSel');
        const yr = yrSel ? yrSel.value : 'current';
        
        return new URLSearchParams({
            yr: yr,
            met: State.met,
            quarters: [...State.qs].join(','),
            months: [...State.ms].join(','),
            partyTypes: [...State.ps].join(','),
            categories: [...State.cats].join(','),
            consignees: [...State.cons].join(','),
            dealerSubTypes: [...State.ds].join(','),
            locations: [...State.ls].join(',')
        }).toString();
    }

    // ── API LAZY LOADERS ──

    async function loadKPIs() {
        try {
            const res = await fetchJson(`/Dashboard/GetKPIs?${getQueryString()}`);
            if (!res) return;

            const t = res.totalValue;
            const avg = res.averageValue;
            const tx = res.txnCount;
            const uniqueLocs = res.uniqueLocs;
            const cv = res.latestMonthValue;
            const mo = res.momGrowth;
            const curr = res.latestMonth;
            const prev = res.prevMonth;

            // Render Header counters
            const hdrTotal = document.getElementById('hdrTotal');
            if (hdrTotal) hdrTotal.textContent = fts(t);
            const hdrTxns = document.getElementById('hdrTxns');
            if (hdrTxns) hdrTxns.textContent = tx.toLocaleString('en-IN');

            const K = [
                { l: 'TOTAL RETAIL SELLING', v: t, sub: res.activeMonths + ' months active', icon: '💰', grad: 'linear-gradient(135deg,var(--grad-sales-start),var(--grad-sales-end))', tg: 'linear-gradient(135deg,#fff,#C4B5FD)', kc: '#6d28d9', d: '.0s' },
                { l: curr + ' · CURRENT', v: cv, badge: res.latestMonthYoYGrowth, badgeText: 'vs LY', sub: 'LY ' + curr + ': ' + fmt(res.latestMonthCompValue), icon: '📈', grad: 'linear-gradient(135deg,var(--grad-incentive-start),var(--grad-incentive-end))', tg: 'linear-gradient(135deg,#fff,#A7F3D0)', kc: '#0d9488', d: '.06s' },
                { l: 'AVERAGE MONTHLY SELLING', v: avg, sub: 'avg retail selling', icon: '📊', grad: 'linear-gradient(135deg,var(--grad-cashin-start),var(--grad-cashin-end))', tg: 'linear-gradient(135deg,#fff,#BFDBFE)', kc: '#2563eb', d: '.12s' },
                { l: 'YTD SALES', v: res.ytdSales, badge: res.txnsGrowth, badgeText: 'vs LYTD', sub: 'LYTD: ' + fmt(res.lytdSales), icon: '🔢', grad: 'linear-gradient(135deg,var(--grad-cashout-start),var(--grad-cashout-end))', tg: 'linear-gradient(135deg,#fff,#FDE68A)', kc: '#ea580c', d: '.18s' },
                { l: 'MTD SALES', v: res.mtdSales, badge: res.branchesGrowth, badgeText: 'vs LMTD', sub: 'LMTD: ' + fmt(res.lmtdSales), icon: '📍', grad: 'linear-gradient(135deg,var(--grad-netbalance-start),var(--grad-netbalance-end))', tg: 'linear-gradient(135deg,#fff,#FBCFE8)', kc: '#db2777', d: '.24s' },
            ];

            const krow = document.getElementById('krow');
            if (krow) {
                krow.innerHTML = K.map(k => `
                    <div class="kcard" style="--kg:${h2r(k.kc, .2)};--kg2:${k.grad};--ktg:${k.tg};--kc:${k.kc};animation-delay:${k.d}">
                        <div class="k-icon">${k.icon}</div>
                        <div class="kl">${k.l}</div>
                        <div class="kv">${k.isN ? k.v.toLocaleString('en-IN') : fmt(k.v)}</div>
                        ${k.badge != null ? `<div class="kb ${k.badge >= 0 ? 'up' : 'dn'}">${k.badge >= 0 ? '▲' : '▼'} ${Math.abs(k.badge).toFixed(1)}% ${k.badgeText}</div>` : ''}
                        <div class="ksub">${k.sub}</div>
                    </div>`).join('');
            }
        } catch (err) {
            console.error("Failed to load KPIs:", err);
        }
    }

    async function loadTrend() {
        try {
            const trendView = document.getElementById('trendView') ? document.getElementById('trendView').value : 'total';
            const trendMode = document.getElementById('trendMode') ? document.getElementById('trendMode').value : 'value';
            const res = await fetchJson(`/Dashboard/GetTrend?${getQueryString()}&view=${trendView}`);
            if (!res) return;

            const isDark = document.body.classList.contains('dark-theme');
            let colors = PALETTE;

            if (trendView === 'total') {
                colors = ['#2563EB', '#0EA5E9'];
            } else if (trendView === 'stacked') {
                colors = res.series.map(s => PC[s.name.toUpperCase()] || '#64748b');
            } else if (trendView === 'category') {
                colors = res.series.map(s => CC[s.name.toUpperCase()] || '#64748b');
            } else {
                colors = res.series.map(s => DC[s.name.toUpperCase()] || '#64748b');
            }

            let chartSeries = res.series;
            let chartColors = colors;
            let isStacked = trendView !== 'total';
            let chartType = 'bar';
            let strokeConfig = {
                width: trendView === 'total' ? [0, 2.5] : 0,
                curve: 'smooth'
            };
            let fillConfig = trendView === 'total'
                ? { type: ['solid', 'gradient'], gradient: { shade: isDark ? 'dark' : 'light', type: 'vertical', shadeIntensity: .5, gradientToColors: ['#0EA5E9'], inverseColors: false, opacityFrom: .7, opacityTo: .1, stops: [0, 90, 100] } }
                : { opacity: 0.92 };

            let showDataLabels = false;
            let labelsFormatter = val => ftsLabelFormat(val);

            if (trendMode === 'growth') {
                showDataLabels = true;
                if (trendView === 'total' && res.series.length >= 2) {
                    const currData = res.series[0].data;
                    const compData = res.series[1].data;
                    const growthData = currData.map((curr, idx) => {
                        const comp = compData[idx] || 0;
                        if (comp > 0) {
                            return Math.round(((curr - comp) / comp) * 1000) / 10;
                        }
                        return curr > 0 ? 100 : 0;
                    });
                    chartSeries = [{ name: 'YoY Growth %', data: growthData }];
                    chartColors = growthData.map(val => val >= 0 ? '#10B981' : '#EF4444');
                    fillConfig = { type: 'solid', opacity: 0.85 };
                    strokeConfig = { width: 0 };
                    labelsFormatter = val => (val >= 0 ? '+' : '') + val + '%';
                }
            } else if (trendMode === 'ytd') {
                if (trendView === 'total' && res.series.length >= 2) {
                    let currSum = 0, compSum = 0;
                    const currYTD = res.series[0].data.map(val => { currSum += val; return currSum; });
                    const compYTD = res.series[1].data.map(val => { compSum += val; return compSum; });
                    chartSeries = [
                        { name: res.series[0].name, data: currYTD },
                        { name: res.series[1].name, data: compYTD }
                    ];
                    chartType = 'line';
                    strokeConfig = { width: [3, 2.5], dashArray: [0, 4], curve: 'smooth' };
                    fillConfig = { opacity: 1 };
                } else {
                    chartSeries = res.series.map(s => {
                        let sum = 0;
                        return {
                            name: s.name,
                            data: s.data.map(val => { sum += val; return sum; })
                        };
                    });
                }
            } else if (trendMode === 'value') {
                showDataLabels = true;
            }

            const options = {
                chart: {
                    height: 360,
                    type: chartType,
                    stacked: isStacked,
                    toolbar: { show: false },
                    fontFamily: 'Inter, sans-serif'
                },
                dataLabels: {
                    enabled: showDataLabels,
                    formatter: labelsFormatter,
                    offsetY: -20,
                    style: { fontSize: '9px', fontWeight: '700', colors: isDark ? ['#ffffff'] : ['#1e293b'] }
                },
                colors: chartColors,
                fill: fillConfig,
                stroke: strokeConfig,
                series: chartSeries,
                xaxis: {
                    categories: res.categories,
                    labels: { style: { fontWeight: 600, fontSize: '11px', colors: isDark ? '#94a3b8' : '#64748b' } }
                },
                yaxis: {
                    labels: {
                        formatter: labelsFormatter,
                        style: { fontWeight: 600, fontSize: '11px', colors: isDark ? '#94a3b8' : '#64748b' }
                    }
                },
                plotOptions: {
                    bar: {
                        columnWidth: '55%',
                        barGap: '20%',
                        borderRadius: 5,
                        borderRadiusApplication: 'end',
                        distributed: trendMode === 'growth' && trendView === 'total',
                        dataLabels: { position: 'top' }
                    }
                },
                legend: {
                    show: trendMode !== 'growth',
                    position: 'bottom',
                    fontWeight: 600,
                    fontSize: '12px',
                    labels: { colors: isDark ? '#e2e8f0' : '#1e293b' }
                },
                tooltip: {
                    theme: isDark ? 'dark' : 'light',
                    y: { formatter: v => trendMode === 'growth' ? (v >= 0 ? '+' : '') + v + '%' : (State.met === 'Transactions' ? Number(v).toLocaleString('en-IN') : fmt(Number(v))) }
                }
            };

            const trendTitle = document.getElementById('trendTitle');
            if (trendTitle) trendTitle.textContent = 'Monthly Net Retail Selling Trend';

            const trendTag = document.getElementById('trendTag');
            if (trendTag) trendTag.textContent = State.ms.size ? State.ms.size + ' MONTHS' : 'ALL MONTHS';

            if (Charts.trend) {
                Charts.trend.destroy();
                Charts.trend = new ApexCharts(document.querySelector("#trendChart"), options);
                Charts.trend.render();
            } else {
                Charts.trend = new ApexCharts(document.querySelector("#trendChart"), options);
                Charts.trend.render();
            }
        } catch (err) {
            console.error("Failed to load Trend Chart:", err);
        }
    }

    async function loadConsigneeMix() {
        try {
            const res = await fetchJson(`/Dashboard/GetConsigneeMix?${getQueryString()}`);
            if (!res) return;

            const isDark = document.body.classList.contains('dark-theme');
            const cols = res.labels.map((l, i) => PALETTE[i % PALETTE.length]);

            const options = {
                chart: { height: 320, type: 'donut', fontFamily: 'Inter, sans-serif' },
                colors: cols,
                series: res.values,
                labels: res.labels,
                legend: { show: true, position: 'right', fontSize: '11px', fontWeight: 600, labels: { colors: isDark ? '#e2e8f0' : '#1e293b' } },
                dataLabels: {
                    enabled: true,
                    formatter: val => val.toFixed(1) + '%'
                },
                plotOptions: {
                    pie: {
                        donut: {
                            size: '65%',
                            labels: {
                                show: true,
                                name: { show: true, fontSize: '13px', fontWeight: 600, color: isDark ? '#94a3b8' : '#64748b' },
                                value: {
                                    show: true,
                                    fontSize: '16px',
                                    fontWeight: 700,
                                    color: isDark ? '#ffffff' : '#1e293b',
                                    formatter: v => State.met === 'Transactions' ? Number(v).toLocaleString('en-IN') : fmt(Number(v))
                                },
                                total: {
                                    show: true,
                                    label: 'Total',
                                    fontSize: '13px',
                                    fontWeight: 600,
                                    color: isDark ? '#94a3b8' : '#64748b',
                                    formatter: w => {
                                        const sum = w.globals.seriesTotals.reduce((a, b) => a + b, 0);
                                        return State.met === 'Transactions' ? sum.toLocaleString('en-IN') : fmt(sum);
                                    }
                                }
                            }
                        }
                    }
                },
                stroke: { show: true, width: 2, colors: isDark ? ['#1e293b'] : ['#ffffff'] },
                tooltip: { theme: isDark ? 'dark' : 'light', y: { formatter: v => State.met === 'Transactions' ? Number(v).toLocaleString('en-IN') : fmt(Number(v)) } }
            };

            if (Charts.consignee) {
                Charts.consignee.destroy();
                Charts.consignee = new ApexCharts(document.querySelector("#consigneeChart"), options);
                Charts.consignee.render();
            } else {
                Charts.consignee = new ApexCharts(document.querySelector("#consigneeChart"), options);
                Charts.consignee.render();
            }
        } catch (err) {
            console.error("Failed to load Consignee Mix:", err);
        }
    }

    async function loadDealerSubTypeMixAPI() {
        try {
            const res = await fetchJson(`/Dashboard/GetDealerSubTypeMix?${getQueryString()}`);
            if (!res) return;

            const isDark = document.body.classList.contains('dark-theme');
            const cols = res.labels.map((l, i) => PALETTE[(i + 3) % PALETTE.length]); // Offset colors for variety

            const options = {
                chart: { height: 320, type: 'donut', fontFamily: 'Inter, sans-serif' },
                colors: cols,
                series: res.values,
                labels: res.labels,
                legend: { show: true, position: 'right', fontSize: '11px', fontWeight: 600, labels: { colors: isDark ? '#e2e8f0' : '#1e293b' } },
                dataLabels: {
                    enabled: true,
                    formatter: val => val.toFixed(1) + '%'
                },
                plotOptions: {
                    pie: {
                        donut: {
                            size: '65%',
                            labels: {
                                show: true,
                                name: { show: true, fontSize: '13px', fontWeight: 600, color: isDark ? '#94a3b8' : '#64748b' },
                                value: {
                                    show: true,
                                    fontSize: '16px',
                                    fontWeight: 700,
                                    color: isDark ? '#ffffff' : '#1e293b',
                                    formatter: v => State.met === 'Transactions' ? Number(v).toLocaleString('en-IN') : fmt(Number(v))
                                },
                                total: {
                                    show: true,
                                    label: 'Total',
                                    fontSize: '13px',
                                    fontWeight: 600,
                                    color: isDark ? '#94a3b8' : '#64748b',
                                    formatter: w => {
                                        const sum = w.globals.seriesTotals.reduce((a, b) => a + b, 0);
                                        return State.met === 'Transactions' ? sum.toLocaleString('en-IN') : fmt(sum);
                                    }
                                }
                            }
                        }
                    }
                },
                stroke: { show: true, width: 2, colors: isDark ? ['#1e293b'] : ['#ffffff'] },
                tooltip: { theme: isDark ? 'dark' : 'light', y: { formatter: v => State.met === 'Transactions' ? Number(v).toLocaleString('en-IN') : fmt(Number(v)) } }
            };

            if (Charts.dealerSub) {
                Charts.dealerSub.destroy();
                Charts.dealerSub = new ApexCharts(document.querySelector("#dealerSubChart"), options);
                Charts.dealerSub.render();
            } else {
                Charts.dealerSub = new ApexCharts(document.querySelector("#dealerSubChart"), options);
                Charts.dealerSub.render();
            }
        } catch (err) {
            console.error("Failed to load Dealer Sub Type Mix:", err);
        }
    }

    async function loadPartyMix() {
        try {
            const res = await fetchJson(`/Dashboard/GetPartyMix?${getQueryString()}`);
            if (!res) return;

            const isDark = document.body.classList.contains('dark-theme');
            const cols = res.labels.map(l => PC[l.toUpperCase()] || '#64748b');

            const partyView = document.getElementById('partyView') ? document.getElementById('partyView').value : 'contribution';

            let options;
            if (partyView === 'growth') {
                options = {
                    chart: {
                        height: 320,
                        type: 'bar',
                        fontFamily: 'Inter, sans-serif',
                        toolbar: { show: false }
                    },
                    plotOptions: {
                        bar: {
                            horizontal: true,
                            borderRadius: 5,
                            barHeight: '58%',
                            distributed: true
                        }
                    },
                    colors: cols,
                    series: [{ name: 'YTD Growth %', data: res.growth }],
                    xaxis: {
                        categories: res.labels,
                        labels: {
                            formatter: val => val + '%',
                            style: { fontWeight: 600, fontSize: '11px', colors: isDark ? '#94a3b8' : '#64748b' }
                        }
                    },
                    yaxis: {
                        labels: { style: { fontWeight: 700, fontSize: '11px', colors: isDark ? '#e2e8f0' : '#1e293b' } }
                    },
                    legend: { show: false },
                    dataLabels: {
                        enabled: true,
                        formatter: val => (val >= 0 ? '+' : '') + val + '%',
                        style: { fontSize: '11px', fontWeight: '700' }
                    },
                    tooltip: {
                        theme: isDark ? 'dark' : 'light',
                        y: { formatter: val => (val >= 0 ? '+' : '') + val + '%' }
                    }
                };
            } else {
                options = {
                    chart: {
                        height: 320,
                        type: 'donut',
                        fontFamily: 'Inter, sans-serif'
                    },
                    labels: res.labels,
                    series: res.values,
                    colors: cols,
                    stroke: {
                        show: true,
                        width: 2,
                        colors: isDark ? ['#1e293b'] : ['#ffffff']
                    },
                    legend: {
                        position: 'right',
                        fontSize: '11px',
                        fontWeight: 600,
                        labels: {
                            colors: isDark ? '#e2e8f0' : '#1e293b'
                        }
                    },
                    dataLabels: {
                        enabled: true,
                        formatter: (val) => val.toFixed(1) + '%'
                    },
                    plotOptions: {
                        pie: {
                            donut: {
                                size: '65%',
                                labels: {
                                    show: true,
                                    name: {
                                        show: true,
                                        fontSize: '13px',
                                        fontWeight: 600,
                                        color: isDark ? '#94a3b8' : '#64748b'
                                    },
                                    value: {
                                        show: true,
                                        fontSize: '16px',
                                        fontWeight: 700,
                                        color: isDark ? '#ffffff' : '#1e293b',
                                        formatter: v => State.met === 'Transactions' ? Number(v).toLocaleString('en-IN') : fmt(Number(v))
                                    },
                                    total: {
                                        show: true,
                                        label: 'Total',
                                        fontSize: '13px',
                                        fontWeight: 600,
                                        color: isDark ? '#94a3b8' : '#64748b',
                                        formatter: w => {
                                            const sum = w.globals.seriesTotals.reduce((a, b) => a + b, 0);
                                            return State.met === 'Transactions' ? sum.toLocaleString('en-IN') : fmt(sum);
                                        }
                                    }
                                }
                            }
                        }
                    },
                    tooltip: {
                        theme: isDark ? 'dark' : 'light',
                        y: { formatter: v => State.met === 'Transactions' ? Number(v).toLocaleString('en-IN') : fmt(Number(v)) }
                    }
                };
            }

            if (Charts.party) {
                Charts.party.destroy();
                Charts.party = new ApexCharts(document.querySelector("#partyChart"), options);
                Charts.party.render();
            } else {
                Charts.party = new ApexCharts(document.querySelector("#partyChart"), options);
                Charts.party.render();
            }
        } catch (err) {
            console.error("Failed to load Party Mix:", err);
        }
    }

    async function loadCategoryMix() {
        try {
            const res = await fetchJson(`/Dashboard/GetCategoryMix?${getQueryString()}`);
            if (!res) return;

            const isDark = document.body.classList.contains('dark-theme');
            const catNames = { 'AA': 'Accessories', 'M': 'Genuine Parts', 'AG': 'Oil', 'T': 'Tools' };
            const CAT_COLORS = ['#1b63f2', '#ff9f43', '#00b074', '#f1c40f'];
            
            const displayLabels = res.labels.map(l => catNames[l.toUpperCase()] || l);
            const cols = res.labels.map((l, i) => CC[l.toUpperCase()] || CAT_COLORS[i % CAT_COLORS.length]);

            const dcv = document.getElementById('dcv');
            if (dcv) dcv.textContent = ftsLabelFormat(res.total);

            const options = {
                chart: { height: 280, type: 'donut', fontFamily: 'Inter, sans-serif' },
                colors: cols,
                series: res.values,
                labels: displayLabels,
                legend: { show: true, position: 'top', fontSize: '11px' },
                dataLabels: {
                    enabled: true,
                    formatter: val => val.toFixed(1) + '%'
                },
                plotOptions: {
                    pie: {
                        donut: {
                            size: '58%',
                            labels: {
                                show: true,
                                name: { show: true, fontSize: '11px', color: '#94a3b8' },
                                value: {
                                    show: true,
                                    fontSize: '15px',
                                    fontWeight: '800',
                                    color: isDark ? '#f8fafc' : '#1e293b',
                                    formatter: val => State.met === 'Transactions' ? Number(val).toLocaleString('en-IN') : fmt(Number(val))
                                },
                                total: {
                                    show: true,
                                    label: 'Total',
                                    fontSize: '11px',
                                    fontWeight: 600,
                                    color: '#94a3b8',
                                    formatter: w => {
                                        const sum = w.globals.seriesTotals.reduce((a, b) => a + b, 0);
                                        return State.met === 'Transactions' ? sum.toLocaleString('en-IN') : fmt(sum);
                                    }
                                }
                            }
                        }
                    }
                }
            };

            if (Charts.cat) {
                Charts.cat.updateOptions(options, true, true);
            } else {
                Charts.cat = new ApexCharts(document.querySelector("#catChart"), options);
                Charts.cat.render();
            }

            // Category card footer badge updates
            const catBadge = document.getElementById('catBadge');
            const catBadgeSub = document.getElementById('catBadgeSub');
            if (catBadge) {
                if (res.pct !== null) {
                    catBadge.style.display = 'inline-flex';
                    const isUp = res.pct >= 0;
                    catBadge.style.background = isUp ? 'rgba(16, 185, 129, 0.1)' : 'rgba(239, 68, 68, 0.1)';
                    catBadge.style.color = isUp ? '#10b981' : '#ef4444';
                    catBadge.innerHTML = `<span style="font-size:11px;margin-right:2px;">${isUp ? '↗' : '↘'}</span> ${Math.abs(res.pct).toFixed(1)}%`;
                    if (catBadgeSub) {
                        catBadgeSub.style.display = 'block';
                        catBadgeSub.textContent = 'vs comparison';
                    }
                } else {
                    catBadge.style.display = 'none';
                    if (catBadgeSub) catBadgeSub.style.display = 'none';
                }
            }

            const catFootVal = document.getElementById('catFootVal');
            const catFootLabel = document.getElementById('catFootLabel');
            if (catFootVal) {
                const diffSign = res.diff >= 0 ? '+' : '';
                catFootVal.textContent = diffSign + ftsLabelFormat(res.diff);
                if (catFootLabel) {
                    const metricLabel = { Net_Sales: 'Revenue', Net_DDL: 'Net DDL', Discount: 'Discount', Transactions: 'Txns' }[State.met] || 'Value';
                    catFootLabel.textContent = metricLabel + ' vs comparison';
                }
            }
        } catch (err) {
            console.error("Failed to load Category Mix:", err);
        }
    }

    async function loadQuarterSummary() {
        try {
            const res = await fetchJson(`/Dashboard/GetQuarterSummary?${getQueryString()}`);
            if (!res) return;

            const isDark = document.body.classList.contains('dark-theme');
            const qtrMode = document.getElementById('qtrMode') ? document.getElementById('qtrMode').value : 'value';
            const colors = ['#2563EB', '#0EA5E9'];

            let chartSeries = res.series;
            let chartColors = colors;
            let chartType = 'bar';
            let strokeConfig = { width: [0, 2.5], curve: 'smooth' };
            let fillConfig = {
                type: ['solid', 'gradient'],
                gradient: {
                    shade: isDark ? 'dark' : 'light',
                    type: 'vertical',
                    shadeIntensity: .5,
                    gradientToColors: ['#0EA5E9'],
                    inverseColors: false,
                    opacityFrom: .7,
                    opacityTo: .1,
                    stops: [0, 90, 100]
                }
            };

            let showDataLabels = false;
            let labelsFormatter = val => ftsLabelFormat(val);

            if (qtrMode === 'growth') {
                showDataLabels = true;
                if (res.series.length >= 2) {
                    const currData = res.series[0].data;
                    const compData = res.series[1].data;
                    const growthData = currData.map((curr, idx) => {
                        const comp = compData[idx] || 0;
                        if (comp > 0) {
                            return Math.round(((curr - comp) / comp) * 1000) / 10;
                        }
                        return curr > 0 ? 100 : 0;
                    });
                    chartSeries = [{ name: 'YoY Growth %', data: growthData }];
                    chartColors = growthData.map(val => val >= 0 ? '#10B981' : '#EF4444');
                    fillConfig = { type: 'solid', opacity: 0.85 };
                    strokeConfig = { width: 0 };
                    labelsFormatter = val => (val >= 0 ? '+' : '') + val + '%';
                }
            } else if (qtrMode === 'ytd') {
                if (res.series.length >= 2) {
                    let currSum = 0, compSum = 0;
                    const currYTD = res.series[0].data.map(val => { currSum += val; return currSum; });
                    const compYTD = res.series[1].data.map(val => { compSum += val; return compSum; });
                    chartSeries = [
                        { name: res.series[0].name, data: currYTD },
                        { name: res.series[1].name, data: compYTD }
                    ];
                    chartType = 'line';
                    strokeConfig = { width: [3, 2.5], dashArray: [0, 4], curve: 'smooth' };
                    fillConfig = { opacity: 1 };
                }
            } else if (qtrMode === 'value') {
                showDataLabels = true;
            }

            const options = {
                chart: {
                    height: 290,
                    type: chartType,
                    toolbar: { show: false },
                    fontFamily: 'Inter, sans-serif'
                },
                dataLabels: {
                    enabled: showDataLabels,
                    formatter: labelsFormatter,
                    offsetY: -20,
                    style: { fontSize: '10px', fontWeight: '700', colors: isDark ? ['#ffffff'] : ['#1e293b'] }
                },
                colors: chartColors,
                fill: fillConfig,
                stroke: strokeConfig,
                series: chartSeries,
                xaxis: {
                    categories: res.labels,
                    labels: { style: { fontWeight: 700, fontSize: '13px', colors: isDark ? '#94a3b8' : '#1e293b' } }
                },
                yaxis: {
                    labels: {
                        formatter: labelsFormatter,
                        style: { fontWeight: 600, fontSize: '11px', colors: isDark ? '#94a3b8' : '#64748b' }
                    }
                },
                plotOptions: {
                    bar: {
                        columnWidth: '55%',
                        barGap: '20%',
                        borderRadius: 5,
                        borderRadiusApplication: 'end',
                        distributed: qtrMode === 'growth',
                        dataLabels: { position: 'top' }
                    }
                },
                legend: {
                    show: qtrMode !== 'growth',
                    position: 'bottom',
                    fontWeight: 600,
                    fontSize: '11px',
                    labels: { colors: isDark ? '#e2e8f0' : '#1e293b' }
                },
                tooltip: {
                    theme: isDark ? 'dark' : 'light',
                    y: { formatter: v => qtrMode === 'growth' ? (v >= 0 ? '+' : '') + v + '%' : (State.met === 'Transactions' ? Number(v).toLocaleString('en-IN') : fmt(Number(v))) }
                }
            };

            if (Charts.qtr) {
                Charts.qtr.destroy();
                Charts.qtr = new ApexCharts(document.querySelector("#qtrChart"), options);
                Charts.qtr.render();
            } else {
                Charts.qtr = new ApexCharts(document.querySelector("#qtrChart"), options);
                Charts.qtr.render();
            }
        } catch (err) {
            console.error("Failed to load Quarter Summary:", err);
        }
    }

    async function loadConsigneeSummary() {
        try {
            const res = await fetchJson(`/Dashboard/GetConsigneeSummary?${getQueryString()}`);
            if (!res) return;

            const isDark = document.body.classList.contains('dark-theme');

            const options = {
                chart: { height: 290, type: 'line', toolbar: { show: false } },
                colors: PALETTE,
                stroke: { width: 2.5, curve: 'smooth' },
                series: res.series,
                xaxis: {
                    categories: res.categories,
                    labels: { style: { fontWeight: 600, fontSize: '11px', colors: isDark ? '#94a3b8' : '#64748b' } }
                },
                yaxis: {
                    labels: { formatter: val => ftsLabelFormat(val) }
                }
            };

            if (Charts.con) {
                Charts.con.updateOptions(options, true, true);
            } else {
                Charts.con = new ApexCharts(document.querySelector("#conChart"), options);
                Charts.con.render();
            }
        } catch (err) {
            console.error("Failed to load Consignee Summary:", err);
        }
    }

    async function loadComparisonMatrix() {
        try {
            const res = await fetchJson(`/Dashboard/GetComparison?${getQueryString()}&cmp=${State.cmp}`);
            if (!res) return;

            const cmpHead = document.getElementById('cmpHead');
            const cmpBody = document.getElementById('cmpBody');
            if (!cmpHead || !cmpBody) return;

            const cmpTitle = document.getElementById('cmpTitle');
            const cmpSub = document.getElementById('cmpSub');
            const cmpTag = document.getElementById('cmpTag');

            if (res.type === 'none') {
                if (cmpTitle) cmpTitle.textContent = 'Monthly Performance Matrix';
                if (cmpSub) cmpSub.textContent = 'All party types · recent months';
                if (cmpTag) cmpTag.textContent = 'OVERVIEW';

                cmpHead.innerHTML = `<tr><th>PARTY TYPE</th>${res.headers.map(h => `<th class="r">${h}</th>`).join('')}<th class="r">TOTAL</th><th class="r">AVG/MO</th></tr>`;
                
                cmpBody.innerHTML = res.rows.map(row => {
                    const dot = `<span style="display:inline-block;width:6px;height:6px;border-radius:50%;background:${PC[row.partyType.toUpperCase()] || '#64748b'};vertical-align:middle;margin-right:5px"></span>`;
                    const mCells = row.values.map(v => `<td class="r">${v ? fts(v) : '—'}</td>`).join('');
                    return `<tr><td class="nm">${dot}${row.partyType}</td>${mCells}<td class="r"><b>${fts(row.total)}</b></td><td class="r" style="color:var(--t2)">${fts(row.avg)}</td></tr>`;
                }).join('');
            } else {
                const tag = res.type === 'mom' ? 'MoM' : 'QoQ';
                if (cmpTitle) cmpTitle.textContent = `${tag}: ${res.headers[0]} vs ${res.headers[1]}`;
                if (cmpSub) cmpSub.textContent = `${tag} Comparison Grid`;
                if (cmpTag) cmpTag.textContent = tag;

                cmpHead.innerHTML = `<tr><th>PARTY TYPE</th><th class="r">${res.headers[0]}</th><th class="r">${res.headers[1]}</th><th class="r">Δ VALUE</th><th class="r">Δ %</th></tr>`;
                
                cmpBody.innerHTML = res.rows.map(row => {
                    const diffSign = row.diff >= 0 ? '+' : '';
                    const diffColor = row.diff >= 0 ? 'var(--green)' : 'var(--rose)';
                    const dot = `<span style="display:inline-block;width:6px;height:6px;border-radius:50%;background:${PC[row.partyType.toUpperCase()] || '#64748b'};vertical-align:middle;margin-right:5px"></span>`;
                    const badgeClass = row.pct >= 0 ? 'tag up' : 'tag dn';
                    const arrow = row.pct >= 0 ? '▲' : '▼';
                    const pctBadge = row.pct !== null ? `<span class="tag ${badgeClass}">${arrow} ${Math.abs(row.pct).toFixed(1)}%</span>` : '—';
                    
                    return `
                        <tr>
                            <td class="nm">${dot}${row.partyType}</td>
                            <td class="r">${fts(row.current)}</td>
                            <td class="r">${fts(row.previous)}</td>
                            <td class="r" style="color:${diffColor}">${diffSign}${fts(row.diff)}</td>
                            <td class="r">${pctBadge}</td>
                        </tr>
                    `;
                }).join('');
            }
        } catch (err) {
            console.error("Failed to load Comparison Matrix:", err);
        }
    }

    async function loadLocationRanking() {
        try {
            const res = await fetchJson(`/Dashboard/GetLocationRanking?${getQueryString()}`);
            if (!res) return;

            const locRank = document.getElementById('locRank');
            if (!locRank) return;

            const max = res[0] ? res[0].value : 1;
            locRank.innerHTML = res.map((item, i) => `
                <div class="ri">
                    <div class="rn">${i + 1}</div>
                    <div class="rb">
                        <div class="rm"><span class="rnm">${item.name}</span><span class="rv">${fts(item.value)}</span></div>
                        <div class="rt"><div class="rf" style="width:${(item.value / max * 100).toFixed(1)}%;background:${LGRAD[Math.min(i, LGRAD.length - 1)]}"></div></div>
                    </div>
                </div>`).join('');

            const locCount = document.getElementById('locCount');
            if (locCount) {
                locCount.textContent = res.length + ' branches';
            }
        } catch (err) {
            console.error("Failed to load Location Ranking:", err);
        }
    }

    // ── DELEGATE DEALER TABLE AND DEALER SUB-TYPE MIX ──

    function renderDealerSales() {
        if (debounceTimer) {
            clearTimeout(debounceTimer);
        }

        const yrSel = document.getElementById('yrSel');
        const yr = yrSel ? yrSel.value : 'current';
        const activeLabel = (yr === 'current') ? 'FY ' + window.ANCHOR_YEAR : 'FY ' + (window.ANCHOR_YEAR - 1);
        const compLabel = (yr === 'current') ? 'FY ' + (window.ANCHOR_YEAR - 1) : 'FY ' + window.ANCHOR_YEAR;

        const dsh = document.getElementById('dealerSalesHeader');
        if (dsh) dsh.textContent = 'Sales (' + activeLabel + ')';
        const dch = document.getElementById('dealerCompHeader');
        if (dch) dch.textContent = 'Sales (' + compLabel + ')';

        const tbody = document.getElementById('dealerSalesBody');
        if (tbody) {
            tbody.innerHTML = `
                <tr>
                    <td><div class="shimmer" style="width: 30px;"></div></td>
                    <td><div class="shimmer" style="width: 80%;"></div></td>
                    <td class="r"><div class="shimmer" style="width: 60px;"></div></td>
                    <td class="r"><div class="shimmer" style="width: 60px;"></div></td>
                    <td class="r"><div class="shimmer" style="width: 80px;"></div></td>
                </tr>
            `.repeat(5);
        }

        debounceTimer = setTimeout(() => {
            fetchDealerSalesData();
        }, 250);
    }

    async function fetchDealerSalesData() {
        if (currentAbortController) {
            currentAbortController.abort();
        }
        currentAbortController = new AbortController();
        const { signal } = currentAbortController;

        try {
            const yrSel = document.getElementById('yrSel');
            const yr = yrSel ? yrSel.value : 'current';
            const targetYearNum = (yr === 'current') ? window.ANCHOR_YEAR : (window.ANCHOR_YEAR - 1);

            const params = new URLSearchParams({
                targetYear: targetYearNum,
                quarters: [...State.qs].join(','),
                months: [...State.ms].join(','),
                partyTypes: [...State.ps].join(','),
                categories: [...State.cats].join(','),
                locations: [...State.ls].join(','),
                dealerSubTypes: [...State.ds].join(','),
                search: document.getElementById('dealerSearchInput') ? document.getElementById('dealerSearchInput').value.trim() : '',
                limit: document.getElementById('dealerLimitSel') ? document.getElementById('dealerLimitSel').value : '50'
            });

            const res = await fetchJson(`/Reports/GetDealerSales?${params.toString()}`, { signal });
            if (!res) return;

            const tbody = document.getElementById('dealerSalesBody');
            if (!tbody) return;

            if (!res.rows || res.rows.length === 0) {
                tbody.innerHTML = `<tr><td colspan="5" class="text-center" style="color:var(--t2);padding:20px;">No dealer records match active filters or search query</td></tr>`;
                const dct = document.getElementById('dealerCountTag');
                if (dct) dct.textContent = '0 DEALERS';
                return;
            }

            const limitVal = document.getElementById('dealerLimitSel') ? document.getElementById('dealerLimitSel').value : '50';
            const dct = document.getElementById('dealerCountTag');
            if (dct) {
                if (limitVal !== 'all') {
                    dct.textContent = `TOP ${res.rows.length} OF ${res.total}`;
                } else {
                    dct.textContent = `ALL ${res.total} DEALERS`;
                }
            }

            tbody.innerHTML = res.rows.map((d, i) => {
                const pctVal = d.pct;
                const diffVal = d.diff;
                const diffSign = diffVal >= 0 ? '+' : '';
                const diffColor = diffVal >= 0 ? 'var(--success)' : 'var(--danger)';

                let pctBadge = '';
                if (pctVal !== null) {
                    const isUp = pctVal >= 0;
                    const badgeBg = isUp ? 'rgba(16, 185, 129, 0.1)' : 'rgba(239, 68, 68, 0.1)';
                    const badgeColor = isUp ? 'var(--success)' : 'var(--danger)';
                    const arrow = isUp ? '&#x2197;' : '&#x2198;';
                    pctBadge = `<span style="margin-left:8px;font-size:11px;padding:3px 8px;border-radius:12px;background:${badgeBg};color:${badgeColor};font-weight:700;">${arrow} ${Math.abs(pctVal).toFixed(1)}%</span>`;
                } else if (d.activeSales > 0 && d.compSales === 0) {
                    pctBadge = `<span style="margin-left:8px;font-size:11px;padding:3px 8px;border-radius:12px;background:rgba(16, 185, 129, 0.1);color:var(--success);font-weight:700;">NEW</span>`;
                } else {
                    pctBadge = `<span style="margin-left:8px;font-size:11px;padding:3px 8px;border-radius:12px;background:rgba(100, 116, 139, 0.1);color:var(--text-muted);font-weight:700;">&mdash;</span>`;
                }

                return `
                    <tr>
                        <td style="font-weight:700;color:var(--text-secondary)">#${i + 1}</td>
                        <td class="nm" style="max-width:300px;white-space:normal;">
                            <span class="party-code-pill" style="display:inline-block;font-size:10px;padding:2px 6px;background:rgba(30,64,175,0.06);border:1px solid rgba(30,64,175,0.15);border-radius:4px;color:var(--primary);font-weight:700;margin-right:8px;margin-bottom:4px;">${d.partyCode}</span>
                            <span style="font-weight:600;color:var(--text-primary);">${d.partyName}</span>
                        </td>
                        <td class="r" style="font-weight:700;color:var(--text-primary)">${fmt(d.activeSales)}</td>
                        <td class="r" style="color:var(--text-secondary);font-weight:500;">${fmt(d.compSales)}</td>
                        <td class="r" style="font-weight:600;color:${diffColor}">
                            ${diffSign}${fmt(diffVal)}
                            ${pctBadge}
                        </td>
                    </tr>
                `;
            }).join('');

            // Also load Dealer Sub-Type Split Donut

        } catch (err) {
            if (err.name !== 'AbortError') {
                console.error('Failed to load dealer sales:', err);
                const tbody = document.getElementById('dealerSalesBody');
                if (tbody) {
                    tbody.innerHTML = `<tr><td colspan="5" class="text-center" style="color:var(--rose);padding:20px;">Error loading dealer records: ${err.message}</td></tr>`;
                }
            }
        }
    }

    function loadDealerSubTypeMix(rows) {
        // Summarize dealer types from active table rows (avoids massive client-side load)
        const counts = {};
        rows.forEach(r => {
            // Find type dynamically from rows dealerSubType property
            const type = (r.dealerSubType || '').toUpperCase().trim();
            const valid = ['AW', 'MW', 'RO'].includes(type) ? type : 'AW';
            counts[valid] = (counts[valid] || 0) + r.activeSales;
        });

        const sorted = Object.entries(counts).sort((a, b) => b[1] - a[1]);
        const labels = sorted.map(e => e[0]);
        const vals = sorted.map(e => e[1]);
        const SUB_COLORS = ['#1b63f2', '#8b5cf6', '#ff9f43'];
        const cols = labels.map(l => DC[l] || SUB_COLORS[0]);
        const tot = vals.reduce((a, b) => a + b, 0);

        const dsv = document.getElementById('dsv');
        if (dsv) dsv.textContent = ftsLabelFormat(tot);

        const isDark = document.body.classList.contains('dark-theme');

        const options = {
            chart: { height: 280, type: 'donut', fontFamily: 'Inter, sans-serif' },
            colors: cols,
            series: vals,
            labels: labels,
            legend: { show: true, position: 'top', fontSize: '11px' },
            dataLabels: {
                enabled: true,
                formatter: val => val.toFixed(1) + '%'
            },
            plotOptions: {
                pie: {
                    donut: {
                        size: '58%',
                        labels: {
                            show: true,
                            name: { show: true, fontSize: '11px', color: '#94a3b8' },
                            value: {
                                show: true,
                                fontSize: '15px',
                                fontWeight: '800',
                                color: isDark ? '#f8fafc' : '#1e293b',
                                formatter: val => State.met === 'Transactions' ? Number(val).toLocaleString('en-IN') : fmt(Number(val))
                            },
                            total: {
                                show: true,
                                label: 'Total',
                                fontSize: '11px',
                                fontWeight: 600,
                                color: '#94a3b8',
                                formatter: w => {
                                    const sum = w.globals.seriesTotals.reduce((a, b) => a + b, 0);
                                    return State.met === 'Transactions' ? sum.toLocaleString('en-IN') : fmt(sum);
                                }
                            }
                        }
                    }
                }
            }
        };

        if (Charts.sub) {
            Charts.sub.updateOptions(options, true, true);
        } else {
            Charts.sub = new ApexCharts(document.querySelector("#subChart"), options);
            Charts.sub.render();
        }

        // SubType Badge comparative updates
        const subBadge = document.getElementById('subBadge');
        if (subBadge) subBadge.style.display = 'none'; // Simple layout preservation
    }

    // ── GENERAL UTILITIES ──

    async function fetchJson(url, opts) {
        const response = await fetch(url, opts);
        if (!response.ok) throw new Error(`HTTP Error: ${response.status}`);
        return await response.json();
    }

    function h2r(h, a) {
        const r = parseInt(h.slice(1, 3), 16), g = parseInt(h.slice(3, 5), 16), b = parseInt(h.slice(5, 7), 16);
        return `rgba(${r},${g},${b},${a})`;
    }

    function fmt(v) {
        if (v == null || isNaN(v)) return '—';
        const a = Math.abs(v);
        if (a >= 1e7) return '₹' + (v / 1e7).toFixed(2) + 'Cr';
        if (a >= 1e5) return '₹' + (v / 1e5).toFixed(2) + 'L';
        return '₹' + v.toLocaleString('en-IN', { maximumFractionDigits: 0 });
    }

    function fts(v) {
        if (v == null || isNaN(v)) return '—';
        const a = Math.abs(v);
        if (a >= 1e7) return (v / 1e7).toFixed(1) + 'Cr';
        if (a >= 1e5) return (v / 1e5).toFixed(1) + 'L';
        if (a >= 1e3) return (v / 1e3).toFixed(1) + 'K';
        return v.toFixed(0);
    }

    function ftsLabelFormat(v) {
        if (v == null || isNaN(v)) return '—';
        const a = Math.abs(v);
        if (a >= 1e7) return (v / 1e7).toFixed(1) + ' Cr';
        if (a >= 1e5) return (v / 1e5).toFixed(1) + ' L';
        if (a >= 1e3) return (v / 1e3).toFixed(1) + ' K';
        return v.toFixed(0);
    }

    function updateAfcBadges() {
        const map = { qs: 'qAfc', ms: 'mAfc', ps: 'pAfc', cats: 'catAfc', cons: 'conAfc', ds: 'dAfc', ls: 'lAfc' };
        Object.entries(map).forEach(([k, id]) => {
            const el = document.getElementById(id);
            if (!el) return;
            if (State[k].size > 0) {
                el.textContent = State[k].size;
                el.style.display = 'inline';
            } else {
                el.style.display = 'none';
            }
        });
        const comp = document.getElementById('compAfc');
        if (comp) {
            if (State.cmp !== 'none') {
                comp.textContent = State.cmp.toUpperCase();
                comp.style.display = 'inline';
            } else {
                comp.style.display = 'none';
            }
        }
    }

    function updateHdrSubtitle() {
        const yrSel = document.getElementById('yrSel');
        const yr = yrSel ? yrSel.value : 'current';
        const yrText = (yr === 'current')
            ? `FY ${window.ANCHOR_YEAR} - ${window.ANCHOR_YEAR + 1}`
            : `FY ${window.ANCHOR_YEAR - 1} - ${window.ANCHOR_YEAR}`;
        
        const hst = document.getElementById('hdrSubTitle');
        if (hst) {
            hst.textContent = `DEALER ANALYTICS · ${yrText} · RAJASTHAN`;
        }
    }

    function bindThemeChange() {
        window.addEventListener('globalThemeChanged', function(e) {
            const isDark = e.detail.isDark;
            const themeMode = isDark ? 'dark' : 'light';
            const gridColor = isDark ? '#1e293b' : '#e2e8f0';

            Object.values(Charts).forEach(chart => {
                if (chart) {
                    chart.updateOptions({
                        theme: { mode: themeMode },
                        grid: { borderColor: gridColor }
                    });
                }
            });
        });
    }

    // Exposed public APIs
    return {
        init: init,
        setMetric: setMetric,
        resetAll: resetAll,
        go: go,
        renderDealerSales: renderDealerSales,
        loadPartyMix: loadPartyMix,
        loadTrend: loadTrend,
        loadQuarterSummary: loadQuarterSummary
    };
})();
