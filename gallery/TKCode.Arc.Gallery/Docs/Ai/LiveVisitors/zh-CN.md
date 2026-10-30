---
title: Live visitors
summary: 实时访客卡片：在线人数逐位滚动，新访客在点阵世界地图上闪现，热门页面带着条形重新排序，注册与升级依次出现。
source-hash: 57663a6874b8
---

<Demo name="Preview" />

## 何时使用

`LiveVisitors` 用一张卡片回答"此刻谁在这里"：一个脉动的实时状态、逐位滚动的站内人数、一张点阵世界地图（每位新访客在上面闪现后淡去）、
带条形的最热门页面，以及一条注册与升级动态，显示城市、国家和多久之前。

- **网站或应用的实时访客**，与刚刚发生的注册和升级放在一起。
- **发布日和推广活动**，团队实时关注流量与转化。
- **管理与增长仪表板**，需要一块一眼看清"此刻"的卡片。

## 何时不用

- **几天或几周的趋势**：使用带坐标轴的图表。
- **精确的地理位置或按国家细分**：使用表格或真正的地图；点阵地图只是装饰。
- **没有上下文的单个数字**：使用统计卡片。

## 用法

没有数据也没有模拟（见下文）时，卡片保持占位状态，状态显示 No data。一旦设置了 `Count`，它就只显示你传入的数据。从你的实时数据源（websocket、服务器推送事件，或每两秒轮询一次分析接口）
设置新的快照即可，卡片会根据 id 判断哪些发生了变化：

```csharp
card.Count = snapshot.Online;
card.Pages = snapshot.Pages.Select(p => new LiveVisitorsPage(p.Path, p.Visitors)).ToArray();
card.Visitors = snapshot.Arrivals          // the last 5 to 8 seconds of arrivals
    .Select(a => new LiveVisitorsPing(a.Id, a.Latitude, a.Longitude)).ToArray();
card.Events = snapshot.Conversions         // newest first
    .Select(c => new LiveVisitorsEvent(c.Id, c.Label, c.City, c.CountryCode, c.At)).ToArray();
card.Connection = socket.IsOpen ? LiveVisitorsConnection.Live : LiveVisitorsConnection.Reconnecting;
```

### 事件到达时推送

<Demo name="YourData" />

数据流逐条发送事件而不是快照时，直接推送。`PushVisitor` 添加一位访客，它立即闪现并在六秒后淡出；`PushEvent` 在动态顶部添加一次转化，
带有经纬度时还会在地图上以更明显的光环闪现，并标出城市两秒。推送的内容与 `Visitors`、`Events` 中的数据合并显示。

```csharp
socket.OnVisit += v => card.PushVisitor(v.Latitude, v.Longitude);
socket.OnConversion += c => card.PushEvent(new LiveVisitorsEvent(c.Id, "Signed up", c.City, c.Country, c.At)
{
    Latitude = c.Latitude,
    Longitude = c.Longitude,
});
```

### 模拟数据流

用于原型或演示时，不要设置 `Count`，而是给卡片一个 `LiveVisitorsSimulation`，描述一个网站：约 `TypicalCount` 人分布在 `SitePages` 上，每 600ms 有一位来自 `Cities` 之一的新访客，页面人数会漂移，每 2.4s 发生一次 `Conversions` 之一，每项都按权重选取。卡片在屏幕上、处于 Live 且未暂停时由自己的时钟推进它，并会说明一次数据流是模拟的。你自己的 `Count` 会让它让位，把 `Simulation` 设为 null 会清空卡片。预览中的网站、城市、页面和转化都是虚构的演示数据。

```csharp
card.Simulation = new LiveVisitorsSimulation
{
    TypicalCount = 128,
    Cities = [new LiveVisitorsSimulationCity("Berlin", "DE", 52.52, 13.40, 4), new LiveVisitorsSimulationCity("Tokyo", "JP", 35.68, 139.69, 3)],
    SitePages = [new LiveVisitorsSimulationPage("/", 34), new LiveVisitorsSimulationPage("/pricing", 22)],
    Conversions = [new LiveVisitorsSimulationConversion("Signed up", 62), new LiveVisitorsSimulationConversion("Upgraded to Pro", 28)],
};
```

### 结构

1. **状态**：一个圆点和一个词：Live（圆点脉动）、Paused、Connecting 或 Reconnecting（警告色圆点）。
2. **暂停**：保留最后一份快照的切换按钮。`IsPauseVisible="False"` 可隐藏它。
3. **人数**：等宽数字，每一位在自己的滚轮上滚动，后面是 `Labels.People` 给出的文字。
4. **地图**：北纬 80° 到南纬 56° 的点阵世界地图。访客是强调色圆点加一圈光环；转化的圆点更大，光环更明显。
5. **热门页面**：最热门的 `MaxPages` 个页面（默认 4 个），条形按最热门页面缩放。
6. **注册与升级**：最新的 `MaxEvents` 次转化（默认 4 次），带国家代码标签、发生了什么、城市和时间。

### 连接状态

`Connection` 反映你的连接状态，而不是把卡片藏起来。`Connecting` 在人数和列表的位置显示占位条。`Reconnecting` 保留最后的数据，
状态切换为警告色圆点和 Reconnecting 字样。

### 暂停

暂停按钮设置 `IsPaused`（双向）并引发 `PausedChanged`；在其中暂停或关闭你自己的订阅。暂停期间卡片保留最后一份快照，
不管你继续传入什么，它的时钟也会停止。恢复后显示最新的数据。从代码设置 `IsPaused` 效果相同，但不会引发该事件。

### 窄容器

<Demo name="Narrow" />

卡片最宽 640px，在更窄的容器中填满宽度。卡片宽度小于 520px 时两个列表上下堆叠，内边距降为 16px；地图随卡片缩放。
路径和城市过长时以省略号结尾，人数和时间保持固定的右对齐列。

### 自定义文案

`Labels` 包含卡片显示或朗读的每个词，包括人数的单复数、简短与朗读用的时间，以及国家名称。替换整个对象即可本地化或改写：

```csharp
card.Labels = new LiveVisitorsLabels
{
    People = n => n == 1 ? "person watching the stream" : "people watching the stream",
    EventsHeading = "New subscribers",
};
```

### 国旗

卡片以小标签显示国家的 ISO 两位字母代码，并为屏幕阅读器读出完整国名。可在 `LiveVisitorsEvent.Flag` 中传入你自己的图片来代替；它仍然只是装饰。

### 内容准则

- 事件标签简短，使用过去时："Signed up"、"Upgraded to Pro"。
- 绝不在动态里放入个人数据，例如电子邮件地址；城市和国家就足够了。
- 以五到八秒的滚动窗口发送访客；卡片根据 id 为到达和离开播放动画。

### 最佳实践

- 在快照之间保持 id 稳定，这样行会就地更新，只有真正的变化才会移动。
- 用 `Connection` 表示连接状态，而不是隐藏或清空卡片。
- 在 `PausedChanged` 中暂停你自己的订阅，让暂停的卡片不产生开销。

### 常见问题

**模拟数据流能在别处使用吗？** 可以。给 `LiveVisitorsSimulation` 提供数据，用经过的时间调用 `Advance`，然后读取 `Count`、`Pages`、
`Visitors` 和 `Events`；`Restart` 会让它从头开始。不会获取或跟踪任何内容。

**为什么数字在计数时宽度不变？** 数字是等宽的，每一位在自己的滚轮上滚动；只有新增的最高位（99 到 100）会展开出现。

## API 参考

<!-- api: LiveVisitors -->

<!-- api: LiveVisitorsPage -->

<!-- api: LiveVisitorsPing -->

<!-- api: LiveVisitorsEvent -->

<!-- api: LiveVisitorsLabels -->

<!-- api: LiveVisitorsSimulation -->

<!-- api: LiveVisitorsSimulationCity -->

<!-- api: LiveVisitorsSimulationPage -->

<!-- api: LiveVisitorsSimulationConversion -->

## 无障碍

- 卡片是一个带名称的分组，名称来自 `Labels.Title`（"Live visitors"）。
- 人数以纯文本读出一次（"128 people on the site now"）；滚动的数字对屏幕阅读器隐藏，人数变化也不会播报，以免信息泛滥。
- 暂停按钮是一个会报告是否按下的切换按钮；它的名称和提示说明操作（"Pause live updates"、"Resume live updates"）。
- 状态从不只靠颜色表达：圆点旁边总有 Live、Paused、Connecting 或 Reconnecting。
- 每个事件行读作一句话，国家完整读出，时间用文字表达（"Signed up, Berlin, Germany, 12 seconds ago"）；代码标签只是装饰。
  每个页面行读作"/pricing, 24 people"。
- 地图是装饰且被隐藏；同样的信息在列表中。
- 一个礼貌的实时区域会报告暂停与恢复，并说明数据流是模拟的。

### 键盘

| 按键 | 操作 |
|---|---|
| Tab | 移到暂停按钮。地图和列表只读。 |
| Enter、Space | 暂停或恢复实时更新。 |

## 动效

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 人数变化 | 每一位在自己的滚轮上滚动（280ms，ease-out）；新增的最高位展开出现 | 立即变化 | 立即变化 |
| 一位访客 | 圆点淡入，一圈光环在 900ms 内扩散并淡去 | 圆点淡入 | 立即出现 |
| 带位置的一次转化 | 更大的圆点和更明显的光环，城市标注 2s | 圆点和标注淡入 | 立即出现；标注仍在 2s 后离开 |
| 访客过期 | 在 400ms 内淡出 | 在 400ms 内淡出 | 立即离开 |
| 行重新排序或新行到来 | 各行滑动一行高度（0.28s 无回弹弹簧），新行在 160ms 内淡入，最后一行滑出裁剪区并淡去 | 各行淡入淡出，不滑动 | 立即 |
| 页面条形 | 在 280ms 内缓动到新宽度 | 在 280ms 内缓动 | 立即 |
| 实时圆点 | 实时且在屏幕上时每 1.6s 发出一圈光环 | 静止 | 静止 |
| 状态文字 | 在预留宽度内交叉淡化（淡出 100ms，淡入 160ms） | 短暂交叉淡化 | 立即 |

卡片的高度从不改变：列表保留各行的空间，地图保持宽高比。卡片离开屏幕或暂停时，脉动、时钟和模拟都会停止。

## 相关组件

- `AgentStatus`：运行中智能体的实时状态。
- `ContextWindow`：另一张实时、可自动播放的数据卡片。
- `AgentTimeline`：一次运行的回放，计数随播放滚动。
