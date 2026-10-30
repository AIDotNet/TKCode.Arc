---
title: Building blocks
summary: 为 AI 应用生成器准备的加载画面：圆角积木无回弹地拼进一个小小的应用布局，填上内容并染上强调色，标签则从“正在布局”一步步走到“完成”。
source-hash: 99c1dbd6aadf
---

<Demo name="Preview" />

## 何时使用

`BuildingBlocks` 在 AI 生成应用时展示应用正被拼装出来的过程。导航栏、主卡片、图表、头像行、设置行和按钮这些圆角积木飞入，
无回弹地吸附进一个小小的应用布局。它们先以着色的占位块落位，接着填上逼真的内容，再在样式阶段染上主题色；状态行则依次显示
Laying out、Adding components、Styling 和 Ready。

- **生成式 UI 产品**：需要一个加载状态，展示应用、页面或仪表板正在被搭建。
- **将返回界面的助手回复**：在代码或布局流式到达期间显示。
- **空状态或引导步骤**：一眼说明 AI 生成器在做什么。

## 何时不用

- **纯文本回答**：改用闪烁的标签或思考指示。
- **图像生成**：直接逐步显示图像本身。
- **步骤和日志都已知的真实进度**：使用 `AgentStatus` 或 `AgentPlan`，让人看到实际发生了什么。
- **与页面真实布局一致的占位**：使用 `Skeleton`。

## 用法

把请求状态映射到 `Status`：请求进行中设为 `Building`，结果到达设为 `Done`，第一次提示之前设为 `Idle`。构建中途收到的
`Done` 会快进剩下的阶段并停在 Ready。不设置 `Status` 时播放内置的模拟构建，它在 Ready 上短暂停留后循环。

```csharp
blocks.Status = BuildingBlocksStatus.Building;
var result = await GenerateAsync(prompt);
blocks.Status = BuildingBlocksStatus.Done;
```

### 由应用驱动

<Demo name="HostDriven" />

`Duration` 是一次构建的时长，从第一块积木到样式结束，平均分给三个阶段（默认 4.8 秒，最少 2.4 秒）。把它设得接近你通常的
生成时间。`Status` 为 `Building` 且 `IsLooping` 开启时，较长的生成会不断重新拼装而不是停住；关闭 `IsLooping` 则停在
Styling。

`AllowsReplay` 在 Ready 时显示 Replay 按钮。按下会触发 `ReplayRequested`：重新开始生成，并把 `Status` 设回 `Building`。
`PhaseChanged` 报告状态行显示的每个阶段，可用于统计或让其他界面保持同步，`Phase` 保存当前阶段。

### 结构

1. **框架**：铺在柔和底色上的迷你应用，每块积木都有一个虚线槽位。
2. **积木**：导航栏、带商品图的主卡片、图表、头像行、带开关的设置行和按钮。
3. **状态行**：标记（构建时是四个小方块，Ready 时是对勾）、阶段标签和 Replay。它的高度固定，换标签或出现 Replay 都不会
   挪动任何东西。

### 尺寸

<Demo name="Sizes" />

`Size` 决定框架能长到的宽度：`Small` 280px、`Medium` 400px（默认）和 `Large` 560px。`StageWidth` 可以改用任意像素宽度。
框架永远不会超出容器，布局、圆角和描边都随宽度缩放，因此在 240px 到 880px 之间都保持比例。窄于 320px 时状态行使用 12px
字号。

### 色调与着色

<Demo name="Tones" />

`Tone` 决定占位块、按钮、图表、开关和状态标记的颜色：`Accent`（默认）或使用前景色的 `Neutral`。`Tint` 可以改用任意颜色，
适合品牌色或按产品着色。

### 只播放一次

<Demo name="PlayOnce" />

没有状态时，`IsLooping="False"` 让模拟构建只播放一次，停在 Ready 并提供 Replay，按下会重新构建。

### 不显示状态行

<Demo name="NoLabel" />

周围界面已经说明正在发生什么时，用 `IsLabelVisible="False"` 隐藏状态行。

### 自定义文字

`Labels` 保存所有文字。可以写明正在构建的东西，或进行翻译：

```csharp
blocks.Labels = new BuildingBlocksLabels
{
    Ready = "Your dashboard is ready",
    Replay = "Build again",
};
```

### 减少动效

<Demo name="ReducedMotion" />

减少动效时，占位、内容和样式都以简单的交叉淡化出现，没有位移，构建只播放一次。关闭动效时每一步都立即切换。阶段仍会推进：
它们是信息，而不是装饰。

### 内容建议

- 在 Ready 标签里写明结果（“Your CRM is ready”），而不是只重复“Ready”。
- 模拟构建留给演示和空状态；真实产品里请由请求驱动 `Status`。
- 不要假装汇报进度：阶段只是节奏，不是测量。

### 最佳实践

- 把 `Duration` 设得接近你通常的生成时间，让 Ready 与结果同时到来。
- 每个待返回的结果用一个实例；页面上要显示很多个时优先使用 `Small`。
- 把它放在结果将出现的位置（回复气泡、画布、空状态），让布局自然交接给结果。

### 常见问题

**结果提前到达怎么办？** 把 `Status` 设为 `Done`：所有积木立即落位、填充并染色，过渡很短，标签显示 Ready。

**滚出屏幕时还在运行吗？** 不会。框架被滚走、隐藏或位于隐藏的窗口时，日程和状态标记都会暂停，回来后从原处继续。

## API 参考

<!-- api: BuildingBlocks -->

<!-- api: BuildingBlocksLabels -->

<!-- api: BuildingBlocksPhaseChangedEventArgs -->

## 无障碍

- 框架是装饰性的，对辅助技术隐藏；阶段通过礼貌（polite）的实时区域播报，读屏软件会听到 Laying out、Adding components、
  Styling 和 Ready。
- 模拟构建会说明自己是模拟的（帮助文本“Simulated build, nothing was generated”），循环播放时不会在每一轮都播报标签。
- Ready 同时显示对勾和标签，状态从不只依赖颜色。
- Replay 是带可见文字的 Arc `Button`。

### 键盘

| 按键 | 操作 |
|---|---|
| Tab | Ready 时出现 Replay 后可聚焦到它；框架本身不获取焦点 |
| Enter、Space | 按下 Replay |

## 动效

| 变化 | 完整 | 减少 | 关闭 |
|---|---|---|---|
| 积木到达 | 从槽位外侧稍小的位置飞入，0.52 秒临界阻尼弹簧（无过冲），间隔 40ms，全部在第一块之后 200ms 内出发 | 原地淡入 240ms | 立即 |
| 占位填充 | 320ms 内过渡为中性内容，间隔 60ms；条形从左缘长出，图表折线在 600ms 内画出 | 240ms 交叉淡化 | 立即 |
| 样式阶段 | 着色、照片和头像在 400ms 内淡入，间隔 50ms；开关滑动一次 | 240ms 交叉淡化；开关直接换边，不滑动 | 立即 |
| 阶段标签 | 旧标签 100ms 淡出，新标签 160ms 淡入并上升 2px，行位置固定 | 120ms 交叉淡化 | 立即 |
| Replay | Ready 时淡入 | 淡入 | 立即 |
| 状态标记 | 一道光依次绕过四个小方块，每块 300ms | 每个阶段点亮一块 | 每个阶段点亮一块 |
| 循环 | 完成的布局原地在 320ms 内淡出，在看不见时复位，再在同样的虚线槽位上重新构建 | 只播放一次 | 只播放一次 |

每次变化都从屏幕上的当前值开始，所以构建中途改变状态也不会生硬切换。框架离开屏幕或被隐藏时，日程和标记都会暂停。

## 相关组件

- `AgentStatus`：展示含真实步骤的智能体运行状态。
- `AgentPlan`：智能体遵循的计划。
- `Skeleton`：与页面真实布局一致的占位。
