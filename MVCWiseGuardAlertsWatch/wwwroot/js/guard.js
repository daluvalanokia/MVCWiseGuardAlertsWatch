// WiseGuard panel — Telugu speech-to-text with tracked word repeat counts,
// segment recording (upload -> /api/guard/transcribe) and a video player
// with pause/stop/loop + progress slider, matching the live app design.
window.Guard = (function () {
    // Telugu or Latin words. Built from char codes: this Chromium build
    // rejects \u escape ranges in both literals and string-built regexes.
    const TELUGU = String.fromCharCode(0x0C00) + '-' + String.fromCharCode(0x0C7F);
    const allowedWord = new RegExp('^[' + TELUGU + ']+|^[a-zA-Z]+$');
    let listening = false;
    let transcript = '';
    let interim = '';
    let trackedWords = [];
    let cumulativeCounts = {};
    let videos = [];
    let recognition = null;
    let audioCtx = null;
    let recorder = null;
    let segmentTimer = null;
    let ytPlayer = null;
    let sliderTimer = null;
    let paused = false;

    const els = id => document.getElementById(id);

    function persist() {
        fetch('/api/guard/state', {
            method: 'POST', headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ trackedWords, cumulativeCounts })
        });
    }

    function renderTranscript() {
        const box = els('transcript');
        box.innerHTML = transcript
            ? `<span>${transcript}</span>`
            : '<span class="placeholder">Your Telugu speech will appear here...</span>';
        box.scrollTop = box.scrollHeight;
    }

    function renderWords() {
        els('wordList').innerHTML = trackedWords.map(w =>
            `<span class="chip">${w}<button onclick="Guard.removeWord('${w}')">x</button></span>`).join('') ||
            '<span class="muted small">No tracked words yet.</span>';
    }

    function renderCounts() {
        els('wordCounts').innerHTML = trackedWords.map(w =>
            `<span class="count-chip">${w} <b>${cumulativeCounts[w] || 0}</b></span>`).join('') ||
            '<span class="muted small">Add words in the tracker config to count repeats.</span>';
    }

    function countWords(finalText) {
        const words = finalText.split(/\s+/).map(w => w.trim()).filter(w => w && allowedWord.test(w));
        words.forEach(w => { if (trackedWords.includes(w)) cumulativeCounts[w] = (cumulativeCounts[w] || 0) + 1; });
        if (words.length) { renderCounts(); persist(); }
    }

    // ---- Live speech recognition (browser, Telugu) ----
    function startRecognition() {
        const SR = window.SpeechRecognition || window.webkitSpeechRecognition;
        if (!SR) return false;
        recognition = new SR();
        recognition.continuous = true;
        recognition.interimResults = true;
        recognition.lang = 'te-IN';
        recognition.onresult = e => {
            interim = '';
            for (let i = e.resultIndex; i < e.results.length; i++) {
                const r = e.results[i];
                if (r.isFinal) {
                    const text = r[0].transcript.trim();
                    if (text) { transcript += text + ' '; countWords(text); }
                } else interim += r[0].transcript;
            }
            renderTranscript();
            els('interim').textContent = interim;
        };
        recognition.onerror = e => { els('micState').textContent = 'error: ' + e.error; };
        recognition.onend = () => { if (listening) { try { recognition.start(); } catch {} } };
        recognition.start();
        return true;
    }

    // ---- Segment recording + server transcription ----
    function startSegments() {
        navigator.mediaDevices?.getUserMedia({ audio: true }).then(stream => {
            audioCtx = new (window.AudioContext || window.webkitAudioContext)();
            recorder = new MediaRecorder(stream);
            recorder.ondataavailable = async e => {
                if (e.data.size === 0) return;
                const blob = new Blob([e.data], { type: 'audio/webm' });
                const form = new FormData();
                form.append('audio', blob, 'segment.webm');
                try {
                    const resp = await fetch('/api/guard/transcribe', { method: 'POST', body: form });
                    const data = await resp.json();
                    if (data.text) {
                        transcript += data.text + ' ';
                        renderTranscript();
                        countWords(data.text);
                    }
                } catch (err) { console.warn('transcribe failed', err); }
            };
            recorder.start();
            segmentTimer = setInterval(() => {
                if (recorder.state === 'recording') { recorder.stop(); recorder.start(); }
            }, 15000);
        }).catch(() => { /* mic denied; live recognition may still run */ });
    }

    function toggleListening() {
        listening = !listening;
        const mic = els('micBtn');
        mic.classList.toggle('listening', listening);
        els('micLabel').textContent = listening ? 'LISTENING...' : 'TAP TO START';
        els('micState').textContent = listening ? 'recording' : '';
        els('guard-status').textContent = listening ? 'Recording' : 'Idle';
        if (listening) {
            if (!startRecognition()) els('micState').textContent = 'speech recognition unavailable in this browser';
            startSegments();
        } else {
            recognition && recognition.stop();
            if (segmentTimer) clearInterval(segmentTimer);
            recorder && recorder.state === 'recording' && recorder.stop();
            audioCtx && audioCtx.close();
            audioCtx = null;
        }
    }

    // ---- Words ----
    function addWord() {
        const input = els('wordInput');
        input.value.split(',').map(w => w.trim().toLowerCase()).filter(Boolean)
            .forEach(w => { if (!trackedWords.includes(w)) trackedWords.push(w); });
        input.value = '';
        renderWords(); renderCounts(); persist();
    }

    function removeWord(w) {
        trackedWords = trackedWords.filter(x => x !== w);
        renderWords(); renderCounts(); persist();
    }

    function resetCounts() {
        Object.keys(cumulativeCounts).forEach(k => cumulativeCounts[k] = 0);
        renderCounts(); persist();
    }

    // ---- Videos ----
    function renderVideos() {
        els('videoList').innerHTML = videos.map(v =>
            `<div class="video-item ${v.selected ? 'selected' : ''}">
                <span>${v.name || v.url || 'Video'}</span>
                <span>
                    <button class="link-btn" onclick="Guard.selectVideo(${v.id})">play</button>
                    <button class="link-btn" onclick="Guard.deleteVideo(${v.id})">delete</button>
                </span>
            </div>`).join('');
    }

    function loadYtApi(cb) {
        if (window.YT && YT.Player) return cb();
        const tag = document.createElement('script');
        tag.id = 'yt-iframe-api';
        tag.src = 'https://www.youtube.com/iframe_api';
        document.head.appendChild(tag);
        window.onYouTubeIframeAPIReady = cb;
    }

    function playSelected() {
        const v = videos.find(x => x.selected) || videos[0];
        els('videoPlayer').innerHTML = '';
        if (!v) {
            els('videoPlayer').innerHTML = '<div style="position:absolute;inset:0;display:flex;align-items:center;justify-content:center;color:#6b7280;font-size:12px">No video added</div>';
            return;
        }
        if (v.youtubeId) {
            const host = document.createElement('div');
            host.id = 'yt-host';
            els('videoPlayer').appendChild(host);
            loadYtApi(() => {
                ytPlayer = new YT.Player('yt-host', {
                    videoId: v.youtubeId,
                    playerVars: { autoplay: 1, modestbranding: 1 },
                    events: {
                        onStateChange: e => {
                            if (e.data === YT.PlayerState.ENDED && els('videoLoop').checked) ytPlayer.playVideo();
                        }
                    }
                });
                startSlider();
            });
        } else {
            els('videoPlayer').innerHTML = `<video id="localVideo" controls autoplay onended="if(document.getElementById('videoLoop').checked)this.play()">
                <source src="${v.storagePath || v.url}"></video>`;
            startSlider();
        }
    }

    function startSlider() {
        if (sliderTimer) clearInterval(sliderTimer);
        sliderTimer = setInterval(() => {
            const slider = els('videoSlider');
            let cur = 0, dur = 100;
            if (ytPlayer && ytPlayer.getCurrentTime) {
                cur = ytPlayer.getCurrentTime() || 0;
                dur = ytPlayer.getDuration() || 100;
            } else {
                const vid = document.getElementById('localVideo');
                if (vid) { cur = vid.currentTime || 0; dur = vid.duration || 100; }
            }
            if (dur > 0 && document.activeElement !== slider) slider.value = (cur / dur) * 100;
        }, 500);
    }

    function onSeek(pct) {
        let dur = 0;
        if (ytPlayer && ytPlayer.getDuration) dur = ytPlayer.getDuration() || 0;
        else {
            const vid = document.getElementById('localVideo');
            if (vid) dur = vid.duration || 0;
        }
        if (dur > 0) {
            if (ytPlayer && ytPlayer.seekTo) ytPlayer.seekTo(dur * pct / 100, true);
            else { const vid = document.getElementById('localVideo'); if (vid) vid.currentTime = dur * pct / 100; }
        }
    }

    function togglePause() {
        if (ytPlayer && ytPlayer.getPlayerState) {
            const state = ytPlayer.getPlayerState();
            if (state === YT.PlayerState.PLAYING) { ytPlayer.pauseVideo(); paused = true; }
            else { ytPlayer.playVideo(); paused = false; }
        } else {
            const vid = document.getElementById('localVideo');
            if (vid) { vid.paused ? vid.play() : vid.pause(); }
        }
    }

    function stopVideo() {
        if (ytPlayer && ytPlayer.stopVideo) ytPlayer.stopVideo();
        const vid = document.getElementById('localVideo');
        if (vid) vid.pause();
    }

    function onLoopChange() { /* handled via checkbox reads */ }

    async function addVideo() {
        const url = els('videoUrl').value.trim();
        if (!url) return;
        const match = url.match(/(?:youtu\.be\/|youtube\.com\/(?:watch\?v=|embed\/|shorts\/))([\w-]{11})/);
        const resp = await fetch('/api/guard/videos', {
            method: 'POST', headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ name: match ? 'YouTube ' + match[1] : url, url: url })
        });
        const video = await resp.json();
        if (video.id) { videos.push(video); els('videoUrl').value = ''; renderVideos(); }
    }

    async function selectVideo(id) {
        await fetch(`/api/guard/videos/${id}/select`, { method: 'POST' });
        videos.forEach(v => v.selected = v.id === id);
        renderVideos(); playSelected();
    }

    async function deleteVideo(id) {
        await fetch(`/api/guard/videos/${id}`, { method: 'DELETE' });
        videos = videos.filter(v => v.id !== id);
        renderVideos(); playSelected();
    }

    function addVideoFromList() { renderVideos(); }

    function toggleConfig() {
        const el = els('wordConfig');
        el.style.display = el.style.display === 'none' ? '' : 'none';
    }

    function toggleVideoMax() {
        const card = document.getElementById('card-6');
        card.classList.toggle('video-max');
    }

    async function init() {
        const resp = await fetch('/api/guard/state');
        const data = await resp.json();
        trackedWords = data.trackedWords || [];
        cumulativeCounts = data.cumulativeCounts || {};
        videos = data.videos || [];
        renderWords(); renderCounts(); renderVideos(); playSelected(); renderTranscript();
    }

    return {
        init, toggleListening, addWord, removeWord, resetCounts,
        addVideo, selectVideo, deleteVideo, addVideoFromList,
        toggleConfig, toggleVideoMax, togglePause, stopVideo, onLoopChange, onSeek
    };
})();
