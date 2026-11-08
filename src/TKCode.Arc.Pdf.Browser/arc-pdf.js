// TKCode.Arc.Pdf.Browser: PDF documents through pdf.js (design/components/pdf/pdf.md section 5.9). pdf.js parses in its own worker and
// paints on the main thread, in slices between animation frames. This module is the only thing that talks to it; everything is
// handed to .NET as flat arrays, and what can be worked out without a browser (coordinates, splitting text, trees) is done there.
//
// A PDF is untrusted input. Scripting stays off: no enableScripting, no sandbox, and the files of the sandbox are not even in the
// package. A password is passed on to pdf.js once and not kept here, and neither passwords nor pdf.js messages are logged.

let pdfjs = null;
let options = null;

// The pages painted most recently: their operator lists stay in memory for the next zoom level, the others are freed.
const paintedPagesKept = 16;

/** Imports pdf.js. The module runs from a data: URL, so asset addresses are resolved against the page, which works under a sub-path. */
export async function init(assetsUrl) {
    const base = new URL(assetsUrl, document.baseURI);
    if (!base.pathname.endsWith('/')) {
        base.pathname += '/';
    }
    pdfjs = await import(new URL('pdf.min.mjs', base).href);
    pdfjs.GlobalWorkerOptions.workerSrc = new URL('pdf.worker.min.mjs', base).href;
    options = {
        cMapUrl: new URL('cmaps/', base).href,
        cMapPacked: true,
        standardFontDataUrl: new URL('standard_fonts/', base).href,
        wasmUrl: new URL('wasm/', base).href,
        iccUrl: new URL('iccs/', base).href,
        // Does nothing in this version, which has no code generation left; it stays so that an upgrade cannot quietly turn it on.
        isEvalSupported: false,
        enableXfa: false,
        verbosity: pdfjs.VerbosityLevel.ERRORS,
    };
}

export function normalizeCompatibility(text) {
    return text.normalize('NFKC');
}

function open(source, password) {
    const task = pdfjs.getDocument({ ...source, ...options });
    const d = {
        task, doc: null, closed: false, pageCount: 0,
        errorName: null, errorStatus: 0, errorMissing: false, errorDetail: null,
        settle: null, views: new Map(), pageIndexes: new Map(), painted: [],
    };
    const settled = new Promise(resolve => {
        d.settle = resolve;
    });
    const fail = (name, status, missing, detail) => {
        d.errorName = name;
        d.errorStatus = status;
        d.errorMissing = missing;
        d.errorDetail = detail;
        d.settle();
    };
    // Asked once for the password and again if it was wrong. Answering through this callback, and not through the parameters of
    // getDocument, means the only reference to the text here ends with the first answer.
    let secret = password;
    task.onPassword = update => {
        if (secret === null || secret === undefined) {
            fail('PasswordException', 0, false, null);
            task.destroy().catch(() => { });
            return;
        }
        const answer = secret;
        secret = null;
        update(answer);
    };
    // A document without a password never asks, and the task lives as long as the document.
    const forget = () => {
        secret = null;
        task.onPassword = null;
    };
    task.promise.then(doc => {
        forget();
        d.doc = doc;
        d.pageCount = doc.numPages;
        d.settle();
    }, error => {
        forget();
        fail(String(error?.name ?? 'Error'), Number(error?.status ?? 0), error?.missing === true, `${error?.message ?? ''} ${error?.details ?? ''}`);
    });
    d.opened = settled;
    return d;
}

export function openUrl(url, password) {
    return open({ url }, password);
}

export function openBytes(view, password) {
    // A copy of its own: the view is .NET's memory, valid only during this call, and pdf.js moves what it gets to the worker.
    return open({ data: view.slice() }, password);
}

/** Resolves when the document is open or has failed, never rejects: the handle says which. */
export function waitOpen(d) {
    return d.opened;
}

/** Stops whatever the document is doing, opening included. In-flight renders end as cancelled. */
export function close(d) {
    d.closed = true;
    d.painted.length = 0;
    d.views.clear();
    d.pageIndexes.clear();
    d.settle?.();
    d.task.destroy().catch(() => { });
}

export function numbers(result, key) {
    return result[key];
}

export function strings(result, key) {
    return result[key];
}

function viewOf(page) {
    return Array.from(page.getViewport({ scale: 1 }).transform);
}

/** The size in points, with rotation, of pages first..first+count-1: two numbers each, NaN for a page that cannot be read. */
export async function readSizes(d, first, count) {
    const sizes = await Promise.all(Array.from({ length: count }, async (_, i) => {
        try {
            const viewport = (await d.doc.getPage(first + i + 1)).getViewport({ scale: 1 });
            return [viewport.width, viewport.height];
        } catch {
            return [NaN, NaN];
        }
    }));
    return { sizes: sizes.flat() };
}

export async function readInfo(d) {
    // Metadata that cannot be read is no reason to refuse pages that can.
    const metadata = await d.doc.getMetadata().catch(() => null);
    const info = metadata?.info ?? {};
    const text = key => (typeof info[key] === 'string' && info[key].length > 0 ? info[key] : null);
    return {
        title: text('Title'), author: text('Author'), subject: text('Subject'), keywords: text('Keywords'),
        creator: text('Creator'), producer: text('Producer'), created: text('CreationDate'), modified: text('ModDate'),
    };
}

/** Every page's label, "" for a page without one; no key when the document has no labels. */
export async function readLabels(d) {
    const labels = await d.doc.getPageLabels();
    return labels ? { labels: labels.map(label => (typeof label === 'string' ? label : '')) } : {};
}

async function pageIndexOf(d, reference) {
    const key = `${reference.num}R${reference.gen}`;
    let index = d.pageIndexes.get(key);
    if (index === undefined) {
        index = await d.doc.getPageIndex(reference);
        d.pageIndexes.set(key, index);
    }
    return index;
}

async function pageViewOf(d, pageIndex) {
    let view = d.views.get(pageIndex);
    if (view === undefined) {
        view = viewOf(await d.doc.getPage(pageIndex + 1));
        d.views.set(pageIndex, view);
    }
    return view;
}

const noPlace = [-1, NaN, NaN, NaN, NaN, NaN, NaN, NaN, NaN];

/** A destination as nine numbers: page, x, y (page space, NaN where it says nothing) and the target page's view transform. */
async function resolveDestination(d, destination) {
    try {
        const explicit = typeof destination === 'string' ? await d.doc.getDestination(destination) : destination;
        if (!Array.isArray(explicit) || explicit.length === 0) {
            return noPlace;
        }
        const target = explicit[0];
        let pageIndex;
        if (Number.isInteger(target)) {
            pageIndex = target;
        } else if (target && typeof target === 'object' && Number.isInteger(target.num)) {
            pageIndex = await pageIndexOf(d, target);
        } else {
            return noPlace;
        }
        const argument = i => (typeof explicit[i] === 'number' ? explicit[i] : NaN);
        let x = NaN;
        let y = NaN;
        switch (explicit[1]?.name) {
            case 'XYZ': x = argument(2); y = argument(3); break;
            case 'FitH': case 'FitBH': y = argument(2); break;
            case 'FitV': case 'FitBV': x = argument(2); break;
            case 'FitR': x = argument(2); y = argument(5); break;
            default: break;
        }
        const view = Number.isNaN(x) && Number.isNaN(y) ? [NaN, NaN, NaN, NaN, NaN, NaN] : await pageViewOf(d, pageIndex);
        return [pageIndex, x, y, ...view];
    } catch {
        return noPlace;
    }
}

/**
 * The outline in reading order: a title and depth + destination numbers for each entry. The tree comes from a worker that has
 * already refused cycles, but a file can still make it enormous, so the walk stops at the same limits as .NET applies again.
 */
export async function readOutline(d, maxNodes, maxDepth) {
    const outline = await d.doc.getOutline();
    const titles = [];
    const values = [];
    const stack = outline ? [...outline].reverse().map(item => [item, 0]) : [];
    while (stack.length > 0 && titles.length < maxNodes) {
        const [item, depth] = stack.pop();
        titles.push(typeof item.title === 'string' ? item.title : '');
        values.push(depth, ...(item.dest ? await resolveDestination(d, item.dest) : noPlace));
        if (depth + 1 < maxDepth && Array.isArray(item.items)) {
            for (let i = item.items.length - 1; i >= 0; i--) {
                stack.push([item.items[i], depth + 1]);
            }
        }
    }
    return { titles, values };
}

/** The page's text items, joined, with twelve numbers per item, and the page's view transform. */
export async function readText(d, pageIndex) {
    const page = await d.doc.getPage(pageIndex + 1);
    // Not normalized: the search does that itself and keeps a map to the original, and copying gives what the document says.
    const content = await page.getTextContent({ disableNormalization: true });
    const parts = [];
    const values = [];
    for (const item of content.items) {
        if (typeof item.str !== 'string') {
            continue;
        }
        const style = content.styles[item.fontName];
        const flags = (item.hasEOL ? 1 : 0) | (item.dir === 'rtl' ? 2 : 0) | (style?.vertical ? 4 : 0);
        parts.push(item.str);
        values.push(item.str.length, ...item.transform, item.width, item.height, style?.ascent ?? 0, style?.descent ?? 0, flags);
    }
    return { text: parts.join(''), values, view: viewOf(page) };
}

/** Each link: an address ("" when internal), and four numbers of its rectangle + destination numbers. */
export async function readLinks(d, pageIndex) {
    const page = await d.doc.getPage(pageIndex + 1);
    const annotations = await page.getAnnotations({ intent: 'display' });
    const uris = [];
    const values = [];
    for (const annotation of annotations) {
        if (annotation.subtype !== 'Link' || !Array.isArray(annotation.rect)) {
            continue;
        }
        // url is what pdf.js validated; unsafeUrl is not used. The viewer has its own list of schemes on top.
        const uri = typeof annotation.url === 'string' ? annotation.url : '';
        const place = annotation.dest && !uri ? await resolveDestination(d, annotation.dest) : noPlace;
        if (uri || place[0] >= 0) {
            uris.push(uri);
            values.push(...annotation.rect.slice(0, 4), ...place);
        }
    }
    return { uris, values, view: viewOf(page) };
}

function notePainted(d, pageIndex) {
    const at = d.painted.indexOf(pageIndex);
    if (at >= 0) {
        d.painted.splice(at, 1);
    }
    d.painted.push(pageIndex);
    if (d.painted.length > paintedPagesKept) {
        const oldest = d.painted.shift();
        // Declines by itself when that page is being painted right now.
        d.doc.getPage(oldest + 1).then(page => page.cleanup(), () => { });
    }
}

/**
 * Starts painting a part of a page drawn at targetWidth x targetHeight: the part (x, y, width, height) is all the canvas holds,
 * so high zoom levels cost the area on screen and not the whole page. The canvas stays off the GPU, because it is read back.
 */
export function beginRender(d, pageIndex, targetWidth, targetHeight, x, y, width, height) {
    const t = { canvas: null, context: null, width, height, task: null, cancelled: false, finished: null };
    t.finished = (async () => {
        try {
            const page = await d.doc.getPage(pageIndex + 1);
            if (t.cancelled || d.closed) {
                return -1;
            }
            const viewport = page.getViewport({ scale: 1 });
            t.canvas = new OffscreenCanvas(width, height);
            // pdf.js asks the canvas for the same context, with the same options.
            t.context = t.canvas.getContext('2d', { alpha: false, willReadFrequently: true });
            if (!t.context) {
                return 0;
            }
            notePainted(d, pageIndex);
            t.task = page.render({
                canvas: t.canvas,
                viewport,
                transform: [targetWidth / viewport.width, 0, 0, targetHeight / viewport.height, -x, -y],
            });
            await t.task.promise;
            return t.cancelled ? -1 : 1;
        } catch (error) {
            return t.cancelled || d.closed || error?.name === 'RenderingCancelledException' ? -1 : 0;
        }
    })();
    return t;
}

/** Resolves to 1 when painted, 0 when the page cannot be painted, -1 when cancelled. Never rejects. */
export function waitRender(t) {
    return t.finished;
}

export function cancelRender(t) {
    t.cancelled = true;
    t.task?.cancel();
}

/**
 * Copies the pixels into .NET's buffer as BGRA. The canvas has no alpha, so they are opaque and already premultiplied. The channels
 * are swapped here because JavaScript is compiled and .NET may be interpreted, and in place, in the array getImageData made anyway:
 * that array to the buffer is the only copy.
 */
export function takePixels(t, view) {
    const image = t.context.getImageData(0, 0, t.width, t.height);
    const bytes = new Uint8Array(image.data.buffer, image.data.byteOffset, image.data.byteLength);
    const pixels = new Uint32Array(image.data.buffer, image.data.byteOffset, image.data.byteLength >>> 2);
    for (let i = 0; i < pixels.length; i++) {
        const value = pixels[i];
        pixels[i] = (value & 0xFF00FF00) | ((value & 0xFF) << 16) | ((value >>> 16) & 0xFF);
    }
    view.set(bytes);
    releaseRender(t);
}

/** Lets go of the canvas's memory without waiting for the collector. */
export function releaseRender(t) {
    if (t.canvas) {
        t.canvas.width = 0;
        t.canvas.height = 0;
        t.canvas = null;
        t.context = null;
    }
}
