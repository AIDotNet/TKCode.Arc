---
name: fallback-font-position-history
description: Windows CI 上 IncrementalLayoutTests 失败的原因：回退字体曾依赖取 run 的起点与 Avalonia 的匹配历史
type: project
updated: 2026-10-19
source: session
evidence: [src/TKCode.Arc.StreamMark/Avalonia/Rendering/TextSegmentView.cs:MatchFallback, tests/TKCode.Arc.StreamMark.Tests/Avalonia/Rendering/IncrementalLayoutTests.cs:StreamedMixedScripts_MatchFullLayout]
---
alpha.16 引入逐行排版后，`StreamedLineBreaks_MatchFullLayout_AtEveryStep(styled: False)` 只在 Windows CI 上失败（2026-10-16 起）：整段排版时缺字片段"。禁则…）。"从"。"开始取 run，逐行重排从行首"首"开始取，回退字体按 run 的第一个字匹配，Windows 上两者落到不同字体，字宽不同、断行不同。macOS 上两者都是苹方，测不出来。另外 Avalonia 的 `FontManager.TryMatchCharacter` 会优先复用之前匹配过的回退字体（实测：匹配过"한"之后"。"从 PingFang SC 变成 Apple SD Gothic Neo），结果依赖进程内历史。alpha.19 两处都修了：按缺字片段起点匹配，并用静态字典固定每个字第一次的结果。

**Why:** 只在一个平台出现的断行差异，根因在字体回退而不在 `SegmentLayout`；韩文与汉字连写的文本能在 macOS 上稳定复现，不必等 Windows CI。
**How to apply:** 改动 `InlineTextSource` 的 run 切分或回退字体时，保证同一段文字无论从哪个位置开始取、何时取，都得到同一字体；跑 `IncrementalLayoutTests`。

2026-10-18 补充：按字缓存会固定 Avalonia 某一刻的偶然结果。PR #2 的 CI 上，`InlineStyles_Render` 按字重探测 `GlyphTypeface` 把"Yu Gothic UI Semibold"加载进了 Avalonia 的缓存，之后"斜"被固定到这个族，渲染成 Yu Gothic Bold + 合成斜体（`3b192e6` 去掉了探测）。曾考虑直接调用系统回退（`IFontManagerImpl.TryMatchCharacter`），但 Avalonia 的引用程序集剥掉了 `AvaloniaLocator.Current` 等私有 API，编译不过；按文字类别的确定回退表被搁置，维持现状。

2026-10-19 再次触发：`8bff953` 新增的表格用例渲染中文粗体表头，同进程里先把按字重拆开的中文回退族缓存好，之后 `FallbackRuns_UseRealFaces_NotSyntheticCopies` 在 Windows CI 上报 `Yu Gothic/Bold/Italic (Oblique)`（macOS 本机复现不了，Avalonia 12.1.3 → 12.1.4 没有字体代码改动）。机制见 Avalonia 12 的 `FontCollectionBase`：精确 key 未缓存而同族已有别的字面时，`TryGetGlyphTypeface(allowNearestMatch)` 取最近字面并 `TryCreateSyntheticGlyphTypeface`，合成字面沿用被取来的那个字面的字重，并缓存在该族里。上一次修复 `e979543` 的 `Simulated()`（先加载 `(Italic, w)` 再看有没有合成）本身就会制造这份合成字面，所以不可靠。现在 `RealFace` 对回退字体一律用正体，不请求斜体；测试固定为暖缓存顺序，失败信息带上排版用的 Typeface 与该族列出的字面。

**How to apply (2026-10-19):** 回退字体不要请求斜体，也不要用 `Typeface.GlyphTypeface` 试探任何不确定存在的字面；字体断言要在「别的用例已缓存好回退族」的状态下也成立。
