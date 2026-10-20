# 0010 Gallery 应用：自建轻量 Gallery

- 状态：已接受
- 日期：2026-10-20

## 命名

最初称 Demo，2026-10-20 更名为 Gallery：它是浏览与验收全部组件的环境，不是一次性示例，也与 AtomUI 的叫法一致。项目为 `TKCode.Arc.Gallery` 与 `TKCode.Arc.Gallery.Desktop`，目录为 `gallery/`。

## 背景

- 组件需要一个可运行的展示与验收环境：浏览全部组件、切换明暗 / accent / 动效等级 / 语言、查看示例代码，并能做 NativeAOT 发布。
- AtomUI 提供产品中立的 `AtomUI.Toolkits.GalleryBase`（已发布到 NuGet），但对外部 Gallery 有限制：
  - 源码片段生成器 `AtomUI.Toolkits.GalleryBase.Generator` 未发布；
  - 语义部件预览硬编码了 `atom` 前缀与 `atom|Owner` 选择器；
  - 依赖 ReactiveUI、AvaloniaEdit 等额外包。

## 决策

1. 自建轻量 Gallery，不依赖 GalleryBase：
   - `gallery/TKCode.Arc.Gallery`：类库，包含页面、示例与导航；
   - `gallery/TKCode.Arc.Gallery.Desktop`：桌面宿主，可 NativeAOT 发布。
2. Gallery 用 Arc 自己的控件搭建（吃自己的狗粮）：
   - 侧边栏复刻 uiarc.dev：大类分组、计数、折叠；
   - 工具栏切换明暗、accent、动效等级、语言；
   - 示例代码用 Arc 的 CodeBlock 展示；
   - 控件尚未实现时用占位页。
3. 页面与示例显式注册，不使用反射扫描，便于 AOT。
4. 示例 AXAML 同时作为 `AvaloniaResource` 嵌入，用于显示源码，无需额外的生成器。
5. Gallery 也是视觉回归与人工验收的入口；每个组件进入"已实现"状态前，必须在 Gallery 中有展示页。

## 备选方案

- **基于 GalleryBase**：导航、路由、展示页结构与 AtomUI Gallery 一致；但需要自行实现 snippet provider、绕开语义部件预览的前缀限制，并引入 ReactiveUI 等依赖。未选。

## 后果

- 正面：没有外部框架限制；Gallery 完全采用 Arc 的视觉风格，同时验证 Arc 控件在真实组合中的表现。
- 负面：主题 / 语言切换、示例代码展示等基础设施要自己做。早期阶段（CodeBlock 在 P2 才实现）需要临时的源码展示方案。
- 配套：
  - [Gallery 应用](../engineering/gallery-app.md) 定义结构与页面模式；
  - Phase 0 spike S0.9 验证 Gallery 骨架与 AOT 发布。

## 关联文档

- [Gallery 应用](../engineering/gallery-app.md)
- [Phase 0](../roadmap/phase-0.md)
