# 0004 命名与命名空间

- 状态：已接受
- 日期：2026-10-20

## 背景

- Arc 组件名与 AtomUI、Avalonia 控件名大量重合。例如 Button、Calendar、DatePicker、Slider、Carousel、TreeView、ContextMenu、Card、Select。
- AtomUI 生成器把语义部件 Style 类统一生成为 `AtomUI.Theme.Styling.{控件短名}{PartPath 的 PascalCase}Style`，并映射到 xmlns `https://atomui.net`。
  - 源码：`src/AtomUI.Generator/SemanticParts/SemanticPartStyleContract.cs`。
  - 若被引用程序集已导出同名公开类型，`SemanticPartStyleTypeValidator.cs` 报 **ATOMUIGEN037**（错误）。
  - 例如 Arc 的 `Button` 若声明 Path 为 `icon` 的部件，会生成 `ButtonIconStyle`，与 AtomUI 已导出的类型冲突。
- Path 的校验只要求点分的 camelCase 段，且不能为 `root`；不要求父部件存在（`SemanticPartContractValidator.IsPartPath`）。
  - 选择器 class 与路由与 Path 无关；AtomUI 内部也存在 `popup.root` 这样没有父部件的点分 Path。
- Token 类与控件按短名匹配：生成器先在 Token 类所在命名空间里找同名控件，找不到时回退到引用程序集里的公开控件，并**优先 AtomUI 目录**（`ControlThemeInfo.cs`）。
- 生成的 Token 键类型位于 `{控件命名空间}.DesignTokens`，不会自动映射到任何 xmlns。

## 决策

1. **类名不加 Arc 前缀**，取 Arc 组件标题的 PascalCase：`Button`、`Checkbox`、`Combobox`、`Textarea`、`MultiSelect`、`JsonViewer`、`InViewTitle`。
   - 唯一例外：Arc 的 "Theme switcher"（slug `theme-switch`）命名为 `ThemeSwitcher`。
2. **扁平命名空间**：所有控件位于 `TKCode.Arc`；大类目录只用于组织源码，不进入命名空间。
   - 生成的 Token 类型位于 `TKCode.Arc.DesignTokens`。
   - 基础层公开类型位于 `TKCode.Arc.Motion`、`TKCode.Arc.Primitives`、`TKCode.Arc.Localization`。
   - 主题资源类位于 `TKCode.Arc.Themes`（internal）。
3. **XAML 命名空间**：上述公开命名空间都映射到 `https://github.com/AtomUICommunity/TKCode.Arc`，推荐前缀 `arc`。
   - 图标包使用 `https://github.com/AtomUICommunity/TKCode.Arc/icons/lucide`，前缀 `lucide`。
   - Arc 的任何命名空间都**不**映射到 `https://atomui.net`。
   - 可选：把 `AtomUI.Theme.Styling` 也映射到 `https://github.com/AtomUICommunity/TKCode.Arc`，作为合法别名。
4. **语义部件 Path 一律以 `arc.` 开头**。例如 `[SemanticPart("label", Path = "arc.label", SelectorClass = "semantic-label")]` 生成 `ButtonArcLabelStyle`；嵌套部件 `popup.option` 写作 `arc.popup.option`。
5. **Token 类与控件同命名空间、同程序集**，命名为 `{控件名}Token`。
6. Arc 源码内部引用 Avalonia 同名类型时使用完全限定名或别名，例如 `using AvaloniaButton = Avalonia.Controls.Button;`。
7. 架构测试强制以下规则：
   - 所有 `[SemanticPart]` 的 Path 以 `arc.` 开头；
   - Token 类与控件同命名空间；
   - 主题文件引用 Arc Token 时只使用 `arc:` 前缀；
   - 控件短名在 Core 与 Arc 之间唯一。

## 备选方案

- **统一加 Arc 前缀**（`ArcButton`）：可以从根本上规避 ATOMUIGEN037，C# 中也无需别名；但 XAML 中 `<arc:ArcButton>` 冗余，与 Arc 原版命名不一致。未选。
- **按大类划分命名空间**（`TKCode.Arc.Buttons`）：C# 按需导入，冲突只出现在导入的大类里；但会生成 22 个 DesignTokens 命名空间，控件换大类即破坏性变更。未选。

## 后果

- 正面：
  - XAML 与 C# 的写法与 Arc 原版一致（`<arc:Button>`）；一个 `using TKCode.Arc;` 覆盖全部控件。
  - `arc.` 前缀让语义部件 Style 名永不与 AtomUI 冲突。若 AtomUI 将来出现以 `arc` 开头的 Path，构建会安全失败（报错），不会静默错配。
- 负面：
  - 消费方同时 `using Avalonia.Controls;`、`using AtomUI.Desktop.Controls;` 与 `using TKCode.Arc;` 时，同名类型会报 CS0104，需要使用别名。
  - Arc 的语义部件 Style 类位于 `https://atomui.net`（如 `atom:ButtonArcLabelStyle`），这是 AtomUI 生成器的规范位置。
- 配套：
  - [AtomUI 集成](../architecture/atomui-integration.md) 提供冲突矩阵与 C# 别名指引。
  - S0.3 spike 验证 `arc.label` 构建通过，以及写成 `icon` 时能复现 ATOMUIGEN037。

## 关联文档

- [AtomUI 集成](../architecture/atomui-integration.md)
- [解决方案布局](../architecture/solution-layout.md)
- [编码规范](../engineering/coding-guidelines.md)
