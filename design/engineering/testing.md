# 测试规范

> 测试分层、Headless 环境、手动帧时钟，以及测试的去留标准。参考 AtomUI 仓库的 `docs/engineering/development/test-value-and-lifecycle.md`。

## 1. 测试栈

| 项 | 选择 |
|---|---|
| 框架 | xunit.v3 |
| UI 环境 | Avalonia.Headless.XUnit（与 Avalonia 12.1.4 同版本） |
| 断言 | Shouldly |
| 替身 | NSubstitute（按需） |
| 目标框架 | net10.0；CI 另跑 net11.0 |
| 并行 | 关闭测试并行（`DisableTestParallelization = true`），与 AtomUI 一致 |

测试应用引导（参照 AtomUI 的 `tests/AtomUI.Desktop.Controls.Tests/AvaloniaTestApp.cs`）：

```csharp
[assembly: AvaloniaTestApplication(typeof(TKCode.Arc.Tests.TestAppBuilder))]
[assembly: AvaloniaTestIsolation(AvaloniaTestIsolationLevel.PerTest)]

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<TestApplication>()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions())
        .UseAtomUI(b => b.UseDesktopControls()
                         .UseArc()
                         .UseLanguages(LanguageTags.EnUS, [LanguageTags.EnUS, LanguageTags.ZhCN, LanguageTags.ZhTW]));
}
```

## 2. 测试分层

| 层 | 项目 | 内容 | 典型去留 |
|---|---|---|---|
| 数学与纯函数 | Core.Tests | 弹簧求解与换算、调色板解析与 OKLCH 推导、格式化、手势公式 | 常驻 |
| Token 契约 | Arc.Tests | 命名冲突、类型允许、identity 完整、明暗刷新、作用域 | 常驻 |
| 架构规则 | Arc.Tests | 见 [编码规范](coding-guidelines.md#3-架构规则的自动检查) | 常驻 |
| 控件行为 | Arc.Tests | 属性、事件、键盘、可访问性 Peer | 关键契约常驻，其余临时 |
| 动效确定性 | Arc.Tests | 用 `ManualFrameClock` 推进：首帧、重定向、等级、分离 | 关键路径常驻 |
| 包消费 | `tests/PackageConsumers/*` | 干净缓存下引用打包结果；裁剪与 AOT 发布冒烟 | CI 作业 |
| 视觉与动效验收 | Gallery | 与 Arc 截图、录屏对照 | 人工验收，不写成自动测试 |

## 3. 手动帧时钟

- 动效相关测试必须使用 `ManualFrameClock`，通过 `Advance(TimeSpan)` 精确推进，不依赖真实时间与 `Task.Delay`。
- 需要渲染的断言配合 `AvaloniaHeadlessPlatform.ForceRenderTimerTick`。
- 断言"空闲时不请求帧"：检查时钟收到的帧请求计数。

## 4. 去留标准

借鉴 AtomUI 的"测试价值与生命周期"原则：

- **默认临时**：探索性测试、复现性测试、spike 中的测试默认是临时的，放在 `tests/<项目>/Temporary/<任务>/` 下，文件头注明"验证后删除"。任务结束前删除，或转为常驻。
- **常驻条件**：同时满足以下四条才保留为常驻测试：
  1. 有明确的契约；
  2. 能独立发现回归（不被其他测试覆盖）；
  3. 长期有益；
  4. 成本合理（单个测试超过 1 秒需要评审）。
- **禁止的低价值模式**：文案快照、数量断言（"有 3 个子元素"）、只为覆盖率存在的测试、墓碑测试。
- 与 AtomUI 不同，Arc 不强制"全局零净增常驻测试"，但每个新增常驻测试在组件规格的"测试计划"中写明理由。

## 5. 验证范围

- 日常开发只运行与改动模块相关的测试（按项目或 `--filter`）。
- 阶段结束、发版前运行全量测试与包消费、AOT 作业。
- 修复缺陷时：先用临时测试复现，修复后决定该测试的去留，最后在清理后的工作区中重新验证受影响的模块。

## 6. 命名

- 测试文件路径镜像源码：`tests/TKCode.Arc.Tests/<大类>/<组件>/<组件><主题>Tests.cs`，例如 `Toggles/Switch/SwitchMotionTests.cs`。
- 常用后缀：`Behavior`、`Keyboard`、`Accessibility`、`ThemeContract`、`Motion`、`SemanticPart`。
- 测试方法名用英文描述行为：`Toggle_Retargets_With_Velocity_When_Clicked_Twice`。
