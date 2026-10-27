---
title: Choropleth explorer
summary: 按分位数分档为地图着色，并配有联动的排行列表；选择一个地区即可飞入并对其县排名，切换指标会让新的颜色扫过整张地图，也可以更换国家。
source-hash: 1c3add29dcfa
---

<Demo name="Preview" />

## 何时使用

`ChoroplethExplorer` 在一个屏幕上回答两个问题：数值在哪里高、哪里低，以及一个地区为什么是这个值。区域内的每个地区都按所选指标着色，同样的地区在地图旁边排名，选择其中一个会让相机飞入该地区，显示下一级，并重新分档、重新排名。

- **按地理位置查看收入、客户或用量。** 州、省或地区按总量着色，领先者和长尾都列在列表中。
- **某一时期内的变化。** 发散型指标以零为界：下降用一种色相，增长用另一种，因此增长地图一眼就能读懂。
- **市场覆盖情况。** 每个地区的占比（已签约账户占目标账户的比例），放大后没有数据的地区会如实显示为无数据。

## 何时不用

- **精确比较。** 颜色分档会把数值归组；要比较几个地区之间的精确差异，请使用条形图或表格。排行列表有帮助，但不是主要的阅读方式。
- **非地理类别。** 只有当位置本身有意义时，着色才有意义。产品线或团队请使用矩形树图或排行条形列表。
- **点数据。** 门店、办公室或事件应放在带标记的地图上，而不是着色区域上。
- **无法交付的地图。** 此区块绘制的是你提供的轮廓，本身不包含任何地图。

## 用法

此区块派生自 Avalonia 的 `TemplatedControl`。你给它一个或多个 `ChoroplethArea`（国家），每个带有自己的 `ChoroplethRegion`，以及用来着色的 `ChoroplethMetric`。地区以区域视图框（默认 975 × 610）内的 SVG 路径数据提供轮廓，另有边界框（可选：可省去为放置相机而解析路径的开销）、每个指标的值，以及可选的、位于同一视图框内的下级区域。轮廓只在第一次绘制时解析一次。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:ChoroplethExplorer Description="Fiscal year ending March 31, 2027">
        <arc:ChoroplethExplorer.Metrics>
            <arc:ChoroplethMetric Key="revenue" Label="Revenue"
                                  HeadlineLabel="Annual recurring revenue"
                                  LegendCaption="Shaded by annual recurring revenue" />
            <arc:ChoroplethMetric Key="growth" Label="Growth" Format="Change" Scale="Diverging"
                                  Aggregate="WeightedAverage" />
        </arc:ChoroplethExplorer.Metrics>
        <arc:ChoroplethArea Key="us" Name="United States" RegionNoun="states" RegionNounSingular="state"
                            SubregionNoun="counties" SubregionNounSingular="county" />
    </arc:ChoroplethExplorer>
</UserControl>
```

```csharp
var california = new ChoroplethRegion("CA", "California", pathData, new Rect(18.7, 160, 143.9, 247));
california.SetMetricValue("revenue", 9_400_000);
california.SetMetricValue("growth", 4.2);   // percent units: +4.2%
area.Regions.Add(california);
```

本页的演示读取公有领域的轮廓（美国用美国人口普查局的制图边界文件，其他国家用 Natural Earth 的一级行政区），这些轮廓是随图库一起发布的文本文件；所有数值均为虚构。

### 结构

- **标题区**：标题（`Title`、区域的 `Title`，或来自 `ChoroplethLabels.TitleFormat` 的 "Revenue by state"）、`Description`、国家开关（仅在多于一个区域时出现，每个选项都带有地区数量）和指标开关。
- **摘要**：区域或已打开地区的名称、头条数字（总和、各地区的加权平均值，或已打开地区自己的值）以及指标的 `HeadlineLabel`。放大时，有一个按钮可以返回上一级。
- **地图**：每个地区按其所在分档着色；指向某个地区会为它描边，并显示一张卡片，包含它的排名、所有指标（当前指标突出显示）以及选择它会发生什么。
- **图例**：每个分档一个色阶，分档边界位于其间；当下级区域缺少值时会多一个无数据色阶；图例说明会写出指针下的色阶（"10 states $120K to $230K"）。
- **排行列表**：每个地区显示排名、名称、数值和一根条；带有下级区域的地区以箭头标记。指向某一行会为对应地区描边，指向地图则会找到对应的行。

### 分档与颜色

连续型指标会按当前视图中数值的分位数切成 `BinCount`（5）档；发散型指标先在零处分开，两侧各按自己的分位数切分，分档数量按落在各侧的数值多少比例分配。每个边界值取两位有效数字，等于边界的值归入其上一档。每一级都会重新计算分档：放大之后，县有自己的分档。

正向色相是强调色，强调色为灰色时则用蓝色；负向色相是与之相对的琥珀色；没有值的地区使用边框颜色。设置 `PositiveColor`、`NegativeColor` 和 `NoDataColor` 即可使用你自己的颜色。

<Demo name="CustomLook" />

### 放大

选择一个地区（在地图上或列表中）会设置 `OpenRegionKey`、触发 `RegionOpened`，并让相机飞行，使该地区占满地图的 80%（缩放不超过 12 倍）。其他地区保留为淡化的背景，可以选择它们直接飞过去。有下级区域时，该地区让位于它们，图例和列表重新分档、重新排名，选择某个下级区域会将其固定（`PinnedSubregionKey`、`SubregionPinned`）。没有下级区域时，列表会说明没有更细的明细，并提供返回的途径。`ZoomTo(key)` 和 `ZoomOut()` 可在代码中完成同样的操作。

<Demo name="Events" />

### 单个区域与发散型指标

只有一个区域时，国家开关会消失。`SelectedMetricKey` 选择起始指标；这里是一个以零为界的增长指标。

<Demo name="SingleArea" />

### 卡片外观与窄容器

`IsCardChrome` 把区块绘制成带边框的卡片。区块会读取自己的宽度：从 820 起，列表位于地图旁边，高度与地图列完全一致；低于这个宽度时，列表移到地图下方（高 400，宽度低于 560 时为 360），控件移到标题下方；低于 560 时，内边距收紧、头条数字缩小，图例铺满整个宽度。

<Demo name="Card" />

### 文字

除数据之外的每一个词都来自 `Labels`，即带有英文默认值的 `ChoroplethLabels`：计数（"51 states"，使用区域的单数和复数名词）、"Ranked by revenue"、图例的色阶名称、悬停卡片的提示、返回按钮、空面板以及每一条播报。指标自带 `Label`、`Noun`、`HeadlineLabel`、`LegendCaption`、`CurrencySymbol`，以及可选的 `Formatter` 和 `TickFormatter`。

## API 参考

<!-- api: ChoroplethExplorer -->

<!-- api: ChoroplethArea -->

<!-- api: ChoroplethRegion -->

<!-- api: ChoroplethMetric -->

<!-- api: ChoroplethLabels -->

<!-- api: ChoroplethValueFormat -->

<!-- api: ChoroplethScale -->

<!-- api: ChoroplethAggregate -->

<!-- api: ChoroplethRegionOpenedEventArgs -->

<!-- api: ChoroplethSubregionPinnedEventArgs -->

<!-- api: ChoroplethSelectionChangedEventArgs -->

## 无障碍

- **Tab** 依次到达国家开关、指标开关（一个停靠点；方向键移动选择）、放大时的返回按钮、每个图例色阶，以及列表（一个停靠点，停在你上次所在的行）。
- 在列表中，**↑** 和 **↓** 每次移动一行并在两端停住，**Home** 和 **End** 跳到第一行和最后一行；获得焦点的行会在地图上描边并滚动到可见位置。
- 在地区所在行上按 **Enter** 或 **Space** 会放大，并把焦点移到其下级区域的第一行（没有下级区域时移到返回按钮）；在下级区域所在行上则是固定或释放它。
- 在图例色阶上按 **Enter** 或 **Space** 会固定它；再按一次则释放。
- **Escape** 先释放已固定的图例色阶，然后缩小回上一级，并把焦点还给该地区所在行。
- 地图是一张名为 "Map of United States shaded by revenue." 的图像，带有指向列表的帮助文字；列表承载同样的数据，是用键盘或屏幕阅读器探索的途径。
- 每一行以排名、名称和数值命名（"1st, California, $9.8M. Open 58 counties"）；负值读作 "minus 4.8%"。下级区域所在行是切换按钮，会报告自己是否已固定。
- 每个图例色阶是一个切换按钮，以数量和范围命名（"10 states $120K to $230K"）。
- 一个礼貌型实时区域会播报放大（"California, 58 counties ranked by revenue. Press Escape to zoom out."）、缩小、新的指标及其领先者、新的国家，以及已固定的下级区域及其数值和排名。
- 数值带有正负号和真正的减号；方向从不只依赖颜色。
- 焦点环为 2px，位于元素外侧，并且只在键盘操作时显示。

## 动效

- **切换指标**：每个地区的颜色用 480 ms 过渡，延迟从西向东递增（其中心的 x 除以地图宽度再乘以 260 ms），因此新的着色在不到 750 ms 内扫过整张地图。各行以临界阻尼弹簧滑到新的排名，每根条用 580 ms 增长或缩短；文字用 240 ms 交叉淡入淡出；头条数字直接跳变。
- **放大或缩小**：相机的缩放和位置共用一根临界阻尼弹簧（约 800 ms，可在飞行途中掉头）。其他地区用 240 ms 淡化到 26%；下级区域从 140 ms 起用 480 ms 淡入，因此在相机仍在飞行时就已出现；离开时用 160 ms 淡出。头条数字用 480 ms 计数到新的值。
- **指向**：悬停卡片用 160 ms 淡入并跟随指针；列表滑动，使该地区所在行居中。在图例上，被点亮的色阶高度增长到 1.6 倍，其他地区淡化到 14%，其他行淡化到 32%，均用 240 ms。
- **更换国家**：旧地图用 180 ms 淡出，新地图从西向东淡入；列表淡出后再淡入。
- 减弱动效只保留文字的交叉淡入淡出：颜色、相机、行、条和悬停卡片立即变化。关闭动效则让所有变化立即生效。

## 相关

- `SegmentedControl` 和 `MorphSelect`，标题区中的开关。
- `Metric explorer` 和 `Revenue explorer`，其他拆解数字的 Analytics 区块。
- `Revenue globe`，以 3D 视图呈现按位置划分的收入。
