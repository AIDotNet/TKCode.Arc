---
title: 底部面板
summary: 停靠在预览高度或完整高度的面板，跟随手指移动。
source-hash: c4dc8fc6229b
---

<Demo name="Detents" />

## 何时使用

`BottomSheet` 是一个模态面板，从窗口底边升起，并停靠在一个或多个高度上，这些高度称为停靠点（detent）。它适合移动端优先屏幕中的次要任务：用户先看到一段有用的预览内容，只有需要更多时才展开面板。它以遮罩覆盖在页面之上，因此是流程中一个聚焦的步骤，而不是第二层导航。

- **结账时的配送与取货选项**：手机上的买家从预览中看到最便宜的选项，为订单 4417 选择配送时段或取货点，然后展开查看完整的承运商与时间列表。
- **商品或订单列表上的筛选**：商店应用在预览中显示已启用的筛选数量，并让用户在完整高度下调整筛选，无需离开正在滚动的列表。
- **分享与导出选项**：平板上的仪表板提供“复制链接”“通过邮件发送”和“导出为 PDF”，放在一个简短的面板中，用户可以向下轻扫将其关闭。
- **地图与媒体视图**：配送追踪地图在预览中显示快递员的预计到达时间，用户向上拖动面板阅读停靠站列表，地图则保持在面板后方可见。
- **行程或预订详情**：行程以预览打开，首日可见，让用户知道预订存在，并可展开阅读其余内容。

## 何时不用

- **需要打断整个任务的决定**，例如删除项目或取消订阅：用 `Dialog`，它居中显示，并把“取消”与破坏性操作保留在同一个可见面板中。
- **宽屏布局中需要与列表并排可见的侧边面板**：在右侧或左侧边缘使用 `Drawer`。
- **锚定在某个控件上的小块内容**，例如列选择器或颜色色块网格：用 `Popover`，它指向触发器，且不会压暗页面。
- **图标按钮的一行提示**：用 `Tooltip`。面板对于一个标签来说太重了。
- **命令列表**：用 `DropdownMenu` 或 `ContextMenu`，它们让命令与触发它们的控件保持在一起。

## 用法

`BottomSheet` 派生自 `ModalOverlay`，而 `ModalOverlay` 本身是一个 `ContentControl`。它的 `Content` 是可滚动的正文，`Title` 与 `Description` 构成页头，`Actions` 接收可选的页脚。控件在声明的位置不渲染任何内容；当 `IsOpen` 为 true 时，面板会显示在窗口的覆盖层上，带有遮罩、焦点陷阱、Escape 与点击外部区域的关闭方式，并且在面板移除之前完成退出动画。面板还加入下面描述的停靠点、拖动与滚动规则。

### 基础面板

```xml
<arc:BottomSheet Title="里斯本之旅"
                 Description="3 晚 · 6 月 12 日至 15 日"
                 IsOpen="{Binding ShowTrip}">
    <StackPanel Spacing="12">
        <TextBlock Text="周五 · 14:05 抵达 LIS，打车前往 Alfama" />
        <TextBlock Text="周六 · 28 路电车与城堡" />
    </StackPanel>
</arc:BottomSheet>
```

### 结构

1. **拖动手柄**：一个按钮，在 36 × 5 px 的横条外围有 56 × 20 px 的点击区域。横条使用边框强调色，悬停及面板被拖动时变为弱化文字颜色。它的无障碍名称在较小的停靠点时为“展开面板”，在最高的停靠点时为“收起面板”。
2. **页头**（`Title`、`Description`）：两侧 20 px 内边距，上方 8 px、下方 16 px（窄屏时两侧为 16 px）。标题使用大号字号、中等字重，颜色为前景色。说明使用小号字号、次要文字颜色，位于标题下方 2 px，为空时隐藏。
3. **关闭按钮**（`ShowCloseButton`、`CloseButtonName`）：位于页头右侧的 32 px 全圆角按钮，带 1 px 边框和弱化的表面填充色。默认显示。
4. **页头分隔线**：页头下方一条边框颜色的 1 px 线。只有内容滚动到页头下方（`:scrolled`）时才会出现。
5. **正文**（`Content`）：可滚动区域，两侧 20 px 内边距，上方 4 px、下方 24 px（窄屏时两侧为 16 px）。滚动条只在最高的停靠点出现。禁用水平滚动。
6. **操作区**（`Actions`）：可选的页脚，位于可滚动的正文内，右对齐，位于内容下方 20 px。`Actions` 为 null 时隐藏。
7. **表面**：凸起表面背景，顶部与侧边有 1 px 边框、底部没有边框，顶部两角使用表面圆角，并带浮动阴影。最宽 576 px（36 rem），居中，并向窗口边缘以下延伸 160 px，因此向上拉伸时不会露出缝隙。
8. **遮罩**：位于面板后方的压暗层，使用面板自己的遮罩颜色（浅色与深色两套）。遮罩不做模糊，其不透明度随面板位置变化。

### 选择停靠点

`Detents` 以窗口高度的比例列出停靠高度。默认值为 0.45 与 0.92，即预览与近乎完整的视图。取值会被排序、去除重复项；大于 1 的值会被限制为 1，零或负值会被忽略。只有一个值时，面板高度固定。预览高度应完整显示第一条有用的内容，因为用户正是根据这段预览做决定的。当面板后方的页面仍需保留上下文时，把最高的停靠点设在完整窗口高度以下；只有行程或媒体查看器这类全屏阅读场景，才使用 0.92 或更高的值。

`BottomSheetDetents.Parse` 读取以逗号或空格分隔的字符串，因此列表可以来自配置或资源。`SnapPoints` 是 `Detents` 的别名，供使用该名称的代码使用。`EffectiveDetents` 返回实际使用的排序后数值，适合记录日志与测试。

<Demo name="ThreeDetents" />

```csharp
share.Detents = BottomSheetDetents.Parse("0.3, 0.6, 0.95");
share.InitialDetentIndex = 1;
```

三个停靠点适合分享或导出面板：一个较小的预览用于快捷操作，一个中间停靠点用于较短的列表，一个完整停靠点用于预览。面板打开期间修改 `Detents` 时，会保留相同的索引并限制在新列表范围内，因此较短的列表可能把面板移到更低的停靠点；面板保持打开。

### 打开并在停靠点之间移动

`InitialDetentIndex` 选择面板打开时所在的停靠点。默认为 0，即最小的停靠点。该索引指向排序后的列表，并限制在列表范围内。`DetentIndex` 报告面板当前停靠的停靠点；它是只读的，因此请通过 `SnapTo`、`Expand` 与 `Collapse` 来改变它。

`OpenCommand` 可在不设置 `IsOpen` 的情况下从按钮打开面板。`Expand` 将面板移到最高的停靠点，`Collapse` 移到最小的停靠点，`SnapTo` 移到任意索引。面板关闭时 `SnapTo` 不起作用。每当面板停靠到另一个停靠点时都会引发 `DetentChanged`；拖动过程中不会引发。

```csharp
openTrip.Command = trip.OpenCommand;

trip.DetentChanged += (_, e) => detent.Text = $"停在 {e.Fraction:P0}";
trip.Expand();
```

### 长内容

内容不做虚拟化：面板打开时会创建正文中的每一项。长列表请保持分页，或在面板内放置一个自行虚拟化项目的控件，例如 `ListBox`。低于最高的停靠点时，内容不滚动。向下滚动滚轮、在内容上触摸拖动、Tab 进入首屏以下的内容，或按下滚动键，都会先把面板完全展开。收起面板会让内容回到顶部，因此预览始终显示内容的开头。

```xml
<arc:BottomSheet Title="行程安排" IsOpen="{Binding ShowItinerary}">
    <ItemsControl ItemsSource="{Binding Items}" />
</arc:BottomSheet>
```

### 关闭

`Closed` 通过 `OverlayClosedEventArgs.Reason` 报告面板关闭的原因。快速滑动或向下拖动超过最小的停靠点会报告 `Gesture`，Escape 报告 `Escape`，按下遮罩报告 `PressOutside`，关闭按钮报告 `CloseButton`，设置了结果的按钮报告 `Action`。用户关闭之前会引发 `Closing`，可以通过 `e.Cancel` 取消。

```csharp
trip.Closed += (_, e) => detent.Text = $"关闭方式：{e.Reason}";
```

### 拖动、快速滑动与橡皮筋

整个页头都是拖动手柄，因此在内容上方的任何位置按下都可以开始拖动。在内容上触摸拖动会移动面板，直到它完全展开；之后内容开始滚动，而从内容顶部向下拉会再次移动面板。面板与指针一对一跟随。超过最高的停靠点后，面板会以最多 120 px 的橡皮筋效果拉伸。松开时，面板会按速度投射 0.2 秒的位移，停靠到最近的停靠点；若投射位置落在最小的停靠点以下，则关闭。松开时速度超过 320 px/s 的快速滑动，总会使面板至少移动一个停靠点，因此快速轻扫永远不会被忽略。在拖动手柄上单击会在最小与最高的停靠点之间切换。

在减弱动效和动效关闭时禁用拖动。这些情况下请使用拖动手柄、键盘或 `SnapTo`，并为所有用户保留关闭按钮。

### 焦点与关闭

焦点首先落在面板本身，因此屏幕阅读器会先播报它的标题。面板为最顶层的模态时，Tab 与 Shift+Tab 停留在面板内；面板关闭时，焦点回到打开它的控件。当 `CloseOnEscape` 为 true（默认值）时，Escape 关闭面板；当 `CloseOnOutsidePress` 为 true 时，按下遮罩关闭面板。如果页面必须保持可交互，请将 `IsModal` 设为 false：此时遮罩与焦点陷阱都会被移除。面板堆叠时，只有最顶层的面板响应 Escape。

### 绑定到视图模型

`IsOpen` 默认为双向绑定，因此通过手势或 Escape 关闭面板时，会把 `false` 写回视图模型。`DetentIndex` 是只读的，因此视图模型通过 `DetentChanged` 事件接收停靠点的变化，并把它们保存到自己的属性中。编译绑定需要在视图上设置 `x:DataType`；示例为简洁起见省略了它。

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

public interface IOrderService
{
    Task SetDeliverySlotAsync(string slotId);
}

public partial class DeliveryViewModel : ObservableObject
{
    private readonly IOrderService _orders;

    public DeliveryViewModel(IOrderService orders) => _orders = orders;

    [ObservableProperty]
    private bool _showOptions;

    [ObservableProperty]
    private int _restingDetent;

    [ObservableProperty]
    private string _selectedSlotId = string.Empty;

    [RelayCommand]
    private async Task ConfirmSlotAsync()
    {
        await _orders.SetDeliverySlotAsync(SelectedSlotId);
        ShowOptions = false;
    }
}
```

```xml
<arc:BottomSheet Title="配送选项"
                 Description="订单 4417 从 Northwind Berlin 发货。"
                 Detents="0.5, 0.9"
                 IsOpen="{Binding ShowOptions}">
    <StackPanel Spacing="12">
        <RadioButton GroupName="slot" Content="明天，08:00 至 12:00（免费）" />
        <RadioButton GroupName="slot" Content="明天，17:00 至 20:00（4.90 EUR）" />
    </StackPanel>
    <arc:BottomSheet.Actions>
        <arc:Button Content="确认配送时段" Command="{Binding ConfirmSlotCommand}" />
    </arc:BottomSheet.Actions>
</arc:BottomSheet>
```

```csharp
delivery.Closed += (_, e) => log.Text = $"选项关闭方式：{e.Reason}";
delivery.DetentChanged += (_, e) => viewModel.RestingDetent = e.Index;
```

### 示例：移动端结账中的配送选项

手机上的结账页面会在页面中显示订单总额与所选时段。点击“更改”会以 50% 的预览高度打开面板，列出两个最便宜的时段，同时不遮住上方的订单总额。第二个选项是 90% 的停靠点，用于查看完整的承运商列表。页头写明订单，让用户看到这次选择会更改哪个订单；底部唯一的操作说明了结果。时段保存后面板关闭：视图模型把 `ShowOptions` 设为 `false`，而不是由按钮设置结果，因为保存可能失败，此时面板必须保持打开并显示其消息。

```xml
<arc:BottomSheet Title="配送选项"
                 Description="订单 4417 从 Northwind Berlin 发货。"
                 Detents="0.5, 0.9"
                 IsOpen="{Binding ShowOptions}">
    <StackPanel Spacing="12">
        <RadioButton GroupName="slot"
                     Content="明天，08:00 至 12:00（免费）"
                     IsChecked="True" />
        <RadioButton GroupName="slot" Content="明天，17:00 至 20:00（4.90 EUR）" />
        <RadioButton GroupName="slot" Content="周四，09:00 至 13:00（免费）" />
        <TextBlock Text="承运商：Northwind Courier。超过 500 EUR 需要签收。"
                   TextWrapping="Wrap"
                   Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
    </StackPanel>
    <arc:BottomSheet.Actions>
        <arc:Button Content="保存配送时段" Command="{Binding ConfirmSlotCommand}" />
    </arc:BottomSheet.Actions>
</arc:BottomSheet>
```

### 文案规范

页头要让用户在展开之前就知道面板的用途。标题是一个简短的名词短语。说明写明范围，数字用阿拉伯数字，日期使用简短格式。操作使用动词加宾语，而预览不应依赖只有展开后才能看到的提示。

| 不要写 | 改为 |
|---|---|
| 选项 | 配送选项 |
| 点击此处查看更多 | 显示全部 12 个站点 |
| 上滑查看详情 | 展开面板，或使用拖动手柄展开 |
| 好 | 保存配送时段 |
| 你能做的事 | 分享行程 |
| 从 6 月 12 日起住 3 晚 | 3 晚，6 月 12 日至 15 日 |

不要在正文第一行重复标题；正文应补充页头没有包含的信息。

### 最佳实践

**推荐**

- 把决定或最有用的一行放在预览中，让面板值得被展开。用户根据预览做决定，需要滚动才能找到的内容常常被跳过。
- 让关闭按钮保持可见。并非所有输入设备都能发现手势，而可见的控件是键盘与辅助技术用户的关闭途径。
- 为每个面板设置 `Title`。它是面板的无障碍名称；没有它，面板会被播报为一个未命名的窗口。
- 当面板的布局取决于停靠位置时（例如面板拉高时地图会平移），用 `DetentChanged` 保存停靠位置。

**避免**

- 把面板叠在另一个面板之上。每个面板都会增加一层遮罩和一个焦点陷阱，且只有最顶层响应 Escape，因此用户必须关闭多层才能回到页面。
- 正文中过长且未分页的列表。内容不做虚拟化，因此面板打开时会创建每一行。
- 只依赖拖动来关闭。减弱动效时禁用拖动，因此必须始终保留按钮或键盘操作。
- 在正文中水平滚动。水平滚动已禁用，因此宽内容会被裁切，而不是滚动显示。
- 用面板做破坏性确认。`Dialog` 把后果与按钮放在一个居中的面板中，供用户在行动前阅读。

### 常见问题

**为什么面板会以最小的停靠点打开？** `InitialDetentIndex` 默认为 0，即排序列表中最小的停靠点。把它设为所需停靠点的索引，或在打开后调用 `Expand`。

**如何在没有代码后置的情况下从按钮打开面板？** 通过名称把按钮的命令绑定到面板的 `OpenCommand`，例如 `Command="{Binding #TripSheet.OpenCommand}"`，或在视图模型中绑定 `IsOpen`。

**为什么预览时我的内容不能滚动？** 低于最高的停靠点时，内容是固定的。向下滚动滚轮、在内容上触摸拖动、Tab 进入首屏以下的内容或按下滚动键，都会先打开面板。这样可以避免预览与页头分离。

**如何阻止点击面板外部将其关闭？** 把 `CloseOnOutsidePress` 设为 `False`。若只想在某些时候允许，保持其为 `True`，并在 `e.Reason` 为 `PressOutside` 且有待处理的工作时取消 `Closing` 事件。

**如何为其他语言重命名关闭按钮？** 设置 `CloseButtonName`。默认为“关闭”，该名称会被辅助技术读取。

**能否在 XAML 中读取停靠点？** `DetentIndex` 是只读的。在代码或视图模型中订阅 `DetentChanged`，并绑定你存储的值。

## API 参考

<!-- api: BottomSheet -->

<!-- api: BottomSheetDetents -->

## 无障碍

- 面板从 `Title` 获得名称，从 `Description` 获得帮助文本。焦点首先落在面板本身。
- 拖动手柄是一个按钮，会公开其展开状态。其名称为“展开面板”或“收起面板”。
- 在拖动手柄上，ArrowUp 与 ArrowDown 每次移动一个停靠点，Home 与 End 分别跳到最高与最低的停靠点，Enter 或空格键则在最低与最高之间切换。
- 停靠点的变化以礼貌方式播报：“面板已展开”“面板已收起”，或以百分比形式播报新的高度。Escape 关闭面板。关闭按钮默认名为“关闭”；设置 `CloseButtonName` 可更改它。
- 面板向辅助技术报告自己是一个窗口，因此会被作为独立的界面播报。

### 键盘

| 按键 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 在面板中的控件之间移动；面板为最顶层时焦点保持在面板内。Tab 进入首屏以下的内容会展开面板 |
| `ArrowUp` / `ArrowDown`（拖动手柄） | 向上或向下移动一个停靠点 |
| `Home` / `End`（拖动手柄） | 移到最高的停靠点或最小的停靠点 |
| `Enter` 或 `Space`（拖动手柄） | 在最小与最高的停靠点之间切换 |
| `ArrowDown`、`PageDown` 或 `Space`（面板或其内容获得焦点时） | 先展开到最高的停靠点；展开后向下滚动内容 |
| `ArrowUp`、`PageUp` 或 `Shift+Space`（面板或其内容获得焦点时） | 向上滚动内容 |
| `Escape` | 关闭面板，除非 `CloseOnEscape` 为 false 或 `Closing` 取消了关闭 |

滚动步长为方向键 48 px，翻页键为可见高度的 85%。

## 动效

- 一个动效值驱动面板。拖动时面板跟随指针；超过最高的停靠点后，面板会以最多 120 px 的橡皮筋效果拉伸。
- 松开后，面板停靠到最近的停靠点，或在向下拖动时关闭。速度超过 320 px/s 的快速滑动，总会使面板至少移动一个停靠点。
- 面板后方的遮罩取决于面板位置。在最小的停靠点时保留完整遮罩的 78%，随面板下移而淡出。
- 减弱动效时，停靠点直接跳转，面板淡入淡出。关闭动效时，面板立即显示或移除。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 打开 | 以平滑弹簧从边缘下方升起，到达其停靠点 | 在停靠点处以短暂淡入显示 | 立即完成 |
| 关闭 | 以 0.3 s 的视觉时长弹回，并保留松手时的速度 | 短暂淡出 | 立即完成 |
| 在停靠点之间移动 | 以平滑弹簧移动到新的停靠点 | 直接跳到新的停靠点 | 立即完成 |
| 拖动 | 与指针一对一跟随；超过最高的停靠点后出现橡皮筋效果 | 禁用拖动 | 禁用拖动 |
| 遮罩 | 跟随面板位置 | 跟随面板位置 | 立即完成 |

## 相关组件

- `Dialog`：必须先回应的决定。
- `Drawer`：宽屏布局中的侧边面板。
- `Popover`：锚定在某个控件上的小块内容。

| 组件 | 适用场景 |
|---|---|
| `Dialog` | 用户必须先确认、取消或输入简短的值，任务才能继续 |
| `Drawer` | 筛选、设置或记录详情需要与宽屏布局中的页面并排显示 |
| `Popover` | 内容很小，锚定在一个控件上，且不需要遮罩 |
| `Tooltip` | 一行文字解释一个图标按钮 |
| `Alert` | 消息留在页面中，直到问题被解决 |
| `Toast` | 简短的结果，例如“配送时段已保存”，应自行消失 |
