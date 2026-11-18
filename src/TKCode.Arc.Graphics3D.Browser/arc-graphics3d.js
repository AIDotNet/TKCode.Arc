// TKCode.Arc.Graphics3D.Browser: a WebGL 2 context for Viewport3D, made known to the OpenGL ES layer of the WebAssembly runtime.
//
// The engine calls OpenGL ES 3.0 through C function pointers. In a .NET WebAssembly app those are Emscripten's OpenGL ES
// functions, which draw into whichever WebGL context Emscripten's GL object holds as current. Avalonia.Browser registers the
// context of its own canvas with that object; this module registers one more, on a canvas that is never shown (the viewport
// draws into framebuffers and reads the picture back), and switches between the two.

// Emscripten's GL object of the .NET runtime: Avalonia.Browser has it exported (EmccExportedRuntimeMethod GL) and finds it the same way.
function emscriptenGl() {
    const self = globalThis;
    const module = self.Module ?? self.getDotnetRuntime?.(0)?.Module;
    return module?.GL ?? self.AvaloniaGL ?? self.SkiaSharpGL ?? null;
}

// handle -> { canvas, context, lost }
const contexts = new Map();

// Creates the context and returns its Emscripten handle, or 0 when the page has no WebGL 2 or the runtime has no GL object.
export function create() {
    const gl = emscriptenGl();
    if (!gl) {
        return 0;
    }
    const canvas = document.createElement('canvas');
    canvas.width = 1;
    canvas.height = 1;
    // No depth, stencil or antialiasing on the canvas itself: nothing is drawn to it. The last four are read by Emscripten.
    const attributes = {
        alpha: true,
        depth: false,
        stencil: false,
        antialias: false,
        premultipliedAlpha: true,
        preserveDrawingBuffer: false,
        powerPreference: 'high-performance',
        majorVersion: 2,
        minorVersion: 0,
        enableExtensionsByDefault: 1,
        explicitSwapControl: 0
    };
    const context = canvas.getContext('webgl2', attributes);
    if (!context) {
        return 0;
    }
    const handle = gl.registerContext(context, attributes);
    const state = { canvas, context, lost: false };
    // The default action of the event is to never restore the context, which is what happens here: the viewports drop their
    // renderers and a new context is created when one attaches again.
    canvas.addEventListener('webglcontextlost', () => { state.lost = true; });
    contexts.set(handle, state);
    return handle;
}

// Makes the context current and returns the handle of the one that was (0 for none), for restore().
export function makeCurrent(handle) {
    const gl = emscriptenGl();
    const previous = gl.currentContext?.handle ?? 0;
    gl.makeContextCurrent(handle);
    return previous;
}

export function restore(previous) {
    emscriptenGl().makeContextCurrent(previous);
}

export function isLost(handle) {
    const state = contexts.get(handle);
    return !state || state.lost || state.context.isContextLost();
}

// Frees the context now instead of when the canvas is collected: a page may hold only a limited number of WebGL contexts.
export function destroy(handle) {
    const state = contexts.get(handle);
    if (!state) {
        return;
    }
    contexts.delete(handle);
    const gl = emscriptenGl();
    const current = gl.currentContext?.handle ?? 0;
    gl.deleteContext(handle);
    if (current === handle) {
        gl.makeContextCurrent(0);
    }
    state.context.getExtension('WEBGL_lose_context')?.loseContext();
}
