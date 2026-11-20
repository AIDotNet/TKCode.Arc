---
name: script-itemized-shaping
description: Linux CI 上 IncrementalLayoutTests(styled: True) 失败的原因：Avalonia 把中英文拼成一个整形缓冲，script 随取 run 的起点变化
type: project
updated: 2026-11-20
source: session
evidence: [src/TKCode.Arc.StreamMark/Avalonia/Rendering/TextSegmentView.cs:ScriptCut, tests/TKCode.Arc.StreamMark.Tests/Avalonia/Rendering/IncrementalLayoutTests.cs:StreamedLineBreaks_MatchFullLayout_AtEveryStep]
---
提交 `75a1f777`（perf(StreamMark): replace Markdig with native incremental parser…）起，`StreamedLineBreaks_MatchFullLayout_AtEveryStep(styled: True)` 只在 ubuntu-latest 失败（期望 `(0,41),(41,41),(82,12)`，实际 `(0,41),(41,47),(88,6)`）。macOS 与 Windows 通过。
Linux runner 只有 DejaVu Sans（[[ci-font-environments]]），中文既不被主字体覆盖也找不到回退字体，于是中文 run 与后面的英文 run 同一字体、内存连续。Avalonia 12.1.6 的 `TextFormatterImpl.ShapeTextRuns` 会把这样的 run 拼成一个缓冲（`TryJoinContiguousMemories` + `CanShapeTogether` 只比字体、字号、基线），`HarfBuzzTextShaper` 用 `GuessSegmentProperties` 按第一个有书写系统的字推断 script：整段排版时"流式…开销。The renderer reshapes the "按汉字整形，DejaVu 不给英文做字距调整（"r"+"e" 5.76）；逐行重排从行首"renderer"开始按拉丁文整形（5.45），这一行宽 0.9px，多放下一个词。
修复：`InlineTextSource.ScriptCut` 在书写系统变化处截断 run，末尾是分界点的 run 用独立字符串，Avalonia 拼不起来，每种书写系统单独整形。整段排版里中文之后的英文也因此有了字距调整（Linux 上第二行从 41 字变 47 字）。

**Why:** 当初的不变量"从行首重排等价于整段排版"只考虑了断行，没考虑整形缓冲从哪里开始；macOS 上中文走回退字体（苹方，另一个字体），英文本来就单独整形，测不出来。主字体同时覆盖中英文（宿主把 Md.FontFamily 设成中文字体）时任何平台都会出现。
**How to apply:** 改动 run 的切分或内存来源时，保证同一段文字从哪个位置开始取 run 都得到同样的整形缓冲边界与 script；用 `mcr.microsoft.com/dotnet/sdk` 镜像 + `fonts-dejavu-core`（不装中文字体）跑 `IncrementalLayoutTests` 复现。
