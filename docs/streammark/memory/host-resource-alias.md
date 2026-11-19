---
name: host-resource-alias
description: 宿主主题必须用 MarkdownResourceAlias 而不是画刷，否则文字会变透明
type: project
updated: 2026-11-19
source: session
evidence: [src/TKCode.Arc.StreamMark/Avalonia/MarkdownResourceAlias.cs, src/TKCode.Arc.Markdown/Content/Markdown/Markdown.cs:s_aliases, docs/streammark/decisions.md]
---
宿主往 `Md.*` 键里写的是 `MarkdownResourceAlias(宿主键)`，由 StreamMark 在 Markdown 控件自身的作用域解析，而不是直接写画刷或 `DynamicResource`。Arc 的 `Markdown` 控件（`TKCode.Arc.Markdown`）就是这样把 `Md.*` 指向 `ArcDesignTokenKind` 的。

**Why:** 资源字典里画刷上的动态资源从字典所在位置向上查找，找不到注册在窗口 / 主题作用域内的宿主 token，结果是文字变成透明。最早在 AtomUI 宿主下发现；StreamMark 并入 Arc（2026-11-19）时删掉了 AtomUI 主题包，规则对任何宿主主题都成立。
**How to apply:** 写宿主主题或新的 `Md.*` 映射时用别名；宿主主题要注册在 `StreamMarkTheme` 之后。
