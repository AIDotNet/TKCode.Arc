---
name: ci-font-environments
description: CI runner 的字体与本机不同：Linux 只有 DejaVu，macOS 的苹方列出 600 却只能加载到 Medium；字体相关断言要按真实加载结果判断
type: project
updated: 2026-10-18
source: session
evidence: [tests/TKCode.Arc.StreamMark.Tests/Avalonia/Rendering/InlineStyleProbeTests.cs, https://github.com/AtomUICommunity/StreamMark/actions/runs/39046728144]
---
alpha.18 的两个字体测试本机通过、CI 失败（2026-10-17 基线运行 39046728144）：
- ubuntu-latest 没有中文字体，只有 DejaVu Sans（不含斜体字面，但自带 😀 等表情）。中文画成方块、表情用主字体，主字体的斜体被合成；这不是回退字体，`FallbackRuns_UseRealFaces_NotSyntheticCopies` 只查非主字体的 run。
- macos-latest 的 PingFang SC 在 `FamilyTypefaces` 里列出 600，按 600 加载得到的却是 Medium（无合成），粗体与常规同为 Medium。`InlineStyles_Render` 的豁免改为按真实加载到的字面判断。
- Windows 上"。"与"首"会匹配到不同的中文字体，见 [[fallback-font-position-history]]。

**Why:** 字体相关测试在本机（字体齐全的 macOS）通过不代表 CI 通过；三个平台的失败原因各不相同。
**How to apply:** 写字体断言时按实际加载到的 GlyphTypeface 判断，不要用 `FamilyTypefaces` 列表推断；Linux 可用 `mcr.microsoft.com/dotnet/sdk:10.0-noble` + `fonts-dejavu-core` 的容器复现。
