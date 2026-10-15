// Settings page helpers: proxy locations, guard words/videos, dynamic data models,
// custom-location geocoding. Mirrors the React settings components.
window.SettingsProxy = (function () {
    async function geocode(query) {
        const resp = await fetch('/api/geocode/citystate', {
            method: 'POST', headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ query: query })
        });
        return await resp.json();
    }

    async function geocodeAdd() {
        const query = document.getElementById('proxyQuery').value.trim();
        if (query.length < 2) return;
        const data = await geocode(query);
        const first = (data.results || [])[0];
        if (!first) { alert('No results.'); return; }
        const name = document.getElementById('proxyName').value.trim() || query;
        const resp = await fetch('/Settings/AddProxyLocation', {
            method: 'POST', headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ name: name, displayName: first.label, lat: first.position[0], lon: first.position[1] })
        });
        if (resp.ok) location.reload();
    }

    async function geocodeCustom() {
        const query = document.getElementById('customQuery').value.trim();
        if (query.length < 2) return;
        const data = await geocode(query);
        const first = (data.results || [])[0];
        if (!first) { alert('No results.'); return; }
        document.querySelector('input[name="CustomLat"]').value = first.position[0].toFixed(6);
        document.querySelector('input[name="CustomLon"]').value = first.position[1].toFixed(6);
        if (!document.querySelector('input[name="CustomLocationLabel"]').value)
            document.querySelector('input[name="CustomLocationLabel"]').value = first.label.split(',')[0];
    }

    async function testModels(provider) {
        const sel = document.getElementById('orModels');
        sel.innerHTML = '<option>loading...</option>';
        const resp = await fetch('/api/dynamicdata/models', {
            method: 'POST', headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ provider: provider })
        });
        const data = await resp.json();
        if (data.error) { sel.innerHTML = `<option>${data.error}</option>`; return; }
        sel.innerHTML = (data.models || []).map(m => `<option>${m}</option>`).join('');
    }

    async function deleteRadarConfig(id) {
        if (!confirm('Delete this radar config?')) return;
        await fetch('/api/radarconfig/' + id, { method: 'DELETE' });
        location.reload();
    }

    return { geocodeAdd, geocodeCustom, testModels, deleteRadarConfig };
})();

window.SettingsGuard = (function () {
    let words = [];

    function render() {
        const el = document.getElementById('gWordList');
        if (el) el.innerHTML = words.map(w => `<span class="chip">${w}</span>`).join('') ||
            '<span class="muted small">No tracked words.</span>';
    }

    async function loadVideos() {
        const resp = await fetch('/api/guard/state');
        const data = await resp.json();
        const tbody = document.querySelector('#gVideos tbody');
        if (tbody) tbody.innerHTML = (data.videos || []).map(v =>
            `<tr><td>${v.name}</td><td>${v.url || v.storagePath || ''}</td>
             <td><button class="btn danger small" onclick="SettingsProxy.deleteGuardVideo(${v.id})">Delete</button></td></tr>`).join('');
    }

    async function add() {
        const input = document.getElementById('gWordInput');
        input.value.split(',').map(w => w.trim().toLowerCase()).filter(Boolean).forEach(w => words.push(w));
        input.value = '';
        render();
        const resp = await fetch('/api/guard/state');
        const data = await resp.json();
        await fetch('/api/guard/state', {
            method: 'POST', headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ trackedWords: words, cumulativeCounts: data.cumulativeCounts || {} })
        });
    }

    async function deleteGuardVideo(id) {
        await fetch(`/api/guard/videos/${id}`, { method: 'DELETE' });
        loadVideos();
    }

    function init(initialWords) {
        words = initialWords || [];
        render();
        loadVideos();
    }

    return { init, add, deleteGuardVideo };
})();
