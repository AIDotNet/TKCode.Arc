# 目标框架与工具链

> SDK、运行时、AtomUI 工具链、AOT 与 CI 的要求矩阵。决策依据见 [ADR 0009](../decisions/0009-tfm-and-sdk.md)。

## 1. 目标框架矩阵

| 项目类型 | 目标框架 | 说明 |
|---|---|---|
| 库（Core、Arc、Icons.Lucide、Fonts） | `net10.0;net11.0` | 打包两套资产 |
| 图标生成器工具 | `net10.0` | 只在开发机运行 |
| 测试 | `net10.0`；CI 另跑 `net11.0` | 共用同一份源码 |
| 消费验证应用 | `net10.0` 与 `net11.0` 各一份 | 包含 NativeAOT 发布 |
| Gallery 宿主 | `net10.0;net11.0` | NativeAOT 发布目标 |

## 2. SDK 与运行时

| 组件 | 要求 | 原因 |
|---|---|---|
| .NET SDK | 11.0.100-rc.2.26504.105 或更新的 11.0 | 构建 net11.0 需要 SDK 11；SDK 11 也能构建 net10.0 |
| .NET 10 运行时 | 10.0.x（同一 dotnet 根目录） | AtomUI 构建任务 worker 是 net10.0，`rollForward: LatestPatch` |
| .NET 11 运行时 | 随 SDK 11 安装 | 运行 net11 测试与应用 |
| 本机现状 | SDK 10.0.401、SDK 11.0.100-rc.2、.NET 10.0.12 运行时 | 满足要求 |

`global.json`：

```json
{
  "sdk": {
    "version": "11.0.100-rc.2.26504.105",
    "rollForward": "latestFeature",
    "allowPrerelease": true
  }
}
```

.NET 11 正式版发布后，把 `version` 改为 GA 版本号，并评估关闭 `allowPrerelease`。

## 3. AtomUI 注册链路在各目标框架下的行为

| 环节 | net10.0 | net11.0 | 依据 |
|---|---|---|---|
| 框架路由 | OfficialTypeMap | OfficialTypeMap（≥10 均走此路径） | `AtomUI.Registration.Framework.targets`、`RegistrationFrameworkPolicy.cs` |
| TypeMap API 检查 | 通过 | .NET 11 RC2 引用包含相同 API，预期通过 | `RegistrationFrameworkResolver.cs` |
| ILLink / ILC 版本校验 | SDK 11 映射 10.0.13，通过（要求主版本一致且 ≥ 10.0.8） | 映射 11.0.0-rc.2，通过 | `TypeMapPublishTasks.cs` |
| 本地化、主题资产、编译主题 targets | 无框架分支 | 无框架分支 | — |
| net8 专用后端 | 跳过 | 跳过 | — |
| Browser 后端 | 要求 SDK 恰好 10.0.300 + `net10.0-browser` | 不支持 | 不在范围内 |

结论：不会出现静默跳过的环节，不支持的情况会以 ATOMUIREG006 等错误失败。net11 不在 AtomUI 的验证范围内，以 Phase 0 的 S0.1 / S0.5 为准。

## 4. AOT 与裁剪

- 库项目设置 `IsAotCompatible=true`、`IsTrimmable=true`，把裁剪与 AOT 警告视为错误。
- 遵循 AtomUI 的 AOT 编程规范：
  - 不做反射扫描；
  - 动态数据走生成器或显式工厂；
  - 不在运行时加载 AXAML 字符串；
  - 不通过类型名字符串创建类型。
- 应用发布时，AtomUI 的 TypeMap 链路会自动保留已启用包的注册内容，Arc 不维护任何 AOT 控件名单。
- 应用不能启用 `InvariantGlobalization`：Arc 的数字、货币与日期格式化依赖文化数据（见 [本地化](localization.md#5-文化数据与-aot)）。
- 验证：
  - S0.5：在 osx-arm64 上分别对 net10.0 与 net11.0 做 NativeAOT 发布，产物能运行、切换主题、播放动画，未使用的控件被裁剪（检查发布收据）。
  - Windows x64 与 Linux x64 的 AOT 验证放在 CI。

## 5. 平台

| 平台 | 状态 |
|---|---|
| Windows x64 / arm64 | 支持 |
| macOS arm64 / x64 | 支持（主开发平台） |
| Linux x64（X11 / Wayland） | 支持 |
| Browser（WASM） | 不支持（见第 3 节） |
| iOS / Android | 不支持（AtomUI.Mobile.Controls 尚不存在） |

## 6. CI 要求

| 作业 | 内容 |
|---|---|
| build | SDK 11 + .NET 10 运行时；Release 构建全部项目（双目标） |
| test-net10 / test-net11 | 分别运行 Core 与 Arc 测试 |
| pack | 打包到 `artifacts/feed` |
| consumers | 清空 NuGet 缓存后构建 `tests/PackageConsumers/*` |
| aot | 三个桌面平台的 NativeAOT 发布与冒烟启动（Gallery + Aot 消费应用） |
| docs | 运行文档校验（见 [文档校验](../engineering/docs-verification.md)） |
