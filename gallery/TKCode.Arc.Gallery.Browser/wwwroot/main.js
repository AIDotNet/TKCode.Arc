import { dotnet } from './_framework/dotnet.js'

const is_browser = typeof window != "undefined";
if (!is_browser) throw new Error(`Expected to be running in a browser`);

// The splash in index.html: the bar follows the runtime's downloads (resources loaded of all it needs); once they are in, the
// bar sweeps until Avalonia draws the first frame and removes the splash.
const splash = document.querySelector('.avalonia-splash');
const splashBar = document.getElementById('splash-bar');
const splashStatus = document.getElementById('splash-status');
let splashShown = 0;
const showProgress = (loaded, total) => {
    if (!splash || !total) {
        return;
    }
    const done = loaded >= total;
    // Never move backwards: the total can grow while the runtime discovers more resources.
    splashShown = Math.max(splashShown, Math.min(loaded / total, 1));
    const percent = Math.round(splashShown * 100);
    splashBar.style.transform = `scaleX(${splashShown})`;
    splash.setAttribute('aria-valuenow', String(percent));
    splashStatus.textContent = done ? 'Starting…' : `${percent}%`;
    splash.classList.toggle('splash-starting', done);
};

const dotnetRuntime = await dotnet
    .withModuleConfig({ onDownloadResourceProgress: showProgress })
    .withDiagnosticTracing(false)
    .withApplicationArgumentsFromQuery()
    .create();

const config = dotnetRuntime.getConfig();

// Binary text of a file of the site, one byte per character: the only binary form a synchronous request allows on the main
// thread. Empty when there is no such file; a host that answers a missing file with its index.html returns text that starts with '<'.
const readFile = path => {
    const request = new XMLHttpRequest();
    request.open('GET', path, false);
    request.overrideMimeType('text/plain; charset=x-user-defined');
    request.send();
    const text = request.status === 200 ? request.responseText : '';
    return text.startsWith('<') ? '' : text;
};
const toBytes = text => {
    const bytes = new Uint8Array(text.length);
    for (let i = 0; i < text.length; i++) {
        bytes[i] = text.charCodeAt(i) & 0xff;
    }
    return bytes;
};
dotnetRuntime.setModuleImports('arc-lazy', {
    // An assembly the publish left out of the boot list (LazyAssemblies.cs), or null when there is none. Synchronous: the runtime
    // asks while it resolves a type. The "MZ" check rejects anything that is not a PE file.
    load: name => {
        const text = readFile(`_lazy/${name}.dll`);
        return text.startsWith('MZ') ? toBytes(text) : null;
    },
    // An assembly a trimmed publish loads on demand (BlazorWebAssemblyLazyLoad in the project file, BrowserAssemblyLoader.cs),
    // fetched and loaded by the runtime's own loader (false: it was loaded already).
    loadAsync: name => dotnetRuntime.INTERNAL.loadLazyAssembly(`${name}.wasm`),
    // The same file, synchronously, for the runtime's Resolving event; null when it is not one of them. "\0asm" is the start of
    // a WebAssembly module, the wrapper of the runtime's assembly files.
    loadSync: name => {
        const asset = (config.resources?.lazyAssembly ?? []).find(lazy => lazy.virtualPath === `${name}.wasm`);
        const text = asset ? readFile(`_framework/${asset.name}`) : '';
        return text.startsWith('\0asm') ? toBytes(text) : null;
    },
    // A file of the site, such as the Chinese font (CjkFont.cs): the layout asks for a glyph in the middle of measuring.
    file: path => toBytes(readFile(path))
});

// The open page in the address bar (BrowserRouter.cs). Addresses are relative to the document's base, so the site can sit in a
// sub-path: with <base href="/"> the page /components/button is "/components/button" here.
const root = new URL(document.baseURI).pathname;
const currentPath = () => '/' + decodeURIComponent(location.pathname.slice(root.length)).replace(/^\/+/, '');
const addressOf = path => root + path.slice(1);
dotnetRuntime.setModuleImports('arc-nav', {
    path: currentPath,
    push: (path, title) => {
        if (path !== currentPath()) {
            history.pushState(null, '', addressOf(path));
        }
        document.title = title;
    },
    replace: (path, title) => {
        history.replaceState(null, '', addressOf(path));
        document.title = title;
    },
    title: title => { document.title = title; },
    listen: onPop => addEventListener('popstate', () => onPop(currentPath()))
});

// The screenshots of the docs pages (HttpShotSource.cs): load() fetches a picture, take() hands over its bytes once, or none
// when the server has no such image.
const shots = new Map();
dotnetRuntime.setModuleImports('arc-shots', {
    load: async url => {
        try {
            const response = await fetch(url);
            if (response.ok && /^image\/(webp|png)\b/.test(response.headers.get('content-type') ?? '')) {
                shots.set(url, new Uint8Array(await response.arrayBuffer()));
            }
        } catch {
            // Offline or blocked: the page shows no picture.
        }
    },
    take: url => {
        const bytes = shots.get(url) ?? new Uint8Array(0);
        shots.delete(url);
        return bytes;
    }
});

// The text of the docs pages and guides (HttpTextSource.cs): '' when the server has no such file. A host that answers a missing
// file with its index.html does not count as having it.
dotnetRuntime.setModuleImports('arc-texts', {
    load: async url => {
        try {
            const response = await fetch(url);
            return response.ok && !/^text\/html\b/.test(response.headers.get('content-type') ?? '') ? await response.text() : '';
        } catch {
            // Offline or blocked: the page says it could not be loaded.
            return '';
        }
    }
});

// A map outline of the Choropleth explorer demos (HttpMapSource.cs). The demo data is read by synchronous code, so the file is
// requested synchronously; it is one small same-origin file per area, read once and kept by the demo data. '' when missing.
dotnetRuntime.setModuleImports('arc-maps', {
    loadSync: url => {
        try {
            const request = new XMLHttpRequest();
            request.open('GET', url, false);
            request.send();
            return request.status === 200 && !/^text\/html\b/.test(request.getResponseHeader('content-type') ?? '') ? request.responseText : '';
        } catch {
            return '';
        }
    }
});

// The GitHub star count of the top bar, kept for an hour (BrowserStarCache.cs); -1 when there is none.
const starsKey = 'arc.gallery.stars';
dotnetRuntime.setModuleImports('arc-stars', {
    read: () => {
        try {
            const kept = JSON.parse(localStorage.getItem(starsKey) ?? 'null');
            return kept && Date.now() - kept.at < 3600000 ? kept.stars : -1;
        } catch {
            return -1;
        }
    },
    write: stars => {
        try {
            localStorage.setItem(starsKey, JSON.stringify({ stars, at: Date.now() }));
        } catch {
            // Storage may be blocked; the count is fetched again next time.
        }
    }
});

// The language of the gallery (BrowserLanguageStore.cs): the browser's preferred languages, most preferred first, decide the first
// visit; the one chosen in the top bar is kept in local storage after that.
const languageKey = 'arc.gallery.language';
dotnetRuntime.setModuleImports('arc-lang', {
    preferred: () => (navigator.languages?.length ? navigator.languages : [navigator.language ?? '']).join(','),
    read: () => {
        try {
            return localStorage.getItem(languageKey) ?? '';
        } catch {
            return '';
        }
    },
    write: tag => {
        try {
            localStorage.setItem(languageKey, tag);
        } catch {
            // Storage may be blocked; the next visit follows the browser's language again.
        }
    }
});

// The second argument is the document's base address: the screenshots and lazy assemblies are relative to it.
await dotnetRuntime.runMain(config.mainAssemblyName, [globalThis.location.href, document.baseURI]);
