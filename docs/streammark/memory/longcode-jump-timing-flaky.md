---
name: longcode-jump-timing-flaky
description: LongCodeTests 的"跳转排版中位数 < 20ms"是计时断言，本机跑全量时偶发失败，单独重跑通过；不是回归
type: project
updated: 2026-10-21
source: session
evidence: [tests/TKCode.Arc.StreamMark.Tests/Avalonia/Transcript/LongCodeTests.cs:ThreeThousandLineCode_IsChunked_AndCopiesExactly]
---
2026-10-21 在 `6516221` 上，`dotnet test TKCode.Arc.slnx --no-build` 全量运行时 `ThreeThousandLineCode_IsChunked_AndCopiesExactly` 失败（"chunk jump layout too slow (median)"，实测 11–24ms，本机阈值 20ms，CI 为 100ms）；单独重跑通过，之后三次全量运行都通过。

**Why:** 断言的是墙钟时间，本机（非 CI）阈值只有 20ms，刚编译完、机器忙或有别的 dotnet 进程时中位数会擦边。它不依赖被测代码的改动。
**How to apply:** 全量测试里只有这一条失败、且消息是 `chunk jump layout too slow` 时，先单独重跑（`--filter "FullyQualifiedName~ThreeThousandLineCode"`）再判断是否回归；别因为它去改渲染代码。若它反复稳定失败，才说明分段排版真的退化了。
