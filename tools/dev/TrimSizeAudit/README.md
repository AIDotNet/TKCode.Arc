# Control registration and publish-size experiment

In a minimal desktop host that creates one Arc `Button`, `UseArcControls()` keeps all 179 control-theme classes after full trimming. This experiment measures selective registration and optional dependency boundaries separately, including their overlapping savings.

This is a research harness, outside the solution. The experimental patch is applied only to an exported, temporary source snapshot. It does not change the library built from this checkout, introduce a public registration API, implement automatic control discovery, or reorganize the shipped packages.

## Reproduce

Requirements: Windows x64, PowerShell 7, Git, the repository's .NET 11 SDK, the .NET 10 runtime, NuGet access, and the measured commit in local Git history. The program initializes Avalonia's desktop platform without showing a window; run in a normal desktop session.

From the repository root:

```powershell
git fetch origin
pwsh -NoProfile -File tools/dev/TrimSizeAudit/run.ps1
```

The default source is pinned to `57e42d28dd56b0d91237b523271cabeb36eafed4` (`1.1.1-preview.2`). The script exports that committed tree using `git archive`, applies [experiment.patch](experiment.patch) in the temporary snapshot, and copies the host projects outside the repository so they do not inherit repository build defaults. It never edits the current checkout or deletes an output directory. Each invocation requires a new output directory; each host has isolated restore/build intermediates and a separate publish directory to prevent stale files and dependency manifests.

For a shorter run:

```powershell
./tools/dev/TrimSizeAudit/run.ps1 -Variants source-full,selective,no-heavy,combined
```

The output directory contains `results.json`, per-file lengths, assembly metadata, the SDK information, publish logs, and startup output. `-OutputRoot` selects a new directory. `-SourceCommit` can target another local commit, but the experimental patch must still apply; the historical numbers below must not be attributed to a different revision.

## Method and limits

- `net10.0`, Avalonia.Desktop `12.1.5`, Release, `win-x64`, self-contained, `PublishTrimmed=true`, `TrimMode=full`; no single-file publishing or NativeAOT.
- Uncompressed file lengths, excluding all `.pdb` and `.xml` files. XML documentation is copied with project references but was absent from the NuGet-host output. MiB means 1,048,576 bytes; this is not filesystem allocation, installer size, or RAM use.
- One Arc button in all source variants. The host checks that its theme resolves. It does not display a window, apply and exercise the full control template, test the loading spinner or focus ring, or validate all control states and theme changes.
- The baseline/core/reference hosts create an Avalonia button. They are technical size baselines, not equivalent complete themed applications; Core alone does not style Avalonia `Button`.
- The selective variant hardcodes only `ButtonTheme` and `ButtonToken` registrations in the Arc assembly. It demonstrates static reachability savings, not a complete dependency-closure algorithm or a production-ready selector. Shared Core registration remains intact.
- Package separation is modeled by excluding components and their themes from the source build, not by actually publishing new NuGet packages. API compatibility and package migrations need separate design and validation.
- NuGet restore caches, source generators, and build-time-only dependencies are not included in publish-size totals. Runtime packs can change with SDK updates; the runner records the actual SDK and runtime pack for each result.

## Recorded Windows x64 results

Measured on 2026-11-16 with SDK `11.0.100` and runtime pack `10.0.15`. The machine-readable source measurements are in [results/windows-x64-2026-11-16.json](results/windows-x64-2026-11-16.json).

| Source-build variant | Publish bytes | MiB | Saving versus full registration |
| --- | ---: | ---: | ---: |
| Full registration | 58,478,232 | 55.77 | — |
| Register only Button theme and tokens | 43,722,405 | 41.70 | 14.07 MiB |
| Exclude Markdown/highlighting dependency group | 49,440,740 | 47.15 | 8.62 MiB |
| Both changes | 43,179,204 | 41.18 | 14.59 MiB |
| Exclude Charts, retaining AnimatedCounter | 58,324,632 | 55.62 | 0.15 MiB |
| Exclude Editors | 58,185,880 | 55.49 | 0.28 MiB |

The separately published NuGet host was 58,478,701 bytes: 469 bytes larger than the source control, with the same `TKCode.Arc.dll` length. Comparisons in the table use source builds only. The separate Avalonia baseline was 42,373,176 bytes (40.41 MiB), Core was 43,046,782 bytes (41.05 MiB), and a host referencing but not using Arc was 42,916,349 bytes (40.93 MiB).

All six source publications and startup checks succeeded. Registered theme counts were 179, 1, 174, 1, 166, and 174 respectively; resolving the button theme loaded one theme in each case. Inspecting metadata showed:

| `TKCode.Arc.dll` | Bytes | Types | Methods | Theme classes |
| --- | ---: | ---: | ---: | ---: |
| NuGet assembly before trimming | 6,924,288 | 4,092 | 39,905 | 179 |
| Full registration after trimming | 3,581,952 | 2,764 | 16,351 | 179 |
| Button-only registration after trimming | 72,704 | 34 | 107 | 1 |

Unused `Calendar`, `Markdown`, `BarChart`, `ColorPicker`, `PropertyGrid`, and `CodeBlock` types were present with full registration and absent with selective registration. Type presence does not imply preservation of every member. The full result included `TextMateSharp.Grammars.dll` at 6,742,528 bytes (6.43 MiB).

### Boundaries and overlapping savings

The heavy group must include **Markdown, CodeBlock, ChatThread, and RichTextEditor**. ChatThread uses StreamMark/Markdown, and RichTextEditor uses `CodeBlockHighlighter`. Excluding only Markdown and CodeBlock does not remove the whole dependency chain. ChatThread also declares ChatComposerTheme, explaining the five-theme reduction when excluding four components.

Selective registration already removes the unused managed StreamMark/TextMate chain. However, the NuGet native asset `libonigwrap.dll` (541,184 bytes) still ships. Removing the heavy package dependency on top of selective registration saves another **543,201 bytes (0.52 MiB)**. In the opposite order, selective registration on top of the heavy-group exclusion saves approximately **5.97 MiB**. The individual savings must not be added together.

The Charts variant keeps AnimatedCounter because BillingPrice's template uses it. Shared chart helpers stay in the source build and can be trimmed normally. This excludes the other 13 chart-component directories, not the already separate AdvancedCharts package. The Editors variant excludes that directory, including RichTextEditor, while Markdown/CodeBlock/ChatThread still retain the highlighting dependency. Its saving overlaps with the heavy-group experiment.

## Why lazy loading does not remove these themes

`UseArcControls()` calls the generated `ArcGeneratedAssets.AddTo()`. `ThemeEmitter` emits complete token and theme arrays, with type keys and direct factories such as `static () => new ButtonTheme()`. Those references keep every registered factory and its reachable compiled XAML in the trimmer's graph. `ArcThemeProvider` delays dictionary construction, which helps startup work and allocations, but does not make these factories unreachable at publish time. Runtime filtering of the complete table would still retain its static references.

The main control themes live under `src/TKCode.Arc/<Category>/<Component>/Themes/`. Core contains shared infrastructure and eight base-theme dictionaries for nine Avalonia built-in types. Keeping themes in Core is not the main cause here. `IsTrimmable`/`IsAotCompatible` and a warning-free publish do not promise an optimal reachability graph.

## Suggested implementation direction

1. Preserve `UseArcControls()` as the existing full-registration path. Add an opt-in path that directly references only selected control descriptors, themes, and required dependencies. Changing the current default would need compatibility review.
2. Start with explicit selection to make dependency closure and diagnostics reviewable. A consumer source generator could later collect XAML/C# usage and call per-control registration entry points exposed by the library. It needs an explicit inclusion mechanism for dynamic controls, referenced component libraries, and token-only customization. Scanning only the application's visible controls is insufficient.
3. Keep control XAML, constructors, properties, events, and normal `ArcTheme.Apply(...)` usage unchanged. Any proposed names such as `UseGeneratedArcControls()` or `UseArcControls(controls => controls.Button())` are design sketches, not implemented APIs. A runtime type filter over the existing global registry would not produce this result.
4. Prioritize optional boundaries around Markdown/highlighting and native/content assets. Splitting every small managed control into its own package adds little value once unused registrations can be removed. Charts/Editors gave much smaller isolated savings in this host. New optional packages must not depend on the original all-inclusive package in a cycle that brings everything back.
5. Add regression checks for unused theme/type absence, heavy dependency absence when not selected, the intended native-asset policy, and actual template behavior for selected controls. Validate composite controls, localization, token overrides, theme changes, and all supported target frameworks separately. Measure NativeAOT and WebAssembly rather than projecting these Windows IL numbers onto them.

If an application uses Markdown/highlighting or many controls, it will retain more of this graph. These results describe a one-button host, not a guaranteed saving for every application.
