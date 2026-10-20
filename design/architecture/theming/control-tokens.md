# 控件 Token

> 每个 Arc 控件的 own token（`XxxToken`）的职责、写法、允许的类型与命名规则。

## 1. 职责

- **必须存在**：每个公开控件都要有 `[ControlDesignToken]` 的 `XxxToken`，即使没有任何属性。原因是生成器只为拥有 own token 类的控件生成 identity，而以下能力都依赖 identity：
  - `XxxTokenResource` 标记扩展；
  - 按控件覆盖 Token（ThemeConfig / 主题 XML 中的 `<Control Id="Xxx">`）；
  - `ConfigureMotionBindingStyle()` 把 `IsMotionEnabled` 绑定到全局 `EnableMotion`；
  - 语义部件描述与注册。
- **只放控件专属值**：例如 Switch 的轨道宽度与拇指尺寸、Button 的按压缩放比例、Tooltip 的显示延迟。
- **不复制语义 Token**：颜色、阴影、圆角、控件高度等全局值直接在主题中引用 ArcDesign Token。原因：
  - 控件 Token 读不到 ArcDesign 的计算结果；
  - 复制会产生第二个事实来源。
- **派生色的归属**：Arc 原版大量使用 color-mix 派生色（例如"前景色 4.5%"的托盘底、按下时加深的高亮）。按是否依赖 accent 区分：

| 派生色类型 | 放在哪里 | 计算方式 |
|---|---|---|
| 与 accent 无关（只依赖明暗与中性色），如 FloatingButtonGroup 的 muted 托盘色 | 控件 Token | 调用与 ArcDesign 相同的纯函数，如 `ArcPalette.Neutrals(isDarkMode)`，再做混色 |
| 依赖 accent，如 accent 的悬停、按下色 | ArcDesignToken（独立 Token） | `ArcPalette.Resolve(seed, isDark)` |

  原因：accent 覆盖只写在 ArcDesign identity 上，控件 identity 的 `EffectiveGlobalToken.ColorPrimary` 拿不到它；中性色只取决于明暗，两边算出的结果一致。
  代价：应用覆盖 ArcDesign 的中性语义 Token 最终值（例如把 `Foreground` 改成别的颜色）时，不会传导到控件 Token 中的派生色。这种覆盖属于少见场景，在 [主题配置](theme-configuration.md) 中说明。

## 2. 允许的属性类型

Own token 的属性类型必须在 AtomUI 内置转换器列表中（`src/AtomUI.Core/Theme/DesignTokens/BuiltInTokenValueConverters.cs`），否则 ThemeConfig 与主题 XML 无法解析覆盖值：

`string`、`int`、`double`、`float`、`bool`、`Color`、`Color?`、`FontFamily`、`FontWeight`、`BoxShadow`、`BoxShadows`、`TextDecorationInfo`、`TextDecorationInfo?`、`LineStyle`、`Thickness`、`CornerRadius`、`Dimension`、`Easing`、`IBrush`、`IImmutableBrush`、`SolidColorBrush`、`ImmutableTransform`、`PenLineCap`、`PenLineJoin`、`Point`、`Size`、`TimeSpan`。

- 枚举用 `string` 表示，在控件中解析。
- 弹簧参数用 `TimeSpan` 加 `double` 两个属性表示（例如 `PressSpringDuration`、`PressSpringBounce`）。

## 3. 写法

```csharp
using AtomUI.Theme.DesignTokens;

namespace TKCode.Arc;   // 与 Switch 同命名空间、同程序集

[ControlDesignToken]
internal sealed class SwitchToken : AbstractControlDesignToken
{
    public double TrackWidth { get; set; }
    public double TrackHeight { get; set; }
    public double ThumbSize { get; set; }
    public double ThumbPressStretch { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        TrackWidth = 44;   // 具体数值以组件规格为准
        TrackHeight = 26;
        ThumbSize = 22;
        ThumbPressStretch = 1.2;
    }
}
```

- 数值来自 Arc 组件 CSS module 中的具体数字，组件规格中给出来源。
- 与外观相关的控件专属值用 `isDarkMode` 分支。与 accent 无关的派生色可以在这里用中性色纯函数计算；依赖 accent 的颜色必须在 ArcDesign 中新增 Token。
- 空 Token 也要写 `CalculateTokenValues` 调用 `base`，保持一致。

在主题中使用：

```xml
<Setter Property="Width" Value="{arc:SwitchTokenResource TrackWidth}" />
```

## 4. 命名规则

1. 类名为 `{控件名}Token`，与控件同命名空间、同程序集。否则生成器会按短名回退，并优先匹配 AtomUI 目录中的同名控件，造成误绑。
2. 属性名不得与 AtomUI 的 255 个全局 Token 同名（ATOMUIGEN019）。常见占用前缀：
   `Color*`、`ControlHeight*`、`Size*`、`Spacing*`、`Margin*`、`Padding*`、`FontSize*`、`FontFamily`、`IconSize*`、`BorderRadius*`、`BoxShadows*`、`MotionDuration*`、`LineWidth*`、`Screen*`、`ZIndex*`。
3. 属性名也不应与 ArcDesign Token 同名，避免读者混淆两个来源。
4. 尺寸变体使用后缀 `SM` / `LG`（默认尺寸不加后缀），与 AtomUI 的习惯一致。

## 5. 内部 presenter

- 模板内部使用的 internal presenter（如 `SwitchThumb`、`MenuSurface`）没有自己的 identity，不写 Token。
- presenter 需要的值由 owner 的主题通过模板绑定或 `TemplateBinding` 传入。
- 多个控件共享的 presenter 如果需要独立定制（例如菜单表面同时被 DropdownMenu、ContextMenu、SplitButton 使用），可以把它提升为 public 控件并给它 Token；在对应组件规格中决定。

## 6. 动效相关

- 控件实现 `IMotionAwareControl`，用 `MotionAwareControlProperty.IsMotionEnabledProperty.AddOwner<T>()` 添加属性，并在初始化时调用 `this.ConfigureMotionBindingStyle()`。
- 控件 Token 可以覆盖弹簧参数（例如某控件的 morph 弹簧用 0.36s）。默认值引用 Token 目录中的预设，不在控件中写死。
- 动效等级的组合规则见 [减少动效](../motion/reduced-motion.md)。
