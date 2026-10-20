# TKCode.Arc 设计文档总览

> TKCode.Arc 是基于 AtomUI（Avalonia 12）的扩展组件库，目标是在 .NET 桌面应用中复刻 Arc（https://uiarc.dev）免费组件的视觉风格与动效。本目录是它的全部方案设计。

## 1. 目标

- 把 Arc 的 110 个免费组件移植到 Avalonia。Arc 原版是 React + CSS 变量 + Motion。
- 视觉、交互、键盘行为与动效要与原版一致。
- 以 AtomUI 为底座：主题管理、Token 体系、包注册、浮层、图标、本地化都复用 AtomUI 的公开能力，不重复造轮子。
- 控件代码按 **大类目录 → 组件目录 → 实现文件** 组织，与 Arc 网站侧边栏的 22 个大类一一对应。
- 库目标框架为 `net10.0;net11.0`，桌面平台为 Windows、macOS、Linux。

## 2. 范围与非目标

| 在范围内 | 不在范围内 |
|---|---|
| Arc 侧边栏 22 个大类下的 110 个免费组件（见 [组件总清单](components/overview.md)） | 13 个 Pro 组件（含整组 "Advanced charts"）：受 Arc Pro 许可约束，不抓取、不复刻 |
| ThemeSwitcher 的 Eclipse / Split / Rise 三种变体（属于同一组件） | Arc 的 Blocks（页面级组合）与 Templates |
| Arc 的设计 Token、动效 Token 与设计规则 | Browser（WebAssembly）发布：AtomUI 的 Browser 链路锁定 SDK 10.0.300 + net10-browser |
| 自建 Gallery 应用 | 移动端：AtomUI.Mobile.Controls 尚未存在 |
| | Slot text：Arc 目录中的免费组件，但不在侧边栏，列为候选（见 [站点快照](components/arc-catalog-snapshot.md)） |

## 3. 关键决策摘要

每项决策的背景、备选方案与后果见 [决策记录](decisions/overview.md)。

| 主题 | 结论 | ADR |
|---|---|---|
| AtomUI 依赖 | NuGet + 中央包管理；AtomUI.* 精确锁定 `[6.2.6]`，Avalonia* 锁定 `[12.1.4]` | [0001](decisions/0001-atomui-dependency.md) |
| 与 AtomUI 控件的关系 | 独立实现：派生自 Avalonia 基类，只复用 AtomUI 公开基础设施 | [0002](decisions/0002-independent-controls.md) |
| 程序集 | `TKCode.Arc.Core` + `TKCode.Arc` + `TKCode.Arc.Icons.Lucide` + `TKCode.Arc.Fonts` | [0003](decisions/0003-assemblies-and-packages.md) |
| 命名与命名空间 | 无 Arc 前缀；扁平命名空间 `TKCode.Arc`；xmlns `https://github.com/AtomUICommunity/TKCode.Arc`；语义部件 Path 以 `arc.` 开头 | [0004](decisions/0004-naming-and-namespaces.md) |
| 设计 Token | 载体控件 `ArcDesign` 承载全局语义 Token；accent 通道为 ArcDesign identity 上的 `ColorPrimary` | [0005](decisions/0005-arcdesign-token.md) |
| 动效 | 自研弹簧引擎（与 Motion 的 `physical(visualDuration, bounce)` 等价）+ ControlTheme 可声明的弹簧过渡；Full / Reduced / Off 三级 | [0006](decisions/0006-motion-engine.md) |
| 文档节奏 | 本期写架构与清单；每个组件的详细规格在其所属阶段开始前编写 | [0007](decisions/0007-docs-before-code.md) |
| 许可证 | MIT；保留 Arc、Lucide、Geist、Inter 的第三方声明 | [0008](decisions/0008-licensing.md) |
| 目标框架 / SDK | `net10.0;net11.0`；global.json 锁 SDK 11 RC2；构建机需 .NET 10 运行时 | [0009](decisions/0009-tfm-and-sdk.md) |
| Gallery 应用 | 自建轻量 Gallery，用 Arc 自己的控件搭建 | [0010](decisions/0010-gallery-app.md) |

## 4. 阅读顺序

1. 本文与 [术语表](glossary.md)。
2. [决策记录](decisions/overview.md)：先理解为什么这样设计。
3. [架构总览](architecture/overview.md)，然后按需阅读：
   - [主题与 Token](architecture/theming/overview.md)
   - [动效系统](architecture/motion/overview.md)
   - [AtomUI 集成](architecture/atomui-integration.md)、[解决方案布局](architecture/solution-layout.md)、[目标框架](architecture/target-frameworks.md)
   - [非动效基元](architecture/primitives.md)、[字段结构](architecture/forms-and-fields.md)、[浮层与输入](architecture/overlays-and-input.md)、[图标与字体](architecture/icons-and-fonts.md)、[可访问性](architecture/accessibility.md)、[本地化](architecture/localization.md)
4. [组件总清单](components/overview.md) 与 22 个大类的 overview；写规格时使用 [规格模板](components/spec-template.md)。
5. [路线图](roadmap/overview.md) 与 [Phase 0](roadmap/phase-0.md)。
6. [工程规范](engineering/overview.md)：编码、测试、Gallery、打包、许可、文档校验。

## 5. 目录结构

| 目录 | 内容 |
|---|---|
| [decisions/](decisions/overview.md) | 架构决策记录（ADR） |
| [architecture/](architecture/overview.md) | 分层、集成、Token、动效、基元、浮层、图标字体、可访问性、本地化 |
| [components/](components/overview.md) | 组件总清单、站点快照、规格模板、22 个大类 overview、图表基础 |
| [roadmap/](roadmap/overview.md) | 分期路线图与 Phase 0 spike |
| [engineering/](engineering/overview.md) | 编码、测试、Gallery、打包发布、许可、文档校验 |

## 6. 文档状态

| 文档 | 状态 |
|---|---|
| 决策记录 0001–0010 | 已确认（用户已选定全部方案） |
| 架构、主题、动效、基元文档 | 设计稿，部分结论待 Phase 0 spike 验证（见 [phase-0.md](roadmap/phase-0.md)） |
| 组件总清单与 22 个大类 overview | 设计稿：组件范围、阶段、依赖已确定；API 为草案 |
| 组件详细规格 | 未开始：在各组件所属阶段开始前按 [规格模板](components/spec-template.md) 编写 |

## 7. 跨组件待决事项

起草过程中发现、需要在后续阶段决定的跨组件问题。每项在指定规格中关闭，涉及架构的决定新增 ADR。

| 事项 | 选项 | 在哪里决定 |
|---|---|---|
| 日期与时间的 API 类型 | `DateTime?` / `TimeSpan?`，或 `DateOnly?` / `TimeOnly?` | Calendar（P1） |
| 共享字段外框 `FieldFrame` 是否公开 | internal / public | DatePicker（P2） |
| 键盘快捷键标签 `Kbd` 的位置：DropdownMenu（P5）需要它，ShortcutRecorder（P6）也导出它 | Core 基元 / Arc `Primitives/` | DropdownMenu（P5） |
| 组件导出的辅助公开类型（如 ShortcutRecorder 的 ShortcutList、BillingToggle 的 BillingPrice）是否作为公开控件（公开控件都需要自己的 Token） | 公开 / 内部 | 各组件规格 |
| Markdown / HTML 解析：自研，或引入 Markdig / AngleSharp | 自研 / 第三方（需评估 AOT 与许可） | RichTextEditor 预研（P9 前），新增 ADR |
| RichTextEditor 技术预研：块模型、选区、IME、撤销、可访问性（Avalonia 没有 ITextProvider） | — | P8 结束前完成 |
| 是否提供 Arc 编译型主题包（ArcNeutral、ArcViolet 等） | 提供 / 只提供 C# 辅助 | Gallery（P0–P2） |
| 系统级减少动效探针 | 启用 / 不启用 | P0 之后评估 |
| ThemeSwitcher 的页面过渡辅助：截取窗口快照，经 `IThemeManager` 切换主题后，用裁剪动画揭示新外观；控件是否直接调用 `IThemeManager`（Arc 原版只报告、由应用切换） | — | P2 开始前做技术验证 |
| 在线状态点 `PresenceDot`：UserMenu 导出，应与 Avatar 的状态点统一 | 模板部件 / 公开控件 | Avatar（P1） |
| 共享菜单项模型的命名与结构（DropdownMenu、ContextMenu、UserMenu、SplitButton、ButtonGroup 共用） | — | DropdownMenu（P5） |
| nuget.org 上 `AtomUI.` 包 ID 前缀的归属 | 获得授权 / 调整包 ID | 0.1.0-preview 发布前 |

## 8. 文档约定

- 正文使用中文；类名、属性名、Token 名与代码保持英文。
- 每个目录的入口文件叫 `overview.md`，不使用 `index.md`。
- 每条事实只在一处维护，其他文档用相对链接引用。单文件超过约 400 行时按职责拆分。
- 文档描述设计意图；实现落地后，以代码与产品文档为准，并回写本目录中失效的结论。
- 校验规则见 [文档校验](engineering/docs-verification.md)。
