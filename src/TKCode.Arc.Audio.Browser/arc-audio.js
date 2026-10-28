// TKCode.Arc.Audio.Browser: one HTMLAudioElement per player. Events go back to .NET through the callback given to create().
const sources = new WeakMap();

function revoke(audio) {
    const url = sources.get(audio);
    if (url) {
        URL.revokeObjectURL(url);
        sources.delete(audio);
    }
}

function finite(value) {
    return Number.isFinite(value) ? value : 0;
}

export function create(callback) {
    const audio = new Audio();
    audio.preload = 'auto';
    audio.addEventListener('timeupdate', () => callback('time', audio.currentTime));
    audio.addEventListener('seeked', () => callback('time', audio.currentTime));
    audio.addEventListener('durationchange', () => callback('duration', finite(audio.duration)));
    audio.addEventListener('playing', () => callback('playing', audio.currentTime));
    audio.addEventListener('pause', () => callback('pause', audio.currentTime));
    audio.addEventListener('ended', () => callback('ended', audio.currentTime));
    audio.addEventListener('error', () => callback('error', audio.error ? audio.error.code : 0));
    return audio;
}

function waitForMetadata(audio) {
    return new Promise((resolve, reject) => {
        const done = () => { cleanup(); resolve(finite(audio.duration)); };
        const fail = () => { cleanup(); reject(new Error(audio.error ? audio.error.message || `media error ${audio.error.code}` : 'media error')); };
        const cleanup = () => { audio.removeEventListener('loadedmetadata', done); audio.removeEventListener('error', fail); };
        audio.addEventListener('loadedmetadata', done);
        audio.addEventListener('error', fail);
    });
}

// owned: the URL is an object URL made by blobUrl(), revoked when the element loads something else or is disposed.
export function loadUrl(audio, url, owned) {
    audio.pause();
    revoke(audio);
    if (owned) {
        sources.set(audio, url);
    }
    const ready = waitForMetadata(audio);
    audio.src = url;
    audio.load();
    return ready;
}

// The view is only valid during this call; slice() copies it.
export function blobUrl(view, mediaType) {
    return URL.createObjectURL(new Blob([view.slice()], { type: mediaType }));
}

// Resolves true when playback started, false when the browser refused (autoplay policy without a user gesture).
export async function play(audio) {
    try {
        await audio.play();
        return true;
    } catch (error) {
        if (error && error.name === 'NotAllowedError') {
            return false;
        }
        if (error && error.name === 'AbortError') {
            return false;
        }
        throw error;
    }
}

export function pause(audio) { audio.pause(); }
export function seek(audio, seconds) { audio.currentTime = seconds; }
export function currentTime(audio) { return audio.currentTime; }
export function setVolume(audio, volume) { audio.volume = volume; }
export function setMuted(audio, muted) { audio.muted = muted; }
export function setLoop(audio, loop) { audio.loop = loop; }

export function dispose(audio) {
    audio.pause();
    audio.removeAttribute('src');
    audio.load();
    revoke(audio);
}
