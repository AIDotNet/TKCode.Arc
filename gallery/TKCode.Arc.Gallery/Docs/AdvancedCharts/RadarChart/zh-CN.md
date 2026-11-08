---
title: 雷达图
summary: 在同一张度量网上比较多个对象：替换对象时形状随之变形，扫过各轴即可读出每个数值。
source-hash: 4b8768c46854
---

<Demo name="TeamSkills" />

## 何时使用

`RadarChart` 展示几个对象在同一组度量上的均衡情况。每个对象是一个闭合形状，读者一眼就能看出谁强、谁弱，再扫过各轴读取准确数值。

- **技能评估**，例如几位候选人的面试评分或团队能力盘点。
- **产品比较**，三款产品按续航、存储、价格等各有刻度的度量打分。
- **前后对比**，例如服务在迁移前后的可靠性、成本与速度。
- **均衡检查**，形状本身就是结论：圆润表示均衡，尖锐表示偏科。

图表最适合不超过三个对象、五到八条轴。

## 何时不用

- **超过约四个对象**：形状会相互遮挡。请使用分组的 `BarChart`，或为每个对象放一张小图。
- **在单一度量上精确排名**：不同角度上的长度难以比较。请使用 `BarChart`。
- **随时间的变化**：请使用横轴为时间的 `LineChart`。
- **整体的组成部分**：请使用 `DonutChart`。雷达图的各轴是相互独立的度量，不能相加。
- **有自然顺序或可能为负的度量**：各轴围成一圈，并从中心的零开始，序列或带符号的值会被误读。请使用 `SlopeChart` 或 `BarChart`。

## 用法

为图表提供轴和系列，可以内联，也可以绑定。`Axes` 和 `Series` 初始是空列表，可在 XAML 中直接添加，也接受视图模型提供的任意 `IList`。可观察的列表会被跟踪。

<Demo name="PolygonGrid" />

### 结构

1. **网**：`Rings` 个环（圆形或多边形，见 `GridShape`），每条轴一根辐条。
2. **轴标签**：位于每根辐条的末端，按 `Axes` 的顺序从顶部开始顺时针排列。
3. **形状**：每个系列一个带填充的闭合轮廓。颜色按系列顺序取用，第一个来自强调色，每个系列还有自己的线型。
4. **标记**：在当前轴上，每个对象一个标记。
5. **提示**：位于轴标签之外，并且始终在图表之内，列出每个对象的线型、名称与数值。
6. **图例**：在网的下方，显示每个对象的线型与名称。

### 轴与系列

`RadarAxis` 有 `Key`、`Label` 和可选的 `Maximum`。`RadarSeries` 有 `Key`、`Label` 和 `Values`，每条轴一个 `RadarValue`，其 `Axis` 是轴的键。

| 类型 | 成员 | 说明 |
|---|---|---|
| `RadarAxis` | `Key` | 稳定的标识，供数值和 `ActiveAxis` 引用 |
| `RadarAxis` | `Label` | 轴末端的文字 |
| `RadarAxis` | `Maximum` | 这条轴外环处的值；为 null 时使用图表的 `Maximum` |
| `RadarSeries` | `Key` | 稳定的标识，用于在更新之间匹配形状 |
| `RadarSeries` | `Label` | 图例和提示中的名称 |
| `RadarSeries` | `Values` | 各个 `RadarValue` |
| `RadarValue` | `Axis` | 轴的键 |
| `RadarValue` | `Value` | 数值；NaN 视为缺失 |

在代码中，系列可接受以轴为键的字典：

```csharp
chart.Axes = [new RadarAxis("speed", "Speed"), new RadarAxis("cost", "Cost", 500), new RadarAxis("reach", "Reach")];
chart.Series = [new RadarSeries("north", "North", new Dictionary<string, double> { ["speed"] = 70, ["cost"] = 320, ["reach"] = 55 })];
```

### 刻度、缺失值与截断

每条轴从中心的 0 延伸到外环的最大值：设置了 `RadarAxis.Maximum` 时用它，否则用图表的 `Maximum`（默认 100）。超过最大值的数值画在外环上，小于零的数值画在中心；提示仍读出原始数值。缺失值或 NaN 画在中心，在提示中显示为破折号。

### 刻度各异的多个产品

为每条轴设置合适的 `Maximum`，再用 `ValueFormatter` 为每条轴加上对应的单位，它会收到数值及其轴。

<Demo name="ProductCompare" />

### 替换对象

替换 `Series` 即可显示另一个对象。保留 `Key` 的系列会变形到新数值。使用新键的系列会接管它在该位置替换掉的系列的形状，因此替换比较中的第二个对象时它会变形。没有来源的系列从中心长出。

```csharp
chart.Series = [maya, selectedCandidate];
```

### 网格形状、环数与填充

`GridShape` 绘制圆形（默认）或每条轴上有一个角的多边形，后者适合轴较少的图表。`Rings` 设置网的环数，`FillOpacity` 设置每个形状的填充强度。

```xml
<arc:RadarChart Label="Coverage" GridShape="Polygon" Rings="5" FillOpacity="0.3" Series="{Binding Regions}" Axes="{Binding Measures}" />
```

### 读取一条轴

`ActiveAxis` 是正在读取的轴的键，支持双向绑定。`ActiveAxisChanged` 报告每次变化，带有 `Key` 和 `Axis`，清除读数时两者都为 null。与任何轴都不匹配的键不显示读数。

```csharp
chart.ActiveAxisChanged += (_, e) => readout.Text = e.Axis?.Label ?? "Sweep the axes to compare";
```

### 尺寸与图例

`Size` 是网的最大直径，默认 320。网会缩小以适应其盒子，图表占据获得的宽度并让网居中。如果页面以其他方式标明了对象，可把 `IsLegendVisible` 设为 false。少于三条轴或没有系列时，图表显示 `EmptyLabel`。

### 绑定到视图模型

```xml
<arc:RadarChart Label="Team skills"
                Axes="{Binding Skills}"
                Series="{Binding Members}"
                ActiveAxis="{Binding SelectedSkill, Mode=TwoWay}" />
```

- `Skills` 和 `Members` 可以是 `RadarAxis`、`RadarSeries` 的数组、列表或可观察集合。
- 就地修改 `RadarValue` 不会被察觉；请替换系列或列表来更新图表。

### 数据形状与性能

轴和系列的键必须唯一，后出现的重复项会被忽略。图表在一个控件内自绘：变形期间几何就地移动，不重新布局，因此在数据变化时让图表留在屏幕上开销很小。

### 内容指南

- **Label**：说明比较的内容，例如"Staff engineer panel scores"。
- **轴标签**：一两个短词。较长的标签会被截断，宽度低于 360 时换成两行。
- **系列标签**：使用短名称，它们与数值共用提示中的一行。

### 最佳实践

**推荐**

- 控制在三个对象、五到八条轴以内。
- 把相关的度量放在相邻位置，因为相邻的轴决定轮廓。
- 刻度不明显时，在标签或格式化函数中写明。

**避免**

- 比较面积：形状的面积取决于轴的顺序，请读数值。
- 把越高越好的度量与越低越好的度量混在一起。

### 常见问题

**为什么新对象没有变形？** 只有当它替换的系列已不存在，且新系列占据其在 `Series` 中的位置时才会变形。如果插入到其他位置，它会从中心长出。

**数值可以为负吗？** 读数正确，但形状停在中心，因为每条轴都从零开始。

## API 参考

<!-- api: RadarChart -->

<!-- api: RadarAxis -->

<!-- api: RadarSeries -->

<!-- api: RadarValue -->

## 无障碍

- 网是可聚焦的分组。它的自动化名称是图表的 `Label` 加按键说明，读取某条轴时会播报该轴及每个对象的数值，例如"Mentoring: Maya 64, Theo 83"。
- 没有读数时，帮助文本概括每个对象在每条轴上的数值。
- 除颜色外，还能通过名称和线型区分对象。每个图例项都是一个 Tab 停靠点，以对象及其线型命名。

### 键盘

| 按键 | 操作 |
|---|---|
| `Tab` | 聚焦到网并读取第一条轴，然后依次经过各图例项。获得焦点的图例项会把其形状提到最前 |
| `Right` / `Down` | 读取顺时针方向的下一条轴 |
| `Left` / `Up` | 读取上一条轴 |
| `Home` / `End` | 读取第一条或最后一条轴 |
| `Escape` | 清除读数 |

## 动效

- 替换对象时，其形状在轻微回弹的弹簧上变形。
- 首次显示时，网从中心展开。
- 标记和提示在各轴之间滑动，标记沿每个轮廓移动。
- 悬停或聚焦图例项时，其形状提到最前，其他形状变淡。
- 减弱动效时，形状立即切换，标记和提示立即移动，首次显示为短暂淡入。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 替换对象 | 在回弹弹簧上变形 | 立即 | 立即 |
| 首次显示 | 从中心展开 | 短暂淡入 | 立即 |
| 当前轴变化 | 标记和提示滑动 | 立即 | 立即 |

## 响应式

- 网会缩小以适应获得的宽度和高度，且不会超过 `Size`。
- 宽度低于 360 时，标签环收紧，轴标签换成两行。
- 提示在任何宽度下都留在图表之内；标签之外放不下时，移到标签旁边。
- 触屏按下时读取手指所在的轴，拖动时扫过其他轴。图例会换到更多行。

## 相关组件

| 组件 | 适用情况 |
|---|---|
| `BarChart` | 需要在一个度量上精确排名 |
| `LineChart` | 横轴是时间 |
| `SlopeChart` | 在多个项目间比较两个时刻 |
| `DonutChart` | 数值是一个整体的组成部分 |
| `Gauge` | 只需展示一个数值相对其范围的位置 |
