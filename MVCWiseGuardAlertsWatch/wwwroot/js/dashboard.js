// Dashboard — 2x3 grid of 6 instances (5 radar maps + WiseGuard), light theme,
// matching the wiseguardalertswatch.base44.app design.
window.Dashboard = (function () {
    const PALETTE = ['#2563eb', '#16a34a', '#9333ea', '#ea580c', '#0891b2', '#be185d', '#65a30d', '#dc2626'];
    const RADAR_COUNT = 5;

    let settings = { interval: 10, step: 300, max: 5000 };
    let proxies = [];
    let radarConfigs = [];
    let dynamic = { enabled: false, provider: 'openrouter' };

    // instance 1..5 -> state
    const state = {};

    function st(inst) {
        if (!state[inst]) {
            state[inst] = {
                map: null, pulse: null, center: null,
                names: [], selected: new Set(), proxyId: null, loop: false,
                running: false, stopFlag: true, threads: {}, foundTotal: 0, flashTimer: null
            };
        }
        return state[inst];
    }

    function colorFor(name) {
        const lower = (name || '').toLowerCase();
        if (lower.includes('er')) return '#dc2626';
        if (lower.includes('et')) return '#f59e0b';
        let h = 0;
        for (const ch of lower) h = (h * 31 + ch.charCodeAt(0)) >>> 0;
        return PALETTE[h % PALETTE.length];
    }

    function proxyById(id) { return proxies.find(p => p.id === Number(id)); }

    function getCenter(inst) {
        const s = st(inst);
        if (s.proxyId) {
            const p = proxyById(s.proxyId);
            if (p) return [p.lat, p.lon];
        }
        return window.APP_CENTER || [33.4735, -82.0105];
    }

    function centerLabel(inst) {
        const s = st(inst);
        if (s.proxyId) {
            const p = proxyById(s.proxyId);
            if (p) return p.name;
        }
        return 'Search location';
    }

    // ---------- names popover ----------
    function parseNames(inst) {
        const s = st(inst);
        const raw = document.getElementById(`names-${inst}`).value;
        s.names = raw.split(',').map(x => x.trim().toLowerCase()).filter(Boolean);
        s.names.forEach(n => { if (!s.selected.has(n) && s.selected.size === 0) s.selected.add(n); });
        // keep only existing selections; default-select all when nothing selected
        if (s.selected.size === 0) s.names.forEach(n => s.selected.add(n));
    }

    function renderNamesPop(inst) {
        const s = st(inst);
        parseNames(inst);
        const pop = document.getElementById(`namespop-${inst}`);
        pop.innerHTML = s.names.map(n =>
            `<label><input type="checkbox" ${s.selected.has(n) ? 'checked' : ''} onchange="Dashboard.toggleName(${inst},'${n.replace(/'/g, "\\'")}')"/>
             <span class="dot" style="background:${colorFor(n)}"></span>${n}</label>`).join('') ||
            '<div class="muted small" style="padding:6px 8px">Type comma-separated names</div>';
    }

    function toggleNamesPop(inst) {
        const pop = document.getElementById(`namespop-${inst}`);
        if (pop.style.display === 'none') { renderNamesPop(inst); pop.style.display = ''; }
        else pop.style.display = 'none';
    }

    function toggleName(inst, name) {
        const s = st(inst);
        if (s.selected.has(name)) s.selected.delete(name); else s.selected.add(name);
        renderNamesPop(inst);
    }

    function onNamesInput(inst) {
        const s = st(inst);
        parseNames(inst);
        // drop selections for removed names
        s.selected.forEach(n => { if (!s.names.includes(n)) s.selected.delete(n); });
    }

    // ---------- proxy select ----------
    function renderProxySelect(inst) {
        const s = st(inst);
        const sel = document.getElementById(`proxy-${inst}`);
        sel.innerHTML = '<option value="">No proxy (use search location)</option>' +
            proxies.map(p => `<option value="${p.id}" ${s.proxyId === p.id ? 'selected' : ''}>${p.name}</option>`).join('');
    }

    function onProxy(inst) {
        const s = st(inst);
        const val = document.getElementById(`proxy-${inst}`).value;
        s.proxyId = val ? Number(val) : null;
        const center = getCenter(inst);
        s.map.setView(center, 13);
        s.pulse.setLatLng(center);
        document.getElementById(`pill-${inst}`).textContent = centerLabel(inst);
        s.center && s.center.remove();
        s.center = L.marker(center).addTo(s.map);
    }

    function onLoop(inst) {
        st(inst).loop = document.getElementById(`loop-${inst}`).checked;
    }

    function toggleCollapse(inst) {
        document.getElementById(`card-${inst}`).classList.toggle('collapsed');
        const s = st(inst);
        if (s.map) setTimeout(() => s.map.invalidateSize(), 50);
    }

    // ---------- status ----------
    function updateStatus(inst) {
        const s = st(inst);
        const statusEl = document.getElementById(`status-${inst}`);
        const foundEl = document.getElementById(`found-${inst}`);
        if (s.running) {
            statusEl.textContent = 'Running';
            statusEl.classList.add('running');
        } else {
            statusEl.textContent = 'Idle';
            statusEl.classList.remove('running');
        }
        const radiusMi = Math.round(maxRadiusReached(s) / 1609 * 10) / 10;
        if (s.foundTotal > 0) {
            foundEl.style.display = '';
            foundEl.textContent = `${s.foundTotal} found · ${radiusMi}mi`;
        } else {
            foundEl.style.display = 'none';
        }
        const btn = document.getElementById(`search-${inst}`);
        btn.innerHTML = s.running ? '&#9632; Stop' : '&#128269; Search';
        btn.classList.toggle('primary', !s.running);
        btn.classList.toggle('red', s.running);

        document.getElementById(`threads-${inst}`).innerHTML = Object.values(s.threads).map(t =>
            `<div class="thread-row"><span class="dot" style="background:${colorFor(t.name)}"></span>
             <b>${t.name}</b><span>iter ${t.iteration}</span><span>${t.radius}m</span>
             <span>${t.found} found</span><span>${t.status}</span></div>`).join('');
    }

    function maxRadiusReached(s) {
        return Math.max(settings.step, ...Object.values(s.threads).map(t => t.radius || 0));
    }

    function flash(inst, name) {
        const s = st(inst);
        const el = document.getElementById(`flash-${inst}`);
        el.textContent = name;
        el.style.display = '';
        clearTimeout(s.flashTimer);
        s.flashTimer = setTimeout(() => el.style.display = 'none', 1500);
    }

    // ---------- search ----------
    async function doSearch(inst, name, radius, iteration) {
        const s = st(inst);
        if (s.stopFlag) return;
        const t = s.threads[name] || (s.threads[name] = { name, radius: 0, iteration: 0, found: 0, status: '' });
        t.radius = radius; t.iteration = iteration; t.status = 'searching';
        s.pulse.setRadius(radius);
        updateStatus(inst);
        try {
            const center = getCenter(inst);
            const resp = await fetch('/api/geocode/search', {
                method: 'POST', headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ query: name, lat: center[0], lon: center[1], radius: radius })
            });
            const data = await resp.json();
            if (data.error) throw new Error(data.error);
            let fresh = 0;
            (data.results || []).forEach(r => {
                const key = r.position[0].toFixed(6) + ',' + r.position[1].toFixed(6);
                if (t.keys && t.keys.has(key)) return;
                (t.keys ||= new Set()).add(key);
                fresh++;
                const color = colorFor(name);
                L.marker(r.position, {
                    icon: L.divIcon({
                        html: `<div style="background:${color};color:#fff;border-radius:50%;width:22px;height:22px;display:flex;align-items:center;justify-content:center;border:2px solid #fff;font-size:11px">&#9679;</div>`,
                        className: '', iconSize: [22, 22], iconAnchor: [11, 11]
                    })
                }).addTo(s.map).bindPopup(`<b>${r.name}</b><br>${r.displayName}`);
            });
            t.found += fresh;
            s.foundTotal += fresh;
            t.status = fresh ? `${fresh} new` : 'none';
            if (fresh > 0) flash(inst, name);
            updateStatus(inst);
        } catch (err) {
            t.status = 'error';
            console.warn(err);
        }
    }

    function run(inst) {
        const s = st(inst);
        if (s.stopFlag) return;
        Array.from(s.selected).forEach(name => {
            const t = s.threads[name];
            if (t && t.running) return;
            s.threads[name] = { name, radius: 0, iteration: 0, found: 0, status: 'starting', running: false, keys: new Set() };
            (async () => {
                const th = s.threads[name];
                th.running = true;
                let radius = settings.step, iteration = 1;
                while (!s.stopFlag) {
                    await doSearch(inst, name, radius, iteration);
                    if (s.stopFlag) break;
                    await new Promise(r => setTimeout(r, settings.interval * 1000));
                    if (s.stopFlag) break;
                    radius += settings.step;
                    if (radius > settings.max) {
                        if (s.loop) { radius = settings.step; iteration++; }
                        else { th.status = 'max reached'; break; }
                    } else iteration++;
                }
                th.running = false;
                updateStatus(inst);
            })();
        });
    }

    function start(inst) {
        const s = st(inst);
        parseNames(inst);
        if (!s.selected.size) { alert('Enter at least one search name.'); return; }
        // clear old markers
        Object.values(s.threads).forEach(t => t.keys?.forEach && null);
        s.threads = {};
        s.foundTotal = 0;
        s.stopFlag = false;
        s.running = true;
        run(inst);
        updateStatus(inst);
    }

    function stop(inst) {
        const s = st(inst);
        s.stopFlag = true;
        s.running = false;
        Object.values(s.threads).forEach(t => { t.status = 'stopped'; t.running = false; });
        updateStatus(inst);
    }

    function toggleSearch(inst) {
        st(inst).running ? stop(inst) : start(inst);
    }

    function startAll() { for (let i = 1; i <= RADAR_COUNT; i++) start(i); }
    function stopAll() { for (let i = 1; i <= RADAR_COUNT; i++) stop(i); }

    // ---------- radar configs ----------
    function refreshConfigSelect(selectedId) {
        const sel = document.getElementById('configSelect');
        sel.innerHTML = '<option value="">Saved configs...</option>' +
            radarConfigs.map(c => `<option value="${c.id}" ${c.id === Number(selectedId) ? 'selected' : ''}>${c.name}</option>`).join('');
    }

    function currentInstances() {
        const out = [];
        for (let i = 1; i <= RADAR_COUNT; i++) {
            const s = st(i);
            parseNames(i);
            out.push({ names: Array.from(s.selected), proxyId: s.proxyId, loop: s.loop });
        }
        return out;
    }

    function applyInstances(instances) {
        instances.slice(0, RADAR_COUNT).forEach((cfg, i) => {
            const inst = i + 1, s = st(inst);
            const names = (cfg.names && cfg.names.length ? cfg.names : cfg.locationIds || []);
            document.getElementById(`names-${inst}`).value = names.join(', ');
            s.selected = new Set(names);
            s.proxyId = cfg.proxyId || null;
            s.loop = !!cfg.loop;
            document.getElementById(`loop-${inst}`).checked = s.loop;
            renderProxySelect(inst);
            onProxy(inst);
        });
    }

    async function saveConfig() {
        const name = document.getElementById('configName').value.trim();
        if (!name) { alert('Enter a config name first.'); return; }
        const resp = await fetch('/api/radarconfig', {
            method: 'POST', headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ id: 0, name, instancesJson: JSON.stringify(currentInstances()) })
        });
        const saved = await resp.json();
        if (saved.id) {
            const exists = radarConfigs.findIndex(c => c.id === saved.id);
            if (exists >= 0) radarConfigs[exists] = saved; else radarConfigs.unshift(saved);
            refreshConfigSelect(saved.id);
        } else alert(saved.error || 'Save failed');
    }

    async function loadConfig() {
        const id = document.getElementById('configSelect').value;
        if (!id) return;
        const cfg = radarConfigs.find(c => c.id === Number(id));
        if (!cfg) return;
        document.getElementById('configName').value = cfg.name;
        applyInstances(JSON.parse(cfg.instancesJson || '[]'));
    }

    async function deleteRadarConfig(id) {
        await fetch('/api/radarconfig/' + id, { method: 'DELETE' });
        radarConfigs = radarConfigs.filter(c => c.id !== id);
        refreshConfigSelect();
    }

    // ---------- init ----------
    function initMaps() {
        for (let inst = 1; inst <= RADAR_COUNT; inst++) {
            const s = st(inst);
            const center = getCenter(inst);
            s.map = L.map(`map-${inst}`, { zoomControl: false, attributionControl: false }).setView(center, 13);
            L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', { maxZoom: 19 }).addTo(s.map);
            s.pulse = L.circle(center, { radius: settings.step, color: '#2563eb', fillColor: '#2563eb', fillOpacity: 0.05, weight: 1 }).addTo(s.map);
            s.center = L.marker(center).addTo(s.map);
            renderProxySelect(inst);
            document.getElementById(`pill-${inst}`).textContent = centerLabel(inst);
            parseNames(inst);
            updateStatus(inst);
        }
    }

    function init() {
        const root = document.getElementById('dashboard');
        proxies = JSON.parse(root.dataset.proxies || '[]');
        radarConfigs = JSON.parse(root.dataset.radarConfigs || '[]');
        dynamic = JSON.parse(root.dataset.dynamic || '{}');
        settings = {
            interval: Number(root.dataset.interval) || 10,
            step: Number(root.dataset.step) || 300,
            max: Number(root.dataset.max) || 5000
        };
        initMaps();
        refreshConfigSelect();
        document.getElementById('saveConfigBtn').onclick = saveConfig;
        document.getElementById('configSelect').onchange = loadConfig;
        document.getElementById('startAllBtn').onclick = startAll;
        document.getElementById('stopAllBtn').onclick = stopAll;
        // close popovers on outside click
        document.addEventListener('click', e => {
            if (!e.target.closest('.names-wrap')) {
                document.querySelectorAll('.names-pop').forEach(p => p.style.display = 'none');
            }
        });
    }

    return {
        init, start, stop, startAll, stopAll, toggleSearch,
        onNamesInput, toggleNamesPop, toggleName, onProxy, onLoop, toggleCollapse,
        saveConfig, loadConfig, deleteRadarConfig
    };
})();
