# AtomUI 集成

> Arc 如何作为第三方控件包接入 AtomUI：依赖与版本锁、包注册、生成器契约、短名冲突矩阵、可用与禁用的 AtomUI API，以及与 AtomUI 控件混用的指引。

## 1. 依赖与版本锁

- 依据：AtomUI 仓库 `docs/guides/theming/third-party-control-packages.md`、`docs/guides/startup/package-authoring.md`（以下简称"第三方包指南"）。
- 控件项目（Core、Arc）只直接引用 `AtomUI.Desktop.Controls`。它会传递 AtomUI.Core、AtomUI.Controls、AtomUI.Localization 等依赖，并经 `buildTransitive` 带来以下工具：
  - 生成器（作为 Analyzer 自动注入）；
  - 主题资产包装任务（`GenerateThemeAssetWrappersTask`）；
  - 注册与 TypeMap targets；
  - 本地化 targets（自动收集 `**/Localization/**/*.xlf`）；
  - 独立的构建任务 worker（`dotnet AtomUI.Build.Tasks.dll`，net10）。
- `Directory.Packages.props`：AtomUI.* 写 `[6.2.6]`，Avalonia* 写 `[12.1.4]`，并设置 `CentralPackageTransitivePinningEnabled=true`（[ADR 0001](../decisions/0001-atomui-dependency.md)）。
- 不要把 AtomUI 的工具二进制复制进 Arc 的包。混用不同的工具包会报 ATOMUIREG005。

## 2. 包注册

### 2.1 入口

```csharp
// src/TKCode.Arc.Core/ThemeManagerBuilderExtensions.cs
using AtomUI;
using AtomUI.Generated.TKCodeArcCore;   // 生成命名空间由程序集名决定

public static class ArcCoreBuilderExtensions
{
    public static IAtomUIBuilder UseArcCore(this IAtomUIBuilder builder)
        => GeneratedControlPackageRegistration.Definition.Register(builder, static p => p
               .DependsOn(static b => b.UseDesktopControls()));
}

// src/TKCode.Arc/ThemeManagerBuilderExtensions.cs
using AtomUI.Generated.TKCodeArc;

public static class ArcBuilderExtensions
{
    public static IAtomUIBuilder UseArc(this IAtomUIBuilder builder, Action<ControlPackageOptions>? configure = null)
        => GeneratedControlPackageRegistration.Definition.Register(builder, p =>
        {
            p.DependsOn(static b => b.UseArcCore());
            p.DependsOn(static b => b.UseDesktopControls());
            configure?.Invoke(p);
        });
}
```

- 生成的 `GeneratedControlPackageRegistration` 是 internal，位于 `AtomUI.Generated.<程序集名去掉点>` 命名空间，不提交到 Git。
- `DependsOn` 幂等，循环依赖会被拒绝；重复确保启用时使用 `Definition.Ensure`。
- `OnCommit`：在 UI 线程、Application 已创建时执行，只做启动必需的轻量操作。例如字体包在这里注册字体集合，释放对象交给 `context.Own(...)` 管理。
- `OnFirstUse`：本应用首次实际使用本包时执行，例如初始化 Toast 管理服务。
- 配置回调中不得访问 Avalonia 平台对象或 `Application.Current`。
- 运行时追加：`application.GetAtomUI().LoadPackages(b => b.UseArc())`，在 UI 线程原子执行。

### 2.2 应用侧

- 应用必须在 `UseAtomUI(...)` 中显式调用 `UseArc()`。只引用 NuGet 包不会启用 Arc。
- 顶层非泛型 `partial Application` 自动获得启动镜像挂接。如果应用已有手写的 `RegisterServices`，必须先调用 `base.RegisterServices()`，再调用 `ApplicationStartup.Attach(this)`，否则编译失败。
- 应用声明的每种语言都必须被所有已启用模块完整覆盖，否则报 ATOMUIREG014（见 [本地化](localization.md)）。

## 3. 生成器契约

| 生成物 | 命名 | Arc 的规则 |
|---|---|---|
| 控件 identity | `(Catalog, Id = 控件短名)`；Catalog 默认为程序集名 | Core 与 Arc 都设 `AtomUIThemeControlCatalog=TKCode.Arc`，所以控件短名在两个程序集之间必须唯一 |
| Token 键类型 | `{控件命名空间}.DesignTokens.{Name}TokenKind / TokenKey / Tokens / TokenResourceExtension`（public） | Token 类与控件同命名空间、同程序集；主题中引用 Arc Token 只用 `arc:` 前缀 |
| 语义部件 Style | `AtomUI.Theme.Styling.{控件短名}{PascalCase(Path)}Style`，映射到 `https://atomui.net` | Path 一律以 `arc.` 开头，例如 `ButtonArcLabelStyle` |
| 语义常量、descriptor、主题包装 | internal，位于 `AtomUI.Generated.{TKCodeArc\|TKCodeArcCore}`，名称带哈希 | 无冲突风险 |
| TypeMap key / 代理 | `v1:` + 组、程序集、触发器的 FNV 哈希 | 无冲突风险 |
| 主题资产 id | `{PackageId}:{资产路径}` | 主题文件命名 `XxxTheme.axaml` |
| 本地化 | 枚举所在命名空间中的 `{枚举名去掉 Kind}Extension`；目录 id `{模块}:{元数据名}` | 枚举放 `TKCode.Arc.Localization` |
| 包注册 | internal `GeneratedControlPackageRegistration` | 公开入口为 `UseArcCore()` / `UseArc()` |
| 编译型主题包 | public `GeneratedThemePackageRegistration`；主题 Id 全局唯一，只能有一个 `IsDefault` | Arc 主题 Id 一律以 `Arc` 开头，`IsDefault=false`（Core 的 DaybreakBlue 已是默认） |

规则来源：
- `src/AtomUI.Generator/SemanticParts/SemanticPartStyleContract.cs`、`SemanticPartStyleTypeValidator.cs`
- `src/AtomUI.Generator/DesignToken/ControlThemeInfo.cs`、`ResourceKeyClassWriter.cs`
- `src/AtomUI.Generator/ThemeGeneratorOptions.cs`

### 3.1 必须遵守

1. 每个公开控件都要有 `[ControlDesignToken]` 的 `XxxToken`（可为空）。没有它就没有 identity、`XxxTokenResource` 与 `IsMotionEnabled` 绑定能力。
2. Own token 名不得与 AtomUI 全局 Token 同名（ATOMUIGEN019）。AtomUI 6.2.6 共 255 个全局 Token；`ControlHeight*`、`Spacing*`、`Size*`、`IconSize*`、`MotionDuration*`、`FontSize*`、`BorderRadius*`、`BoxShadows*` 等都已占用。
3. Own token 的属性类型必须在内置转换器列表内（见 [控件 Token](theming/control-tokens.md)）。
4. 禁止在 Arc 中使用 `[GlobalDesignToken]`、`[ThemeAlgorithm]`、`[TokenValueConverter]`。
5. 不要导出 AtomUI 已拥有的默认主题 key（如 `{x:Type atom:Button}`、Avalonia 内置类型的默认主题），冻结时会报冲突。Arc 只为自己的类型导出主题。
6. 跨资产引用命名主题或非默认 type-key 主题时，在消费字典的 `MergedDictionaries` 中声明 `ResourceInclude`，否则报 ATOMUIREG004。

## 4. AtomUI API 白名单

| 能力 | 可用的公开 API | 位置（AtomUI 源码） |
|---|---|---|
| 包注册 | `IAtomUIBuilder`、`ControlPackageOptions`（`DependsOn` / `OnCommit` / `OnFirstUse`）、`ControlPackageContext.Own` | `src/AtomUI.Core/Registration/ControlPackageDefinition.cs` |
| 主题管理 | `IThemeManager.ApplyThemeAsync`、`ThemeRequest`、`IThemeManagerBuilder`（`WithInitialTheme`、`WithFollowSystemThemes`、`WithDefaultFontFamily`、`AddCompiledThemePackage`） | `src/AtomUI.Core/Theme/` |
| 主题配置 | `ThemeConfigBuilder`（`WithAlgorithms`、`WithToken`、`WithControl`）、`ControlThemeConfigBuilder`、`ControlAlgorithmMode`、`ThemeConfigProvider` | `src/AtomUI.Core/Theme/Configuration/`、`Theme/ThemeConfigProvider.cs` |
| Token 声明与读取 | `[ControlDesignToken]`、`AbstractControlDesignToken`、`ControlTokenAccessor`、`TokenResourceExtension<T>`、`PaletteGenerator` | `src/AtomUI.Core/Theme/` |
| 动效开关 | `IMotionAwareControl`、`MotionAwareControlProperty.IsMotionEnabledProperty`、`MotionAwareControlExtensions.ConfigureMotionBindingStyle()` | `src/AtomUI.Core/Controls/IMotionAwareControl.cs`、`Utils/MotionAwareControlExtensions.cs` |
| 初始过渡抑制 | `AnimatableReflectionExtensions.DisableTransitions()` / `EnableTransitions()`（可用但未采用，Arc 用 `ArcMotion.Tracks` 伪类方案） | `src/AtomUI.Core/Animations/AnimatableReflectionExtensions.cs` |
| 动效场景 | `AbstractMotion` 及 Fade / Zoom / Slide / Move / Collapse 预设，`MotionActor` | `src/AtomUI.Core/MotionScene/`、`src/AtomUI.Controls/Primitives/Motions/` |
| 过渡 | `SolidColorBrushTransition`、`RectTransition`、`PixelPointTransition`、`ColorTransition`、`BaseTransitionUtils` | `src/AtomUI.Core/Animations/` |
| 浮层 | `Popup`（`OpenMotion` / `CloseMotion`、`IsMotionEnabled`、`MarginToAnchor`）、`ScopeAwareOverlayLayer.GetLayer`、`ScopeAwareAdornerLayer.GetLayer` | `src/AtomUI.Desktop.Controls/Popup/`、`src/AtomUI.Controls/Primitives/VisualLayers/` |
| 图标 | `Icon`（`StrokeWidth`、`StrokeLineCap`、`StrokeLineJoin`、`StrokeBrush`）、`IconProvider<TKind>`、`AbstractIconPackageGenerator` | `src/AtomUI.Core/Controls/Icon/`、`src/AtomUI.Icons.Shared/` |
| 图片 | `ImageLoader`、`ImageSource`、`AsyncImage` | `src/AtomUI.Controls.Shared/ImageLoading/`、`src/AtomUI.Controls/ImageLoading/` |
| 本地化 | `[LanguageCatalog]`、`ILocalizer`、`LanguageTags` | `src/AtomUI.Localization/` |
| Composition 参考 | `SplashProgressBar`（`CompositionCustomVisualHandler`）、`AbstractSpinIndicator`（关键帧动画） | `src/AtomUI.Controls/Splash/`、`src/AtomUI.Controls/Spin/` |

## 5. 内部 API 黑名单与替代方案

| 内部 API | 用途 | Arc 的替代 |
|---|---|---|
| `WaveSpiritDecorator`、波纹画笔 | Ant 点击波纹 | Arc 没有波纹，用按压缩放（P1） |
| `FeedbackStack/*` | Message / Notification 的堆叠与寿命引擎 | Arc 自己的 Toast 堆栈（P22、P28），见 [浮层与输入](overlays-and-input.md) |
| `InputControlFrame`、`AddOnDecoratedBox`、`EmbeddedTextBox` | AtomUI 输入框外框 | Arc 字段结构，见 [字段](forms-and-fields.md) |
| `Notifiable*Transition`、`InterpolateUtils` | 可通知的过渡 | `Spring*Transition`，见 [ControlTheme 集成](motion/transitions-integration.md) |
| `MotionTransformOptionsAnimator`、`ContentExpansionAnimator` | Desktop 内部动画器 | `MotionValue<T>`、`SpringSizeDecorator` |
| `FontCollectionLifetime` | 字体集合生命周期 | `FontManager.Current.AddFontCollection` + `context.Own(...)`，见 [图标与字体](icons-and-fonts.md) |
| `UseCommonControls()` | AtomUI.Controls 的单独入口 | 依赖 `UseDesktopControls()` |
| `ICompactSpaceAware` | 紧凑组合 | Arc 的组合控件（ButtonGroup 等）自带组合语义 |
| `ThemeManager`、`ThemeContext`、`ThemeScope`（类型本身） | 主题内部实现 | 通过 `IThemeManager` 与 `ThemeConfigProvider` 操作 |

禁止通过反射访问上述成员。AtomUI 的 `DisableTransitions` / `EnableTransitions` 本身封装了对 Avalonia 私有方法的反射，属于 AtomUI 的公开 API，可以使用。

## 6. 与 AtomUI 控件混用

- Arc 控件与 AtomUI 控件可以出现在同一界面中：
  - AtomUI 控件使用 Ant 全局 Token；
  - Arc 控件使用 ArcDesign Token；
  - 两者共享明暗外观。
- 主色关系：
  - 只覆盖 ArcDesign identity 的 `ColorPrimary` 时，只影响 Arc 控件；
  - 覆盖根级 `ColorPrimary` 时，两套控件一起变化（见 [主题配置](theming/theme-configuration.md)）。
- C# 中同时导入多个命名空间时，同名类型会报 CS0104。推荐写法：

```csharp
using TKCode.Arc;
using AvaloniaControls = Avalonia.Controls;
using AtomControls = AtomUI.Desktop.Controls;

var save = new Button();                 // TKCode.Arc.Button
var window = new AvaloniaControls.Window();
var legacy = new AtomControls.Select();
```

- XAML 中用前缀区分：`arc:` 为 Arc，`atom:` 为 AtomUI，默认命名空间为 Avalonia。

## 7. 验证

| 检查 | 方式 |
|---|---|
| 双目标零诊断构建 | S0.1 spike；CI 常驻 |
| `arc.` 路径规避 ATOMUIGEN037 | S0.3 spike：`arc.label` 通过，`icon` 复现 ATOMUIGEN037 |
| 干净缓存消费 | S0.4 spike：本地 feed + 只引用 Arc 的应用可以构建、启动；缺语言时触发 ATOMUIREG014 |
| 架构规则 | 测试：Path 前缀、Token 同命名空间、短名唯一、Token 名不与 `SharedTokenKind` 重名、主题只用 `arc:` 引用 Arc Token |
