# 解决方案布局

> 仓库目录、项目清单、命名空间与 xmlns 映射，以及"大类目录 → 组件目录 → 实现文件"的代码约定。

## 1. 仓库顶层

```
TKCode.Arc/
├── TKCode.Arc.slnx
├── global.json                 SDK 11 RC2，allowPrerelease，rollForward=latestFeature（见 ADR 0009）
├── NuGet.config                nuget.org + 本地 feed（artifacts/feed，用于消费验证）
├── Directory.Build.props       导入 build/Arc.Repository.props
├── Directory.Build.targets     导入 build/Arc.Repository.targets
├── Directory.Packages.props    CPM；AtomUI.* [6.2.6]、Avalonia* [12.1.4]；开启传递锁定
├── .editorconfig               控制流必须使用 {}；命名空间不必与目录一致
├── LICENSE                     MIT
├── THIRD-PARTY-NOTICES.md      Arc（MIT）、Lucide（ISC）、Geist / Inter（OFL）
├── README.md / README.zh-CN.md
├── CHANGELOG.md
├── AGENTS.md                   面向 AI 协作的入口约定（指向 design/ 与 engineering 规范）
├── build/
│   ├── Versions.props          ArcVersion、AtomUIVersion=6.2.6、AvaloniaVersion=12.1.4
│   ├── ProjectDefaults.props   ArcTargetFrameworks=net10.0;net11.0、Nullable、LangVersion、IsAotCompatible、WarningsAsErrors=Nullable
│   ├── PackageMetadata.props   作者、仓库、许可、图标、README
│   └── PackageValidation.props 包校验（发布时启用）
├── design/                     本设计文档
├── src/
├── tools/
├── tests/
└── gallery/
```

`AtomUIThemeControlCatalog=TKCode.Arc` 只在 `src/` 下的两个控件项目中设置（Core 与 Arc），不放进全局默认值，以免影响测试与 Gallery。

## 2. 项目清单

| 路径 | 类型 | 目标框架 | 说明 |
|---|---|---|---|
| `src/TKCode.Arc.Core` | 类库，打包 | net10.0;net11.0 | ArcDesign、ArcPalette、Motion、Primitives；引用 `AtomUI.Desktop.Controls` |
| `src/TKCode.Arc` | 类库，打包 | net10.0;net11.0 | 110 个控件；引用 Core 与 `AtomUI.Desktop.Controls` |
| `src/TKCode.Arc.Icons.Lucide` | 类库，打包 | net10.0;net11.0 | Lucide 图标全集；只引用 `AtomUI.Core`（图标运行时）。不需要注册，也不需要生成器 |
| `src/TKCode.Arc.Fonts` | 类库，打包 | net10.0;net11.0 | Geist 字体集合；引用 `AtomUI.Controls`，借助其 buildTransitive 中的生成器生成包入口。`AtomUI.Core` 包不带生成器；S0.10 验证 |
| `tools/TKCode.Arc.Icons.Lucide.Generator` | 控制台 | net10.0 | 离线生成图标代码（基于 `AtomUI.Icons.Shared`），生成结果提交到仓库 |
| `tests/TKCode.Arc.Core.Tests` | 测试 | net10.0（CI 另跑 net11.0） | 弹簧引擎、调色板、Token 契约 |
| `tests/TKCode.Arc.Tests` | 测试 | net10.0（CI 另跑 net11.0） | 控件行为、主题契约、架构规则 |
| `tests/PackageConsumers/{Minimal,Trimmed,Aot}` | 应用 | net10.0 / net11.0 | 从本地 feed 以干净缓存消费已打包的 Arc |
| `gallery/TKCode.Arc.Gallery` | 类库 | net10.0;net11.0 | Gallery 页面、示例、导航（见 [Gallery 应用](../engineering/gallery-app.md)） |
| `gallery/TKCode.Arc.Gallery.Desktop` | 可执行 | net10.0;net11.0 | 桌面宿主，NativeAOT 发布目标 |

## 3. 命名空间与 xmlns

| CLR 命名空间 | 所在程序集 | xmlns | 内容 |
|---|---|---|---|
| `TKCode.Arc` | Core + Arc | `https://github.com/AtomUICommunity/TKCode.Arc` | 全部控件（扁平），以及 `ArcDesign`、`ArcMotion` 附加属性 |
| `TKCode.Arc.DesignTokens` | Core + Arc（生成） | `https://github.com/AtomUICommunity/TKCode.Arc` | `XxxTokenKind`、`XxxTokenKey`、`XxxTokenResourceExtension` |
| `TKCode.Arc.Localization` | Core + Arc | `https://github.com/AtomUICommunity/TKCode.Arc` | `[LanguageCatalog]` 枚举与生成的 `XxxLangResourceExtension` |
| `TKCode.Arc.Motion` | Core | `https://github.com/AtomUICommunity/TKCode.Arc` | 弹簧引擎、过渡、Presence、布局动画 |
| `TKCode.Arc.Primitives` | Core | `https://github.com/AtomUICommunity/TKCode.Arc` | 共享基元控件与服务 |
| `TKCode.Arc.Themes` | Core + Arc | 不映射 | internal 主题资源类 |
| `AtomUI.Theme.Styling` | Core + Arc（生成） | `https://atomui.net`（生成器规范位置），可选别名 `https://github.com/AtomUICommunity/TKCode.Arc` | 语义部件 Style 类（如 `ButtonArcLabelStyle`） |
| `TKCode.Arc.Icons.Lucide` | Icons.Lucide | `https://github.com/AtomUICommunity/TKCode.Arc/icons/lucide`（前缀 `lucide`） | 图标类型与 `LucideIcon` 标记扩展 |
| `TKCode.Arc.Fonts` | Fonts | 不映射 | 字体注册 |

`AssemblyInfo.cs` 示例（Arc 程序集）：

```csharp
[assembly: XmlnsDefinition("https://github.com/AtomUICommunity/TKCode.Arc", "TKCode.Arc")]
[assembly: XmlnsDefinition("https://github.com/AtomUICommunity/TKCode.Arc", "TKCode.Arc.DesignTokens")]
[assembly: XmlnsDefinition("https://github.com/AtomUICommunity/TKCode.Arc", "TKCode.Arc.Localization")]
[assembly: XmlnsPrefix("https://github.com/AtomUICommunity/TKCode.Arc", "arc")]
```

## 4. 控件代码组织：大类 → 组件 → 实现

```
src/TKCode.Arc/
├── Buttons/
│   ├── Button/
│   ├── ActionButton/
│   └── …
├── Gestures/  Menus/  TextFields/  SpecialInputs/  Selects/  Toggles/  Sliders/
├── Pickers/  Editors/  Navigation/  Expand/  Overlays/  Messages/  Progress/
├── Avatars/  Cards/  Charts/  Tables/  Activity/  Media/  TextEffects/
├── Primitives/          仅 Arc 程序集内多个控件共享的 internal presenter（如 MenuSurface）
├── Properties/AssemblyInfo.cs
└── ThemeManagerBuilderExtensions.cs   UseArc()
```

- 大类目录名与 Arc 侧边栏一致（PascalCase）。组件属于哪个大类以 [组件总清单](../components/overview.md) 为准。
- 组件目录名等于类名。

### 4.1 组件目录文件约定

以 Switch 为例：

```
src/TKCode.Arc/Toggles/Switch/
├── Switch.cs                      public class Switch : Avalonia.Controls.Primitives.ToggleButton, IMotionAwareControl
├── Switch.SemanticParts.cs        partial；[SemanticPart(..., Path = "arc.*")]（只在需要公开定制部件时创建）
├── SwitchToken.cs                 [ControlDesignToken] internal sealed class SwitchToken（与控件同命名空间；可为空）
├── SwitchThumb.cs                 internal presenter（无 identity，由 owner 绑定 Token）
├── Themes/
│   ├── SwitchTheme.axaml          <ControlTheme x:Class="TKCode.Arc.Themes.SwitchTheme" TargetType="arc:Switch">
│   ├── SwitchTheme.axaml.cs       （需要静态常量 / 转换器时）
│   └── SwitchThumbTheme.axaml     presenter 主题
└── Localization/                  （仅当组件有内置文案时）
    ├── SwitchLangResourceKind.cs  [LanguageCatalog] enum
    ├── en-US.xlf
    ├── zh-CN.xlf
    └── zh-TW.xlf
```

规则：

1. `Themes/` 下每个 `.axaml` 都是一个主题资产。文件名以 `Themes` 结尾的会被生成器当作聚合文件跳过，所以**不要**创建 `XxxThemes.axaml` 聚合文件。
2. 主题文件命名为 `XxxTheme.axaml`，便于生成器识别默认 typed theme。
3. 每个公开控件都必须有 `XxxToken.cs`，即使为空（见 [ADR 0005](../decisions/0005-arcdesign-token.md)）。
4. 语义部件声明放在 `Xxx.SemanticParts.cs` 中，Path 以 `arc.` 开头（见 [ADR 0004](../decisions/0004-naming-and-namespaces.md)）。
5. 组件专属枚举、事件参数、数据模型放在组件目录内；被多个组件共享的放到 Core 的 `TKCode.Arc.Primitives`，或 Arc 的 `Primitives/` 目录。
6. 生成文件（`GeneratedFiles/`）不提交到仓库。

### 4.2 Core 目录

```
src/TKCode.Arc.Core/
├── Theme/            ArcDesign.cs、ArcDesignToken.cs、ArcPalette.cs、ArcTokens.cs（绑定辅助）、ArcTokenValueExtension.cs
├── Motion/           Springs/、Values/、Scheduling/、Transitions/、Presence/、Layout/、Policy/
├── Primitives/       每个基元一个目录（含自己的 Themes/），例如 GlidingIndicator/、RollingDigits/
├── Services/         SizeObserver、ViewportVisibility、ListKeyboardNavigator、Gestures、Formatting、PausableTimer
├── Glyphs/           Core 内置的 Lucide 字形子集（控件内部使用的少量图标）
├── Properties/AssemblyInfo.cs
└── ThemeManagerBuilderExtensions.cs   UseArcCore()
```

## 5. 测试与 Gallery 的镜像约定

- 测试路径镜像源码路径：`tests/TKCode.Arc.Tests/Toggles/Switch/SwitchBehaviorTests.cs`。
- Gallery 展示页路径镜像大类：`gallery/TKCode.Arc.Gallery/Pages/Toggles/SwitchPage.axaml`。
- 详细规则见 [测试](../engineering/testing.md) 与 [Gallery 应用](../engineering/gallery-app.md)。
