---
title: 滑块
summary: 在轨道上选取一个值或一个范围，跟随指针移动。
source-hash: 8edf5e790ada
---

<Demo name="SingleValue" />

## 何时使用

`Slider` 让用户通过沿轨道移动滑块手柄来选择一个值或一个范围。它适用于设置和筛选中，轨道位置本身承载含义、精确数字不那么重要的场景。它位于表单、筛选面板或控制栏中，用户移动滑块时，会在标签旁的读数以及滑块手柄上方的气泡中显示所选的值。

- **商品列表中的价格与金额筛选**：带两个滑块手柄的范围，`IsRange="True"`，下限与上限值成为查询条件。轨道一眼就能看出商品目录的分布，而读数会在滑块手柄移动时显示“$200 至 $800”。
- **计费或采购控制台中的支出上限与预算**：为团队设置的每月上限，范围在 $0 到 $5,000 之间。用户大致能看出上限的位置，而格式化的读数给出精确数字。
- **媒体或培训应用中的播放与显示设置**：带有 0.5x 和 1x 标注刻度的播放速度，或通话音量。带标签的刻度为预设命名，点击标签即可跳到该处。
- **运维仪表板中的告警阈值**：CPU 或错误率阈值，范围在 0 到 100% 之间，并在团队运行手册使用的级别上设置刻度。只有数字时不够直观，而轨道能让刻度一目了然。
- **数据工具中的不透明度、密度或缩放控件**：此时数值是通过滑块手柄移动时屏幕上发生的变化来判断的。

## 何时不用

- **用户需要键入的精确数字**，例如退款金额或数量：用 `NumberField`；带币种的金额用 `MoneyInput`。轨道只能近似一个值。
- **用户无法更改的值**，例如已用存储空间或上传进度：用 `Progress`。
- **应有物理感的单一设置**，轨道会越过两端拉伸，滑块手柄会卡入档位：用 `ElasticSlider`。它只有一个值，没有范围。
- **从短固定列表中选择**，例如套餐等级或配送速度：下拉选择用 `Select`；选项能放进一行时用 `SegmentedControl`。
- **日期或时间跨度**：用 `DatePicker`、`DateRangePicker` 或 `TimePicker`。滑块无法显示日历单位。
- **开或关的设置**：用 `Switch`。滑块意味着中间值是有意义的。

## 用法

基本用法是单个值。`Value` 是滑块在轨道上的位置，`Minimum` 和 `Maximum` 设置其范围。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:Slider Label="音量"
                Value="64"
                Width="360" />
</UserControl>
```

`Slider` 派生自 Avalonia 的 `RangeBase`，因此 `Value`、`Minimum`、`Maximum`、`LargeChange` 和 `ValueChanged` 的行为与任何范围控件一致。与原生滑块有两点不同。`SmallChange` 是数值吸附的步长，在此控件上默认为 1。设置 `IsRange` 会将控件切换为两个滑块手柄，并以 `LowerValue` 和 `UpperValue` 代替 `Value`。该控件还加入了标签、读数、数值气泡、刻度以及内容插槽。

### 结构

1. **标签**（`Label`）：中等字重，位于轨道上方一行，行高 19.6 px。宽度不足时以省略号截断。它为辅助技术提供滑块手柄的名称。
2. **读数**：格式化后的数值，右对齐显示在标签行上，使用次要文字颜色，与前面保持 16 px 的间距。在范围模式下，它以弱化颜色显示低值与高值，中间以短破折号分隔，两个数字相距 4 px。`IsValueVisible` 可将其隐藏。
3. **起始内容**（`StartContent`）：位于轨道之前，间距 12 px。属性为 null 时隐藏。
4. **轨道与填充**：6 px 的轨道，使用控件轨道颜色。填充从起点延伸到滑块手柄；范围模式下则位于两个滑块手柄之间，使用控件填充颜色。
5. **滑块手柄**：22 px 的圆盘，使用控件滑块手柄颜色与上浮阴影。每个滑块手柄的命中区域向圆盘两侧各延伸 11 px。
6. **刻度点**：4 px 的圆点，每个刻度一个。填充之外的刻度点使用前景色、透明度 30%；填充之上的刻度点使用控件字形颜色、透明度 60%。
7. **数值气泡**：位于活动滑块手柄上方 8 px 的上浮气泡（触摸或触控笔按下时为 18 px），高度至少 28 px，水平内边距 12 px，带 1 px 边框。其颜色是反转的：前景色作为背景，背景色作为文字。它在滑块手柄被拖动或获得键盘焦点时出现。
8. **结束内容**（`EndContent`）：位于轨道之后，间距 12 px，与起始内容的处理方式相同。
9. **刻度标签行**：高 20 px，位于轨道下方，使用超小字号。标签为弱化颜色；当数值已到达某刻度时，该刻度的标签使用次要颜色。只有当至少一个刻度设置了 `Label` 时，该行才会出现。

### 起始与结束内容

`StartContent` 和 `EndContent` 分别位于轨道之前和之后，例如图标或静音按钮。静音按钮适合用于音量控件：用户无需移动滑块手柄即可让输出静音。内容应保持为单个字形或小图标按钮，因为轨道会占用其余的宽度。

```xml
<arc:Slider Label="音量"
            Value="64"
            Width="360">
    <arc:Slider.StartContent>
        <TextBlock Text="-" />
    </arc:Slider.StartContent>
    <arc:Slider.EndContent>
        <TextBlock Text="+" />
    </arc:Slider.EndContent>
</arc:Slider>
```

### 范围

设置 `IsRange` 即可显示两个滑块手柄。通过 `LowerValue` 和 `UpperValue` 读写两端的值，并用 `MinStepsBetweenThumbs` 让两个滑块手柄保持间距，该值以步数计。在范围模式下不使用 `Value`。`ValueFormatter` 用于格式化每个滑块手柄的读数、气泡和朗读的数值。

当用户在缩小一个集合时选择范围，例如价格、以天为单位的日期跨度或分数段。阈值使用单个滑块手柄，因为第二个滑块手柄会要求用户同时选择两个相关的数字。

<Demo name="Range" />

```xml
<arc:Slider Label="价格"
            IsRange="True"
            Minimum="0"
            Maximum="1000"
            SmallChange="10"
            LowerValue="{Binding MinPrice, Mode=TwoWay}"
            UpperValue="{Binding MaxPrice, Mode=TwoWay}"
            MinStepsBetweenThumbs="5"
            LowerThumbName="最低价格"
            UpperThumbName="最高价格"
            ValueFormatter="{Binding PriceFormatter}" />
```

`MinStepsBetweenThumbs` 让两个滑块手柄保持一定的步数间距，因此在 `SmallChange="10"` 且值为 `5` 时，两个滑块手柄永远不会靠得比 50 个单位更近。被推向另一个滑块手柄的那个会停在间距处。保持默认值 `0` 即表示两端可以相遇。

### 步长与刻度

`SmallChange` 是数值吸附的步长。`Marks` 会在轨道上添加刻度。带标签的刻度会在轨道下方显示标签，点击标签会将最近的滑块手柄移动到该位置。不带标签的裸 `SliderMark`（或可隐式转换的数字）就是没有标签的刻度。

刻度适用于用户会反复选择的预设，例如播放的 0.5x、1x 和 2x，或运行手册为告警指定的数值。只为需要名称的预设加标签，其余保留为刻度；在狭窄的轨道下，一排标签很快就会互相重叠。

<Demo name="StepsAndMarks" />

刻度本身不会改变数值。拖动、按键或点击标签才会设置数值，刻度只说明预设位于何处。

### 范围与极限值

数值始终保持在 `Minimum` 和 `Maximum` 之内，范围的两端也保持在其中。控件在接收数值时会将其钳制到范围内，因此 XAML 页面中先设置 `Minimum="360"` 再设置 `Maximum="900"` 不会报错；最终的值才是应依赖的值。

`LargeChange` 用于 `PageUp`、`PageDown` 以及方向键配合 `Shift` 产生的跳跃。未设置时，跳跃为范围的十分之一，这适用于大多数刻度。若十分之一对某个刻度来说太粗，应显式设置，例如 0 到 5 的评分，其跳跃应为 1。

### 事件

`ValueCommitted` 在每次松开时、每次按键使数值改变时，以及每次点击刻度标签时各触发一次。应将耗时操作（例如网络请求）放在此事件上。其 `SliderValueCommittedEventArgs` 包含 `Value`、`LowerValue`、`UpperValue` 和 `IsRange`。对于单一值，下限与上限值重复 `Value`；在范围模式下，`Value` 为下端。`RangeChanged` 在范围被拖动或通过按键修改时，每一步都会触发，其 `SliderRangeChangedEventArgs` 包含 `LowerValue` 和 `UpperValue`。

```csharp
price.ValueCommitted += (_, e) => SavePriceFilter(e.LowerValue, e.UpperValue);
price.RangeChanged += (_, e) => committed.Text = $"${e.LowerValue:N0} – ${e.UpperValue:N0}";
```

### 绑定到视图模型

典型场景是筛选面板：视图模型持有两个价格，滑块手柄镜像它们，用户操作完成后由命令执行查询。视图模型使用 CommunityToolkit.Mvvm。

```csharp
public interface ICatalogQuery
{
    Task FilterByPriceAsync(double minPrice, double maxPrice);
}

public partial class PriceFilterViewModel : ObservableObject
{
    private readonly ICatalogQuery _catalog;

    [ObservableProperty]
    private double _minPrice = 200;

    [ObservableProperty]
    private double _maxPrice = 800;

    [ObservableProperty]
    private string _summary = "正在显示 $200 至 $800 之间的商品。";

    public PriceFilterViewModel(ICatalogQuery catalog) => _catalog = catalog;

    [RelayCommand]
    private async Task ApplyAsync()
    {
        Summary = $"正在显示 ${MinPrice:N0} 至 ${MaxPrice:N0} 之间的商品。";
        await _catalog.FilterByPriceAsync(MinPrice, MaxPrice);
    }
}
```

```xml
<StackPanel Spacing="12">
    <arc:Slider Label="价格"
                IsRange="True"
                Minimum="0"
                Maximum="1000"
                SmallChange="10"
                LowerValue="{Binding MinPrice, Mode=TwoWay}"
                UpperValue="{Binding MaxPrice, Mode=TwoWay}"
                MinStepsBetweenThumbs="5"
                Width="360" />
    <TextBlock Text="{Binding Summary}" />
</StackPanel>
```

- `LowerValue` 与 `UpperValue` 是范围模式下要绑定的双向属性；`Value` 是单值模式下的双向属性。绑定与 `IsRange` 相匹配的那一组。
- 显式绑定 `Mode=TwoWay`。视图模型的 `MinPrice` 会在拖动的每一步更新，这对读数或摘要行是正确的，但对查询则不对。应从 `ApplyAsync` 执行查询，或从视图代码后置文件中的 `ValueCommitted` 执行，这样一次拖动只发送一个请求。
- 视图中的命令是绑定到 `ApplyCommand` 的 `Button`；在滑块手柄移动时它保持可用，因此用户可以在任何时候应用新的范围。

### 克制地使用动效

滑块上的动效会告诉用户滑块手柄做了什么：气泡确认数值，数字滚动显示变化方向，轻甩后的惯性显示滑块手柄能被带多远。这些提示有助于用户凭手感搜索范围。但在只需设置一次数值的页面上，它们会造成干扰，此处应使用普通字段。

减弱动效会移除惯性、拉伸和数字滚动。滑块手柄直接到达目标值，气泡只淡入淡出，这样即使没有显示变化方向的动效，控件仍然易于阅读。

### 禁用状态

禁用的滑块会移出 Tab 顺序，并淡化到 50% 不透明度。其轨道显示“禁止”光标，并忽略指针与按键。在数值暂时无法应用时使用，并在附近说明原因，例如“由你的套餐管理员设置”。用户永远无法更改的值应使用 `Progress` 或纯文字。

<Demo name="Disabled" />

### 示例：商品筛选面板

商品列表上的筛选面板，包含价格范围与最低评分。价格的两个滑块手柄有各自的名称，因此屏幕阅读器会读出“最低价格”和“最高价格”，并且标签注明了单位。评分滑块在最低处（“Any”）与最高处使用了带标签的刻度，因此最低与最高的选项都有名称，而无需一排数字。控件下方的摘要行用文字说明当前的选择，用户无需移动滑块手柄即可读懂结果。

![带价格范围滑块与最低评分滑块的商品筛选面板](images/ProductFilters.png)

```xml
<StackPanel Width="560" Spacing="22">
    <TextBlock Text="Filter products"
               FontSize="14"
               FontWeight="SemiBold"
               Foreground="{arc:ArcDesignTokenResource Foreground}" />
    <arc:Slider Label="Price (USD)"
                IsRange="True"
                Minimum="0"
                Maximum="1000"
                SmallChange="10"
                LowerValue="200"
                UpperValue="800"
                MinStepsBetweenThumbs="5"
                LowerThumbName="Minimum price"
                UpperThumbName="Maximum price" />
    <arc:Slider Label="Minimum rating"
                Minimum="0"
                Maximum="5"
                SmallChange="1"
                Value="4">
        <arc:Slider.Marks>
            <arc:SliderMark Value="0" Label="Any" />
            <arc:SliderMark Value="1" />
            <arc:SliderMark Value="2" />
            <arc:SliderMark Value="3" />
            <arc:SliderMark Value="4" />
            <arc:SliderMark Value="5" Label="5" />
        </arc:Slider.Marks>
    </arc:Slider>
    <TextBlock Text="Showing 186 products from 200 to 800 USD, rated 4 stars or more."
               Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
    <StackPanel Orientation="Horizontal" Spacing="8" HorizontalAlignment="Right">
        <arc:Button Content="Clear" Variant="Secondary" />
        <arc:Button Content="Apply filters" />
    </StackPanel>
</StackPanel>
```

在应用中，摘要来自视图模型，并在执行查询的同一个命令中更新，因此文字与结果永远不会不一致。XAML 示例直接写入文字，以使场景完整。

### 文案规范

- **标签**：简短的名词，当读数不包含单位时，在括号中注明单位：“音量”“价格（USD）”。标签为滑块手柄命名，因此必须说明数值是什么。
- **滑块手柄名称**：在范围中，用 `LowerThumbName` 和 `UpperThumbName` 明确命名每个滑块手柄，例如“最低价格”和“最高价格”。默认的“最小”“最大”后缀与单位放在一起读起来不通顺。
- **刻度标签**：简短的预设名称或数字：“0.5x”“1x”“Any”。当数字含义不明确时使用词语，例如评分下限为 0 时用“Any”。
- **读数**：格式化后的数字，而不是句子。应在格式化器中写入单位，使读数、气泡和朗读的数值保持一致。
- **摘要**：在控件下方用完整的文字说明当前选择，例如“正在显示 $200 至 $800 之间的商品。”

| 不要写 | 改为 |
|---|---|
| 价格范围 | 价格（USD） |
| 最小 | 最低价格 |
| 评分 0 | Any |
| $200.00 至 $800.00 | $200 至 $800 |
| 设置音量级别 | 音量 |

### 最佳实践

**推荐**

- 在标签或格式化器中写明单位，并保持读数与朗读的数值一致。没有单位时，对于看不见轨道的用户来说，数字只是猜测。
- 对于用户要缩小的区间，使用 `IsRange` 的范围，并从已提交的值执行查询，使结果不会在拖动的每一步都更新。
- 当两个滑块手柄描述同一件事时设置 `MinStepsBetweenThumbs`，这样在应用逻辑中，最小值永远不会超过最大值。
- 只为用户会选择的预设加标签，其余保留为无标签的刻度。刻度仍能显示刻度的划分位置，而标签足够短，不会在狭窄的轨道上互相重叠。

**避免**

- 用滑块来处理用户必须精确输入的值，例如税率或退款。用户无法在轨道上瞄准某个数字，之后修正数值只能靠运气。
- 在 `ValueChanged` 或 `RangeChanged` 中执行昂贵的请求。拖动会在每一步触发它们，因此一次调整会让请求执行数十次。
- 设置比轨道能显示的精度更细的 `SmallChange`。在 1000 个单位的范围上，0.001 的步长只会使滑块移动不到一个像素，按键看起来毫无作用。
- 依赖气泡来显示数值。气泡只在滑块手柄被拖动或获得键盘焦点时出现，因此稳定下来的数值需要读数或摘要行。

### 常见问题

**开启 `IsRange` 后，为什么滑块忽略了我的 `Value`？** 在范围模式下，控件只读取 `LowerValue` 与 `UpperValue`，不使用 `Value`。请设置两端的值，并从相同的属性中读回。

**如何防止两个滑块手柄相遇？** 将 `MinStepsBetweenThumbs` 设为两个滑块手柄必须保持的步数。按键与拖动都会在间距处停止，因此下限值永远不会超过上限减去间距。

**为什么轻甩有时会让数值在松开位置之外多移动一步？** 松开时会保留拖动的惯性，而当该步在屏幕上宽度至少为 12 px 时，可能会再多移动一步。松开前稍作停顿可以消除惯性，因此缓慢松开会落在最近的步长上。

**在范围模式下点击刻度标签会怎样？** 它会移动离标签数值最近的滑块手柄，并提交更改。另一个滑块手柄保持原位。

**没有标签的刻度能点击吗？** 不能。只有带标签的刻度才会通过其标签响应点击。无标签的刻度需通过拖动或按键到达，因此当用户应能点击某个预设时，请为它加上标签。

## API 参考

<!-- api: Slider -->

<!-- api: SliderMark -->

## 无障碍

- `Label` 为滑块命名。在范围中，每个滑块手柄的名称来自 `LowerThumbName` 与 `UpperThumbName`，或由 `Label` 加上“最小”和“最大”组成。
- 每个滑块手柄都以文字报告其数值，因此朗读的数值遵循 `ValueFormatter`。每个滑块手柄还会报告其允许的最小与最大值，因此另一个滑块手柄会限制它的范围。
- 读数、气泡与刻度标签对辅助技术隐藏。点击标签是指针操作的快捷方式；同样的数值也可通过键盘到达。
- 控件本身是一个分组，每个滑块手柄都是 Tab 顺序中单独的停靠点。
- 焦点环在按键后出现，而不是在指针按下后出现。

### 键盘

每个滑块手柄通过 `Tab` 获得焦点。在范围模式下，`Tab` 会从下限滑块手柄移到上限滑块手柄，下表中的按键作用于当前获得焦点的滑块手柄。

| 按键 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 将焦点移到下一个或上一个滑块手柄，然后移出控件；禁用的滑块会被跳过 |
| `Right` / `Up` | 按 `SmallChange` 增大数值 |
| `Left` / `Down` | 按 `SmallChange` 减小数值 |
| `Shift` + 方向键、`PageUp` / `PageDown` | 按 `LargeChange` 增大或减小 |
| `Home` / `End` | 移到该滑块手柄可取的最低或最高值；在范围模式下，另一个滑块手柄与 `MinStepsBetweenThumbs` 设定限制 |
| 在极限处按键 | 数值保持不变；滑块手柄朝按键方向用力，然后弹回 |

## 动效

- 滑块手柄与指针一对一跟随。松开时，它会吸附到最近的步长，并保留拖动的惯性，因此快速轻甩可能会再多移动一步。惯性仅在该步宽度至少为 12 px 时才会使用。
- 按下轨道时，最近的滑块手柄会以弹簧动画移动到指针处。在极限处按键时，滑块手柄会朝按下的方向用力，然后弹回。
- 数值气泡在活动滑块手柄上方弹出，数值变化时数字逐位滚动，松开后保留 700 ms。被拖动的滑块手柄会略微放大。
- 越过任一极限时，轨道会以最多 9 px 让位，并在松开时回弹，因此用户无需消息也能感觉到范围的边界。
- 减弱动效会移除惯性、拉伸与数字滚动。滑块手柄直接到达目标值，气泡只淡入淡出。关闭动效时直接跳到最终状态。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 越过极限拖动 | 轨道最多让位 9 px，并回弹 | 无拉伸；滑块手柄停在极限处 | 立即完成 |
| 松开 | 滑块手柄带着拖动的惯性吸附到一个步长，至多再多移动一步 | 滑块手柄无惯性地吸附到最近的步长 | 立即完成 |
| 在轨道上按下 | 最近的滑块手柄以弹簧动画移到指针下方 | 滑块手柄直接移到指针处 | 立即完成 |
| 拖动中的滑块手柄 | 以灵敏的弹簧放大到 1.16 倍 | 立即放大 | 立即完成 |
| 数值气泡 | 从 0.6 倍弹出并伴随柔和模糊；以 0.8 倍淡出 | 淡入淡出 | 立即完成 |
| 读数数字 | 数值变化时逐位滚动 | 立即切换 | 立即完成 |

## 相关组件

- `ElasticSlider` 用于单一设置，可越过两端拉伸并卡入刻度档位。
- 用户需要键入精确数字时使用 `NumberField`。
- `Progress` 用于显示但不由用户更改的数值。

| 组件 | 适用场景 |
|---|---|
| `ElasticSlider` | 一个有物理感的设置，例如音量或亮度，带有档位，并在极限处拉伸 |
| `NumberField` | 用户必须键入或步进一个精确数字，并带有单位与界限 |
| `MoneyInput` | 数值是需要币种和解析的金额 |
| `Select` / `SegmentedControl` | 选择是从短固定选项列表中挑一个 |
| `DateRangePicker` | 范围是日期跨度而非数字 |
| `Progress` | 数值向用户显示，并由应用程序设置 |
