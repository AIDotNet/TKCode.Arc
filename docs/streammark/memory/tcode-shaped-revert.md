---
name: tcode-shaped-revert
description: TCode 特有的形状（例如 `- [ ]` 任务清单）被 revert 过，不是 StreamMark 不支持 GFM
type: project
updated: 2026-10-16
source: init
evidence: [README.md]
---
早期为 TCode 适配过的、面向单一宿主的 Markdown 形状后来被回退，只保留通用 GFM 行为；不要把最早期提交里的宿主特化行为当作功能需求。

**Why:** 仓库早期提交（`ec4418a` 附近）包含面向 TCode 的渲染调整，之后被有意移除，以免把宿主形状写进通用库。
**How to apply:** 遇到「某个 Markdown 形状渲染不符合预期」时，先看它是不是宿主特化过的形状，再判断要不要为它加规则；加规则前确认它属于通用 GFM 需求。
