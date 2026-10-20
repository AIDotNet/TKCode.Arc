# 图标与字体

> Lucide 图标包的生成方式与描边规则，以及 Geist / Inter 字体的打包与注册。

## 1. 图标：Lucide

### 1.1 背景

- Arc 使用 lucide-react（70 个免费组件依赖它），图标尺寸 16 / 20 / 24，常用 `strokeWidth={1.75}`。
- 部分组件使用其他描边值：DropdownMenu 15px 图标用 1.8，Toast 的自定义字形用 2 与 2.25。
- Arc 源码不使用 `absoluteStrokeWidth`。因此描边宽度以 24 单位的 viewBox 为基准，**随图标尺寸缩放**：16px 图标上 1.75 的描边实际为 1.75 × 16 / 24 ≈ 1.17px。
- AtomUI 的图标运行时（`src/AtomUI.Core/Controls/Icon/`）支持描边与全部 SVG 基本形状（Path、Rect、Circle、Ellipse、Line、Polyline、Polygon 的绘制指令），提供 `StrokeWidth`、`StrokeLineCap`、`StrokeLineJoin`、`StrokeBrush` 属性。但它的画笔宽度不会随几何变换缩放。
- AtomUI 的 AntDesign 图标生成器只输出 Path 指令，并按 `{Name}{ThemeType}` 命名（如 `ArrowUpOutlined`），不适合直接用于 Lucide。

### 1.2 生成器

- 工具：`tools/lucide/generate_lucide.py`（Python 3），用法 `python3 -I tools/lucide/generate_lucide.py <lucide-static 包目录> <仓库根目录>`。
- 输入：lucide-static 的 `icons/*.svg`（ISC 许可）。版本号写入生成文件头部注释。
- 处理：把每种 SVG 元素（path、circle、ellipse、rect、line、polyline、polygon）换算成路径数据，拼成一个 24 × 24 viewBox 的描边路径。
- 输出两个文件：
  - `src/TKCode.Arc.Core/Glyphs/ArcIcons.g.cs`：Core 内置子集（脚本中的 `CORE_SUBSET`，当前 187 个），每个图标是 `ArcIcons` 上的一个 `Geometry` 静态属性；
  - `src/TKCode.Arc.Icons.Lucide/LucideIcons.g.cs`：完整图标集（当前 2155 个；PascalCase 同名的别名只保留第一个），即 `LucideIconKind` 枚举与 `LucideIconData.Get(kind)`。
- 命名为 `{PascalCase(name)}`（如 `ChevronDown`），不加主题后缀。
- 没有采用 AtomUI 的 `AbstractIconPackageGenerator`：它只输出 Path 指令，并会套用 AntDesign 的缩放逻辑。

### 1.3 描边规则

- 绘制控件是 Core 的 `ArcGlyph`（`Data`、`Size`、`StrokeThickness`、`Foreground`）。`StrokeThickness` 以 viewBox 单位表示，默认 1.75，端点和连接都是圆头。
- 渲染画笔宽度 = `StrokeThickness × Size / 24`。

| 尺寸 | Token | 渲染描边（StrokeThickness = 1.75） |
|---|---|---|
| 16 | `GlyphSizeSM` | 1.17 |
| 20 | `GlyphSizeMD` | 1.46 |
| 24 | `GlyphSizeLG` | 1.75 |

- 颜色默认继承 `TextElement.Foreground`（对应 lucide 的 currentColor）。

### 1.4 Core 内置字形与完整图标包

- Core 内置字形是公开的（`ArcIcons.Check`、`ArcIcons.ChevronDown` …），覆盖 Arc 110 个免费组件源码中引用的全部 Lucide 图标，外加常用的演示图标。`TKCode.Arc` 不依赖完整图标包。
- 不在子集里的图标：用 `ArcIcons.Parse(pathData)`（按内容缓存），或引用 `TKCode.Arc.Icons.Lucide`。
- 完整图标包（xmlns `https://github.com/AtomUICommunity/TKCode.Arc/lucide`，前缀 `lucide`）提供：
  - `LucideIcon` 控件（派生自 `ArcGlyph`）：`<lucide:LucideIcon Kind="Sparkles" Size="20" />`；
  - `LucideGeometry` 标记扩展，用于接受 `Geometry` 的属性：`Icon="{lucide:LucideGeometry Kind=Trash2}"`；
  - `Lucide.Geometry(kind)`：代码中取几何。

### 1.5 验证（S0.8）

- 每种元素类型至少一个样例，在 16 / 20 / 24 下与 lucide 参考渲染（浏览器截图）做像素差，差异在阈值内。
- 记录包体积，以及 Core 内置字形子集的体积。

## 2. 字体

### 2.1 使用规则（来自 Arc design.md）

| 字体 | 用途 | 字重 | 字距 | 行高 |
|---|---|---|---|---|
| Geist | 30px 及以上的标题（`DisplayFontFamily`） | 400 / 500 | −0.03em | 1.1 |
| Inter | 正文、标签、控件、小标题（`BodyFontFamily`） | 400 / 500 | −0.01em | 1.4 |

- 不在控件与小标签中使用 Geist，不在大标题中使用 Inter。
- 字号阶梯为 12、14、16、18、22、28、36、52、72，不小于 12。
- 只用 400 与 500 字重，不用 600 或粗体。
- Avalonia 的 `LetterSpacing` 与 `LineHeight` 是绝对值（DIP），使用时按字号换算：`LetterSpacing = tracking × FontSize`，`LineHeight = leading × FontSize`。为此提供 `ArcTypography` 辅助类或附加属性 `ArcText.Role`（Display / Body）统一设置。

### 2.2 打包与注册

| 字体 | 方案 |
|---|---|
| Inter | 复用 `Avalonia.Fonts.Inter` 12.1.4（`WithInterFont()`，键 `fonts:Inter`，含 Regular、Medium 等字重）。Arc 不重复打包，避免 `fonts:Inter` 键冲突 |
| Geist | `TKCode.Arc.Fonts` 内嵌 Geist Regular / Medium（OFL，来自 npm `geist` 1.8.1），`GeistFontCollection : EmbeddedFontCollection`，键 `fonts:Geist`，族名 `Geist` |
| Geist Mono | 同一个包内嵌 Geist Mono Regular / Medium，`GeistMonoFontCollection`，键 `fonts:GeistMono`，族名 `Geist Mono`；用于代码块、JSON、快捷键等等宽文本 |

注册采用 Avalonia 字体包的惯例（与 `WithInterFont()` 相同），在 `AppBuilder` 上调用，不经过 AtomUI 的包注册：

```csharp
AppBuilder.Configure<App>()
          .UsePlatformDetect()
          .WithInterFont()
          .WithArcFonts()          // Geist + Geist Mono
          .UseAtomUI(builder => builder.UseDesktopControls().UseArc());
```

- ArcDesign 的字体 Token 取自 `ArcFonts`：
  - `BodyFontFamily` = `fonts:Inter#Inter, $Default`；
  - `DisplayFontFamily` = `fonts:Geist#Geist, fonts:Inter#Inter, $Default`；
  - `MonoFontFamily` = `fonts:GeistMono#Geist Mono`，再回退到系统等宽字体。
  未注册的字体逐级回退，最终回退到系统默认字体。
- `TKCode.Arc.Fonts` 只依赖 Avalonia，不需要 AtomUI 的生成器。
- 包内携带 `licenses/Geist-OFL.txt`。

### 2.3 等宽数字

- 变化中的数字与列对齐的数字使用 `tnum` 字体特性（Inter 支持）。
- `RollingDigits` 以 "0" 的宽度作为每个数字单元格的宽度，保证宽度稳定。

## 3. 许可

- Lucide：ISC；Geist、Inter：SIL OFL 1.1。
- 声明写入 `THIRD-PARTY-NOTICES.md`，并随对应的包携带（见 [许可与声明](../engineering/licensing.md)）。
