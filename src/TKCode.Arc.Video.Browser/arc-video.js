// TKCode.Arc.Video.Browser: one HTMLVideoElement per player. Events go back to .NET through the callback given to create();
// frames for the Avalonia canvas are copied into a JavaScript buffer when the element presents them, and handed to .NET by
// takeFrame() with one copy into the .NET buffer.
//
// Event kinds sent to the callback (kind, value):
//   time (seconds), duration (seconds, 0 when unknown or live), size (width * 65536 + height), buffered, waiting, playing,
//   pause, ended, error (MediaError code), seeked (seconds), frame (a new frame is ready while not playing), tainted (a
//   cross-origin video without CORS headers: its frames cannot be read).

const hasFrameCallback = typeof HTMLVideoElement !== 'undefined' && 'requestVideoFrameCallback' in HTMLVideoElement.prototype;
const hasOffscreen = typeof OffscreenCanvas !== 'undefined';

// A surface that has not asked for a frame for this long is gone or hidden: stop copying until it asks again.
const idleAfterMs = 500;

let probe = null;

function finite(value) {
    return Number.isFinite(value) ? value : 0;
}

function revoke(p) {
    if (p.ownedUrl) {
        URL.revokeObjectURL(p.ownedUrl);
        p.ownedUrl = null;
    }
}

function rangesOf(timeRanges) {
    const values = [];
    for (let i = 0; i < timeRanges.length; i++) {
        values.push(timeRanges.start(i), finite(timeRanges.end(i)));
    }
    return values;
}

function reportBuffered(p) {
    const values = rangesOf(p.video.buffered);
    const key = values.join(',');
    if (key !== p.bufferedKey) {
        p.bufferedKey = key;
        p.callback('buffered', values.length / 2);
    }
}

export function create(callback) {
    const video = document.createElement('video');
    video.playsInline = true;
    video.preload = 'auto';
    video.disablePictureInPicture = true;
    const p = {
        video, callback,
        ownedUrl: null, crossOrigin: null,
        // Frame copy state.
        path: 'gpu', canvas: null, context: null,
        buffer: null, frameWidth: 0, frameHeight: 0, frameTime: 0, sequence: 0, taken: 0, reportedCopyError: false,
        targetWidth: 0, targetHeight: 0, lastAsk: 0, stale: false, frameRequest: 0, animationRequest: 0, lastMediaTime: -1,
        native: false, wasPlaying: false, captionTrack: null, disposed: false, bufferedKey: '', settleLoad: null,
        stats: { copies: 0, copyMs: 0, takes: 0, takeMs: 0, skipped: 0 },
    };
    const size = () => callback('size', video.videoWidth * 65536 + video.videoHeight);
    video.addEventListener('timeupdate', () => callback('time', video.currentTime));
    video.addEventListener('durationchange', () => { callback('duration', finite(video.duration)); reportBuffered(p); });
    video.addEventListener('loadedmetadata', size);
    video.addEventListener('resize', size);
    video.addEventListener('progress', () => reportBuffered(p));
    video.addEventListener('waiting', () => callback('waiting', video.currentTime));
    video.addEventListener('playing', () => { callback('playing', video.currentTime); watchFrames(p); });
    video.addEventListener('play', () => watchFrames(p));
    // At the end the element pauses before it reports ended; only 'ended' is passed on then.
    video.addEventListener('pause', () => { if (!video.ended) { callback('pause', video.currentTime); } });
    video.addEventListener('ended', () => callback('ended', video.currentTime));
    video.addEventListener('error', () => callback('error', video.error ? video.error.code : 0));
    video.addEventListener('loadeddata', () => { if (video.paused) { p.stale = true; callback('frame', video.currentTime); } });
    video.addEventListener('seeked', () => {
        reportBuffered(p);
        callback('seeked', video.currentTime);
        if (video.paused) {
            p.stale = true;
            callback('frame', video.currentTime);
        }
    });
    return p;
}

// ---- Loading ----

// Resolves to the duration (>= 0) once the metadata is known, or to -code (MediaError code 1-4) when loading fails. A
// load replaced by another one or by dispose() resolves to -1 (aborted): neither fires 'loadedmetadata' or 'error', and
// the caller's await would otherwise never end.
function waitForMetadata(p) {
    const video = p.video;
    return new Promise(resolve => {
        const settle = value => {
            video.removeEventListener('loadedmetadata', done);
            video.removeEventListener('error', fail);
            if (p.settleLoad === settle) {
                p.settleLoad = null;
            }
            resolve(value);
        };
        const done = () => settle(finite(video.duration));
        const fail = () => settle(-(video.error ? video.error.code : 4));
        p.settleLoad = settle;
        video.addEventListener('loadedmetadata', done);
        video.addEventListener('error', fail);
    });
}

function abortLoad(p) {
    if (p.settleLoad) {
        p.settleLoad(-1);
    }
}

// owned: the URL is an object URL made by blobUrl(), revoked when the element loads something else or is disposed.
export function loadUrl(p, url, owned) {
    const video = p.video;
    abortLoad(p);
    video.pause();
    revoke(p);
    p.reportedCopyError = false;
    if (owned) {
        p.ownedUrl = url;
    }
    p.sequence = 0;
    p.taken = 0;
    p.stale = false;
    p.lastMediaTime = -1;
    p.bufferedKey = '';
    // Same-origin and blob media never need CORS; a cross-origin URL must send CORS headers for its frames to be readable.
    if (!owned && p.crossOrigin) {
        video.crossOrigin = p.crossOrigin;
    } else {
        video.removeAttribute('crossorigin');
    }
    const ready = waitForMetadata(p);
    video.src = url;
    video.load();
    return ready;
}

// The view is only valid during this call; slice() copies it.
export function blobUrl(view, mediaType) {
    return URL.createObjectURL(new Blob([view.slice()], { type: mediaType }));
}

export function setCrossOrigin(p, value) {
    p.crossOrigin = value || null;
}

export function canPlayType(mediaType) {
    probe ??= document.createElement('video');
    return probe.canPlayType(mediaType);
}

// ---- Playback ----

// Resolves true when playback started, false when the browser refused (autoplay policy without a user gesture, or the
// request was cut short by a pause or a new load).
export async function play(p) {
    try {
        await p.video.play();
        return true;
    } catch (error) {
        if (error && (error.name === 'NotAllowedError' || error.name === 'AbortError')) {
            return false;
        }
        throw error;
    }
}

export function pause(p) { p.video.pause(); }
export function isPaused(p) { return p.video.paused; }

export function seek(p, seconds, fast) {
    const video = p.video;
    if (fast && typeof video.fastSeek === 'function') {
        video.fastSeek(seconds);
    } else {
        video.currentTime = seconds;
    }
}

export function currentTime(p) { return p.video.currentTime; }
export function isSeekable(p) { return p.video.seekable.length > 0 && Number.isFinite(p.video.duration); }
export function buffered(p) { return rangesOf(p.video.buffered); }
export function setVolume(p, volume) { p.video.volume = volume; }
export function setMuted(p, muted) { p.video.muted = muted; }
export function setLoop(p, loop) { p.video.loop = loop; }
export function setRate(p, rate) { p.video.playbackRate = rate; p.video.defaultPlaybackRate = rate; }

// ---- Frames (Canvas presentation) ----

function surfaceActive(p) {
    return performance.now() - p.lastAsk < idleAfterMs;
}

// Watches for new frames while playing: requestVideoFrameCallback where the browser has it (it fires once per presented
// frame, also for an element outside the document), else a currentTime check on every animation frame.
function watchFrames(p) {
    if (p.disposed || p.native || p.video.paused) {
        return;
    }
    if (hasFrameCallback) {
        if (!p.frameRequest) {
            p.frameRequest = p.video.requestVideoFrameCallback((now, metadata) => {
                p.frameRequest = 0;
                onPresented(p, metadata.mediaTime);
                watchFrames(p);
            });
        }
    } else if (!p.animationRequest) {
        p.animationRequest = requestAnimationFrame(() => {
            p.animationRequest = 0;
            const time = p.video.currentTime;
            if (time !== p.lastMediaTime && p.video.readyState >= 2) {
                p.lastMediaTime = time;
                onPresented(p, time);
            }
            watchFrames(p);
        });
    }
}

function stopWatching(p) {
    if (p.frameRequest) {
        p.video.cancelVideoFrameCallback(p.frameRequest);
        p.frameRequest = 0;
    }
    if (p.animationRequest) {
        cancelAnimationFrame(p.animationRequest);
        p.animationRequest = 0;
    }
}

function onPresented(p, mediaTime) {
    if (p.native) {
        return;
    }
    if (!surfaceActive(p)) {
        // Nobody is looking; copy lazily when a surface asks again.
        p.stale = true;
        p.stats.skipped++;
        return;
    }
    produce(p, mediaTime);
}

// The size to copy at: the natural size fitted within the requested size (aspect kept, never enlarged).
function fitSize(p) {
    const w = p.video.videoWidth;
    const h = p.video.videoHeight;
    if (w <= 0 || h <= 0) {
        return null;
    }
    if (p.targetWidth <= 0 || p.targetHeight <= 0 || (p.targetWidth >= w && p.targetHeight >= h)) {
        return [w, h];
    }
    const scale = Math.min(p.targetWidth / w, p.targetHeight / h);
    return [Math.max(1, Math.round(w * scale)), Math.max(1, Math.round(h * scale))];
}

function makeCanvas(width, height) {
    return hasOffscreen ? new OffscreenCanvas(width, height) : Object.assign(document.createElement('canvas'), { width, height });
}

// Copies the element's current picture into p.buffer: RGBA, width * 4 bytes per row, scaled to fitSize(p).
//
// Path 'gpu' (the default) draws into an accelerated 2D canvas, so the browser scales on the GPU and reads back only the
// scaled pixels; 'cpu' (willReadFrequently) reads the whole picture back first and scales in software. Measured in headless
// Edge 156 on Apple M5 (NOTES.md): gpu 1.4-1.9 ms per frame at 1280x720 and 1.5-3.3 ms at 1920x1080, cpu 3.7-10 ms.
// VideoFrame.copyTo(RGBA) was no faster than the cpu path (Chromium reads the GPU frame back synchronously) and cannot
// scale, and a WebGL 2 pixel-pack-buffer read-back saved under 1 ms of main-thread time at 640x360 for a frame of
// latency and a shader-compile stall, so neither is used.
function produce(p, mediaTime) {
    const video = p.video;
    if (video.readyState < 2) {
        return;
    }
    const size = fitSize(p);
    if (!size) {
        return;
    }
    const [width, height] = size;
    const started = performance.now();
    try {
        if (!p.canvas || p.canvas.width !== width || p.canvas.height !== height) {
            p.canvas = makeCanvas(width, height);
            p.context = p.canvas.getContext('2d', { alpha: false, willReadFrequently: p.path === 'cpu' });
            p.context.imageSmoothingQuality = 'medium';
        }
        p.context.drawImage(video, 0, 0, width, height);
        // A Uint8Array view of the pixels (no copy): the .NET MemoryView's set() accepts only that type.
        const pixels = p.context.getImageData(0, 0, width, height).data;
        p.buffer = new Uint8Array(pixels.buffer, pixels.byteOffset, pixels.length);
    } catch (error) {
        // A cross-origin video without CORS headers taints the canvas (SecurityError): there is no picture to give.
        p.stale = false;
        if (!p.reportedCopyError) {
            p.reportedCopyError = true;
            console.warn('TKCode.Arc.Video.Browser: cannot read video frames: ' + error);
            // Without a picture the player would play sound over black; tell .NET so it can fail visibly.
            if (error && error.name === 'SecurityError') {
                p.callback('tainted', 0);
            }
        }
        return;
    }
    p.frameWidth = width;
    p.frameHeight = height;
    p.frameTime = mediaTime;
    p.sequence++;
    p.stale = false;
    p.stats.copies++;
    p.stats.copyMs += performance.now() - started;
}

// Called by the surface on every composition frame. Returns 0 when there is no new frame since the last takeFrame(),
// else the frame's width * 65536 + height. A paused element (after a load, a seek or a resize of the surface) is copied
// here, synchronously.
export function peekFrame(p, targetWidth, targetHeight) {
    if (p.disposed || p.native) {
        return 0;
    }
    p.lastAsk = performance.now();
    const sizeChanged = targetWidth !== p.targetWidth || targetHeight !== p.targetHeight;
    p.targetWidth = targetWidth;
    p.targetHeight = targetHeight;
    if (sizeChanged && p.sequence > 0) {
        const size = fitSize(p);
        if (size && (size[0] !== p.frameWidth || size[1] !== p.frameHeight)) {
            p.stale = true;
        }
    }
    if (p.stale) {
        produce(p, p.video.currentTime);
    }
    if (!p.video.paused) {
        watchFrames(p);
    }
    if (p.sequence === p.taken || !p.buffer) {
        return 0;
    }
    return p.frameWidth * 65536 + p.frameHeight;
}

// The surface showing this player changed (re-attached, moved to full screen): hand the current frame out again on the next
// peekFrame(), even when no new frame was decoded, so a paused video shows at once.
export function resendFrame(p) {
    if (p.disposed) {
        return;
    }
    p.taken = -1;
    if (!p.buffer) {
        p.stale = true;
    }
}

// Copies the frame peekFrame() described into view (width * height * 4 bytes, valid only during this call) and returns its
// media time in seconds.
export function takeFrame(p, view) {
    const started = performance.now();
    view.set(p.buffer);
    p.taken = p.sequence;
    p.stats.takes++;
    p.stats.takeMs += performance.now() - started;
    return p.frameTime;
}

// ---- Native presentation ----

// Puts the element itself on the page (inside the host element Avalonia's NativeControlHost provides) and stops copying frames.
export function attachNative(p) {
    p.native = true;
    stopWatching(p);
    const video = p.video;
    video.style.width = '100%';
    video.style.height = '100%';
    video.style.objectFit = 'contain';
    video.style.background = '#000';
    video.style.display = 'block';
    // Clicks and the wheel go to the Avalonia canvas under the element, so the player's own picture gestures keep working.
    // Avalonia reads pointer offsets from the event target, and the layer holding native elements is at the canvas origin.
    video.style.pointerEvents = 'none';
    // Moving the element to a new host (full screen) removed it from the document, which paused it; play on.
    if (p.wasPlaying && video.paused) {
        video.play().catch(() => { });
    }
    p.wasPlaying = false;
    return video;
}

export function detachNative(p) {
    p.native = false;
    const video = p.video;
    const playing = !video.paused;
    p.wasPlaying = playing;
    if (video.parentNode) {
        video.parentNode.removeChild(video);
    }
    // Removing a media element from the document pauses it (HTML spec); resume when it was playing.
    if (playing) {
        setTimeout(() => { if (!p.disposed && !p.native && video.paused) { video.play().catch(() => { }); } }, 0);
    }
    p.stale = true;
    watchFrames(p);
}

// ---- Captions in Native presentation ----

// Replaces the captions the element shows with cues given as parallel arrays (seconds, seconds, WebVTT cue text); empty
// arrays show none. Uses one text track created on first use: a TextTrack cannot be removed, only emptied and disabled.
export function setCaptions(p, starts, ends, texts) {
    const video = p.video;
    if (!p.captionTrack) {
        if (!starts.length || typeof video.addTextTrack !== 'function' || typeof VTTCue === 'undefined') {
            return;
        }
        p.captionTrack = video.addTextTrack('captions');
    }
    const track = p.captionTrack;
    // Cues can be read and removed only while the track is not disabled.
    track.mode = 'hidden';
    while (track.cues && track.cues.length > 0) {
        track.removeCue(track.cues[0]);
    }
    for (let i = 0; i < starts.length; i++) {
        track.addCue(new VTTCue(starts[i], ends[i], texts[i]));
    }
    track.mode = starts.length > 0 ? 'showing' : 'disabled';
}

// ---- Diagnostics and tests ----

// 'gpu' (default) or 'cpu'; for measurements.
export function setCopyPath(p, path) {
    p.path = path === 'cpu' ? 'cpu' : 'gpu';
    p.canvas = null;
    p.context = null;
}

export function stats(p) {
    return JSON.stringify(p.stats);
}

export function dispose(p) {
    p.disposed = true;
    abortLoad(p);
    stopWatching(p);
    const video = p.video;
    video.pause();
    if (video.parentNode) {
        video.parentNode.removeChild(video);
    }
    video.removeAttribute('src');
    video.load();
    revoke(p);
    p.buffer = null;
    p.canvas = null;
    p.context = null;
}

// ---- Page full screen (VideoPlayer FullScreenMode.System) ----

let fullscreenWatched = false;

// Calls onExit when the page leaves full screen (Escape, the browser's own control).
export function watchFullscreen(onExit) {
    if (fullscreenWatched) {
        return;
    }
    fullscreenWatched = true;
    document.addEventListener('fullscreenchange', () => {
        if (!document.fullscreenElement) {
            onExit();
        }
    });
}

// The browser refuses without a user gesture; the player then stays full screen inside the page only.
export function enterFullscreen() {
    const root = document.documentElement;
    if (document.fullscreenElement || typeof root.requestFullscreen !== 'function') {
        return;
    }
    root.requestFullscreen().catch(error => console.info('TKCode.Arc.Video.Browser: full screen refused: ' + error));
}

export function exitFullscreen() {
    if (document.fullscreenElement && typeof document.exitFullscreen === 'function') {
        document.exitFullscreen().catch(() => { });
    }
}
