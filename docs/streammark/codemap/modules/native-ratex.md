---
module: native-ratex
sources: ["native/ratex/**", "tools/streammark/build-ratex.sh", "tools/streammark/build-ratex.ps1", "tools/streammark/sync-native.sh", "src/TKCode.Arc.StreamMark.RaTeX/RaTeXNative.cs", "src/TKCode.Arc.StreamMark.RaTeX/RaTeXEngine.cs"]
updated: 2026-11-19
---
# RaTeX 原生库管道

上游 RaTeX 不再是子模块：`native/ratex/ratex-upstream.json` 记录仓库、提交与每个 RID 的 cargo 目标，`native/ratex/patches/*.patch` 是本地补丁。`tools/streammark/build-ratex.sh` 把该提交检出到 `artifacts/src/ratex-<提交前 12 位>/`（每次先恢复成干净状态），按清单顺序打补丁（打不上直接失败），用 cargo 把 `ratex-ffi` 编成动态库写进 `artifacts/native/<rid>/`（同时写 `ratex-commit.txt`）。托管侧只通过 `LibraryImport` 门面 `RaTeXNative` 调用三个 C ABI 函数，拿到的是 UTF-8 JSON 显示列表。KaTeX 字体与许可证复制在 `src/TKCode.Arc.StreamMark.RaTeX/Fonts/`，作为嵌入资源进包。
**不负责：** 显示列表的绘制与数学布局（`avalonia-extensions`，`RaTeXMathLayout.cs`、`RaTeXMathRenderer.cs`、`KaTeXFonts.cs`）；`artifacts/native/<rid>/` 到使用方的分发（`build-and-packaging`）。

## Entry points
| Symbol | File | Role |
|---|---|---|
| `build-ratex.sh`（无参数） | `tools/streammark/build-ratex.sh` | 本机 RID，普通 `cargo build`：本地开发与测试用 |
| `build-ratex.sh <rid>` | `tools/streammark/build-ratex.sh` | 发布用：osx-* 用 `cargo build --target`；linux-* / win-* 用 `cargo zigbuild`，Linux 目标带 `.2.28`（glibc 下限）。Zig 取 `CARGO_ZIGBUILD_ZIG_PATH` / `TKCODE_ZIG` / PATH 上的 zig / `tools/terminal/install-zig.sh` |
| `build-ratex.ps1` | `tools/streammark/build-ratex.ps1` | Windows 本机（MSVC）构建 |
| `sync-native.sh` | `tools/streammark/sync-native.sh` | `artifacts/native/<rid>/` → `src/TKCode.Arc.StreamMark.RaTeX/runtimes/<rid>/native/` |
| `RaTeXNative.ParseAndLayout` | `src/TKCode.Arc.StreamMark.RaTeX/RaTeXNative.cs` | `[LibraryImport("ratex_ffi", EntryPoint = "ratex_parse_and_layout")]`，`string` 按 UTF-8 封送 |
| `RaTeXEngine.Layout` | `src/TKCode.Arc.StreamMark.RaTeX/RaTeXEngine.cs` | 唯一的托管调用点：调原生 → 失败取错误串 / 成功源生成 JSON 反序列化 → `finally` 里释放原生内存 |

## Key types and functions
| Symbol | File | Role |
|---|---|---|
| `RaTeXNative.Library`（`"ratex_ffi"`） | `src/TKCode.Arc.StreamMark.RaTeX/RaTeXNative.cs:8` | 库名常量，决定 .NET 去找 `libratex_ffi.dylib` / `libratex_ffi.so` / `ratex_ffi.dll` |
| `RaTeXNative.RatexOptions` | `src/TKCode.Arc.StreamMark.RaTeX/RaTeXNative.cs:17` | `StructSize` / `DisplayMode`（0 行内、1 块级）/ `Color*`（null = 黑色）；`StructSize` 是上游的前向兼容约定 |
| `RaTeXNative.RatexResult` | `src/TKCode.Arc.StreamMark.RaTeX/RaTeXNative.cs:25` | `byte* Data` + `int ErrorCode`，与 C 端 `RatexResult` 逐字段对应 |
| `RaTeXNative.RatexColor` | `src/TKCode.Arc.StreamMark.RaTeX/RaTeXNative.cs:11` | 四个 `float`（0..1），与 `ratex.h` 的 `RatexColor` 同布局 |
| `MathLayoutResult` | `src/TKCode.Arc.StreamMark.RaTeX/RaTeXEngine.cs:7` | 成功/失败的联合结果；流式输入下公式未写完属正常失败（`src/TKCode.Arc.StreamMark.RaTeX/RaTeXEngine.cs:6`） |
| `DisplayList` | `src/TKCode.Arc.StreamMark.RaTeX/DisplayList.cs:9` | JSON 协议 v1 的反序列化目标：`Version` / `Width` / `Height`（基线以上）/ `Depth`，em 单位、Y 轴向下 |
| `DisplayItem` | `src/TKCode.Arc.StreamMark.RaTeX/DisplayList.cs:18` | 扁平结构（非多态）：`Type` ∈ GlyphPath/Line/Rect/Path，未知类型由绘制端跳过（`src/TKCode.Arc.StreamMark.RaTeX/DisplayList.cs:20`） |
| `PathCommand` / `DisplayColor` | `src/TKCode.Arc.StreamMark.RaTeX/DisplayList.cs:36`、`:48` | MoveTo/LineTo/CubicTo/QuadTo/Close；颜色为 float RGBA |
| `DisplayListJsonContext` | `src/TKCode.Arc.StreamMark.RaTeX/DisplayList.cs:50` | `JsonKnownNamingPolicy.SnakeCaseLower` 源生成上下文（AOT 安全；`char_code`、`display_mode` 这类 snake_case 字段名靠它对齐 `ratex.h:34-41`） |
| `native/ratex/ratex-upstream.json` | `native/ratex/ratex-upstream.json` | 上游提交、`patches` 列表（2026-11-23 升级至 `d49c6fad` 后上游已包含 `\fbox`/`\boxed` 边框颜色修复，当前无本地补丁）与各 RID 目标 |

## Contracts and traps
- **库名约定**：`"ratex_ffi"` 在各平台解析到 `libratex_ffi.dylib` / `libratex_ffi.so` / `ratex_ffi.dll`；RID 与文件名的对应写在 `ratex-upstream.json` 的 `build.perRid`，`sync-native.sh` 与 CI 的包检查用同一组文件名。
- **ABI 与协议**：三个函数的签名与 `RatexOptions` 布局以上游 `crates/ratex-ffi/include/ratex.h` 为准（检出后在 `artifacts/src/ratex-*/` 下）；显示列表 JSON 协议 v1 用 snake_case 源生成上下文（`DisplayListJsonContext`）反序列化，未知字段 / 条目类型被忽略。
- **线程**：原生错误状态是线程局部的，托管侧可并发调用；`ratex_get_last_error` 的指针只在同线程下一次调用前有效，立即拷贝成 `string`。
- **升级上游**：改清单里的 `commit`，重新复制该提交的 `fonts/*.ttf` 与 `licenses/*` 到 `Fonts/`；若有本地补丁且打不上会让构建失败，上游已包含修复时从清单移除并删除补丁文件。
- **漏复制原生库的症状**：运行时 `DllNotFoundException: ratex_ffi`（AOT 下启动即崩），不是编译错误；仓库内可执行项目要设 `ArcStreamMarkNative=true`。
