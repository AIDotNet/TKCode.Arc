# 主题与 Token 总览

> Arc 的设计 Token 如何接入 AtomUI 主题体系：分层、载体模型、明暗 / accent / 作用域的数据流、消费契约与约束。决策见 [ADR 0005](../../decisions/0005-arcdesign-token.md)。

## 1. 目标

- 完整复刻 Arc 的 foundation Token 在明 / 暗两种外观与 8 个 accent 预设下的取值：颜色、表面、阴影、圆角、尺寸、字体、动效。
- 完整参与 AtomUI 的主题能力：主题切换、明暗算法、`ThemeConfigProvider` 子树作用域、跟随系统、裁剪 / AOT 发布。
- 一处事实：语义 Token 只在 `ArcDesignToken` 中计算，所有控件主题都引用它。

## 2. Token 分层

| 层 | 承载者 | 内容 | 谁可以覆盖 |
|---|---|---|---|
| L0 AtomUI 全局 Token | AtomUI.Core `DesignToken`（Seed / Map / Alias，共 255 个） | `ColorPrimary`、`EnableMotion` 等 | 应用（ThemeConfig、主题定义） |
| L1 ArcDesign Token | 载体控件 `ArcDesign` 的 own token（`ArcDesignToken`） | Arc 全部全局语义值：中性色、表面、文字、边框、accent、选择控件、状态、series、阴影、圆角、尺寸、间距、字体、动效、模糊、焦点、图标 | 应用：通过 ArcDesign identity 的覆盖（own token 覆盖为最终值） |
| L2 控件 Token | 各控件的 `XxxToken` | 控件专属几何值，如 Switch 轨道宽度、Button 按压缩放比例 | 应用：通过控件 identity 的覆盖 |
| L3 ControlTheme | `XxxTheme.axaml` | 把 L1 / L2 的值绑定到模板与状态 | 应用：语义部件 Style（`arc.*` 部件） |

L1 的唯一输入来自 L0：`EffectiveGlobalToken.ColorPrimary`（accent seed）与外观（`isDarkMode`）。L2 不能读取 L1 的计算结果，所以 L2 只放控件专属的几何值，以及与 accent 无关的派生色。派生色调用与 L1 相同的中性色纯函数计算，依赖 accent 的派生色必须放在 L1（见 [控件 Token](control-tokens.md#1-职责)）。

## 3. 载体模型

```csharp
namespace TKCode.Arc;

[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class ArcDesign : Control { }          // 只承载 identity，不在界面上使用

[ControlDesignToken]
internal sealed class ArcDesignToken : AbstractControlDesignToken
{
    public Color Accent { get; set; }
    public BoxShadows ShadowFloating { get; set; }
    public CornerRadius RadiusControl { get; set; }
    // … 完整清单见 token-catalog.md

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var palette = ArcPalette.Resolve(EffectiveGlobalToken.ColorPrimary, isDarkMode);
        Accent = palette.Accent;
        // …
    }
}
```

生成器会在 `TKCode.Arc.DesignTokens` 中生成公开的 `ArcDesignTokenKind`、`ArcDesignTokenKey`、`ArcDesignTokens.Identity` 与 `ArcDesignTokenResourceExtension`。细节见 [载体 Token](arc-design-token.md)。

## 4. 数据流

### 4.1 明暗

1. 应用切换主题（`IThemeManager.ApplyThemeAsync`）或跟随系统。
2. AtomUI 为当前快照计算外观（Light / Dark），设置 `Application.RequestedThemeVariant`。
3. 外观作为 `isDarkMode` 传入 `ArcDesignToken.CalculateTokenValues`，选择明 / 暗表。暗色值是手调值，不是反相。
4. Token 资源提供者发出资源变更通知，所有 `{arc:ArcDesignTokenResource X}` 动态刷新。

### 4.2 accent

- 通道：ArcDesign identity 上的 `ColorPrimary` 覆盖，**不写 Algorithm 属性**（模式为 Disabled，值原样透传）。
- 未覆盖时使用根级 `ColorPrimary`。暗色下根级值已被 AtomUI 算法派生，`ArcPalette` 会把它映射回对应预设。
- 解析顺序与推导算法见 [accent 与调色板](accent-and-palette.md)；配置方法见 [主题配置](theme-configuration.md)。

### 4.3 作用域

- 子树局部的 accent 或外观使用 AtomUI 的 `ThemeConfigProvider`（`ThemeVariantScope` 子类），对应 Arc 原版在元素上设置 `data-accent` / `data-theme`。
- 子树内的 ArcDesign Token 按该作用域的快照计算，其余部分不受影响。

## 5. 消费契约

| 场景 | 写法 | 说明 |
|---|---|---|
| AXAML 动态值 | `{arc:ArcDesignTokenResource Accent}` | DynamicResource；颜色以 `ImmutableSolidColorBrush` 提供 |
| AXAML 控件 Token | `{arc:SwitchTokenResource TrackWidth}` | 控件自己的 Token |
| AXAML 静态值 | `{arc:ArcTokenValue Kind=DurationFast}` | 一次性读取，用于 `Transition.Duration` 等不能用动态资源的位置；参照 AtomUI 的 `SharedTokenResourceValue` 实现 |
| C# 绑定 | `ArcTokens.Bind(target, property, ArcDesignTokenKind.Accent)` | 内部用原始 boxed key 调用 `GetResourceObservable`；不能用 AtomUI 的 `CreateControlTokenBinding`（它会把 key 包装成 owner 类型，跨 owner 时被拒绝） |
| C# 一次性读取 | `ControlTokenAccessor.Capture<ArcDesign>(owner).GetOwn<Color, ArcDesignTokenKind>(ArcDesignTokenKind.Accent)` | 主题发布后需要重新捕获 |

需要不同透明度或混色的颜色，必须在 `ArcDesignToken` 中各自成为独立 Token（例如 `AccentSubtle`、`ControlOnSubtle`、`FocusRing`）。原因是 DynamicResource 不能再加转换器。

## 6. 约束与诊断

| 约束 | 后果 / 诊断 |
|---|---|
| 外部包不能新增全局 Token 与主题算法 | 用载体控件承载 |
| 禁用 `[GlobalDesignToken]` | 会生成第二个 `SharedTokenKind`，破坏消费者编译 |
| 禁用 `[ThemeAlgorithm]`、`[TokenValueConverter]` | 封闭集合 / 第三方转换器不生效 |
| Own token 名不得与全局 Token 同名 | ATOMUIGEN019 |
| Own token 类型只能用内置转换器支持的类型 | 主题 XML / ThemeConfig 无法解析，见 [控件 Token](control-tokens.md) |
| Own token 覆盖在计算之后应用 | 不能当 seed；可作为最终值或运行时开关（如 `MotionMode`） |
| 每个公开控件必须有 `XxxToken` | 否则没有 identity |
| Token 类与控件同命名空间 | 否则可能误绑到 AtomUI 的同名控件 |

## 7. 子文档

| 文档 | 内容 |
|---|---|
| [arc-design-token.md](arc-design-token.md) | 载体的声明、注册、消费、限制 |
| [token-catalog.md](token-catalog.md) | 全部 ArcDesign Token 的名称、类型、明 / 暗值（唯一来源） |
| [accent-and-palette.md](accent-and-palette.md) | 8 个 accent 预设、匹配规则、OKLCH 推导、图表 series 色 |
| [control-tokens.md](control-tokens.md) | 控件 Token 的规则与允许的类型 |
| [theme-configuration.md](theme-configuration.md) | C# 辅助方法、主题 XML、作用域、按外观覆盖 |
| [verification.md](verification.md) | Token 契约测试与命名冲突检查 |
