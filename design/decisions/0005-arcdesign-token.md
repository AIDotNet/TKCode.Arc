# 0005 设计 Token 接入方式：载体控件 ArcDesign

- 状态：已接受（待 Phase 0 spike S0.2 验证）
- 日期：2026-10-20

## 背景

- Arc 的设计系统是一组 CSS 变量：
  - oklch 中性色阶，语义色（background、surface、foreground、border、accent、control-*、status、series）。
  - 阴影、圆角、控件高度、间距、字体、动效参数。
  - 明 / 暗两套值与 8 个 accent 预设。
- AtomUI 的全局 Token 集与主题算法在 AtomUI.Core 中是封闭的：
  - 运行时 schema 只来自 Core 生成的 `GeneratedThemeSchema`；`ThemeAlgorithm` 是封闭枚举。
  - 外部程序集使用 `[GlobalDesignToken]` 会生成第二个 `AtomUI.Theme.Resources.SharedTokenKind`，导致冲突。
  - 第三方 `[TokenValueConverter]` 不生效。
- 外部程序集可以声明控件 own token（`[ControlDesignToken]`）。它在 `CalculateTokenValues(bool isDarkMode)` 中由 `EffectiveGlobalToken` 计算，并参与明暗切换、`ThemeConfigProvider` 作用域与裁剪发布。
- 运行时资源提供者把 boxed 的 own token key 解析到其 owner 的槽位，**与请求方是谁无关**（`ThemeTokenResourceProvider.cs`）。因此任意元素都能引用某个控件的 own token。
- 生成器只为拥有 own token 类的控件生成 identity（`ControlThemeInfo.cs`、`TypeMapRegistrationWriter.cs`）。
- Own token 的覆盖值在 `CalculateTokenValues` **之后**应用，不能充当 seed；但可以作为最终值被运行时读取（`ThemeCompiler.cs`）。
- 按 identity 覆盖 `ColorPrimary` 时，若写 `Algorithm="Global"`，暗色下会重新跑算法，得到派生色而不是 seed。不写 Algorithm（模式为 Disabled）时，值原样透传（`ThemeCompiler.cs`、`ThemeConfigMerger.cs`）。

## 决策

1. 在 `TKCode.Arc.Core` 中声明载体控件与载体 Token：
   - `[EditorBrowsable(EditorBrowsableState.Never)] public sealed class ArcDesign : Control`，只承载 identity，必须是 public，否则注册失败。
   - `[ControlDesignToken] internal sealed class ArcDesignToken : AbstractControlDesignToken`，承载 Arc 的全部全局语义 Token。
2. `ArcDesignToken.CalculateTokenValues(isDarkMode)` 由 `EffectiveGlobalToken.ColorPrimary`（accent seed）与 `isDarkMode` 计算全部值。
   - 计算逻辑集中在 `ArcPalette.Resolve(seed, isDark)` 等纯函数中，见 [accent 与调色板](../architecture/theming/accent-and-palette.md)。
3. **accent 通道**：ArcDesign identity 上的 `ColorPrimary` 覆盖，**不写 Algorithm 属性**。
   - 未覆盖时，回落到根级 `ColorPrimary`。暗色下根级值已被 AtomUI 派生，`ArcPalette` 会把派生值映射回对应预设。
4. 消费方式：
   - AXAML 动态资源：`{arc:ArcDesignTokenResource Accent}`。
   - 静态值：Arc 自己的 `{arc:ArcTokenValue Kind=...}`。
   - C# 绑定：`ArcTokens.Bind` 辅助方法，内部使用原始 boxed key；不能用 `CreateControlTokenBinding`。
   - C# 一次性读取：`ControlTokenAccessor.Capture<ArcDesign>(owner)`。
5. **每个公开 Arc 控件都有自己的 `XxxToken`**（允许为空），从而获得 identity、`XxxTokenResource` 与 `IsMotionEnabled` 绑定能力。
   - 控件 Token 只存控件专属几何值，不复制语义 Token。
   - 控件 Token 也不能读取 ArcDesign 的计算结果。
6. 颜色变体（如 subtle、hover）各自成为独立 Token，因为颜色 Token 以 `ImmutableSolidColorBrush` 发布，DynamicResource 不能再加转换器。
7. 禁止在 Arc 中使用 `[GlobalDesignToken]`、`[ThemeAlgorithm]`、`[TokenValueConverter]`。Token 名不得与 AtomUI 全局 Token 同名（ATOMUIGEN019）。

## 备选方案

- **抽象基 Token 继承**（`public abstract [ControlDesignToken] ArcTokenBase`）：AtomUI 文档化支持跨程序集继承；但每个控件各持一份副本，应用没有统一的一处覆盖入口。未选。
- **Avalonia ThemeDictionaries**（Light / Dark 资源字典 + accent 字典）：简单直观；但没有 seed 派生、ThemeConfig 覆盖与紧凑模式，也无法参与 AtomUI 的主题事务。未选。

## 后果

- 正面：
  - Arc 的全局 Token 完整参与 AtomUI 的主题切换、明暗、作用域与裁剪。
  - 应用可以用一处覆盖改变 Arc 的 accent，并保持与 AtomUI 控件的主色独立。
- 负面：
  - 这是从 AtomUI 源码与测试夹具（`tests/AtomUI.Registration.Fixtures/ThirdParty/TriggerControls.cs`）推断出的用法，不是文档化模式，存在 AtomUI 升级时行为变化的风险。
  - Own token 不能当 seed，所以"按外观分别覆盖品牌色"需要两份主题定义或 FollowSystem 的两个请求模板。
- 验证：S0.2 spike 必须证明以下各项；任何一项失败都重开本 ADR。
  - 明暗切换生效；ArcDesign 上的 `ColorPrimary` 覆盖在两种外观下都得到预设值。
  - `ThemeConfigProvider` 作用域生效；`Capture<ArcDesign>` 可用。
  - 运行时能读取 `MotionMode`。

## 关联文档

- [主题与 Token 总览](../architecture/theming/overview.md)
- [载体 Token](../architecture/theming/arc-design-token.md)
- [Token 目录](../architecture/theming/token-catalog.md)
- [accent 与调色板](../architecture/theming/accent-and-palette.md)
