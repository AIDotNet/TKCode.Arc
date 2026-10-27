---
title: 轮播
summary: 通过拖动、轻扫或方向键浏览一排幻灯片。
source-hash: 4a960604b2d2
---

<Demo name="Slides" />

## 何时使用

`Carousel` 在页面上展示一组简短、有序的同类条目，让查看者快速浏览后选择其中一项。当前幻灯片位于中央，两侧的相邻项提示还有更多条目。它适合页头、商城版块或内容中心，这些条目属于同一类事物，且查看者无需对其中任何一项做出决定。

- **商城首页的精选合集**：四到六个季节性合集，每个都是一张照片加标题和商品数量。拖动或点击箭头即可看到其余合集，查看者打开符合需要的那一个。
- **酒店或民宿预订页的房型**：每张幻灯片是一个房间，附带面积和每晚价格，因此住客通过滑动即可比较房间，而不必打开五个页面。
- **产品更新日志中的版本亮点**：最新版本的三到四项功能，每张幻灯片一项，并关闭自动播放，让读者按自己的节奏浏览。
- **营销网站上的客户案例**：三到五个案例研究，每个包含一个标志、一句引语和一个链接，排成一排，访客无需长时间滚动即可浏览。

## 何时不用

- **需要逐项做出决定**：用 `CardStack`，它会对每张卡片要求保留或跳过，并记录下来。
- **比较两张图片**：用 `ImageCompare`，它把前后对比放在同一画面上，并以分隔线区分。
- **需要扫读或排序的长记录列表**：用 `Table`。轮播一次只显示一张幻灯片，读者无法跨行比较字段。
- **必须完整阅读的文字，例如条款、说明或清单**：用 `Accordion` 或 `ExpandableText`。幻灯片会自行切换，把读者尚未读到的文字隐藏起来。
- **在内容各不相同的面板之间切换**：用 `Tabs`。轮播意味着幻灯片是同一类的同级条目，而标签页则代表不同的分区。
- **用户不能错过的消息，例如账单警告或服务中断**：用 `Alert` 或 `AnnouncementBar`。自动播放会把消息移走，轮播并不适合承载这类内容。

## 用法

每个子元素成为一张幻灯片。`Label` 为轮播命名，供辅助技术使用。

```xml
<arc:Carousel Label="港湾套房精选">
    <Border Height="200" Background="#2F4858" CornerRadius="12" />
    <Border Height="200" Background="#D9D2C5" CornerRadius="12" />
    <Border Height="200" Background="#8AA399" CornerRadius="12" />
</arc:Carousel>
```

`Carousel` 派生自 Avalonia 的 `SelectingItemsControl`。它拥有条目列表，即带有 `ItemTemplate` 的 `ItemsSource`，以及双向的 `SelectedIndex`，且始终选中一张幻灯片。这使轮播与列表拥有相同的选择模型，因此绑定、`SelectionChanged` 与自动化选择的工作方式都一致。只有当前位置附近的幻灯片会被实例化，因此长列表的成本与短列表大致相当。

### 结构

1. **视口**：容纳幻灯片的轨道。当前幻灯片位于中央。幻灯片宽度为轮播宽度的 `SlideWidthRatio`，上限为 `MaxSlideWidth`，`SlideGap` 设置幻灯片之间的间距。
2. **相邻项**：两侧的幻灯片缩放至 90%，并随着远离中心而降至 50% 的不透明度。它们的边缘显示在边距中，这是存在更多幻灯片的提示。
3. **视口焦点环**：当视口获得键盘焦点时，在幻灯片外侧 4 px 处绘制的 2 px 轮廓。
4. **控制行**：位于视口下方 16 px 的一行。当轮播只有一张或没有幻灯片时隐藏。
5. **播放与暂停**（`Interval`）：行左侧的按钮。只有当 `Interval` 为正值且动效为完整模式时才会出现。它的名称在“开始幻灯片轮换”和“暂停幻灯片轮换”之间切换。
6. **指示器**：行中间的页面圆点。每个圆点为 6 px，当前圆点会拉伸成 22 px 的胶囊，并随轨道移动。该行高 36 px。
7. **上一张与下一张**：右侧的箭头按钮，相距 8 px。在第一张和最后一张幻灯片处，对应的按钮处于软禁用状态：它仍保留焦点，但不执行任何操作。

宽度低于 340 px 时轮播进入紧凑模式：箭头按钮之间的间距从 8 px 减为 4 px，播放按钮更靠近箭头。

### 幻灯片宽度与间距

幻灯片占轮播宽度的 80%（`SlideWidthRatio` 为 0.8），上限为 340 px（`MaxSlideWidth`）。轮播宽度达到 425 px 时即达到上限，因此在宽页面上幻灯片保持固定的阅读尺寸，多出的宽度用来显示更多相邻项。若想让幻灯片占据更多行宽，可将比例提高到 0.9；若想在窄栏中显示更多相邻项，可降到 0.7。除非幻灯片自带需要更多留白的阴影或边框，否则 `SlideGap` 保持 16 px。

幻灯片尺寸跟随轮播自身的宽度，而不是窗口宽度，因此放在窄侧边栏中的轮播与放在主内容区的轮播保持相同的比例。

### 绑定条目

当幻灯片来自数据时，使用 `ItemsSource` 与 `ItemTemplate`。

```xml
<arc:Carousel Label="港湾套房" ItemsSource="{Binding Suites}">
    <arc:Carousel.ItemTemplate>
        <DataTemplate>
            <Border Height="200" Background="#2F4858" CornerRadius="12">
                <TextBlock Text="{Binding Name}" Margin="18" />
            </Border>
        </DataTemplate>
    </arc:Carousel.ItemTemplate>
</arc:Carousel>
```

### 绑定到视图模型

视图模型拥有幻灯片，以及轮播应当显示的位置。将 `SelectedIndex` 设为双向绑定，使视图模型能在启动时设定幻灯片，并在查看者切换时读回位置。将 `IsAutoPlay` 设为双向绑定，使设置开关能开启或关闭轮换。

```csharp
public interface IStorefrontService
{
    IEnumerable<Collection> GetFeatured();
}

public partial class HomeViewModel : ObservableObject
{
    public HomeViewModel(IStorefrontService storefront)
    {
        Collections = new ObservableCollection<Collection>(storefront.GetFeatured());
    }

    public ObservableCollection<Collection> Collections { get; }

    [ObservableProperty]
    private int _featuredIndex;

    [ObservableProperty]
    private bool _rotateFeatured;
}
```

```xml
<StackPanel Spacing="16">
    <arc:Carousel Label="精选系列"
                  ItemsSource="{Binding Collections}"
                  SelectedIndex="{Binding FeaturedIndex, Mode=TwoWay}"
                  Interval="0:0:6"
                  IsAutoPlay="{Binding RotateFeatured, Mode=TwoWay}">
        <arc:Carousel.ItemTemplate>
            <DataTemplate>
                <Border Height="200" Background="#2F4858" CornerRadius="12">
                    <TextBlock Text="{Binding Title}" Margin="18" />
                </Border>
            </DataTemplate>
        </arc:Carousel.ItemTemplate>
    </arc:Carousel>
</StackPanel>
```

- 优先使用绑定的 `SelectedIndex`，而不是在视图模型中调用 `GoTo()`。这样视图模型无需持有控件的引用即可移动轮播，同一绑定也能处理查看者自己的操作。
- `SelectionChanged` 会在查看者操作和轮换时触发。用它来更新读数或记录浏览，并把该值视为当前幻灯片，而不是一个决定。
- `IsRotating` 与 `CanRotate` 是只读的。读取它们可以得知轮换是否正在运行，但只能通过 `IsAutoPlay` 改变轮换状态。
- 轮播没有命令接口。下一张、上一张以及跳转都是控件上的方法，因此需要它们的视图模型应将轮播的 `SelectedIndex` 保持为唯一的数据来源。

### 由代码驱动

```csharp
carousel.SelectionChanged += (_, _) => readout.Text = $"第 {carousel.SelectedIndex + 1} 张，共 {count} 张";

carousel.Next();
carousel.Previous();
carousel.GoTo(3);
```

`Next` 与 `Previous` 在两端停止，不会循环。`GoTo` 会把索引限制在有效范围内，因此超过最后一张的值会选中最后一张幻灯片。

### 幻灯片尺寸与标签

`SlideLabelFormatter` 生成为每张幻灯片播报的文字，并作为幻灯片的无障碍名称。若未设置，轮播使用本地化的“2 / 5”格式。当幻灯片名称本身有意义时（例如幻灯片上内容的标题），应使用格式化器。

```csharp
carousel.SlideLabelFormatter = (index, count) => $"档案照片 {index + 1}，共 {count}";
```

### 自动播放

将 `Interval` 设为正的时间跨度即可添加播放控件。轮播会按该间隔向前移动，并在最后一张之后回到第一张。`IsAutoPlay` 开始轮换，播放控件则切换其状态。轮换默认关闭，因此由查看者决定幻灯片何时移动。

幻灯片显示期间，指示器会从右向左逐渐耗尽，让查看者知道距离下一张还有多久。只有在动效为完整模式且至少有两张幻灯片时，轮换才可用。

<Demo name="Rotation" />

```xml
<arc:Carousel Label="季节性优惠" Interval="0:0:4" IsAutoPlay="True">
    <Border Height="200" Background="#2F4858" CornerRadius="12" />
    <Border Height="200" Background="#D9D2C5" CornerRadius="12" />
</arc:Carousel>
```

### 克制地使用动效

轮换会在没有请求的情况下移动内容，因此只应在幻灯片属于装饰或预告，且读者无需看完每一张时使用。对于查看者必须阅读的内容，例如优惠条款、产品价格或警告，应保持关闭。

当查看者需要控制时，轮换会自动暂停：指针位于轮播上方时、键盘焦点位于其中时、拖动进行时、轮播滚出可视区域时以及窗口最小化时。原因消失后，轮换会恢复。播放控件始终可见，因此查看者可以彻底停止轮换。使用至少四秒的间隔，让幻灯片有时间被读完再移动。

减弱动效会完全关闭轮换，因此要求减少动效的查看者会看到第一张幻灯片，并可以手动浏览其他幻灯片。

### 长轮播

有 120 张幻灯片时，只会实例化当前位置附近的幻灯片。指示器则会显示 120 个圆点。它会滚动，使当前胶囊保持在中央，并在两端淡出，因此这一排不会变成一堵圆点的墙。

<Demo name="LongCarousels" />

### 照片

幻灯片可以在绘制的场景之上显示 `AsyncImage` 照片，因此离线时看起来仍然完整。

<Demo name="Photos" />

### 示例：商城精选合集

商城首页上一排四个季节性合集。每张幻灯片有一个代替照片的色块、一个标题，以及一行包含商品数量和起始价格的文字。自动播放关闭，由查看者决定何时切换，两侧的相邻项提示还有更多合集。文案简短，只陈述购物者会比较的事实：有多少件商品、起价多少。轮播宽度与页面内容宽度一致，因此幻灯片尺寸为 340 px 的上限，相邻项填满两侧的边距。

![A featured collections carousel with the middle slide active, its neighbors peeking in from both sides](images/FeaturedCollections.png)

```xml
<StackPanel Width="560" Spacing="16">
    <TextBlock Text="Featured collections"
               FontSize="16"
               FontWeight="Medium"
               Foreground="{arc:ArcDesignTokenResource Foreground}" />
    <arc:Carousel Label="Featured collections" SelectedIndex="1">
        <Border Height="236"
                BorderThickness="1"
                ClipToBounds="True"
                Background="{arc:ArcDesignTokenResource Surface}"
                BorderBrush="{arc:ArcDesignTokenResource Border}"
                CornerRadius="{arc:ArcDesignTokenResource RadiusPanel}">
            <StackPanel>
                <Border Height="150" Background="#2F4858" />
                <StackPanel Margin="18,14,18,18" Spacing="4">
                    <TextBlock Text="Spring linen"
                               FontSize="16"
                               FontWeight="Medium"
                               Foreground="{arc:ArcDesignTokenResource Foreground}" />
                    <TextBlock Text="24 pieces · from $180"
                               FontSize="14"
                               Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
                </StackPanel>
            </StackPanel>
        </Border>
        <Border Height="236"
                BorderThickness="1"
                ClipToBounds="True"
                Background="{arc:ArcDesignTokenResource Surface}"
                BorderBrush="{arc:ArcDesignTokenResource Border}"
                CornerRadius="{arc:ArcDesignTokenResource RadiusPanel}">
            <StackPanel>
                <Border Height="150" Background="#D9D2C5" />
                <StackPanel Margin="18,14,18,18" Spacing="4">
                    <TextBlock Text="Trail essentials"
                               FontSize="16"
                               FontWeight="Medium"
                               Foreground="{arc:ArcDesignTokenResource Foreground}" />
                    <TextBlock Text="38 pieces · from $95"
                               FontSize="14"
                               Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
                </StackPanel>
            </StackPanel>
        </Border>
        <Border Height="236"
                BorderThickness="1"
                ClipToBounds="True"
                Background="{arc:ArcDesignTokenResource Surface}"
                BorderBrush="{arc:ArcDesignTokenResource Border}"
                CornerRadius="{arc:ArcDesignTokenResource RadiusPanel}">
            <StackPanel>
                <Border Height="150" Background="#8AA399" />
                <StackPanel Margin="18,14,18,18" Spacing="4">
                    <TextBlock Text="Harbor kitchen"
                               FontSize="16"
                               FontWeight="Medium"
                               Foreground="{arc:ArcDesignTokenResource Foreground}" />
                    <TextBlock Text="17 pieces · from $60"
                               FontSize="14"
                               Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
                </StackPanel>
            </StackPanel>
        </Border>
    </arc:Carousel>
</StackPanel>
```

### 文案规范

- **标签**：为这组幻灯片命名，例如“精选系列”或“港湾套房精选”。标签是轮播对辅助技术的唯一名称，因此避免使用“轮播”或“幻灯片”。
- **幻灯片标题**：一个简短的名词短语，用来命名条目。在幻灯片宽度内保持一行。
- **幻灯片细节**：一个数量加上读者会比较的一个事实，例如“24 件 · 起价 $180”。数字使用阿拉伯数字，货币符号与金额放在一起。
- **幻灯片名称**：当位置很重要时，使用 `SlideLabelFormatter` 为幻灯片命名，例如“档案照片 3 / 共 120”，而不是只说位置。

| 不要写 | 改为 |
|---|---|
| 轮播 | 精选系列 |
| 点击这里查看更多！ | 徒步必备，38 件，起价 $95 |
| 幻灯片 1 | 春季亚麻 |
| 自动滚动的优惠 | 季节性优惠，每 6 秒轮换一次 |

### 最佳实践

**推荐**

- 为每个轮播设置一个说明幻灯片内容的 `Label`。它是屏幕阅读器最先播报的内容。
- 让当前幻灯片的文案自成一体，使从链接进入的查看者能立即理解。
- 除非幻灯片只是装饰，否则保持 `IsAutoPlay` 关闭。没有请求动效的读者不应失去正在阅读的幻灯片。
- 使用 `SelectedIndex` 让页面从需要的幻灯片开始，例如最新版本，而不是总是从第一张开始。

**避免**

- 对包含警告、价格或截止日期的内容使用自动播放。轮换会在被读完之前把它移出视野。
- 幻灯片类型不一致，例如横幅与商品卡片并排。相邻项会变成噪音，而不像是同类内容的更多项目。
- 把页面唯一的导航放在轮播里。键盘和屏幕阅读器用户仍能访问它，但链接列表更易于扫读和记忆。
- 过短的间隔。几秒钟内就移动的幻灯片不足以让文案被读完。
- 每次数据刷新都替换 `ItemsSource`。位置会被限制，并且条目变化时轨道可能跳动；数据变化时应原地更新集合。

### 常见问题

**为什么轮播在第一张和最后一张处停止，而不是循环？** 手动导航不会循环：`Previous` 与 `Next` 在两端停止，对应的按钮会被软禁用。只有自动播放会循环，并在最后一张之后回到第一张。

**为什么没有出现播放按钮？** 只有当 `Interval` 为正的时间跨度且动效为完整模式时，按钮才会出现。在减弱动效模式下轮换不可用，因此按钮保持隐藏，`CanRotate` 为 `False`。

**即使设置了 `IsAutoPlay`，为什么自动播放仍未开始？** 轮换需要间隔、至少两张幻灯片以及完整动效。当指针位于轮播上方、键盘焦点位于其中、拖动期间、轮播滚出可视区域以及窗口最小化时，它也会暂停。`IsRotating` 可告诉你轮换是否正在运行。

**如何从某一张特定的幻灯片开始？** 设置 `SelectedIndex`，例如在 XAML 中写 `SelectedIndex="2"`，或在轮播加载后调用 `GoTo(2)`。`SelectedIndex` 是双向的，因此查看者停留的值可以被读回。

**为什么触控板滑动会自行停下？** 水平方向的触控板滑动会让轨道与手指一对一地移动。在 120 毫秒内没有滚轮事件后，轮播会停在最近的幻灯片上，因此半途的滑动不会让轨道停在两张幻灯片之间。

## API 参考

<!-- api: Carousel -->

## 无障碍

- `Label` 为轮播命名。选择变化时，幻灯片会通过 `SlideLabelFormatter` 以“2 / 5”之类的形式播报。
- 左右方向键每次移动一张幻灯片。Home 与 End 分别跳到第一张与最后一张。
- Tab 依次切换幻灯片、播放控件、指示器以及上一张、下一张按钮。
- 非当前激活的幻灯片会从焦点顺序和自动化树中移除。
- 自动播放会在指针位于轮播上方、键盘焦点位于其中、正在拖动、滚出可视区域以及窗口最小化时暂停。自动播放不会播报每一张幻灯片。
- 指示器是一个名为“选择幻灯片”的标签列表，每个圆点是一个标签，其名称与它所选的幻灯片相同，例如“2 / 4”。
- 播放控件名为“开始幻灯片轮换”或“暂停幻灯片轮换”，上一张与下一张按钮分别名为“上一张幻灯片”和“下一张幻灯片”。
- 由查看者或按键引起的选择变化会以礼貌方式播报。轮换不做播报。

### 键盘

| 按键 | 操作 |
|---|---|
| `Left` / `Right` | 后退或前进一张幻灯片；当某个指示器标签获得焦点时，焦点移动到新的指示器标签 |
| `Home` / `End` | 跳到第一张或最后一张幻灯片 |
| `Tab` / `Shift+Tab` | 在视口、播放控件、指示器以及上一张、下一张按钮之间移动 |
| `Enter` 或 `Space` | 按下已获得焦点的按钮：播放或暂停、上一张或下一张 |

轮播不处理带有 `Ctrl`、`Alt` 或 `Cmd` 的方向键，在文本框中输入的方向键则留在文本框内。

## 动效

- 拖动时幻灯片跟随指针移动，并带着惯性吸附到最近的幻灯片。键盘或按钮操作会沿移动方向切换幻灯片。
- 相邻项会随与当前幻灯片的距离而缩小并变暗。
- 指示器会拉伸成跟随当前幻灯片的胶囊；自动播放等待期间，胶囊会逐渐耗尽。
- 减弱动效时直接跳到目标幻灯片，并停止自动播放。关闭动效时立即到达最终状态。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 拖动释放 | 惯性带动轨道吸附到最近的幻灯片；快速轻扫至少移动一张幻灯片 | 直接跳到最近的幻灯片 | 立即 |
| 键盘或按钮移动 | 以平滑弹簧沿曲线滑向新幻灯片 | 直接跳到新幻灯片 | 立即 |
| 相邻项的缩放与不透明度 | 每一帧跟随轨道位置 | 随轨道一起跳动 | 立即 |
| 指示器胶囊 | 拉伸并跟随当前幻灯片 | 直接跳到当前圆点 | 立即 |
| 自动播放 | 按间隔前进，并使胶囊耗尽 | 不可用 | 不可用 |

## 相关组件

- 每个条目都需要做决定而不只是浏览时，用 `CardStack`。
- 同一张图片的前后对比，用 `ImageCompare`。
- 在不相似的面板之间切换时，用 `Tabs`。

| 组件 | 适用场景 |
|---|---|
| `CardStack` | 行中的每个条目都需要保留或跳过的决定 |
| `ImageCompare` | 用可拖动的分隔线比较同一张图片的两个版本 |
| `Tabs` | 这一排在内容不同的分区之间切换 |
| `Accordion` / `ExpandableText` | 读者必须按顺序完整阅读内容 |
| `Alert` / `AnnouncementBar` | 消息必须保持可见直到用户阅读 |
| `Table` | 读者扫读、排序或比较大量记录 |
