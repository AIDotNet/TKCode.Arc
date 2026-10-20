# Arc 站点快照（2026-10-20）

> 记录确定组件范围时 Arc 网站的状态：总数、Pro 排除项、各大类计数、URL 规则与更新方法。Arc 后续新增组件时，用本文作为对比基线。

## 1. 来源

| 来源 | 地址 | 说明 |
|---|---|---|
| 侧边栏 | `https://uiarc.dev/components/button` 页面中的组件库侧边栏 | 大类分组与计数的依据（与用户提供的截图一致） |
| llms.txt | `https://uiarc.dev/llms.txt` | 总览：170 个组件、94 个区块；免费 114 个组件与 22 个区块 |
| catalog.json | `https://uiarc.dev/r/catalog.json`（generatedAt 2026-10-19T13:01:30Z） | 每个组件的 whenToUse、motion、accessibility 等结构化字段 |
| 组件文档 | `https://uiarc.dev/components/<slug>/markdown` | 属性、键盘、可访问性、动效、响应式、性能 |
| 注册表 | `https://uiarc.dev/r/<slug>.json` | 免费组件源码（TSX + CSS module）与 registryDependencies |
| 许可 | `https://uiarc.dev/license` | 免费部分 MIT（Copyright (c) 2026 Elia Kuratli）；Pro 为商业许可 |

注意：catalog.json 的 `category` 字段（Actions、Inputs、Disclosure、Feedback、Data、Text、Special）与侧边栏分组不同。本项目按**侧边栏分组**组织代码。

## 2. 侧边栏统计

侧边栏共 23 个分组、123 个组件，其中 13 个为 Pro；去掉 Pro 后剩 22 个分组、110 个免费组件。

| 侧边栏分组 | 总数 | Pro | 在范围内 |
|---|---|---|---|
| Buttons | 10 | 0 | 10 |
| Gestures | 3 | 0 | 3 |
| Menus | 3 | 0 | 3 |
| Text fields | 7 | 0 | 7 |
| Special inputs | 7 | 0 | 7 |
| Selects | 5 | 0 | 5 |
| Toggles | 6 | 0 | 6 |
| Sliders | 2 | 0 | 2 |
| Pickers | 6 | 1（Emoji picker） | 5 |
| Editors | 4 | 1（Avatar upload） | 3 |
| Navigation | 4 | 1（Mega menu morph） | 3 |
| Expand | 5 | 0 | 5 |
| Overlays | 7 | 1（Share access） | 6 |
| Messages | 5 | 0 | 5 |
| Progress | 5 | 0 | 5 |
| Avatars | 4 | 1（Presence stack） | 3 |
| Cards | 3 | 0 | 3 |
| Charts | 14 | 1（Split bar） | 13 |
| Advanced charts | 7 | 7（Sunburst、Sankey flow、Funnel chart、Radar chart、Realtime stream、Race bar chart、Data grid） | 0 |
| Tables | 6 | 0 | 6 |
| Activity | 3 | 0 | 3 |
| Media | 3 | 0 | 3 |
| Text effects | 4 | 0 | 4 |
| **合计** | **123** | **13** | **110** |

在范围内的组件清单见 [组件总清单](overview.md)。

## 3. 侧边栏之外的免费目录项

catalog.json 中有 114 个免费组件，比侧边栏多 4 个：

| slug | 说明 | 处理 |
|---|---|---|
| `theme-switch-eclipse` | ThemeSwitcher 的 Eclipse 变体：新外观像日食一样扫过页面 | 并入 ThemeSwitcher |
| `theme-switch-split` | ThemeSwitcher 的 Split 变体：新外观从中缝展开 | 并入 ThemeSwitcher |
| `theme-switch-rise` | ThemeSwitcher 的 Rise 变体：新外观升起就位 | 并入 ThemeSwitcher |
| `slot-text` | 文字与数字以交错的老虎机滚轮切换到新值（catalog 分类 Special） | 不在本期范围；列为候选，可在 TextEffects 或 Charts 的 RollingDigits 基础上低成本实现 |

## 4. URL 规则

| 用途 | 规则 |
|---|---|
| 组件页面 | `https://uiarc.dev/components/<slug>` |
| 组件 markdown | `https://uiarc.dev/components/<slug>/markdown` |
| 注册表项（免费） | `https://uiarc.dev/r/<slug>.json` |
| foundation Token | `https://uiarc.dev/r/arc-foundation.json` |
| 动效 Token | `https://uiarc.dev/r/arc-motion-tokens.json` |
| 设计技能文档 | `https://uiarc.dev/r/skills/arc/{design,motion,accessibility,components,composition,responsive,copy}.md` |

## 5. 更新方法

1. 抓取侧边栏（只读），按分组提取组件链接与 Pro 标记。
2. 与本文第 2 节对比：新增的免费组件先加入 [组件总清单](overview.md) 并分配阶段，再更新对应大类 overview。
3. Pro 组件只记录名称，不抓取源码与文档内容。
4. 更新本文的日期与统计。
