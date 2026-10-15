// WiseGuard panel — Telugu speech-to-text with tracked word repeat counts,
// segment recording (WAV upload -> /api/guard/transcribe) and a video player.
// Mirrors the React GuardPanel / WordConfigPanel / VideoPlayer components.
window.Guard = (function () {
    const allowedWord = /^[\u0C00-\uC7F]+|^[a-zA-Z]+$/; // Telugu or Latin words
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

    const els = {
        transcript: () => document.getElementById('transcript'),
        interim: () => document.getElementById('interim'),
        micBtn: () => document.getElementById('micBtn'),
        micState: () => document.getElementById('micState'),
        decibels: () => document.getElementById('decibels'),
        wordList: () => document.getElementById('wordList'),
        wordCounts: () => document.getElementById('wordCounts'),
        wordInput: () => document.getElementById('wordInput'),
        videoList: () => document.getElementById('videoList'),
        videoPlayer: () => document.getElementById('videoPlayer'),
        videoUrl: () => document.getElementById('videoUrl'),
        videoLoop: () => document.getElementById('videoLoop')
    };

    function persist() {
        fetch('/api/guard/state', {
            method: 'POST', headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ trackedWords, cumulativeCounts })
        });
    }

    function renderWords() {
        els.wordList().innerHTML = trackedWords.map(w =>
            `<span class="chip">${w}<button onclick="Guard.removeWord('${w}')">x</button></span>`).join('') ||
            '<span class="muted small">No tracked words yet.</span>';
    }

    function renderCounts() {
        els.wordCounts().innerHTML = trackedWords.map(w => {
            const n = cumulativeCounts[w] || 0;
            return `<span class="count-chip">${w}<b>${n}</b></span>`;
        }).join('') || '<span class="muted small">Add words to count repeats.</span>';
    }

    function countWords(finalText) {
        const words = finalText.split(/\s+/).map(w => w.trim()).filter(w => w && allowedWord.test(w));
        words.forEach(w => {
            if (trackedWords.includes(w)) cumulativeCounts[w] = (cumulativeCounts[w] || 0) + 1;
        });
        if (words.length) { renderCounts(); persist(); }
        return words;
    }

    // ---- Live speech recognition (browser) ----
    function startRecognition() {
        const SR = window.SpeechRecognition || window.webkitSpeechRecognition;
        if (!SR) return false;
        recognition = new SR();
        recognition.continuous = true;
        recognition.interimResults = true;
        recognition.lang = 'te-IN'; // Telugu, as in the WiseGuard app

        recognition.onresult = (e) => {
            interim = '';
            for (let i = e.resultIndex; i < e.results.length; i++) {
                const r = e.results[i];
                if (r.isFinal) {
                    const text = r[0].transcript.trim();
                    if (text) { transcript += text + ' '; countWords(text); }
                } else {
                    interim += r[0].transcript;
                }
            }
            els.transcript().textContent = transcript;
            els.interim().textContent = interim;
        };
        recognition.onerror = (e) => { els.micState().textContent = 'error: ' + e.error; };
        recognition.onend = () => { if (listening) { try { recognition.start(); } catch {} } };
        recognition.start();
        return true;
    }

    // ---- Segment recording + server transcription (mirrors guardTranscribe) ----
    function startSegments() {
        navigator.mediaDevices?.getUserMedia({ audio: true }).then(stream => {
            audioCtx = new (window.AudioContext || window.webkitAudioContext)();
            recorder = new MediaRecorder(stream);
            recorder.ondataavailable = async (e) => {
                if (e.data.size === 0) return;
                const blob = new Blob([e.data], { type: 'audio/webm' });
                const form = new FormData();
                form.append('audio', blob, 'segment.webm');
                try {
                    const resp = await fetch('/api/guard/transcribe', { method: 'POST', body: form });
                    const data = await resp.json();
                    if (data.text) {
                        transcript += data.text + ' ';
                        els.transcript().textContent = transcript;
                        countWords(data.text);
                    }
                } catch (err) {
                    // Live recognition may still be active; surface quietly.
                    console.warn('transcribe failed', err);
                }
            };
            recorder.start();
            segmentTimer = setInterval(() => {
                if (recorder.state === 'recording') {
                    recorder.stop();
                    recorder.start();
                }
            }, 15000); // 15s segments
        }).catch(() => { /* mic denied; live recognition may still run */ });
    }

    function toggleListening() {
        listening = !listening;
        els.micBtn().textContent = listening ? 'Stop Listening' : 'Start Listening';
        els.micState().textContent = listening ? 'listening...' : 'idle';
        if (listening) {
            startRecognition();
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
        const input = els.wordInput();
        input.value.split(',').map(w => w.trim().toLowerCase()).filter(Boolean)
            .forEach(w => { if (!trackedWords.includes(w)) trackedWords.push(w); });
        input.value = '';
        renderWords(); renderCounts(); persist();
    }

    function removeWord(w) {
        trackedWords = trackedWords.filter(x => x !== w);
        renderWords(); renderCounts(); persist();
    }

    // ---- Videos (YouTube iframe API with loop) ----
    let ytPlayer = null;
    function renderVideos() {
        els.videoList().innerHTML = videos.map(v =>
            `<div class="video-item ${v.selected ? 'selected' : ''}">
                <span>${v.name || v.url || v.storagePath || 'Video'}</span>
                <span>
                    <button class="link-btn" onclick="Guard.selectVideo(${v.id})">play</button>
                    <button class="link-btn" onclick="Guard.deleteVideo(${v.id})">delete</button>
                </span>
            </div>`).join('');
    }

    function playSelected() {
        const v = videos.find(x => x.selected) || videos[0];
        els.videoPlayer().innerHTML = '';
        if (!v) return;
        if (v.youtubeId) {
            const id = 'yt-host';
            const div = document.createElement('div');
            div.id = id;
            els.videoPlayer().appendChild(div);
            if (window.YT && YT.Player) {
                ytPlayer = new YT.Player(id, {
                    videoId: v.youtubeId,
                    playerVars: { autoplay: 1 },
                    events: { onStateChange: e => { if (e.data === YT.PlayerState.ENDED && els.videoLoop().checked) ytPlayer.playVideo(); } }
                });
            } else {
                // Fallback iframe if the API script is unavailable
                els.videoPlayer().innerHTML =
                    `<iframe src="https://www.youtube.com/embed/${v.youtubeId}?autoplay=1&loop=1&playlist=${v.youtubeId}" allowfullscreen allow="autoplay; encrypted-media"></iframe>`;
            }
        } else if (v.storagePath || v.url) {
            const src = v.storagePath || v.url;
            els.videoPlayer().innerHTML = `<video controls autoplay onended="this.play()"><source src="${src}"></video>`;
        }
    }

    async function addVideo() {
        const url = els.videoUrl().value.trim();
        if (!url) return;
        const match = url.match(/(?:youtu\.be\/|youtube\.com\/(?:watch\?v=|embed\/|shorts\/))([\w-]{11})/);
        const body = { name: match ? 'YouTube ' + match[1] : url, url: url };
        const resp = await fetch('/api/guard/videos', {
            method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body)
        });
        const video = await resp.json();
        if (video.id) { videos.push(video); els.videoUrl().value = ''; renderVideos(); }
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

    function toggleConfig() {
        const el = document.getElementById('wordConfig');
        el.style.display = el.style.display === 'none' ? '' : 'none';
    }

    function toggleVideoMax() {
        const card = document.querySelector('.video-card');
        card.classList.toggle('maximized');
        card.style.gridColumn = card.classList.contains('maximized') ? '1 / -1' : '';
    }

    async function init() {
        els.micBtn().onclick = toggleListening;
        els.wordInput() && (els.wordInput().onkeydown = e => { if (e.key === 'Enter') addWord(); });

        const resp = await fetch('/api/guard/state');
        const data = await resp.json();
        trackedWords = data.trackedWords || [];
        cumulativeCounts = data.cumulativeCounts || {};
        videos = data.videos || [];
        renderWords(); renderCounts(); renderVideos(); playSelected();
    }

    return { init, addWord, removeWord, addVideo, selectVideo, deleteVideo, toggleConfig, toggleVideoMax };
})();
