# Selective registration audit after the Markdown/highlighting split

[PR #138](https://github.com/AIDotNet/TKCode.Arc/pull/138) already moved Markdown and TextMate highlighting into optional packages. This draft now focuses on the remaining question: what can selective theme/token registration save **after that change**, and does the selected Button fixture actually render and behave like full registration?

The fixture is pinned to merge commit `5a2bb2aa92e83f66d1e5343faa787e018134d9ae` (`1.1.1-preview.5`). It adds research tooling only. Production sources are patched exclusively in independent temporary exports; no public selector or automatic dependency-discovery API is implemented.

## Reproduce

Requirements: Windows x64 with a normal desktop session, PowerShell 7, Git, exactly .NET SDK **11.0.100**, and .NET runtime **10.0.15**. NuGet access is required for Avalonia and the repository's dependencies.

```powershell
git fetch origin
pwsh -NoProfile -File tools/dev/TrimSizeAudit/run.ps1
```

An optional `-OutputRoot` must name a new directory outside the checkout. Existing directories are rejected, never cleaned or reused. `-Variants full,selected` runs a subset; the default runs all four cases. For an order/CWD check:

```powershell
$runner = (Resolve-Path tools/dev/TrimSizeAudit/run.ps1).Path
Push-Location $env:TEMP
try {
    & $runner -Variants missing-token,selected,selected-with-languages,full
}
finally { Pop-Location }
```

The output contains the unedited `results.json`, SDK/build/probe logs, complete file inventories with SHA-256, resolved package identities and SHA-512, source-type manifests, and PNG evidence for each successful state. The checked-in [schema-v2 result](results/windows-x64-2026-11-17.rendered.json) is copied directly from a completed runner invocation.

### Isolation and version policy

- There are **no NuGet Arc/Core comparison variants and no `-SourceCommit` override**. Every case uses project references from the same pinned committed tree. A different revision requires updating the reviewed contract and experimental patch together.
- Each case has its own complete source export, including Foundation, Core, generators, all `obj/bin`, its host, and publish directory. No source-build products are shared across cases.
- Every `dotnet` command runs from the output root under an explicit `global.json` (`11.0.100`, `rollForward=disable`, `allowPrerelease=false`). The same pin replaces the snapshot's original preview-SDK policy. The host pins its runtime pack to 10.0.15; the inspector also pins runtime 10.0.15 with runtime roll-forward disabled.
- Empty output-root `Directory.Build.props/targets` and `Directory.Packages.props` stop accidental parent settings. The source exports retain their own repository settings. Host/inspector projects are `.csproj.template` files until materialized outside the checkout, so they neither inherit repository settings in place nor affect CI's `**/*.csproj` cache glob.
- Package resolution and binary hashes are recorded. The exact runtime pack is checked after publish. NuGet packages are restored from nuget.org; archive contents and package versions are fixed by the pinned source and recorded graph.

## Cases and reviewed dependency closure

| Case | Arc themes/tokens | Arc language catalogs | Purpose |
| --- | --- | --- | --- |
| `full` | Current full registration | All six | Post-#138 control |
| `selected-with-languages` | Button theme/token only | All six | Isolate theme/token selection |
| `selected` | Button theme/token only | None | Include localization selection |
| `missing-token` | Button theme; Button token deliberately omitted | None | Expected-failure control; not a usable size result |

[selection-contract.json](selection-contract.json) records the reviewed Button closure and immutable source blob IDs for the Button, its token/template, and Core's `ArcContentSwap`, `SpringSizeDecorator`, `ArcSpinner`, and `LazyFocusRing`. **All existing Core registration stays enabled**, including its global ArcDesign descriptor and eight base-theme dictionaries. Thus this is a conservative fixture, not a claim to have minimized Core itself.

The runner rejects changed reviewed sources, missing/duplicate source theme identities, and a Button template whose token identities differ from `ArcDesign` and `Button`. The Button fixture uses caller-supplied text and no Arc language catalog; `selected` explicitly removes catalog entries from the generated language table, verifies zero catalogs at runtime, and checks its token identities are exactly ArcDesign and Button. The separate retained-language case measures the effect of keeping those catalogs.

This is a **manually reviewed, pinned dependency closure**, not a general algorithm for arbitrary controls. Runtime checks cover the code-created spinner/focus ring as well as the compiled template. A future general selector must compute dependencies for composite controls, custom content, referenced component libraries, localization, token-only overrides, and dynamic usage.

## Validation beyond theme lookup

Each valid host creates a real off-screen native Window and attaches the Button to its visual/logical tree. Motion is disabled for deterministic static comparisons. It performs **222 assertions** covering:

- The actual visual root, instantiated template, `PART_Frame`, `PART_Label`, and `PART_LabelSlot`.
- Every Button and ArcDesign descriptor resource, default font/foreground, padding, height, and Small/Large geometry.
- Lazy spinner creation, inherited foreground, token-driven size, centering, and hiding.
- Keyboard focus and the visible focus ring, its margins/radius/brush/thickness.
- Light-to-dark resource propagation through the template, accent changes, live spinner/padding token overrides, and disabled styling.
- Absence of captured binding/resource warnings and errors.

Eight **nonempty pixel buffers** are rendered and saved: light-primary, small, large, loading, keyboard-focus, dark-focus, overrides-loading, and disabled. When `full` is present, every selected-case buffer must exactly match its full-registration counterpart in the same run. Mismatches fail the runner.

The negative control omits Button token registration while preserving the theme factory. It must exit unsuccessfully with `CHECK FAILED: resource Button.*`. This demonstrates that successful dictionary lookup alone cannot pass the new probe. Its failure is intentional and excluded from savings claims.

The metadata inspector takes the exact theme `x:Class` identities extracted from the committed source, requires retained matches to be top-level ResourceDictionary classes, and never counts arbitrary names ending in `Theme`. Tracked control identities must all exist in the **untrimmed** input before absence in the trimmed result can be interpreted. A rename or missing input fails the run instead of silently becoming a trimming success.

Both the untrimmed host-output Arc DLL and the trimmed published DLL are inspected by the same tool and included in the same result schema. Published dependency lengths and hashes come from the full per-file inventory. No separate manual inspection step is needed to produce the reported metrics.

## Results

Windows x64, `net10.0`, Avalonia 12.1.5, Release, self-contained, `PublishTrimmed=true`, `TrimMode=full`. Values are uncompressed file lengths excluding `.pdb` and `.xml`, not allocation on disk, installer/download size, or RAM. MiB = 1,048,576 bytes.

| Case | Publish bytes | MiB | Saving versus full |
| --- | ---: | ---: | ---: |
| `full` | 49,992,378 | 47.68 | — |
| `selected-with-languages` | 44,067,952 | 42.03 | 5.65 MiB |
| `selected` | 44,057,712 | 42.02 | 5.66 MiB |

All three valid cases pass 222 checks. The eight selected-case frames match full registration bit for bit. Registered themes fall from **176 to 1**; the selected case also reduces Arc catalogs from **6 to 0**. Catalog selection saves **10,240 bytes (10 KiB)** beyond theme/token selection. The deliberate missing-token case fails at `Button.InlinePaddingSM` as required.

The full Arc assembly is **6,733,824 bytes / 3,985 types / 38,856 methods / 176 source theme classes before trimming**, and **3,469,312 bytes / 2,714 types / 16,130 methods / 176 source theme classes after trimming**. The current fixture has no StreamMark, Markdig, TextMate, or Onigwrap publish files; the runner rejects their presence. Their removal is credited to upstream #138, not to this draft.

The recorded file is raw runner output, including the expected-failure row. Consumers should filter `NegativeControl=true` out of size comparisons. A different SDK/runtime, OS/font environment, source revision, or host program requires a new result; within-run pixel equality is not a cross-machine pixel guarantee.

Cold runs in the default and reversed variant order checked order/CWD independence. A repeated cold `selected-with-languages` run reproduced its final recorded byte total and pixel buffers. Exact totals and binary hashes belong to the recorded fixture inputs. The checked-in [eight PNGs](results/frames) show the selected fixture. For example:

![Loading spinner](results/frames/loading.png)
![Dark theme and keyboard focus ring](results/frames/dark-focus.png)

## Review follow-up

| Review concern | Change |
| --- | --- |
| Factory lookup does not exercise a control | Real visual tree, template parts, 222 assertions, eight rendered states, full/selected pixel comparison, and a failing missing-token control |
| Missing dependency closure and languages | Pinned reviewed Button/Core closure; immutable source identities; separate retained/omitted-catalog cases; zero-catalog and selected-token assertions; no general selector claim |
| Before-trim numbers and hand-assembled result schema | Same inspector produces both before/after metrics; complete file inventory; committed result is unedited schema-v2 runner output; v1 summary explicitly archived |
| Source obj/bin shared across cases | Separate full source export and build products for every case |
| Commit/package version mismatch | Source-only comparisons from one pinned commit; removed Arc/Core NuGet cases and free-form commit override |
| SDK depends on invocation directory | Stable exact SDK pin, fixed dotnet CWD, snapshot SDK override, pinned/checked runtime pack, exact inspector runtime, resolved-package recording |
| Real project files inside repository | Project templates only, materialized behind build-setting barriers outside the checkout |
| Theme/control counting heuristics | Source-derived exact theme identities plus top-level/base-type checks; control identities must exist before trimming |

## Scope and follow-up

These measurements include the common rendering/verification host, which itself roots framework functionality. They establish a validated result for this fixed Button fixture, not a universal saving or a complete dependency selector. They do not validate other Arc controls, arbitrary icon/custom content, real pointer/keyboard message sequences, localized composite controls, animation timing (motion is Off), NativeAOT, WebAssembly, or other RIDs/TFMs.

A production design could preserve existing `UseArcControls()` behavior and add an opt-in path referencing only a selected dependency closure. Explicit selection plus diagnostics is a reviewable first step; consumer-side XAML/C# discovery could follow. No proposed API name is introduced by this PR. The Markdown/highlighting split is already implemented in #138 and is no longer a requested change here.

## Historical record

[The original 2026-11-16 summary](results/historical-2026-11-16.lookup-only.json) is archived separately. It used a pre-#138 source revision, a much weaker theme-lookup-only host, and an older summary schema; source build intermediates were shared. Its **14.07 MiB** figure was an optimistic reachability experiment, not proof of a rendered working application's saving. It is not a current reproducible result and must not be combined with the new table. The original tooling/report remains available in Git history at `78b8e368e28a3bf4ab34b68bc1c34744dc541c01`.
