---
name: upstream-and-license-references
description: 上游依赖、调研报告与许可文本的出处；改依赖或许可文本前先读这里
type: reference
updated: 2026-10-16
source: init
evidence: [THIRD-PARTY-NOTICES.md, README.md, docs/streammark/research/, .gitmodules]
---
- RaTeX（原生公式引擎）—— https://github.com/erweixin/RaTeX ，按 `native/ratex/ratex-upstream.json` 固定的提交由 `tools/streammark/build-ratex.sh` 检出。
- remend —— 移植自 vercel/streamdown 的 `packages/remend`（Apache-2.0，remend 1.4.0）；C# 与 TypeScript 的刻意差异记在 `src/TKCode.Arc.StreamMark/Core/Remend/DEVIATIONS.md`。
- 上游生态调研：`docs/streammark/research/01-streamdown.md` … `05-native-streaming.md`（含 @lobehub/streamdown、smd、Incremark、Zed、Codex CLI、Gemini CLI 等的做法），结论汇总在 `docs/streammark/decisions.md`。
- 许可与第三方声明：`LICENSE`（MIT）、`THIRD-PARTY-NOTICES.md`。

**Why:** 这些是仓库外的真相来源，出问题时要去上游看，而不是只看本地代码。
**How to apply:** 改依赖版本、重新移植上游代码或加入新的第三方组件时，同时更新 `THIRD-PARTY-NOTICES.md` 与 `docs/streammark/decisions.md`，并在本条目里补链接。
