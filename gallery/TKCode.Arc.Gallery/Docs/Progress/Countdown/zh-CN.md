---
title: 倒计时
summary: 发布倒计时，数字逐位滚动，归零时转为实时状态。
source-hash: ff9aa97fdedb
---

<Demo name="Launch" />

## 何时使用

`Countdown` 以逐位滚动的数字显示距离某个固定时刻还剩多久，或距离从某个时刻起经过一段固定时长还剩多久。当剩余时间是页面或某一行的主要信息时使用它。时刻到达后，控件会自行切换为实时状态，因此页面不需要自己的计时器来切换文案。

- **发布与活动页面**：产品发布、大会主题演讲或在固定时间开始的网络研讨会。大号变体让页面在首屏有一个视觉焦点，数字也便于一眼读出开始时间。
- **促销与限时活动窗口**：结账横幅或定价页上的限时抢购，将 `Target` 设为结束时刻。活动开始时控件切换为“正在进行”，状态变化无需服务端往返。
- **表格与卡片中的截止时间**：账单列表中的付款截止日期、即将过期的发票，或订阅行中即将结束的试用期。`Compact` 变体是一个单行胶囊，能以固定高度放进表格单元格。
- **保留与访问时段**：票务流程中的座位保留，或开发者控制台中的抢先体验时段。`Duration` 从控件显示时起倒数一段固定时长；访客在另一步骤等待时，`IsPaused` 会冻结它。
- **状态页上的计划维护**：维护窗口的剩余时间，并将 `CompleteContent` 设为“维护进行中”，让倒计时结束后的状态与倒计时写在同一位置。

## 何时不用

- **带倒计时和操作按钮的站点横幅**：用 `AnnouncementBar`，它把消息、操作和关闭控件放在同一条横幅中。
- **已完成工作的进度**：用 `Progress` 表示已完成工作的占比。倒计时显示的是剩余时间，而不是工作完成了多少。
- **相对固定上限的消耗**：用 `UsageMeter` 表示相对配额测量的存储、席位或 API 调用量。
- **需要在截止临近时被屏幕阅读器播报的提醒**：`Countdown` 不是实时区域，它的变化不会被播报。需要关注的时刻应弹出 `Alert` 或 `Toast`。
- **随数据变动的数字**：随数据到达而变化的总数（例如今天收到的订单数）用 `AnimatedCounter`。倒计时按时钟运行，只会向零递减。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Countdown Label="距离发布" />
</UserControl>
```

将 `Target` 设为要倒数到的时刻，或将 `Duration` 设为从控件显示时起倒数的固定时长。默认为大号变体，显示天、时、分、秒。

```csharp
launch.Target = new DateTimeOffset(2030, 1, 1, 9, 0, 0, TimeSpan.Zero);
```

`Countdown` 派生自 Avalonia 的 `TemplatedControl`，因此常用的外观属性（`Background`、`BorderBrush`、`BorderThickness`、`CornerRadius` 和 `Padding`）会转发给它的外壳，`Foreground`、`FontFamily` 和 `FontSize` 由主题设置。主题从 Arc 令牌设置数字与单位的字体排版，因此页面无需修改模板就能改变颜色和间距。倒计时不会获得焦点，也没有可见标签：`Label` 只作为无障碍名称。

### 结构

1. **外壳**：一个边框，会弹性伸缩到所含内容的尺寸，因此单位组出现或消失时宽度随之变化。大号变体本身没有背景或边框。紧凑变体绘制一条 1 px 的边框（使用边框颜色）、表面色背景、胶囊圆角和左右各 12 px 的内边距。
2. **数字转轮**：每个数字是一个裁剪的转轮，顶部和底部边缘柔和渐隐。大号变体的数字使用展示字体、中等字重和展示字距，字号随控件宽度变化：宽度的 12.5%，介于 28 px 与 72 px 之间。紧凑变体的数字使用正文字体、无字距，字号与文字相同。
3. **单位名称**：大号变体中，位于每组数字下方，使用弱化文字颜色和超小字号。紧凑变体中，每组数字后跟一个较淡的单位字母，字号为小号。
4. **分隔符**：大号变体中，组与组之间是冒号。紧凑变体将各组显示在同一行。
5. **完成状态**：一个 10 px、成功色的脉动圆点，加上 `CompleteContent` 文字。大号变体的文字使用展示字体、中等字重，字号为宽度的 11%，介于 28 px 与 52 px 之间。紧凑变体的文字使用正文字体和成功色。

### 选择变体

当倒计时是其所在区域的主要内容时，使用 `Large`（默认值）：例如主视觉区块、活动页头部，或有空间容纳四组数字的仪表盘卡片。大号数字需要约 320 px 的宽度才能显示四组，因此应给它整列或整张卡片，而不是侧边栏中的一个单元格。

当倒计时只是众多信息之一时，使用 `Compact`：例如横幅、表格单元格、卡片底部或通知中的一句话。胶囊至少 32 px 高，并随文字调整大小，因此能保持表格行的常规高度。

<Demo name="Compact" />

```xml
<arc:Countdown Variant="Compact"
               Units="Hours, Minutes"
               Label="优惠截止于" />
```

### 选择单位

`Units` 列出要显示的单位，由大到小排列。列出的最大单位会吸收其上的所有单位，因此只设置 `Hours` 时，两天三小时的窗口会显示为 51 小时。默认为天到秒，适合距离发布还有一天以上的活动。时长以小时计的窗口用 `Hours, Minutes` 更易读，较短的保留时段则最适合 `Minutes, Seconds`。

| 值 | 显示内容 |
|---|---|
| `Days` | 整天数；最大单位会吸收小时及以下的单位 |
| `Hours` | 整小时数；不显示天时可以超过 24 |
| `Minutes` | 整分钟数 |
| `Seconds` | 整秒数 |
| `All` | 天、时、分、秒（默认值） |

这些标志可以组合使用，例如 `Hours | Minutes`。`None` 按 `All` 处理。运行时修改 `Units` 或 `Variant` 会原地更新各组，因此页面可以在短显示和长显示之间切换，而无需重新创建控件。

```xml
<arc:Countdown Units="Minutes, Seconds"
               Duration="00:00:12" />
```

### 目标与时长

对每位访客都相同的截止时间用 `Target`：例如发布时刻、促销结束时刻，或存储在服务端的到期日期。从控件显示时开始计算的时长用 `Duration`：例如结账保留、试用计时或审阅窗口。两者同时设置时，`Target` 优先。

目标倒计时在访客离开再返回后，仍会倒数到同一时刻。时长倒计时在离屏期间保留剩余时间，因此访客切走再切回时，看到的是剩下的时间。

### 时区与时钟

文本不含偏移量时，用 `ResolveTarget` 解析目标。带 `Z` 或偏移量的目标文本是精确的。挂钟时间按你传入的时区读取，因此每位访客都倒数到同一时刻。

```csharp
countdown.Target = Countdown.ResolveTarget("2030-01-01T09:00:00", timeZone);
```

`ResolveTarget` 不读取设备的时区。挂钟时间会针对它所指定的日期解析，因此夏令时对该日期生效。若该日期上的某个挂钟时间因时钟跳过而不存在，则解析为之后的那一时刻。

当设备时钟不可信时，通过 `TimeProvider` 属性传入 `TimeProvider`。与服务端同步的时间提供程序能让每位访客的显示与完成时刻使用同一时钟。默认值为 `TimeProvider.System`。

### 暂停、继续与重新开始

时长倒计时在暂停期间保留剩余时间。目标倒计时仍倒数到同一时刻，因此暂停只会冻结显示。目标被暂停时，`Completed` 仍会准时触发。

<Demo name="DurationWithPause" />

```xml
<arc:Countdown Duration="00:00:12"
               IsPaused="{Binding IsTimerPaused, Mode=TwoWay}" />
```

```csharp
timer.Pause();
timer.Resume();
timer.Restart(); // 从完整时长重新开始
```

`IsPaused` 是双向的，因此视图模型可以读取状态，按钮也可以绑定到它。`Restart` 会清除完成状态，并从完整的 `Duration` 重新开始时长倒计时。对于目标倒计时，`Restart` 会重新读取目标。

### 完成

`CompleteContent` 在归零时替换数字，显示在一个脉动的实时圆点旁。默认值为“正在进行”。处理 `Completed` 可在倒计时到达零的那一刻做出一次响应。

```csharp
timer.CompleteContent = "报名已开始";
timer.Completed += (_, _) => offers.Refresh();
```

`Completed` 仅在倒计时显示期间且到达零时触发。控件显示时目标已经过去，则会立即显示完成状态，且不触发 `Completed`，因此在促销结束后才加载的页面不会重复执行优惠代码。倒计时完成后 `Remaining` 为零，`IsCompleted` 则告诉视图模型当前屏幕上显示的是哪种状态。

### 示例：活动页上的产品发布

发布页需要在首屏给出一个关键信息：产品何时可用。倒计时是区块的主角，采用大号变体，配一行简短说明和一个操作。文案写明优惠及其结束时间，让访客知道这个数字的含义。本示例使用时长，以保证每次截图的场景稳定。若每位访客都应在同一时刻看到发布，则改用 `Target`，并以 UTC 或带偏移量的形式写出该时刻。

![产品发布的主视觉，包含大号倒计时、抢先体验说明和主要操作](images/LaunchHero.png)

```xml
<Border Width="680"
        Padding="32"
        CornerRadius="12"
        Background="{arc:ArcDesignTokenResource Surface}"
        BorderBrush="{arc:ArcDesignTokenResource Border}"
        BorderThickness="1">
    <StackPanel Spacing="24">
        <StackPanel Spacing="6">
            <TextBlock Text="Northwind Commerce 2026 Summer Release"
                       FontSize="20"
                       FontWeight="SemiBold"
                       Foreground="{arc:ArcDesignTokenResource Foreground}" />
            <TextBlock Text="Early-access pricing ends when the countdown reaches zero."
                       FontSize="14"
                       TextWrapping="Wrap"
                       Foreground="{arc:ArcDesignTokenResource TextMuted}" />
        </StackPanel>
        <arc:Countdown Duration="3.07:42:18" />
        <StackPanel Orientation="Horizontal" Spacing="12">
            <arc:Button Content="Reserve early access" Variant="Primary" />
            <arc:Button Content="View pricing" Variant="Ghost" />
        </StackPanel>
    </StackPanel>
</Border>
```

布局让倒计时占满卡片的整个宽度，因此大号数字会放大到主题允许的最大尺寸，又不挤压文案。主操作是一个动词加一个对象。次操作是幽灵按钮，让页面只有一个明确的下一步。

### 文案规范

倒计时没有可见标签，因此旁边的文字承担含义。在数字旁完整写出截止时间，并把 `CompleteContent` 写成时间耗尽后读起来依然正确的简短状态。使用句首大写，数字用阿拉伯数字，文案中不要使用催促性的词语。控件负责显示数字和单位名称，周围的文字说明它们在倒数到什么。

| 不要写 | 改为 |
|---|---|
| `Hurry, the time is nearly up` | `Early-access pricing ends 2026-10-29 at 09:00 UTC` |
| `Time remaining`（作为唯一文字） | `Registration closes in`，时区写在下一行 |
| `Done` | `Live now` 或 `Registration is open` |
| `Countdown expired` | `Offer closed`，当优惠不再适用时使用 |

`CompleteContent` 控制在两到三个词，以便放进紧凑胶囊。时区要写在文字里，而不要只放在倒计时中，因为数字本身不会说明该时刻属于哪个时区。

### 最佳实践

**推荐**

- 对所有访客共享的截止时间，为 `Target` 设置偏移量或时区，这样同一时刻在每台设备上都显示一致。
- 在倒计时旁的文字中写明截止的是什么，因为单靠数字无法说明归零时会发生什么。
- 在表格行和横幅中使用 `Compact`，因为大号数字会让密集列表难以浏览。
- 设备时钟不可信时（例如结账会关闭保留），传入与服务端同步的 `TimeProvider`。

**避免**

- 用倒计时表示访客必须在几秒内行动的截止时间，因为控件在变化时不会被播报。结果重要时应弹出 `Alert` 或 `Toast`。
- 同一屏幕上放置多个大号倒计时，因为每个都每秒移动一次，会分散对页面的注意力。
- 目标使用设备的时区，因为其他时区的访客会倒数到不同的时刻。应使用 `ResolveTarget` 并指定活动所属的时区。
- 通过重新开始目标倒计时来表示新的截止时间。`Restart` 只会重新读取同一个目标；应改为设置新的 `Target`。

### 常见问题

**目标已经过去时，倒计时显示什么？** 它会立即显示 `CompleteContent` 对应的完成状态，且不触发 `Completed`。如果页面应为已结束的优惠显示不同的布局，请在视图模型中检查 `IsCompleted`。

**如何倒数一段固定时长，例如 15 分钟的保留？** 将 `Duration` 设为 `00:15:00`，`Units` 设为 `Minutes, Seconds`。倒计时从显示时开始。将 `IsPaused` 绑定到视图模型属性，这样访客阅读表单时，结账步骤就能暂停计时器。

**窗口隐藏时，为什么倒计时不更新？** 控件离屏或不在可视树中时，时钟会停止走动。时长在离开期间保留剩余时间，目标则继续倒数到同一时刻，因此返回时显示是正确的。

**倒计时结束时如何做出响应？** 处理路由事件 `Completed`，或在代码中订阅 `timer.Completed += (_, _) => offers.Refresh();`。该事件仅在控件显示且归零时触发一次。

## API 参考

<!-- api: Countdown -->

### 配套类型

`CountdownUnits` 是一个标志枚举。由于 `Units` 接受它们的组合，这里列出其成员。

| 成员 | 含义 |
|---|---|
| `Days` | 天；作为显示的最大单位时，吸收小时及以下的单位 |
| `Hours` | 小时；作为显示的最大单位时，吸收分钟和秒 |
| `Minutes` | 分钟 |
| `Seconds` | 秒 |
| `All` | 天、时、分、秒 |
| `None` | 未选择任何单位；按 `All` 处理 |

`CountdownVariant` 有两个成员：`Large`（带单位名称的展示数字，默认值）和 `Compact`（用于横幅和表格单元格的单个胶囊）。

## 无障碍

- `Label` 为倒计时命名。默认为“剩余时间”。
- 控件没有实时区域。剩余时间超过一分钟时，朗读的文字省略秒数；倒计时结束后朗读 `CompleteContent`。非文本的 `CompleteContent` 会朗读为“已完成”。
- 可见数字变化时不会被播报，因此屏幕阅读器不会每秒重复。
- 控件类型为文本。可见数字位于原始视图中，朗读的文字是辅助技术读取的值。
- 在倒计时旁放一行文字，完整写明截止的是什么，例如“截止于 2026-10-29 09:00 UTC”。单独的倒计时不说明它在倒数到什么。

### 键盘

`Countdown` 不可获得焦点，也没有键盘交互。对它进行操作的控件（例如同一组中的暂停按钮）应保留在 Tab 顺序中，并绑定到 `IsPaused`。

## 动效

- 每个数字都是一个转轮，沿较短的方向旋转，窗口的顶部和底部边缘柔和过渡。
- 第一次显示时，数字依次滚入。之后每秒变化一次。
- 单位组出现或消失时，容器以弹簧效果伸缩到新尺寸。
- 归零时，数字淡出并缩放到 0.92 进入完成标签，实时圆点脉动。
- 减弱动效时，转轮直接跳到新值，容器立即调整尺寸，内容以淡入淡出切换，并停止脉动。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 数字每秒变化一次 | 转轮沿较短方向旋转；模糊程度随移动距离增大 | 数字直接跳到新值 | 立即 |
| 初始数值出现 | 数字从零开始滚入，每位错开 35 ms，最长 0.3 s | 数字立即显示 | 立即 |
| 单位组出现或消失 | 宽度以变形弹簧伸缩，高度以平滑弹簧伸缩 | 容器立即变为新尺寸 | 立即 |
| 归零 | 数字淡出并缩放到 0.92 进入完成标签 | 内容以淡入淡出切换 | 立即 |
| 实时圆点 | 以两秒为周期脉动 | 停止 | 停止 |

### 克制地使用动效

数字每秒移动一次，足以表明时间在流逝。页面其余部分保持静止，不要在倒计时旁再叠加第二个运动元素，例如闪烁的横幅或循环动画。每个屏幕区域只放一个倒计时。长表格中的多个倒计时各自每秒移动，因此表格应只使用紧凑胶囊，并保持少量行数。

## 相关组件

- `AnnouncementBar`：带倒计时和操作按钮的站点横幅。
- `Progress`：工作有已知进度而非截止时间时使用。
- `UsageMeter`：相对固定配额（例如存储或席位）的消耗。
- `Stepper`：倒计时只是多步骤流程的一部分时使用。
- `Alert` 或 `Toast`：截止时间到达时必须被播报的消息。

| 组件 | 适用场景 |
|---|---|
| `AnnouncementBar` | 全站横幅需要倒计时、消息和按钮 |
| `Progress` | 已完成工作的占比已知 |
| `UsageMeter` | 配额或上限相对当前用量进行测量 |
| `Stepper` | 倒计时属于流程中的某一步 |
| `Alert` / `Toast` | 截止结果必须向访客播报 |
