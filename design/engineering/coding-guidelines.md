# 编码规范

> 继承自 AtomUI 的编码规则（AtomUI 仓库 `AGENTS.md`、`docs/engineering/development/code-style-guidelines.md`、`control-development-guidelines.md`、`aot-programming-guidelines.md`），以及 Arc 的补充规则。

## 1. 继承自 AtomUI 的规则

| 规则 | 说明 |
|---|---|
| 控制流必须使用大括号 | `if` / `else` / `for` / `foreach` / `while` / `using` 的主体即使只有一行也必须写 `{}` |
| ControlTheme 优先 | 模板、伪类驱动的 Setter、RenderTransform、Transitions，只要 ControlTheme 能等价表达，就写在 ControlTheme 中；代码绑定只是兜底，并在注释中写明 ControlTheme 不可行的具体原因 |
| Transitions 的位置 | 只能声明在 ControlTheme 的 style setter 中，不能写在 ControlTemplate 内容里（模板构建期没有时钟，会抛 NullReferenceException） |
| 迁移义务 | 发现已有的代码绑定可以等价迁移到 ControlTheme 时，在触及该文件的改动中一并迁移 |
| 选择器边界 | 一个选择器最多跨越一个 `/template/` 边界 |
| 语义部件定制 | 示例、文档与测试对语义部件的定制，只使用生成的专用 Style 类，不在 code-behind 中直接设置部件属性 |
| 类成员顺序 | `#region 公共属性定义`（StyledProperty 字段，然后 CLR 包装）→ 事件 → `#region 内部属性定义` → 运行时字段 → 构造函数 → 重写与方法 |
| 共享属性 | 通过 `AddOwner<T>()` 复用，例如 `MotionAwareControlProperty.IsMotionEnabledProperty.AddOwner<T>()` |
| 首帧不动画 | 主题设置 `arc:ArcMotion.Tracks="True"`，过渡只声明在 `^:motion-full` / `^:motion-reduced` 下 |
| AOT | 不做反射扫描；不按名称字符串创建类型；不在运行时加载 AXAML；动态数据走生成器或显式工厂 |
| 资源依赖 | 跨资产引用命名主题时，在 `MergedDictionaries` 中声明 `ResourceInclude`（否则 ATOMUIREG004） |
| 可空性 | 开启 Nullable，可空警告视为错误 |

## 2. Arc 的补充规则

### 2.1 命名与组织

- 类名无前缀，取 Arc 标题的 PascalCase；命名空间统一为 `TKCode.Arc`（[ADR 0004](../decisions/0004-naming-and-namespaces.md)）。
- 代码位于 `src/TKCode.Arc/<大类>/<类名>/`，文件约定见 [解决方案布局](../architecture/solution-layout.md#41-组件目录文件约定)。
- 引用 Avalonia 的同名类型时使用别名或完全限定名，例如 `using AvaloniaButton = Avalonia.Controls.Button;`。
- Arc 的属性名与 Avalonia 已有成员冲突时，换用不冲突的名称，并在规格中注明对应关系。例如 Arc 的 `name` 改为 `DisplayName`（避开 `StyledElement.Name`），`theme` 改为 `ThemeVariant`（避开 `StyledElement.Theme`）。
- 每个公开控件都有 `XxxToken`（可为空），与控件同命名空间。
- 语义部件的 Path 以 `arc.` 开头。
- 主题文件命名为 `XxxTheme.axaml`；不创建以 `Themes` 结尾的聚合文件。

### 2.2 依赖边界

- 只使用 AtomUI 的公开 API，白名单与黑名单见 [AtomUI 集成](../architecture/atomui-integration.md#4-atomui-api-白名单)。
- 禁止通过反射访问 AtomUI 或 Avalonia 的 internal 成员（测试代码开启无头浮层层除外）。
- 禁止在 Arc 中使用 `[GlobalDesignToken]`、`[ThemeAlgorithm]`、`[TokenValueConverter]`。
- Core 不得引用 `TKCode.Arc` 控件程序集。

### 2.3 Token 与主题

- 颜色、阴影、圆角、控件高度、字体只引用 ArcDesign Token。控件 Token 只放控件专属的几何值，以及与 accent 无关的派生色（用中性色纯函数计算）；依赖 accent 的派生色放在 ArcDesignToken（见 [控件 Token](../architecture/theming/control-tokens.md#1-职责)）。
- 主题中引用 Arc Token 时只使用 `arc:` 前缀。
- 硬编码的数值必须能在组件规格中找到 Arc 来源（例如 CSS module 中的具体数字）。

### 2.4 动效

- 状态动画写在 ControlTheme 中，使用 `Spring*Transition` 或补间过渡，并放在 `^[IsMotionEnabled=True]` 下。
- 手势与连续驱动使用 `MotionValue<T>`，并注释 ControlTheme 不可行的原因。
- 每个动画都实现 Reduced 与 Off 分支，见 [减少动效](../architecture/motion/reduced-motion.md)。
- 动画只写 RenderTransform、Opacity、Effect、Clip 或 `AffectsRender` 的属性；宽高动画只通过 `SpringSizeDecorator`。
- 推进路径中不分配托管对象。

### 2.5 文案与可访问性

- 内置文案使用本地化目录（en-US、zh-CN、zh-TW），不在代码中写死字符串。
- 英文文案遵循 Arc 规则：句子式大小写，按钮用"动词 + 对象"，不用破折号，不用全大写。
- 图标按钮必须有具体的无障碍名称。
- 只在键盘焦点时显示焦点环。

### 2.6 注释与文档

- 公开 API 的 XML 文档注释使用英文（面向 NuGet 用户）。实现注释可以用中文或英文，同一文件保持一致。
- 注释解释"为什么"，不复述代码。
- 移植自 Arc 的算法或数据（例如国家电话格式、密码强度规则）在文件头注明来源组件，THIRD-PARTY-NOTICES 中已覆盖 Arc 的 MIT 声明。

## 3. 架构规则的自动检查

以下规则由 `tests/TKCode.Arc.Tests` 中的架构测试强制执行（见 [测试](testing.md)）：

- 所有 `[SemanticPart]` 的 Path 以 `arc.` 开头；
- 每个公开控件有同命名空间的 `XxxToken`；
- Token 属性名不与 AtomUI 全局 Token 重名，且类型在允许列表内；
- 控件短名在 Core 与 Arc 之间唯一；
- 主题文件只用 `arc:` 引用 Arc Token；
- 不存在以 `Themes` 结尾的 AXAML 聚合文件；
- Core 程序集不引用控件程序集。
