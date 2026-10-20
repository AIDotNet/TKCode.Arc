# 主题配置

> 应用如何选择 accent、覆盖 Arc Token、按外观分别覆盖、在子树内局部换色，以及 Arc 提供的 C# 辅助方法。

## 1. 配置入口一览

| 需求 | 做法 |
|---|---|
| 选择 Arc accent 预设，只影响 Arc 控件 | ArcDesign identity 上设置 `ColorPrimary`，不写 Algorithm |
| 让 Arc 与 AtomUI 控件使用同一主色 | 设置根级 `ColorPrimary`；Arc 会把暗色派生值映射回对应预设 |
| 覆盖某个 Arc 语义 Token 的最终值 | ArcDesign identity 上设置 own token（如 `RadiusControl`）。注意：覆盖中性语义色（如 `Foreground`）不会传导到控件 Token 中与 accent 无关的派生色，需要同时覆盖相关控件的 Token |
| 覆盖某个控件的专属值 | 该控件 identity 上设置 own token |
| 子树局部换 accent / 外观 | `ThemeConfigProvider` 包裹子树 |
| 明 / 暗使用不同品牌值 | 两份主题定义（Appearance Light / Dark），或 `WithFollowSystemThemes` 的两个请求模板 |
| 降低动效 | ArcDesign 上设置 `MotionMode`，或使用 `ArcMotion.Level` 附加属性（见 [减少动效](../motion/reduced-motion.md)） |

## 2. C# 辅助方法

AtomUI 的 `ThemeConfigBuilder.WithControl(identity, config)` 对同一 identity 会**整体替换**配置。如果应用分多次设置 ArcDesign 的不同项，后一次会覆盖前一次。为此 Arc 提供一个累加器式的扩展方法集合，放在 Core 中：

```csharp
var config = new ThemeConfigBuilder()
    .WithAlgorithms(ThemeAlgorithm.Default, ThemeAlgorithm.Dark)
    .WithArc(arc => arc
        .Accent(ArcAccentPreset.Violet)          // 写入 ArcDesign identity 的 ColorPrimary
        .Token("RadiusControl", "12")            // own token 最终值
        .MotionMode(ArcMotionLevel.Reduced))     // own token MotionMode
    .Build();

await themeManager.ApplyThemeAsync(new ThemeRequest("DaybreakBlue", config, "user-settings"));
```

- `WithArc` 把多次设置合并成一个 `ControlThemeConfig`，最后调用一次 `WithControl(ArcDesignTokens.Identity, …)`。
- `Accent(Color)` 接受任意颜色（走 OKLCH 推导）；`Accent(ArcAccentPreset)` 写入预设 seed。
- 不写 Algorithm：`ControlThemeConfigBuilder` 不调用 `WithAlgorithm`，模式保持 Unspecified，合并后为 Disabled，值原样透传。

## 3. 主题 XML

AtomUI 支持编译型主题定义（`AtomUIThemeDefinition`）与用户主题目录。Arc 的覆盖写法：

```xml
<Control Catalog="TKCode.Arc" Id="ArcDesign">
  <Tokens>
    <Token Name="ColorPrimary" Value="#7747FF" />
    <Token Name="RadiusControl" Value="12" />
  </Tokens>
</Control>
```

- **不要**写 `Algorithm="Global"`：它会在暗色下重新运行算法，把 `ColorPrimary` 变成派生色（`ThemeCompiler.cs`、`ThemeConfigMerger.cs`）。
- Arc 可以选择性地提供编译型主题包，例如 `ArcNeutral`、`ArcViolet`：
  - Id 以 `Arc` 开头，`IsDefault=false`；Core 的 DaybreakBlue 已是默认主题，只能有一个默认。
  - 应用用 `builder.Theme.WithInitialTheme("ArcViolet")` 选择。
  - 是否提供这些主题包，在 Gallery 实现时决定（[Gallery 应用](../../engineering/gallery-app.md)）。

## 4. 子树作用域

```xml
<ThemeConfigProvider Config="{Binding PromoThemeConfig}">
  <arc:Card>
    <arc:Button Content="Upgrade plan" />
  </arc:Card>
</ThemeConfigProvider>
```

- `ThemeConfigProvider`（`src/AtomUI.Core/Theme/ThemeConfigProvider.cs`）继承 `ThemeVariantScope`，按 `Config` 为子树编译独立快照。
- 子树中的 ArcDesign Token 按该快照计算。适用于 Arc 原版中在元素上设置 `data-accent` 的场景，例如营销卡片使用不同 accent。
- 作用域有编译成本，不要在列表项级别大量使用。

## 5. 按外观分别覆盖

Arc 原版允许在 `:root` 与 `:root[data-theme="dark"]` 分别定义品牌值。由于 own token 覆盖不能作为 seed，Arc 采用以下两种方式之一：

1. **两份主题定义**：一份 Appearance=Light，一份 Appearance=Dark，各自在 ArcDesign identity 上写入不同的 own token 最终值。
2. **跟随系统的两个请求模板**：`builder.Theme.WithFollowSystemThemes(lightRequest, darkRequest)`，两个请求各带一份 `ThemeConfig`。

## 6. 运行时切换

- 切换外观或 accent 都通过 `IThemeManager.ApplyThemeAsync(new ThemeRequest(themeId, config, reason))`。它是一个事务（Capture → Prepare → Commit → Publish），失败时保留原状态。
- ThemeSwitcher 组件的 Eclipse / Split / Rise 过渡在切换前截取窗口快照，切换后用裁剪动画揭示新外观；见 Buttons 大类 overview 与 ThemeSwitcher 规格。
- 切换不会重建控件，DynamicResource 自动刷新。自绘控件需要订阅主题发布并重新捕获 Token（见 [载体 Token](arc-design-token.md#32-c)）。
