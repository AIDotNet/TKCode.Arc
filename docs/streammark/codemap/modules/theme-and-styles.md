---
module: theme-and-styles
sources: ["src/TKCode.Arc.StreamMark/Avalonia/MarkdownResourceKeys.cs", "src/TKCode.Arc.StreamMark/Avalonia/MarkdownResourceAlias.cs", "src/TKCode.Arc.StreamMark/Avalonia/Themes/**", "src/TKCode.Arc.StreamMark/Avalonia/Rendering/MarkdownStyle.cs", "src/TKCode.Arc.Markdown/Content/Markdown/Markdown.cs"]
commit: e01b99a
updated: 2026-10-16
---
# 主题与样式解析（`Md.*` 资源 → `MarkdownStyle` 快照 → 宿主令牌）

StreamMark 的全部外观（颜色、字体、行高、间距、圆角、明暗）都走一套 Avalonia 资源键 `Md.*`：默认值写在 `StreamMarkTheme` 的 Light / Dark 主题字典里，宿主（如 Arc 的 `Markdown` 控件）用同名键或 `MarkdownResourceAlias` 覆盖，`MarkdownStyle.Resolve(control)` 在控件可见的资源作用域内一次性读成不可变快照，由 `MarkdownView` / `TranscriptPanel` 下发给每个块视图。

快照时刻说明：本页写入时工作树 HEAD 已前进到 `349b77f`（`git log`），但本模块的 5 个文件与 `e01b99a` 逐字节相同（`git diff -- src/TKCode.Arc.StreamMark/Avalonia/Themes src/TKCode.Arc.StreamMark/Avalonia/Rendering/MarkdownStyle.cs src/TKCode.Arc.StreamMark/Avalonia/MarkdownResourceKeys.cs src/TKCode.Arc.StreamMark/Avalonia/MarkdownResourceAlias.cs` 无输出），故 front matter 的 `commit: e01b99a` 对本页所有引用成立。

**不负责：** 快照如何被消费到排版与绘制（`avalonia-text-rendering`，`src/TKCode.Arc.StreamMark/Avalonia/Rendering/TextSegmentView.cs:309`、`CodeBlockView.cs:339`）；`RenderContext` 的身份语义与视图重建（`avalonia-text-rendering`，`src/TKCode.Arc.StreamMark/Avalonia/Rendering/RenderContext.cs:12`、`BlockView.cs:27`）；会话虚拟化的行高估算（`avalonia-transcript`，`src/TKCode.Arc.StreamMark/Avalonia/Transcript/TranscriptPanel.cs:392`）；`MarkdownServices` 附加属性本身（`avalonia-plugin-system`，`src/TKCode.Arc.StreamMark/Avalonia/Extensions/MarkdownServices.cs`）；TextMate 主题内部（`avalonia-extensions`，`src/TKCode.Arc.StreamMark.TextMate/ThemeContext.cs:31`）；`IDiagramRenderer` 的 `DiagramRenderContext` 定义（`src/TKCode.Arc.StreamMark/Avalonia/Extensions/IDiagramRenderer.cs:7`，本页只到“谁把 `style.IsDark` 传进去”为止）。

## Entry points
| Symbol | File | Role |
|---|---|---|
| `MarkdownResourceKeys` | `src/TKCode.Arc.StreamMark/Avalonia/MarkdownResourceKeys.cs:7` | 公开契约：24 个 `Md.*` 键常量，宿主覆盖这些键即可换肤 |
| `MarkdownResourceAlias` | `src/TKCode.Arc.StreamMark/Avalonia/MarkdownResourceAlias.cs:8` | `record MarkdownResourceAlias(object Key)`：把一个 `Md.*` 键指向宿主另一个资源键（不写死值） |
| `StreamMarkTheme` | `src/TKCode.Arc.StreamMark/Avalonia/Themes/StreamMarkTheme.axaml:1`、`.axaml.cs:7` | 默认主题：`Styles.Resources` 里的 Light / Dark 主题字典 + 3 个不随明暗变化的键 |
| `MarkdownStyle.Resolve` | `src/TKCode.Arc.StreamMark/Avalonia/Rendering/MarkdownStyle.cs:66` | 唯一的解析入口：从控件可见资源 → 不可变 `MarkdownStyle`（含全部硬编码回退值） |
| `Markdown.s_aliases`（宿主示例） | `src/TKCode.Arc.Markdown/Content/Markdown/Markdown.cs:s_aliases` | Arc 的 `Markdown` 控件：16 个 `Md.*` 键 → `MarkdownResourceAlias(ArcDesignTokenKind.*)`，挂在控件自己的资源上 |

## Key types and functions
| Symbol | File | Role |
|---|---|---|
| `MarkdownStyle` | `src/TKCode.Arc.StreamMark/Avalonia/Rendering/MarkdownStyle.cs:15` | 不可变快照：16 个 `IBrush` + `FontFamily` / `MonospaceFontFamily` / `FontSize` / `LineHeight` / `BlockSpacing` / `CornerRadius` / `IsDark` 为 `required … { get; init; }`，`CodeFontSize` / `CodeLineHeight` 为可选 `{ get; init; }` |
| `MarkdownStyle.Resolve` 内的局部 `Get<T>(key, fallback)` | `src/TKCode.Arc.StreamMark/Avalonia/Rendering/MarkdownStyle.cs:81` | 单个键的解析：`TryFindResource(key, variant)` → 解别名 → `is T` 命中 / `Color` 转 `ImmutableSolidColorBrush` → 否则返回 `fallback` |
| `MarkdownStyle.CodeFontSize` / `CodeLineHeight` | `src/TKCode.Arc.StreamMark/Avalonia/Rendering/MarkdownStyle.cs:46` / `:53` | 可覆盖资源键 `Md.CodeFontSize` / `Md.CodeLineHeight`；未设置时分别回落为 `Math.Round(FontSize * 0.9)` / `Math.Round(CodeFontSize * 1.55)` |
| `MarkdownStyle.HeadingFontSize` | `src/TKCode.Arc.StreamMark/Avalonia/Rendering/MarkdownStyle.cs:46` | 标题字号倍数：1 → 1.75，2 → 1.5，3 → 1.25，4 → 1.1，5/6 → 1.0 |
| `MarkdownStyle.AlertBrush` | `src/TKCode.Arc.StreamMark/Avalonia/Rendering/MarkdownStyle.cs:56` | 提示块强调色：TIP / IMPORTANT / WARNING / CAUTION 硬编码（按 `IsDark` 分两套），其它 kind 返回 `LinkForeground` |
| `MarkdownStyle.HighlightTheme` | `src/TKCode.Arc.StreamMark/Avalonia/Rendering/MarkdownStyle.cs:43` | `IsDark ? Dark : Light` 的 `HighlightThemeKind`，是高亮器 / Mermaid 的明暗入口 |
| `StreamMarkTheme`（XAML 资源） | `src/TKCode.Arc.StreamMark/Avalonia/Themes/StreamMarkTheme.axaml:9` / `:22` / `:36` | 每个主题字典 11 个 `SolidColorBrush`；主题字典之外声明 `Md.MonospaceFontFamily`（:36）、`Md.BlockSpacing`（:37）、`Md.CornerRadius`（:38） |
| `StreamMarkTheme`（代码） | `src/TKCode.Arc.StreamMark/Avalonia/Themes/StreamMarkTheme.axaml.cs:11` | `AvaloniaXamlLoader.Load(this)`，以 `x:Class` 编译加载（NativeAOT 安全，不用运行时 `StyleInclude`，见 :6 注释） |

## Dependencies
- Uses: `core-highlighting` via `HighlightThemeKind`（`MarkdownStyle.HighlightTheme` 的返回类型）— `src/TKCode.Arc.StreamMark/Avalonia/Rendering/MarkdownStyle.cs:7`、`:43`
- Used by: `avalonia-text-rendering` via 读取快照属性（`Style.BlockSpacing` / `Style.LineHeight` / `Style.CodeBackground` / `Style.SelectionBackground`…）— `src/TKCode.Arc.StreamMark/Avalonia/Rendering/BlockView.cs:73`、`src/TKCode.Arc.StreamMark/Avalonia/Rendering/TextSegmentView.cs:311`、`:454`、`src/TKCode.Arc.StreamMark/Avalonia/Rendering/CodeBlockView.cs:339`
- Used by: `avalonia-transcript` via `MarkdownStyle.Resolve(this)` 与 `_style` 缓存 / 失效 — `src/TKCode.Arc.StreamMark/Avalonia/Transcript/TranscriptPanel.cs:232`、`:392`、`:717`、`:219`
- Used by: `avalonia-extensions` via `style.HighlightTheme` 选高亮主题、`style.IsDark` / `style.Foreground` / `style.CodeBackground` / `style.LinkForeground` 组 `DiagramRenderContext` — `src/TKCode.Arc.StreamMark/Avalonia/Rendering/CodeBlockState.cs:51`、`src/TKCode.Arc.StreamMark/Avalonia/Rendering/DiagramBlockView.cs:103`、`src/TKCode.Arc.StreamMark.Mermaid/MermaidDiagramRenderer.cs:132`
- Used by: `avalonia-plugin-system` via 把 `MarkdownStyle` 交给插件（`InlineComponentStyle(style.LinkForeground, …)`、`InlineComponentContext(MarkdownStyle Style, …)`）— `src/TKCode.Arc.StreamMark/Avalonia/Plugins/BuiltInComponentsPlugin.cs:243`、`src/TKCode.Arc.StreamMark/Avalonia/Plugins/IMarkdownPlugin.cs:124`
- Used by: 宿主 / 示例 / 测试 via `Styles.Add(new StreamMarkTheme())`（宿主主题在其后加入）— `README.md:27`、`tools/streammark/Gallery/Program.cs:61`、`tests/TKCode.Arc.StreamMark.Tests/Avalonia/HeadlessApp.cs:20`
- Used by: `tests` via `ThemeSnapshotTests` / `StyleKeysTests` 直接调 `MarkdownStyle.Resolve` 并读 `MarkdownResourceKeys` — `tests/TKCode.Arc.StreamMark.Tests/Avalonia/ThemeSnapshotTests.cs:20`、`tests/TKCode.Arc.StreamMark.Tests/Avalonia/Rendering/StyleKeysTests.cs:20`

## State it owns
- `MarkdownStyle` 实例本身是**不可变、无缓存**的：`Resolve` 每次都 `new` 一份（`MarkdownStyle.cs:100`）。唯一持有处是调用方：`MarkdownView._context`（`MarkdownView.cs:34`，`_context ??= CreateContext()`，:165）与 `TranscriptPanel._style`（`TranscriptPanel.cs:66`，`_style ??= …`，:232 / :717）。
- 重新解析的触发点（置空缓存 + 请求下一帧）：`MarkdownView` 在构造时挂 `ResourcesChanged` / `ActualThemeVariantChanged` → `InvalidateContext()`（`MarkdownView.cs:64`、`:65`、`:172`），`MarkdownServices.ServicesProperty` / `ImageBaseUriProperty` 变化同样（:44–45），进入可视树时也置空（:186）；`TranscriptPanel` 挂同样的两个事件 → `InvalidateStyle()`（`TranscriptPanel.cs:90`、`:91`、`:219`），`MarkdownServices.ServicesProperty` 变化（:96）、`OnAttachedToVisualTree`（:830）也走它，`InvalidateStyle` 还会把每个 entry 的 `Context` 置空（:222–225）；`ImageBaseUri` 在 `ContextFor` 里逐次比较（:235）。
- 别名的作用域规则：`MarkdownResourceAlias` 只存目标键；解析时**从 Markdown 控件本身**再查一次（`control.TryFindResource(alias.Key, variant, …)`，`MarkdownStyle.cs:79`），因此能找到只在窗口 / 主题作用域注册的资源；链长上限 4（:77）。
- 从宿主控件继承的值：`Md.FontFamily` / `Md.FontSize` 缺失时取 `control.GetValue(TextElement.FontFamilyProperty / FontSizeProperty)`（`MarkdownStyle.cs:98`、`:99`）；`Md.LineHeight` 缺失时是 `round(fontSize * 1.6)`——用的是**解析后**（可能继承来的）字号（:119）；`IsDark` 来自 `control.ActualThemeVariant`（:68–69、:122）；`Md.BlockSpacing` 缺失时 12（:120）。
- 无状态的部分：`AlertBrush`（:56）与 `HeadingFontSize`（:46）每次调用现算，`TIP` 等用 `Brushes.*` 静态画刷；`MarkBackground` 的默认值是每次解析新建，非资源值（:106）。

## Contracts it exposes
- `Md.*` 键清单（`MarkdownResourceKeys.cs`，共 24 个），按类别：
  - 文字 / 交互色（9）：`Foreground`、`SecondaryForeground`、`LinkForeground`、`SelectionBackground`、`HoverBackground`、`ListMarkerForeground`（默认 = `SecondaryForeground`）、`MarkBackground`、`SearchHighlightBackground`、`SearchCurrentBackground`。
  - 代码与图表容器（8）：`CodeBackground`、`CodeBorderBrush`、`InlineCodeBackground`、`InlineCodeForeground`（默认 = `Foreground`）、`QuoteBorderBrush`、`TableBorderBrush`、`TableHeaderBackground`、`CornerRadius`。
  - 字体与度量（7）：`FontFamily`、`MonospaceFontFamily`、`FontSize`、`LineHeight`、`CodeFontSize`（默认 = `Math.Round(FontSize * 0.9)`）、`CodeLineHeight`（默认 = `Math.Round(CodeFontSize * 1.55)`）、`BlockSpacing`。
  覆盖范围说明：`Md.TableBorderBrush` 被表格外框与分隔线、行内代码边框、脚注分隔线共用（`BlockView.cs:215`、`ContainerViews.cs:94`、`:320`）；`Md.CodeBackground` / `Md.CodeBorderBrush` 同时用于代码块、图表块与引用来源卡片（`CodeBlockView.cs:339`、`DiagramBlockView.cs:223`、`SourceCard.cs:86`）。
- `MarkdownResourceAlias` 的宿主契约：值可以是任意宿主资源键；目标值类型要与取值处一致（`IBrush` 或 `Color`）；`Color` 仅在 `Get<IBrush>` 时自动转 `ImmutableSolidColorBrush`（`MarkdownStyle.cs:88`）；找不到目标键**不报错**，直接走 `fallback`（:81）。
- 宿主映射示例：Arc 的 `Markdown` 控件（`src/TKCode.Arc.Markdown/Content/Markdown/Markdown.cs:s_aliases`，16 条）把颜色、`MonospaceFontFamily` 与 `CornerRadius` 指向 `ArcDesignTokenKind`；未映射的键（字号、行高、间距）取 `StreamMarkTheme` 默认值或 `Markdown` 自己写入的排版资源。原来的 `StreamMark.Theme.AtomUI` 在并入 Arc（2026-11-19）时删除。
- 默认字体 / 行高约定：默认字典故意**不设** `Md.FontFamily` / `Md.FontSize` / `Md.LineHeight` / `Md.CodeFontSize` / `Md.CodeLineHeight`（`StreamMarkTheme.axaml:5` 注释、`docs/streammark/decisions.md:28`），正文沿用控件继承的 `TextElement` 字体与字号、行高 = 字号 × 1.6，代码字号 = 正文字号 × 0.9、代码行高 = 代码字号 × 1.55；等宽字体默认 `Menlo, Consolas, "DejaVu Sans Mono", monospace`（`StreamMarkTheme.axaml:37`，回退值 `Menlo, Consolas, monospace`）。
- 24 个键中只有 14 个在默认字典里有条目（Light / Dark 各 11 个画刷 + 3 个非主题键），其余 10 个（`FontFamily`、`FontSize`、`LineHeight`、`CodeFontSize`、`CodeLineHeight`、`InlineCodeForeground`、`ListMarkerForeground`、`MarkBackground`、`SearchHighlightBackground`、`SearchCurrentBackground`）故意依赖 `Resolve` 的 fallback。

## How to extend
- To add 一个新的 `Md.*` 键（必须同步的位置，缺一处都有症状）：
  1. `MarkdownResourceKeys` 加 `public const string X = "Md.X";`（`MarkdownResourceKeys.cs:9`）——缺了后面无从引用。
  2. `MarkdownStyle` 加 `required` 属性（`MarkdownStyle.cs:17–37`）并在 `Resolve` 的对象初始化器里 `Get<T>(MarkdownResourceKeys.X, 默认值)`（:95–121）——漏掉属性会直接编译失败（`required` 成员未赋值）。需要宿主写 `Color` 也能用时把 `T` 取成 `IBrush`（走 :88 的转换）。
  3. 在消费点（`src/TKCode.Arc.StreamMark/Avalonia/Rendering/**`）真的读它；只加键不读等于没加。
  4. 默认值写进 `StreamMarkTheme.axaml`：随明暗变化的放 Light（:9）/ Dark（:22）两个主题字典各一条，不随明暗变化的放主题字典之外（参照 `Md.BlockSpacing`，:37）。**漏掉这一处的症状**：`TryFindResource` 返回 false，静默回落到 `Resolve` 里的硬编码 fallback（`MarkdownStyle.cs:72`），宿主即使覆盖了该键也不生效（键从未被任何字典定义时不会报错，只是看起来“没换肤”）。
  5. 可选：在宿主的映射里（Arc：`src/TKCode.Arc.Markdown/Content/Markdown/Markdown.cs:s_aliases`）把键指向宿主 token。**漏掉这一处的症状**：宿主下该键仍取 `StreamMarkTheme` 的默认值（别名缺失 → 键直接命中前面的默认主题）。
  6. 测试模板：`tests/TKCode.Arc.StreamMark.Tests/Avalonia/Rendering/StyleKeysTests.cs:12`（同时断言 fallback 与覆盖生效）。
- To add 一套 XAML 主题字典：复制 `Themes/StreamMarkTheme.axaml` 的结构（`Styles` + `Styles.Resources` + `ThemeDictionaries`），代码侧同样用 `x:Class` + `AvaloniaXamlLoader.Load`（`StreamMarkTheme.axaml.cs:11`，保持 NativeAOT 安全），宿主按 `Styles.Add(new StreamMarkTheme()); Styles.Add(new MyTheme());` 的顺序加入（`README.md:27`）——**必须后加**，否则被默认值盖住（Avalonia 资源按后加入者优先）。
- To add 一套宿主主题：在宿主控件或 `Styles` 的资源里往 `Md.*` 键写 `new MarkdownResourceAlias(宿主键)`（Arc 的写法见 `src/TKCode.Arc.Markdown/Content/Markdown/Markdown.cs:s_aliases`）。不要写 `DynamicResource` 画刷（见下条陷阱）。

## Invariants and traps
- 宿主主题用**资源别名而不是画刷**：资源字典里画刷上的动态资源从字典所在位置向上查找，找不到宿主注册在窗口 / 主题作用域内的 token，文字会变成透明；别名由 StreamMark 从 Markdown 控件本身解析 — `docs/streammark/decisions.md:27`，实现见 `src/TKCode.Arc.StreamMark/Avalonia/Rendering/MarkdownStyle.cs:77`、`MarkdownResourceAlias.cs:4`（注释）。
- `MarkdownStyle.Resolve` 只读一次并下发：`Resolve` 每次返回新实例，消费方靠 `RenderContext` 的**实例身份**判断“样式变了”，原地改快照不会触发任何重排 — `src/TKCode.Arc.StreamMark/Avalonia/Rendering/RenderContext.cs:25`、`src/TKCode.Arc.StreamMark/Avalonia/Rendering/BlockView.cs:27`、`src/TKCode.Arc.StreamMark/Avalonia/Rendering/MarkdownStyle.cs:11`（注释）。
- 主题变体 / 资源变化后必须重新解析：`ResourcesChanged`、`ActualThemeVariantChanged`、`Services`、`ImageBaseUri`、进入可视树，都会置空缓存并重建 — `src/TKCode.Arc.StreamMark/Avalonia/MarkdownView.cs:64`、`:65`、`:172`、`src/TKCode.Arc.StreamMark/Avalonia/Transcript/TranscriptPanel.cs:90`、`:91`、`:219`。反过来说，直接改宿主资源而宿主控件不在同一资源作用域内不会自动生效。
- 未设置 `Md.FontFamily` / `Md.FontSize` 时沿用控件继承的 `TextElement` 字体，行高为字号 1.6 倍 — `src/TKCode.Arc.StreamMark/Avalonia/Rendering/MarkdownStyle.cs:98`、`:99`、`:119`；默认字典也刻意不设这三个键（`src/TKCode.Arc.StreamMark/Avalonia/Themes/StreamMarkTheme.axaml:5`）。注意 `LineHeight` 依赖解析后的字号，宿主改窗口字号会连带改行高。
- 别名链上限是 4 层：第 5 层仍为别名时循环退出，值不是 `T`，直接 `return fallback`（**不报错**）— `src/TKCode.Arc.StreamMark/Avalonia/Rendering/MarkdownStyle.cs:77`、`:92`。
- 解析失败一律静默回退：`TryFindResource` 未命中返回 `fallback`，别名目标未命中同样返回 `fallback` — `src/TKCode.Arc.StreamMark/Avalonia/Rendering/MarkdownStyle.cs:72–74`、`:79–82`。因此键名写错只会表现为“颜色不对”，不会有异常。
- `InlineCodeForeground` / `ListMarkerForeground` 的默认值是 `Foreground` / `SecondaryForeground` 的**同一实例**，覆盖后者会连带改变前者（这也是 `StyleKeysTests` 断言的 `Assert.Same`）— `src/TKCode.Arc.StreamMark/Avalonia/Rendering/MarkdownStyle.cs:104–105`、`tests/TKCode.Arc.StreamMark.Tests/Avalonia/Rendering/StyleKeysTests.cs:21`。
- `StreamMarkTheme` 必须保持 `x:Class` 编译加载（NativeAOT：不用运行时 `StyleInclude`）— `src/TKCode.Arc.StreamMark/Avalonia/Themes/StreamMarkTheme.axaml.cs:6`。
- `AlertBrush` / `HeadingFontSize` 不受资源键控制（提示块颜色、标题倍数都是硬编码），要换肤只能改 `MarkdownStyle` 本身 — `src/TKCode.Arc.StreamMark/Avalonia/Rendering/MarkdownStyle.cs:46`、`:56`。
- 无头测试宿主只加载 `StreamMarkTheme`（`tests/TKCode.Arc.StreamMark.Tests/Avalonia/HeadlessApp.cs:20`），所以主题回归测试跑的是默认字典，不是宿主别名路径（Arc 的别名路径由 `tests/TKCode.Arc.Tests` 的 Markdown 测试覆盖）。

## Tests
- 位置与命名：xUnit v3 + `[Fact]`，测试在 `tests/TKCode.Arc.StreamMark.Tests/Avalonia/`。本模块直接相关的是根下的 `ThemeSnapshotTests.cs`（类名 = 被验证主题场景）与 `Rendering/StyleKeysTests.cs`（类名 = 被测资源键族，方法名描述断言），两者都经 `HeadlessApp.Run` / `HeadlessApp.Session.Dispatch` 在界面线程执行（`tests/TKCode.Arc.StreamMark.Tests/Avalonia/HeadlessApp.cs:55`、`:34`）。
- `ThemeSnapshotTests.DefaultTheme_ResolvesResources_AndRenders`（`tests/TKCode.Arc.StreamMark.Tests/Avalonia/ThemeSnapshotTests.cs:12`）：断言 `window.TryFindResource(Md.Foreground, window.ActualThemeVariant)` 命中且是 `IBrush`（:20–21）、`Md.BlockSpacing` == 12（:22–23）。
- `StyleKeysTests.InlineCodeAndListMarkerColors_FallBack_AndCanBeOverridden`（`tests/TKCode.Arc.StreamMark.Tests/Avalonia/Rendering/StyleKeysTests.cs:12`）：先在裸 `MarkdownView` 上断言两条 `Assert.Same` 回退（:21–22），再往 `view.Resources[...]` 写覆盖值并用 `MarkdownStyle.Resolve(view)` 验证取到覆盖值（:27–36）。
- 只跑一个文件：仓库口径是 `dotnet test TKCode.Arc.slnx`（`README.md:108`）。单文件可用 `dotnet test tests/TKCode.Arc.StreamMark.Tests/Avalonia/StreamMark.Avalonia.Tests.csproj --filter "FullyQualifiedName~StyleKeysTests"`——仓库脚本与 CI 里没有这种写法（unverified，本次未运行任何测试或构建）。
- 没有被测覆盖的部分（本包内）：别名链、`Color→IBrush` 转换与 4 层上限也没有测试。

## Related flows
- [theme-resolution](../flows/theme-resolution.md)
- [streaming-token-to-view](../flows/streaming-token-to-view.md)
