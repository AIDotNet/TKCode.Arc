import { dotnet } from './_framework/dotnet.js'

const is_browser = typeof window != "undefined";
if (!is_browser) throw new Error(`Expected to be running in a browser`);

const dotnetRuntime = await dotnet
    .withDiagnosticTracing(false)
    .withApplicationArgumentsFromQuery()
    .create();

const config = dotnetRuntime.getConfig();

// Bytes of an assembly the publish left out of the boot list (LazyAssemblies.cs), or null when there is none. Synchronous:
// the runtime asks while it resolves a type. The response is read as text with one byte per character, the only binary
// form a synchronous request allows on the main thread. A host that answers a missing file with its index.html is not a
// PE file, so the "MZ" check turns that into null as well.
dotnetRuntime.setModuleImports('arc-lazy', {
    load: name => {
        const request = new XMLHttpRequest();
        request.open('GET', `_lazy/${name}.dll`, false);
        request.overrideMimeType('text/plain; charset=x-user-defined');
        request.send();
        const text = request.status === 200 ? request.responseText : '';
        if (!text.startsWith('MZ')) {
            return null;
        }
        const bytes = new Uint8Array(text.length);
        for (let i = 0; i < text.length; i++) {
            bytes[i] = text.charCodeAt(i) & 0xff;
        }
        return bytes;
    }
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
            if (response.ok && /^image\/(png|webp)\b/.test(response.headers.get('content-type') ?? '')) {
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

// The second argument is the document's base address: the screenshots and lazy assemblies are relative to it.
await dotnetRuntime.runMain(config.mainAssemblyName, [globalThis.location.href, document.baseURI]);
