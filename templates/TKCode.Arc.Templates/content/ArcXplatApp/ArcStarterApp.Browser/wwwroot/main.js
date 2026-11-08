import { dotnet } from './_framework/dotnet.js'

const dotnetRuntime = await dotnet
    .withDiagnosticTracing(false)
    .withApplicationArgumentsFromQuery()
    .create();

// Functions the C# side imports with [JSImport("...", "main.js")].
dotnetRuntime.setModuleImports('main.js', {
    settings: {
        read: key => globalThis.localStorage.getItem(key),
        write: (key, value) => globalThis.localStorage.setItem(key, value),
    },
});

const config = dotnetRuntime.getConfig();
await dotnetRuntime.runMain(config.mainAssemblyName, [globalThis.location.href]);
