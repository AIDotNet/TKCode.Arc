# 0009 目标框架与 SDK

- 状态：已接受（net11 部分待 Phase 0 spike S0.1 / S0.5 验证）
- 日期：2026-10-20

## 背景

- 要求：组件库支持 .NET 10 与 .NET 11。
- AtomUI 6.2.6 的包提供 net10.0 与 net8.0 资产；Avalonia 12.1.4 提供 net10.0 与 net8.0。net11.0 项目可以消费 net10.0 资产。
- AtomUI 的注册链路按目标框架路由（`AtomUI.Registration.Framework.targets`）：
  - .NETCoreApp 10.0 及以上走 OfficialTypeMap，net8 走兼容后端，其他（如 net9）在发布时报 ATOMUIREG006。
  - .NET 11 RC2 的引用包含相同的 TypeMap API（`TypeMapAttribute<T>`、`TypeMapping.GetOrCreateExternalTypeMapping<T>()`）。
  - 工具链校验要求 ILLink / ILC 主版本与目标框架一致，且 ≥ 10.0.8。SDK 11 RC2 为 net10.0 映射 10.0.13，为 net11.0 映射 11.0.0-rc.2，均满足。
- AtomUI 的构建任务 worker 是 net10.0 程序，`rollForward: LatestPatch`。因此任何构建机都必须安装 .NET 10 运行时，即使只构建 net11。
- AtomUI 的 Browser 发布锁定 SDK 10.0.300 + `net10.0-browser`；本机为 10.0.401，无法满足，net11 更不支持。
- AtomUI 文档声明的产品框架是 net10.0；net11 不在 AtomUI 的验证范围内。
- 本机已装：SDK 10.0.401、SDK 11.0.100-rc.2.26504.105、.NET 10.0.12 运行时。

## 决策

1. 库项目（Core、Arc、Icons.Lucide、Fonts）的 `TargetFrameworks` 为 `net10.0;net11.0`。
2. `global.json` 锁定 SDK 11（`allowPrerelease: true`，`rollForward: latestFeature`）；.NET 11 GA 安装后会自动采用：
   ```json
   { "sdk": { "version": "11.0.100-rc.2.26504.105", "rollForward": "latestFeature", "allowPrerelease": true } }
   ```
3. 构建机与 CI 必须同时安装 SDK 11 与 .NET 10 运行时。
4. 测试项目以 net10.0 为主，CI 另加 net11.0 运行。Gallery 与消费验证应用分别在 net10.0 与 net11.0 下做 NativeAOT 发布验证。
5. 平台范围：Windows、macOS、Linux 桌面。Browser（WebAssembly）不在范围内。
6. 库设置 `IsAotCompatible=true` 与 `IsTrimmable=true`，遵循 AtomUI 的 AOT 编程规范：不使用反射扫描，动态数据走生成器。

## 备选方案

- **只支持 net10.0**：最稳妥，但不满足需求。未选。
- **net8.0;net10.0;net11.0**：AtomUI 的 net8 链路依赖锁定的 SDK 10.0.300 与 net8 专用的 ILLink / ILC 后端，复杂且不是需求。未选。

## 后果

- 正面：一套源码覆盖 .NET 10 与 .NET 11；net11 的 AOT 校验在原理上可行。
- 负面：
  - net11 在 .NET 11 GA 前依赖 RC SDK；
  - net11 不在 AtomUI 的验证范围内，遇到问题时需自行排查或向上游反馈；
  - 构建机必须额外安装 .NET 10 运行时。
- 验证：
  - S0.1：net10 与 net11 双目标都能构建，零 ATOMUIGEN / ATOMUIREG 诊断。
  - S0.5：osx-arm64 下 net10 与 net11 的 NativeAOT 发布产物都能运行、切换主题并播放动画。

## 关联文档

- [目标框架与工具链](../architecture/target-frameworks.md)
- [解决方案布局](../architecture/solution-layout.md)
- [Phase 0](../roadmap/phase-0.md)
