# 记忆库怎么运作

本目录记录读代码看不出来的上下文。规则属于 `docs/streammark/agent-guide.md`；对代码的描述属于 `docs/streammark/codemap/`。
记忆装的是其余部分：为什么是这样，以及发生过什么。

## 类型
- `project` — 决定及其理由、被否决的方案、事故、进行中的工作（用绝对日期）。
- `reference` — 外部资源及其用途。
- `feedback` — 纠正，或经过确认的、对所有人适用的非显然做法。
- `user` — 个人偏好。永远不放这里：它们不放在仓库里。

## 条目格式
一个事实一个文件，文件名即 `name`。frontmatter：`name`、`description`、`type`、`updated`、
`source`（`init` 或 `session`）、`evidence`。正文：事实本身，然后 **Why:** 与 **How to apply:**。
相关条目用 `[[name]]` 互链。在 MEMORY.md 中按类型补一行。

## 维护
- 新增前先找有没有覆盖同一件事的条目，有就更新它。
- 相对日期一律换算成绝对日期。给出证据：提交、`path:Symbol`、文档或链接。
- 一旦发现某个条目是错的，立刻删掉它并移除索引行。
- 条目描述的是它写下的那个时点。依赖之前先核实它引用的路径、符号和命令。
- 当某个 `feedback` 条目被再次确认且适用于所有人时，提议把它升格为 agent-guide.md 的规则；
  不要自己动手改 agent-guide.md 的规则。
- 记忆的改动和别的改动一样出现在 diff 里。除非被要求，不要单独提交它们。
