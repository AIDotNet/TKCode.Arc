---
module: build-and-packaging
sources: ["build/ProjectDefaults.props", "build/Arc.Repository.targets", "Directory.Packages.props", "TKCode.Arc.slnx", ".github/workflows/ci.yml", ".github/workflows/ci-macos.yml", "src/TKCode.Arc.StreamMark*/*.csproj"]
updated: 2026-11-19
---
# 构建与打包（并入 TKCode.Arc 之后）

StreamMark 的四个包是 Arc 仓库 `src/` 下的普通库项目，沿用 Arc 的构建：`build/ProjectDefaults.props` 给 `src/` 下的项目开启可打包、`IsTrimmable`、`IsAotCompatible`、XML 文档与 `Nullable` 错误；`build/Arc.Repository.targets` 开启 PublicAPI 检查（RS0016 / RS0017）；版本是 `build/Versions.props` 的 `ArcVersion`，包随 Arc 每次发布一起打。
**不负责：** RaTeX 原生库怎么编（`native-ratex`）；Gallery 的 `--smoke` 检查了什么（`gallery-and-benchmarks`）。

## 项目与包

| 项目 | 包 | 目标框架 | 依赖 |
|---|---|---|---|
| `src/TKCode.Arc.StreamMark` | `TKCode.Arc.StreamMark` | `net8.0;net10.0;net11.0`（`ArcTargetFrameworks`） | Avalonia、Markdig |
| `src/TKCode.Arc.StreamMark.TextMate` | `TKCode.Arc.StreamMark.TextMate` | 同上 | TextMateSharp、TextMateSharp.Grammars、基础包 |
| `src/TKCode.Arc.StreamMark.RaTeX` | `TKCode.Arc.StreamMark.RaTeX` | `net10.0;net11.0` | Avalonia、基础包；`runtimes/<rid>/native/` 原生库 |
| `src/TKCode.Arc.StreamMark.Mermaid` | `TKCode.Arc.StreamMark.Mermaid` | `net10.0;net11.0` | Avalonia、Avalonia.Skia、Mermaider、Svg.*、基础包 |
| `tests/TKCode.Arc.StreamMark.Tests` | — | `ArcTestTargetFramework`（net10.0） | 四个包 |
| `tools/streammark/{Gallery,CpuHarness,Benchmarks}` | — | net10.0 | Gallery / CpuHarness `PublishAot=true` |

依赖表由 `tests/TKCode.Arc.Tests/Architecture/PackageReferenceTests.cs` 固定；四个包没有 `UseArc` 入口，列在 `PackageEntryTests` 的例外表里。

## 规则与陷阱

- **裁剪 / AOT 警告即错误**：`build/ProjectDefaults.props` 只对名字以 `TKCode.Arc.StreamMark` 开头的库加 IL 警告列表（沿用独立仓库的门禁）；CI 另外要求 Gallery 的 NativeAOT 发布日志里 0 个 `warning IL`。
- **Svg.Skia 版本组合**：`Directory.Packages.props` 把 Svg.Skia / Svg.Animation 固定在 5.1.1（SkiaSharp 3，与 Avalonia 12 同一套原生库），Svg.Model / Svg.Custom / ShimSkiaSharp / Svg.SceneGraph 固定在 5.2.3。Arc 开启了传递依赖固定，所以这组版本也约束引用 Mermaid 包的应用；不要改成 `VersionOverride`。见 [[svgia-version-pin]]。
- **原生库两条路径**：包内 `runtimes/<rid>/native/`（`tools/streammark/sync-native.sh` 从 `artifacts/native/<rid>/` 复制，目录不提交）；仓库内可执行项目设 `ArcStreamMarkNative=true`，由 `build/Arc.Repository.targets` 按自己的 RID 从 `artifacts/native/<rid>/` 复制（SDK 不把 `RuntimeIdentifier` 传给被引用项目）。见 [[native-library-two-paths]]。
- **XAML 里引用程序集名**：类型的命名空间仍是 `StreamMark.Avalonia`，程序集名是 `TKCode.Arc.StreamMark`，XAML 写 `clr-namespace:StreamMark.Avalonia;assembly=TKCode.Arc.StreamMark`。
- **`Core/` 与 `Avalonia/` 在同一程序集**：编译器不再拦 Core 引用 Avalonia，靠 `tests/TKCode.Arc.StreamMark.Tests/Core/CoreLayerTests.cs` 扫源码。

## CI

- `ci.yml` 的 `streammark` 作业（ubuntu、windows）：构建本机 RaTeX → `dotnet test tests/TKCode.Arc.StreamMark.Tests` → Gallery NativeAOT 发布（0 个 IL 警告）+ `--smoke`。`ci-macos.yml` 有同样的 macOS 作业。
- `ci.yml` 的 `native` 作业（发布时）：Linux 主机用 cargo-zigbuild 编 linux-x64 / linux-arm64 / win-x64 / win-arm64，macOS 主机编 osx-arm64 / osx-x64；`pack` 作业 `tools/streammark/sync-native.sh` 后打包，并检查 RaTeX 包含六个 RID 的库。
