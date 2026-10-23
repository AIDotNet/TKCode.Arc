---
title: 布局变形
summary: 同一组卡片的六种视图，从网格和列表到扇形堆叠、三维环形和螺旋，每张卡片都飞向它的新位置。
source-hash: 0b6dc3d6d5e1
---

<Demo name="Preview" />

## 何时使用

`LayoutMorph` 展示一组卡片，例如商品、人员或项目，并让读者选择以哪种方式查看。同一批卡片可以是网格、瀑布流、列表、扇形堆叠、三维轮播或螺旋。切换视图时，每张卡片都会沿着自己的弹簧从当前位置飞到新位置，飞行从所选选项附近开始并向外扩散，因此读者能跟随每张卡片完成这一变化。指向一张卡片会将它抬起，并在舞台下方的面板中显示它的数据；选中一张卡片则会把它变为详情，显示图片、描述、事实和一个操作。

- **商店的商品集合页或产品目录。** 网格便于快速浏览，列表把制作者和价格并排放在每个名称旁边，轮播则以大图展示商品。详情中的按钮（"Add to bag"）在读者继续浏览时保持其状态。
- **团队或演讲者页面。** 轮播和螺旋可以逐一翻过人像，面板无需指向即可显示当前位于最前的人员。
- **作品集或案例研究索引。** 瀑布流视图保留每张图片自身的比例，堆叠则让一小组内容看起来像一手牌。

## 何时不用

- **长列表或持续增长的集合。** 每张卡片都是一个实时元素，每次视图变化都会移动它们；本区块设计用于大约 10 至 40 张卡片。数百个条目请使用虚拟化列表、`DataGrid` 或分页网格。
- **供读者比较的数据。** 当读者需要在许多行之间排序、筛选或比较数值时，请使用表格；这里的列表视图每行只显示一个数值，无法排序。
- **只有单一视图。** 如果内容只适合一种视图，请使用普通网格或 `Carousel`，省去视图切换。
- **导航。** 卡片会在原处打开详情，而不是跳转页面。当选择一项应当带读者去别处时，请使用链接或卡片列表。

## 用法

### 条目

每个 `LayoutMorphItem` 是一张卡片。`Title` 在各处都是名称；`Subtitle` 是网格、轮播、浮动标签和面板中名称下方的那一行；`Meta` 是列表行第二行以及详情中的文字；`Value` 位于列表行的右侧，以及轮播说明的末尾。带有 `Image` 的条目会按填充方式裁剪显示；没有图片的条目则显示一张封面，标题位于顶部，数值位于底部，字号随卡片大小变化。`AspectRatio`（高度除以宽度）决定瀑布流视图中的形状。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:LayoutMorph Title="Studio shelf" DefaultPanelTitle="Shelf">
        <arc:LayoutMorphItem Key="ink-tray"
                             Title="Ink pen tray"
                             Subtitle="By Yuki Aoyama"
                             Meta="Yuki Aoyama · Blackened steel"
                             Value="$36"
                             AspectRatio="0.9"
                             Description="A shallow tray for pens and a clip or two, folded from one sheet.">
            <arc:LayoutMorphItem.Facts>
                <arc:LayoutMorphFact Label="Price" Value="$36" />
                <arc:LayoutMorphFact Label="Size" Value="24 × 8 cm" />
            </arc:LayoutMorphItem.Facts>
            <arc:LayoutMorphItem.Stats>
                <arc:LayoutMorphFact Label="Price" Value="$36" />
                <arc:LayoutMorphFact Label="In stock" Value="31" />
                <arc:LayoutMorphFact Label="Rating" Value="4.8" />
            </arc:LayoutMorphItem.Stats>
        </arc:LayoutMorphItem>
        <arc:LayoutMorphItem Key="yuki-aoyama"
                             Title="Yuki Aoyama"
                             Subtitle="Metalsmith, Osaka"
                             Meta="Metalsmith in Osaka"
                             Value="2 pieces"
                             ActionText="Follow"
                             ActiveActionText="Following" />
    </arc:LayoutMorph>
</UserControl>
```

为每个条目设置 `Key`：重建集合时，键保持不变的卡片会保留其位置、焦点和状态。对于你自己管理的集合，请使用 `ItemsSource`；`ObservableCollection` 会被监听，新增或移除的卡片会让其他卡片移动到新位置。

### 视图

`Layout` 是当前视图，并且是双向的，因此控件外部也能驱动它。`Layouts` 决定视图切换器提供哪些视图以及排列顺序；卡片上的方向键会按同一列表逐个切换。`SetLayout` 可以用代码切换视图，并能指定错峰动画的起点。

- **网格**：较宽的容器每行显示八张带说明的卡片；较窄的容器显示方形卡片，按六、五或四列排布，取能让卡片最大的那一种。
- **瀑布流**：按各自比例排列的多列卡片；当最高的一列放不下时，每张卡片会等比缩小，因此瀑布流视图永远不需要滚动。
- **列表**：一到三列的行，每行包含缩略图、两行文字和数值。高于舞台的列表可以用滚轮或垂直拖动滚动。
- **堆叠**：卡片呈扇形排列；指针所在的卡片会从扇形中向上滑出。
- **轮播**：大卡片沿环形排开，从第一张到最后一张。拖动或滑动后，它会停在最近的卡片上；快速轻扫可以翻过多张卡片。它不会首尾相接：拖过第一张或最后一张时会被拉伸，松开后弹回。水平滚轮（或按住 Shift 滚动滚轮）会轻推它一下。选中最前方旁边的卡片会把它转到最前方；选中最前方的卡片才会打开它。
- **螺旋**：每圈十张卡片的螺旋，向读者方向倾斜。拖动会使其旋转并沿螺旋移动，因此位于前方的卡片始终保持在中间。

轮播和螺旋会记住你离开时所处的位置。`FrontItem` 是位于最前方的卡片。

<Demo name="Catalogue" />

### 面板和详情

舞台下方的面板显示读者指向、聚焦、位于最前或已打开的卡片，以及它的三个 `Stats`；否则它显示 `DefaultPanelTitle`、`DefaultPanelSubtitle` 和 `DefaultStats`。诸如 "$1,240"、"4.8" 或 "2 days" 之类的数值会逐位滚动；当数值的文本不是普通数字时，请设置 `NumericValue`。

选中卡片会触发 `ItemInvoked`（将 `Handled` 设为 true 可以阻止它）、执行 `OpenCommand` 并打开详情：卡片本身会放大成一个对话框，其余卡片则淡出。`OpenVariant="Hero"` 会得到一张宽卡片和大图，适合带照片的商品；`Profile` 则在文字旁边放一张小图。详情中的按钮会切换 `IsActive` 并显示 `ActiveActionText`；它会触发 `ItemActionInvoked` 并执行 `ActionCommand`。设置 `OpenedItem` 可以用代码打开某张卡片，设为 null 则关闭；也可以调用 `Open` 和 `Close`。

```csharp
morph.ItemActionInvoked += (_, e) =>
{
    if (e.Item.IsActive)
    {
        bag.Add(e.Item.Key);
    }
    else
    {
        bag.Remove(e.Item.Key);
    }
};
```

<Demo name="Team" />

### 带框或扁平

`IsFramed`（默认值）会把舞台和面板绘制为带边框的圆角表面，适用于与其他内容并排的区块。当区块占满整页或全屏视图时，请将它设为 false；此时卡片会直接浮在页面上。`Title` 和 `Lede` 都是可选的：省略它们即可自行放置标题。`IsPanelVisible` 和 `IsHintVisible` 分别隐藏面板和提示。

<Demo name="Flat" />

### 窄容器

本区块读取的是它自身的宽度，而不是窗口的宽度。宽度低于 720 时，视图切换器只显示图标并横向铺满，名称作为提示显示；提示移到切换器下方，面板把名称放在统计数据上方，详情则把图片堆叠在文字上方。宽度低于 520 时，卡片之间的间距更紧，瀑布流视图使用四列。舞台高度为 600；宽度为 720 及以下时为 540，低于 480 时为 500，低于 360 时为 460。`StageHeight` 可以设置你自己的高度。

<Demo name="Narrow" />

### 文本

每段文字都有英文默认值，也都有对应属性：视图名称（`GridLabel` 至 `HelixLabel`）、提示（`GridHint` 至 `HelixHint`）、切换器的名称（`ViewLabel`）、面板的默认文字，以及供屏幕阅读器朗读的格式（`StageLabelFormat`、`LayoutChangedFormat`、`OpenedFormat`、`CloseTextFormat`、`ActionOnFormat`、`ActionOffFormat`）。

```xml
<arc:LayoutMorph Title="Ateliers"
                 ViewLabel="Vue de la galerie"
                 GridLabel="Grille"
                 GridHint="Choisissez une carte pour l’ouvrir"
                 DefaultPanelSubtitle="Survolez une carte"
                 CloseTextFormat="Fermer {0}"
                 OpenedFormat="{}{0} ouvert" />
```

### 数据形状与性能

- 卡片用位图图片按其在屏幕上显示的尺寸（按 1.25 倍的级差向上取整）制作的副本来绘制，因此大图片几乎不占 GPU 资源，缩小显示时也不会闪烁；打开的详情绘制图片本身。副本只制作一次：要更换图片，请把 `Image` 设为新的图片（重新绘制过的位图仍使用旧副本）。图片的尺寸请使用它们显示时最大尺寸的约两倍（英雄图详情约为 600 × 550），并且只解码一次；示例中的图片是根据种子在代码中绘制的。
- 卡片由自身绘制，并且只通过变换移动，因此视图切换不会进行任何布局计算。每张卡片都有自己的弹簧，所有卡片静止后舞台会停止请求帧；空闲的区块不产生任何开销。
- 请把集合控制在几十张卡片以内。轮播一次大约显示五张卡片，更远的卡片在环形背面隐藏等待。

## API 参考

<!-- api: LayoutMorph -->

<!-- api: LayoutMorphItem -->

<!-- api: LayoutMorphFact -->

## 无障碍

- 视图切换器是一组单选按钮，名称由 `ViewLabel` 指定。只有被选中的选项是一个 Tab 停靠点；方向键（循环）、Home 和 End 会在移动的同时进行选择。在窄容器中，每个图标仍保留自己的名称。
- 舞台是一个群组，名称来自 `StageLabelFormat`：包括标题、视图、卡片数量以及如何使用按键。每张卡片都是一个按钮，名称为 "Title, Subtitle, Value"（或 `AutomationName`），并由其 meta、描述和事实进行说明，因此逐张卡片获取的信息与网页中隐藏的表格相同。
- 只有一张卡片是 Tab 停靠点。上下方向键按数据顺序移动到上一张或下一张卡片，Home 和 End 移动到第一张和最后一张，Enter 或空格键打开卡片，左右方向键切换到上一个或下一个视图，并保持焦点在同一张卡片上。在轮播和螺旋中，获得焦点的卡片会转到最前方，位于最前的卡片就是 Tab 停靠点；在可滚动的列表中，获得焦点的行会滚动到可见范围内。
- 打开的详情是一个对话框，名称取自条目的标题，并由其描述说明。焦点移到名为 "Close" 的关闭按钮和标题上，Tab 键会一直停留在对话框内，直到它关闭。按 Escape、点击关闭按钮，或在舞台上卡片之外按下，都会关闭详情，焦点随之返回卡片。
- 视图变化、打开的卡片以及详情中的按钮都会以礼貌的方式播报（例如 "Carousel view"、"Halo floor lamp opened"）。
- 浮动标签只重复卡片名称的内容，不会被朗读。面板是纯文本。

## 动效

- **视图变化**：每张卡片沿自己的弹簧（刚度 100，阻尼 20，临界阻尼，约 1.15 秒）从当前位置移动到目标位置，包括尺寸、圆角和图片。离所选选项最近的卡片最先开始，最远的卡片晚 330 ms（`StaggerDuration`）。飞行过程中，卡片会朝读者方向抬起（最快时为距离的 0.1 倍）并随运动倾斜。背面朝向读者的卡片（螺旋中，或离开环形时尚未转回）只显示底板，透明度为 72%。在变化进行中被打断时，每张卡片都会从当前位置重新开始，不会跳动。
- **说明**：旧说明立即淡出；新说明会在卡片基本到达后淡入，因此两种布局的说明不会重叠。提示和面板的文字在 240 ms 内上升出现；面板中的数字会滚动。
- **指向**：卡片以快速弹簧（刚度 500，阻尼 49）抬起并带有阴影；在堆叠中它会从扇形中滑出，在螺旋中它会向前移动。在瀑布流、堆叠和螺旋视图中，名称标签会浮在卡片上方。
- **轮播和螺旋**：环形会一对一地跟随指针；松开后，它会停在轻扫所指向的卡片上（刚度 110，阻尼 19.5），并保留松开时的速度。拖过第一张或最后一张时会遇到橡皮筋阻力，松开后弹回。螺旋会向读者方向倾斜，并随旋转沿螺旋移动。
- **打开**：卡片以（刚度 130，阻尼 26）的弹簧放大成详情，同时舞台后退，其他卡片淡化到 12%。文字在接近结束时淡入，按钮则在开始后 260 ms 出现。
- 减少动效会让每张卡片立即就位，只保留短促的淡入淡出（详情中的按钮和标签）。关闭动效则会立即应用每一次变化。

## 相关组件

- `Carousel`，用于带分页的单行幻灯片。
- `CardStack`，用于可滑动的卡片堆。
- `SegmentedControl`，用于区块之外的视图切换。
- `RollingDigits`，用于面板中滚动的数字。
- `ExplodedView` 和 `CustomerGalaxy`，同为三维的展示型区块。
