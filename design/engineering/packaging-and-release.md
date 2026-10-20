# 打包与发布

> 包清单、版本策略与 AtomUI 版本锁、包内容、消费验证与发布节奏。

## 1. 包清单

| 包 | 内容 | 依赖 |
|---|---|---|
| `TKCode.Arc.Core` | 载体 Token、调色板、动效引擎、共享基元、Core 内置字形 | `AtomUI.Desktop.Controls [6.2.6]` |
| `TKCode.Arc` | 110 个控件 | `TKCode.Arc.Core`（同版本）、`AtomUI.Desktop.Controls [6.2.6]` |
| `TKCode.Arc.Icons.Lucide` | Lucide 图标全集 | `AtomUI.Core [6.2.6]` |
| `TKCode.Arc.Fonts` | Geist 字体 | `AtomUI.Controls [6.2.6]` |

## 2. 版本策略

- Arc 使用语义化版本，1.0 之前为 `0.x.y-preview`（里程碑见 [路线图](../roadmap/overview.md#5-里程碑版本)）。
- 每个 Arc 版本只对应一个 AtomUI 版本：依赖写成精确版本 `[6.2.6]`，Avalonia 写成 `[12.1.4]`。
- AtomUI 发布新版本时：
  1. 更新 `build/Versions.props` 中的 `AtomUIVersion` 与 `AvaloniaVersion`；
  2. 阅读 AtomUI 的发版说明（`docs/releases/*-api-changes*.md`），检查白名单 API 是否变化；
  3. 重建，运行全部测试、Token 契约测试、包消费与 AOT 作业；
  4. 发布新的 Arc 版本，在发布说明中更新版本对应表。
- 同一应用中不能混用基于不同 AtomUI 版本构建的 Arc 包。主题资产带全局 Token 指纹，不匹配会在启动时校验失败。

版本对应表（随发布更新）：

| Arc 版本 | AtomUI 版本 | Avalonia 版本 | 目标框架 |
|---|---|---|---|
| 0.1.0-preview（计划） | 6.2.6 | 12.1.4 | net10.0、net11.0 |

## 3. 包内容

| 内容 | 说明 |
|---|---|
| `lib/net10.0/`、`lib/net11.0/` | 程序集与 XML 文档 |
| `buildTransitive/<PackageId>.props` | 由 AtomUI 本地化 targets 在打包时自动生成；不要手写 |
| `contentFiles/**/en-US.xlf` | 同上，由本地化 targets 加入 |
| 符号包 | `.snupkg` |
| `LICENSE`、`THIRD-PARTY-NOTICES.md`、`README.md`、图标 | 由 `build/PackageMetadata.props` 统一配置 |

不包含：

- AtomUI 的生成器、构建任务与注册后端。它们通过 `AtomUI.Desktop.Controls` 的依赖传递给消费者；复制会导致 ATOMUIREG005。
- 生成的源文件（`GeneratedFiles/`）。

包元数据：`PackageLicenseExpression=MIT`，`RepositoryUrl`、`PackageProjectUrl`、`PackageTags`（avalonia;atomui;arc;ui;controls;motion）、`PackageReadmeFile`。

## 4. 消费验证

发布前必须通过以下验证（CI 的 consumers 与 aot 作业）：

1. 打包到 `artifacts/feed`。
2. 清空 NuGet 缓存（或使用隔离的 `NUGET_PACKAGES` 目录）。
3. `tests/PackageConsumers/Minimal`：只引用 `TKCode.Arc`，构建并启动，显示若干组件。
4. `tests/PackageConsumers/Trimmed`：裁剪发布并运行。
5. `tests/PackageConsumers/Aot`：在 net10.0 与 net11.0 下 NativeAOT 发布并运行。
6. 检查发布目录中没有 AtomUI 的构建工具，检查未使用的控件被裁剪（发布收据）。

## 5. 发布节奏

- 里程碑版本按路线图发布。
- AtomUI 发布补丁版本时，Arc 尽快发布对应的补丁版本。
- 发布说明包含：版本对应表、新增组件、破坏性变更、已知问题、已覆盖的语言。

## 6. 包 ID 前缀

- nuget.org 上的 `AtomUI.` 包 ID 前缀可能已被 AtomUI 组织保留。发布前确认前缀归属：
  - 若已保留，需要 AtomUI 组织授权（共同所有权）；
  - 或调整包 ID（例如去掉 `AtomUI.` 前缀），并同步更新程序集名、生成命名空间与文档。
- 此事项列入 [路线图的风险登记](../roadmap/overview.md#6-风险登记)，在首次发布（0.1.0-preview）前解决。
