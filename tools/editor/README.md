# Editor tools

## `Benchmarks/`

BenchmarkDotNet benchmarks of the editor engine (`TKCode.Arc.Editor`): loading, random edits, reading lines, snapshots
and find on large texts.

```bash
dotnet run -c Release --project tools/editor/Benchmarks -- --filter '*'
```

## `monarch-convert/`

Generates the editor's built-in languages from monaco-editor's language definitions (MIT): for each language,
`src/TKCode.Arc.Editor/Languages/BuiltIn/<Name>Language.g.cs` with its registration (id, extensions, file names,
aliases, mime types), its language configuration and its Monarch grammar, plus `BuiltInLanguages.g.cs` listing them,
and the definitions' own tokenization tests as JSON under `tests/TKCode.Arc.Editor.Tests/Languages/BuiltIn/TestData`.

It loads the TypeScript definitions in Node (24 or later, which strips types): `hooks.mjs` points the editor API,
`registerLanguage` and the test runner at the shims in `shims/`, drops the `<T>expr` casts type stripping cannot erase,
and adds the `.ts` extensions TypeScript leaves out. What is converted is what monaco-editor registers.

```bash
git clone --depth 1 --filter=blob:none --sparse https://github.com/microsoft/monaco-editor.git /tmp/monaco-editor
git -C /tmp/monaco-editor sparse-checkout set src/languages/definitions
cd tools/editor/monarch-convert
node --import ./register-hooks.mjs convert.mjs /tmp/monaco-editor/src/languages/definitions ../../.. "$(git -C /tmp/monaco-editor rev-parse --short HEAD)"
```

Then run the engine's tests (`BuiltInLanguageTokenizationTests` replays every language's cases) and update the commit
named in `THIRD-PARTY-NOTICES.md`. Regular expressions keep their JavaScript source; the engine translates them
(`JsRegex.TranslateMonarch` for grammars, `JsRegex.FromLiteral` for configurations).
