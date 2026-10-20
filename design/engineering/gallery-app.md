# Gallery 应用

> 自建轻量 Gallery 的结构、布局与导航、展示页模式、Appearance 与发布方式。决策见 [ADR 0010](../decisions/0010-gallery-app.md)。

## 1. 目标

- 浏览全部组件，未实现的组件显示占位页。
- 在明暗、accent、动效等级、语言、减少透明度之间实时切换，用于验收。
- 外观与交互复刻 uiarc.dev/components，对照 Arc 原站验收。
- 展示示例代码。
- 作为 NativeAOT 发布的验证载体（S0.5、S0.9）。
- 用 Arc 自己的控件搭建 Gallery 本身，验证真实组合场景。

## 1.1 当前实现状态（2026-10-20）

- 整体布局复刻 uiarc.dev/components：顶栏、左侧组件侧边栏、组件页（含右侧 "On this page"）。详见下文第 3、4 节。
- 窗口是 AtomUI 的 `Window`；组件清单是生成的 `Catalog/GalleryCatalog.g.cs`（与 [组件总清单](../components/overview.md) 一致，测试校验 110 / 22）。
- 页面是 C# 工厂（`Pages/<Category>/<Component>Page.cs` 的 `Create()`），用 `Shell/GalleryUi.cs` 的 `Card` / `Row` / `Column` / `Text` 搭建；每个大类在 `Pages/<Category>/<Category>Pages.cs` 中实现 partial 方法 `Register<Category>()` 登记页面，没有反射。
- 尚未实现：页面的 "Code" 标签（等 CodeBlock 完成后接入）、语言切换、减少透明度开关、开发者面板、视口宽度预览。
- Gallery 的截图验收：用 Avalonia.Headless + Skia 渲染 `GalleryWindow` 即可得到真实像素（`UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })` 加 `UseSkia()`，`window.CaptureRenderedFrame()`），不需要屏幕录制权限。

## 2. 项目结构

```
gallery/
├── TKCode.Arc.Gallery/              类库（net10.0;net11.0）
│   ├── Shell/                       外壳（见下表）
│   ├── Catalog/                     GalleryCatalog：大类与组件清单（生成）
│   ├── Pages/
│   │   ├── GalleryPages*.cs         页面注册表（显式注册）与页面创建
│   │   ├── Buttons/ButtonPage.cs
│   │   ├── Toggles/SwitchPage.cs
│   │   └── …                        与 src 的大类目录镜像
│   ├── Examples/                    （规划）示例 AXAML，同时作为 AvaloniaResource 嵌入，用于显示源码
│   └── Localization/                （规划）Gallery 自身文案（en-US / zh-CN / zh-TW）
└── TKCode.Arc.Gallery.Desktop/      可执行宿主（Program.cs、AOT 发布配置、发布脚本）
```

`Shell/` 中的类型：

| 类型 | 职责 |
|---|---|
| `GalleryWindow` | 窗口：顶栏 + 侧边栏 + 页面宿主；`Cmd/Ctrl+K` 聚焦搜索 |
| `GalleryTopBar` | 品牌、"Components" 标签、搜索快捷入口、Appearance 弹层、明暗切换 |
| `GalleryAppearancePanel` | Appearance 弹层内容：Mode、Accent（8 个预设）、Motion（Full / Reduced / Off） |
| `GallerySidebar` | 搜索框、All / Ready / Planned 筛选、22 个可折叠分组、进度卡片 |
| `GalleryPageView` | 组件页：面包屑、标题、说明、用法与状态、Preview 标签、示例、"On this page" |
| `GalleryToc` | "On this page" 列表与滚动联动 |
| `GalleryNavRow` | 通栏导航行（侧边栏分组 / 条目、目录项、链接） |
| `GallerySegmented` | 胶囊分段选择，选中块弹簧滑动（P7） |
| `GalleryUi` | 示例页的搭建工具：`Card`（一个示例，也是 "On this page" 的一项）、`Row`、`Column`、`Text` |
| `GalleryTheme` | 当前明暗、accent、动效等级，经 AtomUI 应用 |
| `GalleryIcons` | 见下方"已知问题" |

依赖：`AtomUI.Desktop.Controls`、`TKCode.Arc`、`TKCode.Arc.Icons.Lucide`、`TKCode.Arc.Fonts`、`Avalonia.Fonts.Inter`。不依赖 ReactiveUI 或 GalleryBase。

## 3. 布局与导航

整体与 uiarc.dev 一致：

```
+-- top bar --------------------------------------------------------------+
| (arch) Arc [Gallery]  Components             [Search] [Appearance] (sun) |
+-- sidebar ------+-- page ----------------------------+-- On this page --+
| [Search   Cmd K]| Components > Button                |  | Variants      |
| (All|Ready|Plan)| Button                             |    Sizes         |
| Components  110 | summary sentence                   |    ...           |
| v Buttons    10 | $ <arc:Button />  * Ready  [link]  |                  |
|  | Button       | Preview | Code          Live       | View on uiarc.dev|
|    Action button| Example title / guidance           | Back to top      |
| > Gestures    3 | +-- stage (soft rounded panel) --+ |                  |
| ...             | +--------------------------------+ |                  |
| [* Implemented  8 of 110 >]                                              |
+-----------------+------------------------------------+------------------+
```

顶栏：

- 品牌标记、当前栏目 "Components"（2px 下划线压在顶栏底边上）、右侧的搜索快捷入口（聚焦侧边栏搜索）、Appearance 按钮（弹层，见第 5 节）、明暗切换图标。

侧边栏（宽 280）：

- 搜索框按标题、类名、slug 过滤；过滤时各分组自动展开，只显示有匹配的分组，计数随之变化；`Enter` 打开第一个匹配，`Esc` 清空，`↑ ↓` 在可见条目间移动选中项。
- All / Ready / Planned 分段选择：全部 / 已实现 / 规划中（"已实现"= 注册了展示页）。
- "Components" 总行可折叠整个列表；22 个分组可单独折叠，展开 / 收起用 P6 高度动画，分组标题旁是计数。
- 条目挂在一条竖线（rail）上，当前项由一根沿 rail 滑动的竖条标记（P7）；规划中的条目显示阶段（P3 …）并使用静音色。
- 底部进度卡片显示已实现数量，点击等同于选择 Ready 筛选。
- 列表底部淡出到卡片，不会被硬切。

页面：见第 4 节。

- `GalleryCatalog` 显式注册每个大类与组件，页面经 `GalleryPages.Register("Button", ButtonPage.Create)` 登记，不使用反射扫描页面（AOT）。
- 组件清单与 [组件总清单](../components/overview.md) 一致，由测试校验数量（110 / 22）。

## 4. 展示页模式

`GalleryPages.Create(entry)` 返回 `GalleryPageView`，页面由外壳统一搭建，各组件的展示页只提供示例本身：

| 区域 | 内容 |
|---|---|
| 页头 | 面包屑、组件名、一句话说明 |
| 用法与状态 | 可复制的 `<arc:Class />` 标签（仅已实现的组件）、状态芯片（Ready / Planned · Pn）、"View on uiarc.dev" |
| 标签 | Preview（实时示例）；Code 暂为禁用状态，提示将随 CodeBlock（P2）提供 |
| 示例 | 每个示例一个 `GalleryUi.Card`：标题、说明、舞台（柔和圆角面板，Arc 的预览区外观）；`Row` 在舞台上居中，其他内容撑满舞台宽度 |
| On this page | 由页面中的 `Card` 自动生成，至少两个示例且页面宽度 ≥ 960 时显示；滚动联动高亮，点击以弹簧滚动到对应示例 |
| 占位页 | 未实现的组件显示一个 "Planned" 示例，说明所属阶段 |

规则：

- 示例只使用公开 API；需要定制时使用语义部件 Style（`arc.*`），不在 code-behind 中改模板部件。
- 示例文案遵循 Arc 规则：句子式大小写，按钮用"动词 + 对象"。
- 一个示例一个 `Card`；标题要能单独读懂，因为它同时是 "On this page" 里的条目。
- 状态矩阵（尺寸 × 状态的静态排列）、动效演示按钮照常放进 `Card` 里。

## 5. Appearance 与工具

| 控件 | 位置 | 作用 | 实现 |
|---|---|---|---|
| 明暗图标 | 顶栏 | Light / Dark 一键切换 | `GalleryTheme.ToggleDarkAsync` → `IThemeManager.ApplyThemeAsync`；P2 之后换成 ThemeSwitcher 组件本身（含 Eclipse / Split / Rise 过渡） |
| Mode | Appearance 弹层 | Light / Dark | 同上 |
| Accent | Appearance 弹层 | 8 个预设（当前明暗下的真实 accent 色） | `ThemeConfigBuilder.WithArc(a => a.Accent(...))` |
| Motion | Appearance 弹层 | Full / Reduced / Off | 窗口上的 `ArcMotion.Level` |
| 减少透明度 | （未实现） | 开 / 关 | `ArcEffects.ReduceTransparency` |
| 语言 | （未实现） | en-US / zh-CN / zh-TW | AtomUI 语言切换 |
| 开发者面板（Debug） | （未实现） | 动效诊断（活动值数量、每帧耗时）、焦点可视化 | `ArcMotion.Diagnostics` |

弹层是 `ArcPopup`，锚定在 Appearance 按钮下方，点击按钮再次点击关闭。

## 6. 示例代码展示

- 示例 AXAML 文件同时作为 `AvaloniaResource` 嵌入，运行时按资源路径读取并显示，无需源码生成器。
- P2 之前用等宽字体的 `SelectableTextBlock` 显示；CodeBlock（P2）实现后替换为 CodeBlock（含复制按钮与语法高亮）。

## 7. 发布

- `gallery/TKCode.Arc.Gallery.Desktop` 提供 NativeAOT 发布配置与脚本，分别发布 net10.0 与 net11.0。
- CI 在 Windows x64、macOS arm64、Linux x64 上发布并做冒烟启动：打开窗口、切换主题、触发一个动画后退出。
- 发布产物用于各阶段的人工验收。

## 8. 视觉对照工作流

1. 在 Arc 网站上截取组件在 neutral 与 violet、明与暗四种组合下的截图（固定窗口宽度与缩放比例）。
2. 在 Gallery 中打开对应示例，使用相同尺寸与缩放截图。
3. 做像素差对比（容差在 S0.2 中确定），差异超出容差时记录到组件规格的未决问题。
4. 动效用录屏逐帧对比关键帧。

## 9. 已知问题

- `ArcIcons`（由 `tools/lucide/generate_lucide.py` 生成）里，由多个图形组成的 Lucide 图标被拼成一条路径，后续图形开头的 `m` 仍是相对坐标，会从上一个图形的终点起笔，绘制错位。Core 的 187 个图标中有 47 个受影响（例如 `X`、`Sun`、`Code`、`CircleCheck`、`ArrowRight`）。生成器应把每个图形开头的 `m` 改写成绝对的 `M`（其后的隐式坐标对改成相对 `l`）并重新生成；重新生成需要 lucide-static 包。在此之前，Gallery 外壳用到的 `Sun`、`Code`、`X` 使用 `Shell/GalleryIcons.cs` 中改好的路径数据。
