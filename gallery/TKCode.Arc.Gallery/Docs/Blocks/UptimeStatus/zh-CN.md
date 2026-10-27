---
title: Uptime status
summary: 一组服务的状态页：90 天的每日柱及可用性数字，每天一张悬停卡片；所选服务相对于目标的响应时间图表，带 p50 和 p95 曲线；以及事故时间线。全部由服务的采样和事故推导而来，并支持用键盘按一周翻页。
source-hash: ff329a7a9498
---

<Demo name="Preview" />

## 何时使用

`UptimeStatus` 是把公开或内部状态页集于一个区块。它按人们提问的顺序回答三个问题：现在一切正常吗（页眉），每个服务有多可靠（90 天里每天一根柱，外加可用性数字），以及发生了什么、在何时发生（事故时间线）。选择一个服务，就会绘制它最近一天的响应时间，这样柱中某个缓慢的周二就可以与延迟曲线和一次事故对应起来。

- **面向客户或团队的状态页。** 事故未解决期间，页眉从对勾变为感叹号并说明出了什么问题；每一行都会说明它是哪个服务。
- **服务级别复盘。** 柱和可用性数字由你给出的事故计算而来，因此页面不会与事故列表不一致。
- **运行手册的配套。** 延迟图表把 p95 与目标放在一起，于是"慢"就是一个数字，旁边还有一条线。

## 何时不用

- **实时监控。** 区块显示每日历史和每半小时一次的采样；它不会轮询任何东西。请从你的监控系统向它提供数据，并让它跟随你更改的数据。
- **随时间变化的单一指标。** 当没有可与之关联的服务、事故或目标时，请使用折线图或 `Metric explorer` 区块。
- **数百个服务。** 页面为大约 3 到 10 行而设计；其余的请先分组再显示。
- **分钟级的细节。** 一根柱就是完整的一个 UTC 日。事故的精确时间请在其时间线中显示。

## 用法

区块派生自 Avalonia 的 `TemplatedControl`，本身不含数据。为它提供 `Services` 和 `Incidents`：

```xml
<arc:UptimeStatus PageName="Larkspur status" UpdatedAt="2027-03-14T14:32:00Z">
    <arc:UptimeService Key="edge" Name="Edge" Description="Routing and TLS" TargetP95="350" />
    <arc:UptimeService Key="console" Name="Console" Description="console.larkspur.test" TargetP95="900" />
</arc:UptimeStatus>
```

```csharp
status.Services.AddRange(services);                        // each with 48 half-hourly LatencySamples
status.Incidents.Add(new UptimeIncident
{
    Title = "Elevated 5xx on Edge",
    Severity = ServiceStatus.Outage,
    Start = DateTimeOffset.Parse("2027-02-20T07:40:00Z"),
    Duration = TimeSpan.FromMinutes(19),
    ServiceKeys = { "edge" },
    Updates = { new IncidentUpdate("Investigating", start, "Some requests fail.", IncidentTone.Warning) }
});
```

`ServicesSource` 和 `IncidentsSource` 接受由你自己持有的集合（会跟随 `ObservableCollection` 的变化）。区块还会跟随服务采样的变化以及事故每个属性的变化，并在 UI 线程的下一轮把一连串的变化一起显示出来。

### 推导的内容

页面上的一切都由数据推导而来，因此没有需要手动保持同步的东西：

- **某一天的可用性**是 1 减去落在该 UTC 日内的事故分钟数除以 1,440；跨越午夜的事故会在两天之间拆分。悬停卡片以两位小数写出（"98.40%"）。
- **某一天的状态**取当天最严重的事故：没有事故为正常，降级为 65% 的柱，中断为 35% 的柱。降级和中断的分钟数都计为停机时间。
- **某个服务的可用性**是 1 减去它的全部分钟数除以 `DayCount` 乘以 1,440（90 天内 69 分钟为 "99.95%"）。
- **某个服务的状态**（其行上的徽章、标题）来自它未解决的事故。已解决的事故会让当天的柱保持降级，因为当天发生过事故，但页面已恢复正常。
- "Incidents" 下方的**计数**是窗口内的事故数，列表按最新在前排列。

`Today` 是窗口的最后一天（默认：最新延迟采样所在的日子），`DayCount` 是柱的数量。

### 柱

每个柱带与行等宽。柱宽为 `round((W - 2 (n - 1)) / n)`，不小于 3，第 *i* 根柱从 `round(i (W - w) / (n - 1))` 开始：1,112px 的带得到 10px 的柱，间隙为 2 或 3px，472px 的带得到 3px 的柱。指向该带会吸附到最近的柱（卡片跟随柱，而不是指针），将该服务其他柱调暗到 38%，并在带的上方打开悬停卡片：服务和日期及当天的可用性、状态，以及当天的每次事故及其分钟数，或 "No incidents recorded"。卡片宽 256，位于带上方 8 处，与分区边缘保持 8 的距离，可能会盖住上面一行。

### 响应时间图表

通过名称、点击其柱，或在其柱上按 Enter 或 Space 来选择服务；`SelectedService` 是双向的。图表显示最近 48 个半小时采样：p95 与 p50 之间的色带，p95 曲线（2px，强调色），p50 曲线（1.5px），以及 `TargetP95` 处的一条虚线。两条曲线都是单调三次曲线，因此永远不会超出数据。y 轴从 0 开始，到 `ChartHeadroom`（1.2）乘以目标与最高 p95 中较大者之上的整数为止：目标 400、最高 p95 为 266 时得到 0 到 500，步长 100；目标 1,000、最高 612 时得到 0 到 1,250，步长 250。图表上方的三个数字（p95、p50、目标）会滚动到新服务的值。

指向图表（或在获得焦点的图表上按 → 或 ←）会把带有每条曲线一个圆点的十字准线吸附到最近的采样，并用该采样的数字和右上角的时间替换原有内容，不带滚动；离开后恢复为最新的采样。

<Demo name="Events" />

### 事故

每个事故是一个按钮，带有严重程度标记、标题、元信息行（"Major outage · Feb 20, 07:40 UTC · 19 min · Edge"）、状态（持续期间为徽章，之后为 "Resolved"）和一个箭头。展开它会显示更新，最新在前，最新的圆点以其 `IncidentUpdate.Tone` 填充，较早的为空心，还有关于负责响应的人员的那一行。`IsExpanded` 是双向的，可以同时展开多个事故。

### 变化的状态

添加一个 `IsResolved` 为 false 的事故，区块就会改变该事故涉及的一切：页眉图标变为感叹号，标题换成 `DegradedTitle`（或 `OutageTitle`），受影响服务的行出现徽章，今天的柱淡化为降级并缩短，它的可用性滚动，计数滚动，新事故在列表顶部生长出来。设置 `IsResolved`、`Duration` 并添加一条 "Resolved" 更新，页面就恢复正常，徽章消失，今天的柱保持降级，时间线中最新的条目生长出来。移除该事故则全部还原。`StatusSummary` 是出问题时页面名称旁的那一行（"Events delayed since 13:58 UTC"）；没有它时，这一行根据 `UpdatedAt` 显示为 "Updated …"。

Preview 演示正是用一个按钮驱动这一切；模拟逻辑位于演示的代码中，而不在区块里。

### 页眉操作

`ActionContent` 是你想放在页眉右侧的任何内容（按钮、链接）。区块宽度小于 560 时，它移到文字下方。

### 其他示例

<Demo name="AllGreen" />

<Demo name="Outage" />

### 窄容器

区块读取自己的宽度，而不是窗口的宽度。小于 860 时下面两个分区堆叠；小于 640 时图例移到提示下方；小于 560 时内边距收紧到 16 和 14，页眉操作移到文字下方，行的描述和 "uptime" 一词被隐藏。柱保持最小 3px，所以在手机上间隙会缩小。

<Demo name="Compact" />

### 文字和格式

每个词都在 `Labels`（一个 `UptimeStatusLabels`）中，三个标题是属性，`LatencyFormatter`、`PercentFormatter` 和 `DateFormatter` 会改变悬停卡片、图表刻度标签和播报中数字与日期的写法。

<Demo name="Formats" />

## API 参考

<!-- api: UptimeStatus -->

<!-- api: UptimeService -->

<!-- api: LatencySample -->

<!-- api: UptimeIncident -->

<!-- api: IncidentUpdate -->

<!-- api: UptimePerson -->

<!-- api: UptimeDay -->

<!-- api: UptimeDayIncident -->

<!-- api: UptimeStatusLabels -->

<!-- api: UptimeServiceEventArgs -->

<!-- api: UptimeDayEventArgs -->

<!-- api: UptimePointEventArgs -->

<!-- api: UptimeIncidentEventArgs -->

<!-- api: ServiceStatus -->

<!-- api: IncidentTone -->

## 无障碍

- 每个服务有一个以其可用性和状态命名的按钮（"Edge, 99.95% uptime, Operational"），在该服务被绘制时处于按下状态；还有一个可获得焦点的柱分组，名称为 "Edge, daily status for the last 90 days"，并以窗口内的可用性和事故作为说明。
- 用键盘查看某一天会以礼貌的方式播报："Sat, Feb 20: Major outage, 98.68% uptime. Elevated 5xx on Edge."。查看某个采样会播报 "Mar 14, 14:00 UTC: p50 80 ms, p95 210 ms."。悬停卡片本身对辅助技术隐藏；播报承载了它的内容。
- 图表是一个可获得焦点的分组，带有文字摘要（最新的 p50 和 p95、最高的 p95 及其时间、目标），以及每半小时一行的隐藏表格。
- 事故按钮会说明是否已展开；每条时间线是一个名为 "Updates, newest first" 的列表，会读出每条更新。
- 一个单独的状态区域会播报出现的事故和已解决的事故，`Announce` 让你说出自己的句子。
- 颜色从不是唯一的信号：柱除了颜色之外高度也不同，徽章和页眉图标带有文字和形状。
- 柱、图表、服务按钮和事故按钮上的焦点环为 2px，位于外侧 2px，仅用于键盘焦点。

### 键盘

- **Tab** 依次经过页眉操作，然后是每个服务的名称按钮及其柱，再到图表，然后是每个事故。
- 在**柱**上，通过键盘获得焦点时会查看最新的一天。**← →** 移动一天，**PageUp / PageDown** 移动七天，**Home / End** 到两端并在那里停止。**Escape** 清除（下一次 **←** 从最新一天的前一天开始），**Enter / Space** 绘制该服务。
- 在**图表**上，获得焦点不会查看。**→** 显示最新的采样，**←** 显示它之前的一个；之后 **← →** 移动半小时，**Home / End** 跳到两端。翻页键没有作用。**Escape** 清除。
- 在**事故**上，**Enter / Space** 展开或收起。

## 动效

- **柱**：指向时其他柱在 120ms（Standard）内调暗到 38%，而被查看的那根的全强度副本保持在最上层。状态变化的一天，颜色在 240ms 内淡入，高度弹动（k 170，c 27）。
- **悬停卡片**：从 97% 缩放在 160ms 内淡入；在柱之间移动时，它以较硬的弹簧（k 925，c 58，约 240ms）跟随，行与行之间也是如此；离开时，它在 120ms 内淡出到 98%。
- **选择服务**：高亮滑向该行（k 150，c 24，约半秒），同时曲线、色带、目标线和 y 域一起弹动（k 171，c 26）；刻度标签交叉淡入淡出，副标题升出再升入，三个数字滚动。保留的刻度随域滑动。
- **页眉**：图标的线端在对勾和感叹号之间弹动（k 160，c 26），同时颜色在 240ms 内淡变；标题和摘要以短暂的上升（约 200ms）互换，徽章的颜色在 240ms 内淡变。
- **事故**：新事故从零高度在弹簧上生长，同时在 200ms 内淡入；被移除的事故在 130ms 内淡出并折叠。时间线条目在弹簧上生长进入（k 180，c 27）。展开事故时高度在弹簧上变化（k 171），内容在 240ms 内淡入；箭头在快速弹簧上转半圈。
- 减弱动效保留短暂的淡入淡出（悬停卡片、调暗、离开的刻度标签），并让弹簧、滚动数字和变形立即就位。关闭则立即应用所有变化。

## 相关

- `Metrics dashboard`、`KPI drilldown` 和 `Metric explorer`，即其他 Analytics 区块。
- `Badge` 和 `Avatar`，状态徽章和响应负责人所借鉴的小部件。
- `Release readiness`，一个页眉状态同样会变化的 Projects 区块。
