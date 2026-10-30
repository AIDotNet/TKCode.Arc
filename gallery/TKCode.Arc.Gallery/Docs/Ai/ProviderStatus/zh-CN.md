---
title: Provider status
summary: 一张展示产品所依赖的 AI 服务商状态的卡片：以图标和标签表示健康状况，显示中位延迟和每天都带提示的 30 天可用率条，事件可以展开查看更新，下一次检查有可见的倒计时。
source-hash: 59e821a9305c
---

<Demo name="Preview" />

## 何时使用

`ProviderStatus` 是一张卡片，列出产品所路由到的 AI 服务商。每一行显示服务商的标志、以图标和标签表示的健康状况、中位（p50）延迟，以及由一个个小刻度组成的 30 天可用率条，每一天都有自己的提示。带有事件的行是一个按钮，点击后按从新到旧的顺序展开事件的更新。标题栏汇总所有服务商的健康状况，并按可见的倒计时环再次检查。

- **产品所依赖的模型服务商的实时健康状况**，放在设置页或管理仪表板上。
- **向用户或管理员解释 AI 功能为何降级**，并就地展示服务商的事件更新。
- **路由与回退设置**，需要让人看清此刻哪些服务商是健康的。

## 何时不用

- **完整的公开状态页**，包含组件、订阅和历史：应当做成一个页面，而不是卡片。
- **聊天标题栏中单个服务商的状态**：使用 `AgentStatus` 或一个小徽标。
- **随时间变化的详细延迟**：使用图表。

## 用法

传入你的服务商，以及一个从你自己的聚合端点获取最新状态的 `RefreshHandler`。不要在客户端轮询服务商的状态页。处理程序返回新的服务商列表，或者自行更新 `Providers` 并返回 `null`。它在倒计时结束时和点击刷新按钮时运行；新的检查会通过取消令牌取消较早的检查。

```csharp
card.Providers = await statusApi.GetProvidersAsync();
card.RefreshHandler = async token =>
{
    var response = await http.GetAsync("/api/provider-status", token);
    if (!response.IsSuccessStatusCode)
    {
        throw new InvalidOperationException("Couldn’t reach the status feeds. Showing the last results.");
    }
    return await response.Content.ReadFromJsonAsync<List<ProviderStatusItem>>(token);
};
card.IncidentOpened += (_, e) => OpenBrowser(e.Incident.Url);
card.Footnote = "Latency is the median over the last 5 minutes.";
```

既没有服务商、处理程序，也没有模拟时，卡片显示空状态：标题区显示 "No providers"，刷新控件隐藏。

### 模拟检查

用于演示或原型时，不设置 `Providers` 和 `RefreshHandler`，而是给卡片一个带有若干 `ProviderStatusSimulationStep` 的 `ProviderStatusSimulation`。卡片立即显示第一步，并每隔 `Interval`（8 秒）进行一次模拟检查：每次检查耗时 `CheckDelay`（900ms），然后进入下一步并循环，因此事件会开启、发布更新并解决；带有 `Fails` 的步骤会显示错误状态并保留上一次的结果。其 Status page 链接会说明该操作是模拟的；没有 `Footnote` 时，卡片会注明数据是示例。

```csharp
card.Simulation = new ProviderStatusSimulation
{
    Steps =
    [
        new ProviderStatusSimulationStep { Providers = beforeTheIncident },
        new ProviderStatusSimulationStep { Providers = incidentOpened },
        new ProviderStatusSimulationStep { Fails = true },
        new ProviderStatusSimulationStep { Providers = resolved },
    ],
};
card.Footnote = "Sample data: the incidents and numbers here are invented.";
```

预览播放的就是这样一段故事，使用真实的服务商名称及其状态页列出的组件；其中每个事件、延迟和可用率数字都是虚构的，绝不是服务商的真实历史，脚注也会说明这一点。

### 你的服务商

<Demo name="OwnData" />

每个 `ProviderStatusItem` 有一个 `Id`（只要它还在，行就会被复用）、一个 `Name`、一行 `Detail`（例如你调用的模型）、它的 `Health`、以 `LatencyMs` 表示的中位延迟，以及它的 `Days`。卡片会自行格式化数字（412 ms、1.92 s、99.61%），因此请以普通数字传入。

`Mark` 是一个 24 × 24 的描边图形，以 16px 和文字颜色绘制；传入服务商标志的路径即可，`MarkBrush` 用于为它着色。没有标志时，行内会在一个着色的方块中显示名称的首字母。

### 可用率条

`Days` 是按日记录的历史，最早的在前。可用率条显示截至最后一天的 30 天，最后一天标为 Today，缺失的日期补为 No data。每一天有一个 `Date`、一个 0 到 100 的 `Uptime`（或 null）、一个可选的 `Health`（省略时由可用率推导：低于 99.9 为降级，低于 99 为中断），以及一条用于提示的可选 `Note`。条旁边的百分比是所有有数据的日子的平均值。

整张卡片共用一个提示：指向某个刻度、轻点它，或在条上横向拖动，提示就会逐日移动。

### 事件

在服务商上设置 `Incident`，它所在的行就会变成一个按钮，用于展开事件的 `Updates`。每条更新有一个 `Stage`（Investigating、Identified、Monitoring、Resolved）、一个以 `At` 表示的时间（显示为 HH:MM UTC）或预先格式化的 `AtText`，以及一条 `Message`。更新按从新到旧显示；新的更新会在顶部淡入，同时面板随之变高。

`ExpandedId` 指明当前展开的服务商，是双向的；`ExpandedChanged` 报告用户展开和收起行。更新下方的 Status page 链接会触发 `IncidentOpened`。没有任何处理程序处理它时，链接会打开事件的 `Url`；两者都没有时，链接隐藏。

### 检查与错误

`RefreshInterval` 设置两次自动检查之间的时间（默认 30 秒）。倒计时环朝下一次检查逐渐消退，卡片不在屏幕上或被隐藏时会暂停。检查进行期间，一个短暂的加载圈和一个闪烁的 "Checking" 取而代之。处理程序抛出异常时，标题栏显示异常的消息，上一次的结果保留在屏幕上，倒计时变为重试，最多等待 10 秒。`RefreshInterval="{x:Null}"` 会关闭自动检查，但保留刷新按钮。没有处理程序时，刷新控件隐藏。`CheckNow()` 会立即检查。

### 状态

<Demo name="States" />

`IsLoading` 在首次加载时显示行高相同的骨架行。`ErrorText` 在标题栏中显示你自己的错误，例如首次加载失败。

### 窄卡片

<Demo name="Narrow" />

卡片宽度不小于 560px 时，每一行占一行，分列显示状态、p50 延迟、可用率条和 30 天百分比。更窄的卡片每行占两行：上面是名称和健康状况，延迟位于名称下方；下面是占满宽度的可用率条及其百分比。这条规则跟随卡片，而不是窗口，在 360px 时依然可用；过长的名称和说明以省略号结尾。

### 自定义文案

`Labels` 包含卡片显示或朗读的每一个词；数字、日期以及带数值的句子是函数：

```csharp
card.Labels = new ProviderStatusLabels
{
    Operational = "Opérationnel",
    Degraded = "Dégradé",
    Outage = "Panne",
    NextCheck = seconds => $"Prochaine vérification dans {seconds} s",
};
```

### 最佳实践

- 把 `RefreshHandler` 接到你自己的、汇总各服务商状态源的端点上。
- 用 `Footnote` 标注示例数据或合成数据。
- `Detail` 保持简短，例如你调用的模型；空间不够时它会以省略号结尾。
- 事件解决后，在服务商上再保留一段时间，让人能读到发生了什么。

## API 参考

<!-- api: ProviderStatus -->

<!-- api: ProviderStatusItem -->

<!-- api: ProviderStatusDay -->

<!-- api: ProviderStatusIncident -->

<!-- api: ProviderStatusUpdate -->

<!-- api: ProviderStatusLabels -->

<!-- api: ProviderStatusSimulation -->

<!-- api: ProviderStatusSimulationStep -->

## 无障碍

- 卡片是一个以标题命名的分组。各行组成一个列表；每一行以服务商及其健康状况命名。
- 事件行有一个覆盖整行的按钮，会报告展开或收起状态，并以服务商、健康状况和事件标题命名。
- 健康状况从不只靠颜色表达：每种状态都有图标和标签，每一天的健康状况也写在它的提示和可用率条的名称中。
- 每个可用率条是一个 Tab 停靠点，以其 30 天的摘要命名。方向键在各天之间移动，一个礼貌的实时区域逐一朗读，因此 180 个刻度不会挤满 Tab 顺序。
- 同一个实时区域还会播报检查、失败的检查、`Notice` 以及模拟操作（"Simulated, no requests were sent"）。
- 键盘焦点在行按钮内侧和可用率条周围显示焦点环；指针焦点不显示。

### 键盘

| 按键 | 操作 |
|---|---|
| Tab | 依次移到刷新按钮、每个事件行和每个可用率条 |
| Enter、Space | 在事件行上展开或收起其更新 |
| 左方向键、右方向键 | 在可用率条上把日期提示向前或向后移动一天 |
| Home、End | 在可用率条上跳到最早的一天或今天 |
| Escape | 在可用率条上隐藏日期提示 |

## 动效

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 健康状况、延迟、可用率和摘要变化 | 在预留的单元格中做一次交叉淡化：旧内容 100ms 淡出，新内容 160ms 淡入并上升 2px | 交叉淡化，不上升 | 立即 |
| 事件展开或收起 | 高度以弹簧过渡到测得的内容高度（0.24s，无回弹），内容淡入淡出 | 立即 | 立即 |
| 新的更新到达 | 在顶部淡入，同时面板以弹簧过渡到新高度 | 立即 | 立即 |
| 刻度的健康状况变化 | 颜色在 240ms 内过渡 | 颜色在 240ms 内过渡 | 立即 |
| 倒计时 | 环线性消退；秒数每秒更新一次 | 相同 | 相同 |
| 检查中 | 短暂的加载圈和闪烁的标签 | 静止的标签，没有加载圈 | 静止的标签，没有加载圈 |
| 骨架行 | 脉动 | 静止 | 静止 |
| 箭头 | 以弹簧转动 | 立即 | 立即 |

卡片不在屏幕上或所在窗口被隐藏时，倒计时会暂停。各行的列宽固定，因此数值变化时不会重新排版。

## 相关组件

- `AgentStatus`：单次智能体运行的实时状态。
- `ToolCall`：智能体对话记录中的一次工具调用。
- `Tooltip`：简短的辅助说明文字。
