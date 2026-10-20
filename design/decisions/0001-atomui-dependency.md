# 0001 AtomUI 依赖方式

- 状态：已接受
- 日期：2026-10-20

## 背景

- AtomUI 6.2.6 已发布到 nuget.org（AtomUI.Core、AtomUI.Controls、AtomUI.Desktop.Controls、AtomUI.Desktop.Controls.Extras、AtomUI.Icons.Shared、AtomUI.Toolkits.GalleryBase 等）。
- AtomUI 为第三方控件包提供了正式接入路径，并经过"独立包作者 + 冷消费"验证。见 AtomUI 仓库 `docs/guides/theming/third-party-control-packages.md` 与 `docs/guides/startup/package-authoring.md`。
  - 引用 `AtomUI.Desktop.Controls` 后，生成器、主题资产包装任务、注册与本地化 targets 会经 `buildTransitive` 自动接入，无需额外配置。
  - `AtomUI.Core` 包本身不带这些构建工具。
- AtomUI 的生成物与 AtomUI 版本强绑定：
  - 主题资产带全局 Token 名称的指纹，版本不匹配时启动校验失败。
  - 启动清单版本为 2；注册组标识中混入包版本。
  - AtomUI 发版说明（`docs/releases/6.2.6-api-changes.zh-CN.md`）要求自定义包随 AtomUI 版本重建。
- AtomUI 使用 Avalonia 私有 API（`AvaloniaAccessUnstablePrivateApis=true`），Avalonia 版本也必须与 AtomUI 完全一致。

## 决策

1. 通过 NuGet 引用 AtomUI，使用中央包管理（`Directory.Packages.props`）。
2. AtomUI.* 精确锁定 `[6.2.6]`，Avalonia* 精确锁定 `[12.1.4]`，开启 `CentralPackageTransitivePinningEnabled`。
3. AtomUI 每次发版，Arc 都基于新版本重建、验证并重新发版；Arc 的版本说明中注明对应的 AtomUI 版本。
4. 控件项目只直接引用 `AtomUI.Desktop.Controls`（它会传递 AtomUI.Core 等依赖与构建工具）；不要把 AtomUI 的工具复制进 Arc 的包（混用工具包会报 ATOMUIREG005）。

## 备选方案

- **本地源码 ProjectReference**：直接引用 `/Users/token/code/AtomUI` 下的项目，可以边开发边修 AtomUI。
  - 需要手工导入生成器与注册 targets，构建会与 AtomUI 仓库的工具链、目录布局和 SDK 锁定（10.0.300）耦合。
  - 未选。
- **可切换**：默认 NuGet，开关切到本地源码。
  - 要同时维护两套接入路径，验证矩阵翻倍。
  - 未选。需要联调 AtomUI 时，可临时在本地 feed 发布 AtomUI 的预览包来替代。

## 后果

- 正面：
  - 走 AtomUI 官方验证路径，消费方式与普通第三方包一致。
  - Arc 与 AtomUI 的发版节奏解耦；LGPL 依赖以"链接已发布二进制"的方式满足。
- 负面：
  - 必须跟随 AtomUI 版本节奏重建，不能混用不同 AtomUI 版本的 Arc 包。
  - 发现 AtomUI 缺陷时只能向上游反馈或绕开，不能直接改源码。
- 配套：
  - Phase 0 spike S0.4 验证"干净缓存 + 只引用 Arc 包"的消费应用可构建、可启动。
  - [打包与发布](../engineering/packaging-and-release.md) 记录版本对应表。

## 关联文档

- [AtomUI 集成](../architecture/atomui-integration.md)
- [目标框架与 SDK](0009-tfm-and-sdk.md)
- [打包与发布](../engineering/packaging-and-release.md)
