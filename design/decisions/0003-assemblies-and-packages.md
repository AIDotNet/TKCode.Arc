# 0003 程序集与包划分

- 状态：已接受
- 日期：2026-10-20

## 背景

- 22 个大类之间存在少量跨大类依赖：BillingToggle 与 MetricCard 依赖 Charts 中的 AnimatedCounter，CodeBlock 依赖 CopyButton，ColorPicker 依赖 TextMorph。
- 动效引擎、设计 Token、共享基元被几乎所有组件使用，也可能被第三方单独使用。
- AtomUI 的包入口由生成器为每个程序集生成；每个程序集都需要自己的 `UseXxx()`。
- 生成器默认把程序集名作为 Token Catalog。Core 与控件程序集若不统一 Catalog，ArcDesign 的 identity 会变成 `TKCode.Arc.Core:ArcDesign`。

## 决策

| 程序集 / 包 | 内容 | 入口 |
|---|---|---|
| `TKCode.Arc.Core` | 载体控件 `ArcDesign` 与 `ArcDesignToken`、`ArcPalette`、动效引擎（`TKCode.Arc.Motion`）、共享基元与服务（`TKCode.Arc.Primitives`）、Core 内置的少量 Lucide 字形 | `UseArcCore()` |
| `TKCode.Arc` | 22 个大类目录下的全部控件 | `UseArc()`：依赖 `UseArcCore()` 与 `UseDesktopControls()` |
| `TKCode.Arc.Icons.Lucide` | 由生成器工具离线生成的完整 Lucide 图标集 | 不需要注册（与 AtomUI.Icons.AntDesign 相同） |
| `TKCode.Arc.Fonts` | Geist 字体集合；Inter 复用 Avalonia.Fonts.Inter | `UseArcFonts()` |

- `TKCode.Arc.Core` 与 `TKCode.Arc` 都设置 `<AtomUIThemeControlCatalog>TKCode.Arc</AtomUIThemeControlCatalog>`，共享一个 Catalog。因此两个程序集的控件短名必须互不重复。
- 控件代码在 `TKCode.Arc` 内按 **大类目录 → 组件目录 → 实现文件** 组织（见 [解决方案布局](../architecture/solution-layout.md)）。

## 备选方案

- **单一程序集**：只有一个包和一个入口；但基础层与控件层没有编译器保证的边界，第三方无法只使用动效与 Token。未选。
- **每大类一个程序集**（22 个 + Core）：按需引用的粒度最细；但包与入口数量多，跨大类依赖会形成复杂的包依赖图，版本发布与验证成本高。未选。

## 后果

- 正面：
  - Core 不能引用控件层，分层由编译器保证。
  - 第三方可以只引用 Core 来使用 Arc 的 Token 与动效。
  - 控件层仍然只有一个包。
- 负面：
  - 应用要调用 `UseArc()`（它自动依赖 `UseArcCore()`）。
  - Core 与 Arc 必须同版本发布。
- 配套：
  - S0.1 验证两个程序集在共享 Catalog 下零诊断构建。
  - 架构测试校验控件短名在两个程序集之间唯一。

## 关联文档

- [解决方案布局](../architecture/solution-layout.md)
- [AtomUI 集成](../architecture/atomui-integration.md)
- [图标与字体](../architecture/icons-and-fonts.md)
