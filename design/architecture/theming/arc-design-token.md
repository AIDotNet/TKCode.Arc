# 载体 Token：ArcDesign

> `ArcDesign` 与 `ArcDesignToken` 的声明、注册、消费方式与已知限制。

## 1. 声明

位置：`src/TKCode.Arc.Core/Theme/`，命名空间 `TKCode.Arc`。

```csharp
using System.ComponentModel;
using Avalonia.Controls;

namespace TKCode.Arc;

/// <summary>
/// Arc 全局设计 Token 的 identity 载体。不在界面上使用。
/// 必须是 public：internal 类型无法注册（ATOMUIREG003）。
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class ArcDesign : Control
{
}
```

```csharp
using AtomUI.Theme.DesignTokens;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class ArcDesignToken : AbstractControlDesignToken
{
    // 属性清单与类型见 token-catalog.md；属性名即 Token 名。

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var seed    = EffectiveGlobalToken.ColorPrimary;
        var palette = ArcPalette.Resolve(seed, isDarkMode);     // 纯函数，可单测
        ApplyNeutrals(isDarkMode);
        ApplySemantic(palette, isDarkMode);
        ApplyGeometry();                                        // 圆角、尺寸、间距：与外观无关
        ApplyTypography();
        ApplyMotion();
        MotionMode = EffectiveGlobalToken.EnableMotion ? "full" : "off";   // 可被 own token 覆盖为最终值
    }
}
```

要点：

- Token 类与 `ArcDesign` 同命名空间、同程序集，生成器才能正确配对。
- `CalculateTokenValues` 只依赖 `EffectiveGlobalToken.ColorPrimary`、`EffectiveGlobalToken.EnableMotion` 与 `isDarkMode`，不读取任何控件状态。
- 计算逻辑放在 `ArcPalette`、`ArcTypography` 等纯静态类中，便于单元测试与 S0.2 校准。

## 2. 注册

- `ArcDesign` 位于 Core 程序集，随 `UseArcCore()` 注册；`UseArc()` 通过 `DependsOn` 自动启用 Core。
- 只要 Arc 的主题文件引用了 `{arc:ArcDesignTokenResource …}`，应用链接检查就会把 ArcDesign 记录为"来自其他程序集的必需 Token owner"，并确认 Core 已启用。
- Token 在首次使用时物化。

## 3. 消费

### 3.1 AXAML

```xml
<ControlTheme x:Key="{x:Type arc:Switch}" TargetType="arc:Switch">
  <Setter Property="Background" Value="{arc:ArcDesignTokenResource ControlTrack}" />
  <Style Selector="^:checked">
    <Setter Property="Background" Value="{arc:ArcDesignTokenResource ControlOn}" />
  </Style>
  <Style Selector="^[IsMotionEnabled=True]">
    <Setter Property="Transitions">
      <Transitions>
        <SolidColorBrushTransition Property="Background" Duration="{arc:ArcTokenValue Kind=DurationFast}" />
      </Transitions>
    </Setter>
  </Style>
</ControlTheme>
```

- `ArcDesignTokenResource` 由生成器产出，返回 `DynamicResourceExtension`，主题切换时自动刷新。
- `ArcTokenValue` 是 Arc 在 Core 中提供的 `MarkupExtension`，参照 AtomUI 的 `src/AtomUI.Core/Data/SharedTokenResourceValue.cs`。它一次性读取当前值，用于 `Transition.Duration`、`Easing` 等不支持动态资源的属性。
  - 主题切换不会刷新已经创建的 Transition。时长类 Token 与外观无关，因此没有影响。

### 3.2 C#

```csharp
// 动态绑定：随主题刷新
ArcTokens.Bind(this, ForegroundProperty, ArcDesignTokenKind.TextSecondary);

// 一次性读取：例如自绘控件在 Render 中需要颜色
var accessor = ControlTokenAccessor.Capture<ArcDesign>(this);
var accent   = accessor.GetOwn<Color, ArcDesignTokenKind>(ArcDesignTokenKind.Accent);
```

- `ArcTokens.Bind` 内部调用 `target.GetResourceObservable(boxedKey)`。boxed own-token key 由资源提供者解析到 ArcDesign 的槽位，与请求方是谁无关（`ThemeTokenResourceProvider.cs`）。
- 不要用 `TokenResourceBinder.CreateControlTokenBinding(owner, …)` 绑定 ArcDesign 的 Token：它用 `owner.GetType()` 包装 key，提供者会拒绝跨 owner 的 key。
- 自绘控件需要订阅主题发布事件，在外观或 accent 变化时重新捕获，并调用 `InvalidateVisual()`。

## 4. 覆盖

- 覆盖 accent：在 ArcDesign identity 上设置 `ColorPrimary`，不写 Algorithm（见 [主题配置](theme-configuration.md)）。
- 覆盖某个语义 Token 的最终值：在 ArcDesign identity 上设置 own token，例如把 `RadiusControl` 改为 `12`。覆盖在计算之后应用，所以它不会影响其他 Token 的推导；例如覆盖 `Accent` 不会改变 `AccentStrong`。
- 覆盖动效等级：在 ArcDesign identity 上设置 own token `MotionMode` 为 `reduced` 或 `off`（见 [减少动效](../motion/reduced-motion.md)）。

## 5. 已知限制

| 限制 | 影响 | 应对 |
|---|---|---|
| Own token 覆盖不能当 seed | 不能用 own token 指定"品牌色"，再让 Arc 推导整套 accent | 品牌色走 `ColorPrimary` 通道 |
| 控件 Token 读不到 ArcDesign 的计算值 | 控件不能在 Token 层直接组合 ArcDesign 的语义色 | 控件主题直接引用 ArcDesign Token；依赖 accent 的组合色在 ArcDesign 中定义为独立 Token；与 accent 无关的组合色可在控件 Token 中用中性色纯函数计算（见 [控件 Token](control-tokens.md#1-职责)） |
| 颜色以画刷形式发布 | XAML 中不能对颜色再做运算 | 每个变体都做成独立 Token |
| 这是推断出的模式，非 AtomUI 文档化 | AtomUI 升级可能改变行为 | S0.2 spike + 常驻契约测试；升级 AtomUI 时重跑 |
| `ArcTokenValue` 是一次性读取 | 动态变化的值不能用它 | 只用于与外观无关的时长、缓动、尺寸 |

## 6. 验证

见 [Token 验证](verification.md) 与 [Phase 0](../../roadmap/phase-0.md) 的 S0.2。
