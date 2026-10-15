// Dashboard — multi-instance radar panels with expanding-radius multi-name search.
// Mirrors the React Home/RadarPanel/map components of WiseGuardAlertsWatch.
window.Dashboard = (function () {
    const COLORS = {
        blue: '#3b82f6', green: '#22c55e', red: '#ef4444', amber: '#f59e0b',
        purple: '#a855f7', pink: '#ec4899', teal: '#14b8a6', orange: '#f97316'
    };
    const PANEL_COUNT = 6;
    const RADAR_COUNT = 5;

    let settings = { interval: 10, step: 300, max: 5000 };
    let locations = [];
    let proxies = [];
    let radarConfigs = [];
    let dynamic = { enabled: false, provider: 'openrouter' };

    // instance index (1..5) -> state
    const state = {};

    function colorFor(name, fallback) {
        // Priority color logic from the React app: "er" -> red, "et" -> yellow
        const lower = (name || '').toLowerCase();
        if (lower.includes('er')) return COLORS.red;
        if (lower.includes('et')) return COLORS.amber;
        return fallback || COLORS.blue;
    }

    function getOrCreate(inst) {
        if (!state[inst]) {
            state[inst] = {
                map: null,
                centerMarker: null,
                pulse: null,
                threads: {},          // locationId -> thread
                locationIds: [],
                proxyId: null,
                loop: false,
                stopFlag: true,
                flashTimer: null
            };
        }
        return state[inst];
    }

    function getLocation(id) { return locations.find(l => l.id === id); }

    function getCenter(inst) {
        const st = getOrCreate(inst);
        if (st.proxyId) {
            const p = proxies.find(x => x.id === st.proxyId);
            if (p) return [p.lat, p.lon];
        }
        // Default center: first proxy or a configured custom location
        const custom = window.APP_CENTER;
        if (custom) return custom;
        return [33.4735, -82.0105]; // Augusta, GA default
    }

    // Found results are offset to the proxy position (React ProxyLocation behavior)
    function displayPosition(inst, originalPos) {
        const st = getOrCreate(inst);
        const originalCenter = window.APP_ORIGINAL_CENTER || getCenter(inst);
        if (!st.proxyId) return originalPos;
        const p = proxies.find(x => x.id === st.proxyId);
        if (!p) return originalPos;
        return [p.lat + (originalPos[0] - originalCenter[0]), p.lon + (originalPos[1] - originalCenter[1])];
    }

    function iconHtml(color, icon) {
        const glyphs = {
            'map-pin': '&#128205;', landmark: '&#127963;', pill: '&#128138;', utensils: '&#127869;',
            car: '&#128663;', home: '&#127968;', 'shopping-bag': '&#128717;', coffee: '&#9749;',
            fuel: '&#9981;', star: '&#11088;'
        };
        return `<div style="background:${color};color:#fff;border-radius:50%;width:26px;height:26px;display:flex;align-items:center;justify-content:center;border:2px solid #fff;font-size:13px">${glyphs[icon] || '&#128205;'}</div>`;
    }

    function renderNameMultiSelect(inst) {
        const st = getOrCreate(inst);
        const box = document.getElementById(`names-${inst}`);
        box.innerHTML = '';
        locations.filter(l => l.enabled).forEach(l => {
            const label = document.createElement('label');
            const cb = document.createElement('input');
            cb.type = 'checkbox';
            cb.value = l.id;
            cb.checked = st.locationIds.includes(l.id);
            cb.onchange = () => {
                st.locationIds = Array.from(box.querySelectorAll('input:checked')).map(i => Number(i.value));
                updateConfigSummary(inst);
            };
            label.appendChild(cb);
            label.appendChild(Object.assign(document.createElement('span'), {
                innerHTML: `<span class="dot" style="background:${colorFor(l.name, COLORS[l.color])};margin-right:6px"></span>${l.name}`
            }));
            box.appendChild(label);
        });
        if (!locations.length) box.innerHTML = '<div class="muted small" style="padding:6px 10px">No search names. Add some in Settings.</div>';
    }

    function renderProxySelect(inst) {
        const st = getOrCreate(inst);
        const sel = document.getElementById(`proxy-${inst}`);
        sel.innerHTML = '<option value="">None</option>' +
            proxies.map(p => `<option value="${p.id}" ${st.proxyId === p.id ? 'selected' : ''}>${p.name}</option>`).join('');
        sel.onchange = () => { st.proxyId = sel.value ? Number(sel.value) : null; onProxyChange(inst); };
    }

    function updateConfigSummary(inst) {
        const st = getOrCreate(inst);
        const el = document.getElementById(`cfg-${inst}`);
        const names = st.locationIds.map(id => getLocation(id)?.name).filter(Boolean).join(', ');
        el.textContent = `${st.locationIds.length} name(s)${names ? ': ' + names : ''} · step ${settings.step}m · max ${settings.max}m`;
    }

    function onProxyChange(inst) {
        const st = getOrCreate(inst);
        const center = getCenter(inst);
        st.map && st.map.setView(center, 13);
        if (st.centerMarker) {
            st.centerMarker.setLatLng(center);
            st.pulse && st.pulse.setLatLng(center);
        }
    }

    function initMap(inst) {
        const st = getOrCreate(inst);
        st.map = L.map(`map-${inst}`, { zoomControl: true }).setView(getCenter(inst), 13);
        L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
            maxZoom: 19, attribution: '&copy; OpenStreetMap'
        }).addTo(st.map);
        const center = getCenter(inst);
        st.pulse = L.circle(center, {
            radius: settings.step, color: '#3b82f6', fillColor: '#3b82f6', fillOpacity: 0.06, weight: 1
        }).addTo(st.map);
        st.centerMarker = L.marker(center).addTo(st.map);
        renderNameMultiSelect(inst);
        renderProxySelect(inst);
        document.getElementById(`loop-${inst}`).onchange = e => { st.loop = e.target.checked; };
        updateConfigSummary(inst);
    }

    function flashName(inst, name) {
        const st = getOrCreate(inst);
        const el = document.getElementById(`flash-${inst}`);
        el.textContent = name;
        el.style.display = 'block';
        clearTimeout(st.flashTimer);
        st.flashTimer = setTimeout(() => { el.style.display = 'none'; }, 1600);
    }

    function updateStatus(inst) {
        const st = getOrCreate(inst);
        const el = document.getElementById(`status-${inst}`);
        const rows = Object.values(st.threads).map(t => {
            const color = colorFor(t.name, COLORS[t.color]);
            return `<div class="thread">
                <span class="dot" style="background:${color}"></span>
                <b style="min-width:90px">${t.name}</b>
                <span class="muted">iter ${t.iteration}</span>
                <span class="muted">radius ${t.radius}m</span>
                <span>found ${t.found}</span>
                <span class="muted">${t.status}</span>
            </div>`;
        });
        el.innerHTML = rows.join('') || '<div class="muted small">Stopped. Select names and press Start.</div>';
    }

    async function doSearch(inst, locId, radius, iteration) {
        const st = getOrCreate(inst);
        const loc = getLocation(locId);
        if (!loc || st.stopFlag) return;
        const thread = st.threads[locId] || (st.threads[locId] = { name: loc.name, color: loc.color, radius: 0, iteration: 0, found: 0, status: '' });

        thread.radius = radius;
        thread.iteration = iteration;
        thread.status = `searching within ${radius}m...`;
        st.pulse.setRadius(radius);
        updateStatus(inst);

        try {
            const center = getCenter(inst);
            const resp = await fetch('/api/geocode/search', {
                method: 'POST', headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ query: loc.name, lat: center[0], lon: center[1], radius: radius })
            });
            const data = await resp.json();
            if (data.error) throw new Error(data.error);

            let fresh = 0;
            data.results.forEach(r => {
                const key = r.position[0].toFixed(6) + ',' + r.position[1].toFixed(6);
                if (thread.foundKeys && thread.foundKeys.has(key)) return;
                (thread.foundKeys ||= new Set()).add(key);
                fresh++;
                const disp = displayPosition(inst, r.position);
                const color = colorFor(loc.name, COLORS[loc.color]);
                L.marker(disp, { icon: L.divIcon({ html: iconHtml(color, loc.icon), className: '', iconSize: [26, 26], iconAnchor: [13, 13] }) })
                    .addTo(st.map)
                    .bindPopup(`<b>${r.name}</b><br>${r.displayName}<br><span class="muted">${loc.name} · ${Math.round(radius)}m</span>`);
            });
            thread.found += fresh;
            thread.status = fresh ? `${fresh} new found` : 'no new results';
            if (fresh > 0) flashName(inst, loc.name);
            updateStatus(inst);
        } catch (err) {
            thread.status = 'error: ' + err.message;
            updateStatus(inst);
        }
    }

    function runInstance(inst) {
        const st = getOrCreate(inst);
        if (st.stopFlag) return;
        st.locationIds.forEach(locId => {
            const thread = st.threads[locId] ||= {
                name: getLocation(locId).name, color: getLocation(locId).color,
                radius: 0, iteration: 0, found: 0, foundKeys: new Set(), status: 'starting...'
            };
            if (thread.running) return;
            thread.running = true;
            (async () => {
                let radius = settings.step, iteration = 1;
                while (!st.stopFlag) {
                    await doSearch(inst, locId, radius, iteration);
                    if (st.stopFlag) break;
                    await new Promise(r => setTimeout(r, settings.interval * 1000));
                    if (st.stopFlag) break;
                    radius += settings.step;
                    if (radius > settings.max) {
                        if (st.loop) { radius = settings.step; iteration++; }
                        else { thread.status = `max radius (${settings.max}m) reached`; updateStatus(inst); break; }
                    } else {
                        iteration++;
                    }
                }
                thread.running = false;
            })();
        });
    }

    function start(inst) {
        const st = getOrCreate(inst);
        if (!st.locationIds.length) { alert('Select at least one search name.'); return; }
        st.stopFlag = false;
        st.threads = {};
        Object.values(st.threads);
        runInstance(inst);
    }

    function stop(inst) {
        const st = getOrCreate(inst);
        st.stopFlag = true;
        Object.values(st.threads).forEach(t => { t.status = 'stopped'; t.running = false; });
        updateStatus(inst);
    }

    // ---------- Instance carousel ----------
    let activeInstance = 1;

    function showInstance(inst) {
        activeInstance = inst;
        for (let i = 1; i <= PANEL_COUNT; i++) {
            document.getElementById(`panel-${i}`).style.display = i === inst ? '' : 'none';
        }
        document.querySelectorAll('#instanceDots button').forEach((b, i) => {
            b.classList.toggle('active', i + 1 === inst);
        });
        document.getElementById('instanceLabel').textContent = inst === 6 ? 'WiseGuard' : `Instance ${inst}`;
        const st = getOrCreate(inst);
        if (st.map) setTimeout(() => st.map.invalidateSize(), 60);
        updateStatus(inst);
    }

    function renderDots() {
        const box = document.getElementById('instanceDots');
        box.innerHTML = '';
        for (let i = 1; i <= PANEL_COUNT; i++) {
            const b = document.createElement('button');
            b.title = i === 6 ? 'WiseGuard' : `Instance ${i}`;
            b.onclick = () => showInstance(i);
            box.appendChild(b);
        }
    }

    // ---------- Radar configs (Base44 RadarConfig entity) ----------
    function refreshConfigSelect() {
        const sel = document.getElementById('configSelect');
        sel.innerHTML = '<option value="">Config...</option>' +
            radarConfigs.map(c => `<option value="${c.id}">${c.name}</option>`).join('');
    }

    function currentSelection() {
        const instances = [];
        for (let i = 1; i <= RADAR_COUNT; i++) {
            const st = getOrCreate(i);
            instances.push({ locationIds: st.locationIds, proxyId: st.proxyId, loop: st.loop });
        }
        return instances;
    }

    function applySelection(instances) {
        instances.slice(0, RADAR_COUNT).forEach((inst, i) => {
            const st = getOrCreate(i + 1);
            st.locationIds = inst.locationIds || [];
            st.proxyId = inst.proxyId || null;
            st.loop = !!inst.loop;
            renderNameMultiSelect(i + 1);
            renderProxySelect(i + 1);
            const loopEl = document.getElementById(`loop-${i + 1}`);
            if (loopEl) loopEl.checked = st.loop;
            updateConfigSummary(i + 1);
        });
    }

    async function loadConfig() {
        const id = document.getElementById('configSelect').value;
        if (!id) return;
        const cfg = radarConfigs.find(c => c.id === Number(id));
        if (!cfg) return;
        applySelection(JSON.parse(cfg.instancesJson || '[]'));
    }

    async function saveConfig() {
        const name = prompt('Save config as:');
        if (!name) return;
        const resp = await fetch('/api/radarconfig', {
            method: 'POST', headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ id: 0, name: name, instancesJson: JSON.stringify(currentSelection()) })
        });
        const saved = await resp.json();
        if (saved.id) {
            radarConfigs.unshift(saved);
            refreshConfigSelect();
            document.getElementById('configSelect').value = saved.id;
        }
    }

    async function deleteRadarConfig(id) {
        if (!confirm('Delete this radar config?')) return;
        await fetch('/api/radarconfig/' + id, { method: 'DELETE' });
        radarConfigs = radarConfigs.filter(c => c.id !== id);
        refreshConfigSelect();
    }

    // ---------- Dynamic data panel (Base44 dynamicDataSearch) ----------
    function toggleDynamic(inst) {
        const body = document.getElementById(`dd-body-${inst}`);
        body.style.display = body.style.display === 'none' ? '' : 'none';
    }

    async function fetchDynamic(inst) {
        const el = document.getElementById(`dd-state-${inst}`);
        const out = document.getElementById(`dd-result-${inst}`);
        const prompt = document.getElementById(`dd-prompt-${inst}`).value.trim();
        if (!prompt) return;
        if (!dynamic.enabled) { out.textContent = 'Dynamic data is disabled in Settings.'; return; }
        el.textContent = 'loading...';
        const st = getOrCreate(inst);
        try {
            const resp = await fetch('/api/dynamicdata/search', {
                method: 'POST', headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    prompt: prompt,
                    context: {
                        location: st.proxyId ? proxies.find(p => p.id === st.proxyId)?.name : null,
                        names: st.locationIds.map(id => getLocation(id)?.name).filter(Boolean),
                        radiusMiles: Math.round(settings.max / 1609)
                    }
                })
            });
            const data = await resp.json();
            el.textContent = '';
            out.textContent = data.content || data.error || 'No response.';
        } catch (e) {
            el.textContent = '';
            out.textContent = 'Error: ' + e.message;
        }
    }

    function init() {
        const root = document.getElementById('dashboard');
        const d = JSON.parse(root.dataset.locations || '[]');
        // JSON from ASP.NET uses PascalCase by default for POCOs serialized via JsonSerializer
        locations = d.map(l => ({
            id: l.id ?? l.Id, name: l.name ?? l.Name, color: l.color ?? l.Color,
            icon: l.icon ?? l.Icon, enabled: l.enabled ?? l.Enabled ?? true
        }));
        proxies = (JSON.parse(root.dataset.proxies || '[]')).map(p => ({
            id: p.id ?? p.Id, name: p.name ?? p.Name, lat: p.lat ?? p.Lat, lon: p.lon ?? p.Lon
        }));
        radarConfigs = (JSON.parse(root.dataset.radarConfigs || '[]')).map(c => ({
            id: c.id ?? c.Id, name: c.name ?? c.Name, instancesJson: c.instancesJson ?? c.InstancesJson
        }));
        dynamic = JSON.parse(root.dataset.dynamic || '{}');
        settings = {
            interval: Number(root.dataset.interval) || 10,
            step: Number(root.dataset.step) || 300,
            max: Number(root.dataset.max) || 5000
        };

        renderDots();
        refreshConfigSelect();
        for (let i = 1; i <= RADAR_COUNT; i++) initMap(i);
        showInstance(1);

        document.getElementById('prevInstance').onclick = () =>
            showInstance(activeInstance === 1 ? PANEL_COUNT : activeInstance - 1);
        document.getElementById('nextInstance').onclick = () =>
            showInstance(activeInstance === PANEL_COUNT ? 1 : activeInstance + 1);
        document.getElementById('loadConfigBtn').onclick = loadConfig;
        document.getElementById('saveConfigBtn').onclick = saveConfig;
    }

    return { init, start, stop, toggleDynamic, fetchDynamic, deleteRadarConfig };
})();
