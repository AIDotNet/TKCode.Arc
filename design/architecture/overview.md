# 架构总览

> TKCode.Arc 的分层、程序集职责、启动与注册序列、运行时数据流，以及各横切主题的文档入口。

## 1. 定位与非目标

- **定位**：在 AtomUI 之上提供一套 Arc 风格的独立控件库，视觉、键盘行为与动效与 Arc 原版等价。
- **非目标**：
  - 不改造 AtomUI 的 Ant 风格控件；
  - 不提供 Ant 与 Arc 两套风格之间的自动转换；
  - 不支持 Browser 与移动端；
  - 不移植 Arc 的 Pro 组件与 Blocks。

## 2. 设计原则

1. **AtomUI 是底座，不是父类**：复用其主题、Token、注册、浮层、图标、本地化等公开能力；控件本身派生自 Avalonia 基类（[ADR 0002](../decisions/0002-independent-controls.md)）。
2. **ControlTheme 优先**：模板、伪类驱动的 Setter、Transitions 都写在 ControlTheme 中；只有 ControlTheme 无法表达时才写代码，并在注释中说明原因（继承 AtomUI 的强约束）。
3. **一处事实**：语义 Token 只在 `ArcDesignToken` 中计算，动效参数只在 `ArcMotionPresets` 中定义，组件总清单是阶段分配的唯一来源。
4. **动效解释因果**：继承 Arc 的动效原则：可打断、首帧不动画、退场快于进场、每个动画都有减少动效分支。
5. **AOT 是一等约束**：显式注册，不做反射扫描；生成器产物不手写。
6. **可验证**：每个基础结论都有对应的 spike 或测试（见 [Phase 0](../roadmap/phase-0.md)）。

## 3. 分层

```
┌────────────────────────────────────────────────────────────┐
│ 应用 / Gallery（UseAtomUI → UseDesktopControls → UseArc）       │
├────────────────────────────────────────────────────────────┤
│ TKCode.Arc            22 个大类控件（Buttons … TextEffects） │
├────────────────────────────────────────────────────────────┤
│ TKCode.Arc.Core       ArcDesign Token · ArcPalette          │
│                       Motion（弹簧引擎、过渡、Presence、FLIP）│
│                       Primitives（尺寸观察、视口、浮层、手势…）│
├───────────────────┬────────────────────────────────────────┤
│ TKCode.Arc.Icons  │ TKCode.Arc.Fonts（Geist；Inter 复用      │
│ .Lucide           │ Avalonia.Fonts.Inter）                  │
├───────────────────┴────────────────────────────────────────┤
│ AtomUI 6.2.6：Core（Theme/Token/MotionScene/Icon）·          │
│ Controls（Primitives、ImageLoading）· Desktop.Controls（Popup）│
│ · Localization · 生成器与注册 targets（buildTransitive）     │
├────────────────────────────────────────────────────────────┤
│ Avalonia 12.1.4（控件基类、样式、Composition、动画）          │
└────────────────────────────────────────────────────────────┘
```

依赖方向只能自上而下。Core 不引用控件层；Icons 与 Fonts 不引用 Core。

## 4. 程序集职责

| 程序集 | 职责 | 主要命名空间 | 包入口 |
|---|---|---|---|
| `TKCode.Arc.Core` | 载体 Token、调色板、动效引擎、共享基元与服务、Core 内置字形 | `TKCode.Arc`、`TKCode.Arc.Motion`、`TKCode.Arc.Primitives`、`TKCode.Arc.DesignTokens` | `UseArcCore()` |
| `TKCode.Arc` | 110 个控件，按大类目录组织 | `TKCode.Arc`、`TKCode.Arc.DesignTokens`、`TKCode.Arc.Localization` | `UseArc()` |
| `TKCode.Arc.Icons.Lucide` | Lucide 图标全集（离线生成） | `TKCode.Arc.Icons.Lucide` | 无需注册 |
| `TKCode.Arc.Fonts` | Geist 字体集合注册 | `TKCode.Arc.Fonts` | `UseArcFonts()` |

详见 [解决方案布局](solution-layout.md) 与 [ADR 0003](../decisions/0003-assemblies-and-packages.md)。

## 5. 启动与注册序列

```csharp
AppBuilder.Configure<App>()
    .UsePlatformDetect()
    .UseAtomUI(builder =>
    {
        builder.UseLanguages(LanguageTags.EnUS, [LanguageTags.EnUS, LanguageTags.ZhCN, LanguageTags.ZhTW]);
        builder.UseDesktopControls();
        builder.UseArc();        // 依赖 UseArcCore() 与 UseDesktopControls()
        builder.UseArcFonts();   // 可选
    });

public partial class App : Application { }  // 顶层非泛型 partial Application 自动挂接启动镜像
```

1. **配置期**：`UseArc()` 调用生成的 `GeneratedControlPackageRegistration.Definition.Register(builder, p => p.DependsOn(...))`，只记录声明与依赖，不访问平台对象。
2. **提交期**（UI 线程，Application 已创建）：执行各包的 `OnCommit`，例如字体包注册 `EmbeddedFontCollection`。
3. **首次使用**：Token、主题资产、语义描述按需物化；`OnFirstUse` 每个应用每个包执行一次。
4. **运行时**：主题切换由 `IThemeManager.ApplyThemeAsync` 驱动，Token 资源以 DynamicResource 方式刷新。

详见 [AtomUI 集成](atomui-integration.md)。

## 6. 运行时数据流

### 6.1 Token

```
AtomUI 全局 Seed（ColorPrimary 等）
        │  ThemeConfig / 主题定义 / ThemeConfigProvider 作用域
        ▼
ArcDesign identity 的 EffectiveGlobalToken（可被 identity 级 ColorPrimary 覆盖）
        │  ArcDesignToken.CalculateTokenValues(isDarkMode) → ArcPalette.Resolve(...)
        ▼
ArcDesign own tokens（Accent、Surface、ShadowFloating …）──► {arc:ArcDesignTokenResource X}
        │                                                        （任意 Arc 控件主题中引用）
控件 own tokens（XxxToken：控件专属几何值）────────────────► {arc:XxxTokenResource Y}
```

详见 [主题与 Token](theming/overview.md)。

### 6.2 动效

```
状态变化（伪类 / 属性）──► ControlTheme Transitions（Spring*Transition / ArcSpringEasing）
手势 / 连续驱动 ────────► MotionValue<T> ──► MotionScheduler（RequestAnimationFrame）──► RenderTransform / Opacity / Effect / 自绘属性
进出场 / 重排 / 共享元素 ► PresenceHost · LayoutAnimator（FLIP）· SharedElementScope
            ▲
            └── 有效动效等级 = min(ArcMotion.Level, MotionMode Token, IsMotionEnabled, 系统探针)
```

详见 [动效系统](motion/overview.md)。

## 7. 横切关注点导航

| 主题 | 文档 |
|---|---|
| 仓库、项目、命名空间、组件目录约定 | [solution-layout.md](solution-layout.md) |
| 包注册、生成器契约、冲突矩阵、API 白 / 黑名单 | [atomui-integration.md](atomui-integration.md) |
| SDK、运行时、AOT、CI | [target-frameworks.md](target-frameworks.md) |
| Token 体系 | [theming/overview.md](theming/overview.md) |
| 动效体系 | [motion/overview.md](motion/overview.md) |
| 非动效服务（尺寸观察、视口、列表导航、手势、格式化、计时器） | [primitives.md](primitives.md) |
| 字段通用结构 | [forms-and-fields.md](forms-and-fields.md) |
| 浮层、焦点陷阱、手势与键盘约定 | [overlays-and-input.md](overlays-and-input.md) |
| 图标与字体 | [icons-and-fonts.md](icons-and-fonts.md) |
| 可访问性 | [accessibility.md](accessibility.md) |
| 本地化与格式化 | [localization.md](localization.md) |

## 8. 已知限制

| 限制 | 原因 | 应对 |
|---|---|---|
| 不能新增全局 Token 或主题算法 | AtomUI.Core 的 schema 与算法封闭 | 载体控件 `ArcDesign`（[ADR 0005](../decisions/0005-arcdesign-token.md)） |
| 不能使用 AtomUI 内部类型 | `InternalsVisibleTo` 只开放给 AtomUI 自家包 | 在 Arc 内实现等价能力（如 Toast 堆栈引擎） |
| 不能加入 AtomUI 的 `CompactSpace` | `ICompactSpaceAware` 是 internal | Arc 的 ButtonGroup 等自带组合语义 |
| Browser 发布不可用 | AtomUI 锁定 SDK 10.0.300 + net10-browser | 不在范围内 |
| net11 不在 AtomUI 验证范围内 | AtomUI 文档的产品框架为 net10.0 | Phase 0 自行验证（S0.1 / S0.5） |
| 没有系统级减少动效 API | Avalonia 12.1.4 未提供 | `ArcMotion.Level` + 可选平台探针 |
| 宽高弹簧触发逐帧布局 | Avalonia 布局模型 | 仅在尺寸本身就是信息时使用，并做性能预算 |
