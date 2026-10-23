---
title: 空状态集
summary: 同一幅插图中的四种空状态，切换标签页时，图形会在各场景之间变形。
source-hash: 68ea32edeae6
---

<Demo name="Preview" />

## 何时使用

空状态告诉用户视图为何没有内容，以及接下来可以做什么。`EmptyStates` 用一个区块涵盖应用中最常遇到的四种情况（没有匹配的搜索、连接中断、已清空的队列与不存在的页面），每种都配有图形和文案。把它放在空视图占满整个页面或面板、且下一步可能需要一点时间的位置，因为区块会在同一处展示进行中的工作和最终结果。

- **没有匹配项的筛选发票列表**：账单控制台中，状态与月份筛选把所有发票都过滤掉了。`NoResults` 场景在标题中说明原因，并提供一个操作，例如显示已付款的发票。
- **支持离线的编辑器中连接断开**：现场应用会把编辑内容保存在设备上。`Offline` 场景告诉用户编辑内容是安全的，并提供重试；重试时会先展示连接尝试的过程，再给出结果。
- **已清空的工单或审批队列**：支持收件箱或审核队列中已没有待读内容。`CaughtUp` 场景确认这一状态，其操作会再次检查是否有新项目。
- **指向已迁移页面的链接**：帮助中心或 CRM 中保存了已归档项目的链接。`NotFound` 场景说明页面已迁移，并提供搜索新位置的入口。
- **设计评审或新手引导**：一个并排展示四种状态的页面，方便团队在同一处检查文案与图形。

## 何时不用

- **只有一个带图标和按钮的空列表**：使用 `EmptyState`。它只有单一状态，没有标签页与阶段，在信息密集的页面上更轻量。
- **内容仍在加载**：使用 `Skeleton`。它展示即将到来的内容的形状；空状态会告诉用户什么都没有，而实际上有内容正在路上。
- **页面已显示内容时出现错误**：使用 `Alert`。它位于内容旁边，并让内容保持可读。
- **保存或发送之后的确认**：使用 `Toast`。它说明发生了什么，并会自行消失。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:EmptyStates Label="工作区" />
</UserControl>
```

`EmptyStates` 派生自 `TemplatedControl`。它的标签页是 `EmptyStatesItem` 对象，图形是会在场景之间做动画的控件，操作则是一个按钮。区块本身不做任何工作。应用处理 `ActionInvoked`（或执行 `ActionCommand`），完成工作，并把 `Phase` 切换为 `Loading`，之后再切换为 `Done`，或回到 `Idle`。图形与文案随 `Phase` 和所选标签页变化。

### 结构

1. **标签行**（`Items`）：每个 `EmptyStatesItem` 对应一个标签，显示其 `Header`。标签位于 30 px 高的轨道中，内缩 3 px，圆角 12 px。在 460 px 以内，标签平分整行，且标签的宽度不会小于其文字。只有一项时隐藏标签行。选中的标签使用表面色、1 px 边框和静止时的阴影。
2. **图形**（`Scene`）：320 × 216 的逻辑画布，按比例缩放至最宽 360 px。十个形状与 1.5 px 的描边不随图形缩放。颜色来自主题令牌：主体形状使用表面色与前景色，轮廓使用弱化文字颜色，辅助形状使用较强的边框色。
3. **标题**（`Title`、`DoneTitle`）：大号文字，中等字重。区块宽度达到 560 px 或以上时，使用特大字号。
4. **描述**（`Description`、`DoneDescription`、`LoadingDescription`）：小号的次要文字，宽度约 350 px，长句会换成更多行。
5. **操作按钮**（`ActionLabel`、`DoneActionLabel`）：小号主按钮。`Phase` 为 `Loading` 时，它显示加载指示器并忽略按下。
6. **卡片**：1 px 边框、20 px 圆角与表面色。内边距随区块宽度增长：左右为 16 到 40 px，上下为 16 到 48 px。各部分之间的间距为 20 到 32 px。

### 场景与文案

每个 `EmptyStatesItem` 是一个标签页。`Scene` 决定图形，文案属性则为每个阶段设置文字。未设置的文字使用 Arc 为该场景提供的文案，因此标签页只需设置其 `Header` 与操作文字即可。

| 场景 | 图形 | 默认标签文字 | 默认标题 |
|---|---|---|---|
| `NoResults` | 放大镜覆盖在空行上方 | No results | No results for “Q3 roadmap” |
| `Offline` | 一朵云通过断开的线连接到一台设备 | Offline | You are offline |
| `CaughtUp` | 一叠收件箱，上面有一个对勾 | Caught up | All caught up |
| `NotFound` | 一张折叠的地图，路线终止于一个叉号 | Not found | Page not found |

有多个标签时，请为每个标签都设置 `Header`。标题是标签行中唯一的文字，用户据此判断自己正在看哪种状态。应按原因选择场景，而不是按喜欢的外观：筛选之后用 `NoResults`，连接断开时用 `Offline`，链接失效时用 `NotFound`，没有任何待办时用 `CaughtUp`。

<Demo name="CustomItems" />

```xml
<arc:EmptyStates>
    <arc:EmptyStatesItem Scene="Offline"
                         Title="同步已暂停"
                         ActionLabel="恢复同步" />
    <arc:EmptyStatesItem Scene="NotFound" />
</arc:EmptyStates>
```

`Items` 是一个 `AvaloniaList<EmptyStatesItem>`，而不是 `ItemsSource`。请在 XAML 中添加标签页，或在代码中使用 `Items.Add` 与 `Items.Remove`。区块会将选中项保持在列表中的某个项上：当被选中的项被移除时，选中会移到一个有效的索引；在项到达之前做出的选择会被保留。

### 操作与阶段

按下操作按钮会引发 `ActionInvoked`，参数为所选项，以及按下时所处的阶段；随后以相同的事件参数执行 `ActionCommand`。阶段为 `Loading` 时会忽略按下，因此应用永远不会为同一项工作收到第二次按下。

| 阶段 | 显示的标题 | 显示的描述 | 按钮 |
|---|---|---|---|
| `Idle` | `Title`（或场景的文案） | `Description` | `ActionLabel` |
| `Loading` | 开始时所处阶段的标题 | 若设置了 `LoadingDescription` 则显示它，否则显示当前描述 | 加载指示器，忽略按下 |
| `Done` | `DoneTitle` | `DoneDescription` | `DoneActionLabel` |

工作进行期间，将 `Phase` 设为 `Loading`。从 `Done` 开始的 `Loading` 会保留完成状态的标题，因此在操作进行中，文案不会突然跳回空状态。工作成功时设为 `Done`，失败时设为 `Idle`。当结果本身有下一步操作时（例如“隐藏已付款的发票”），使用 `Done` 展示该结果；需要再次提供操作时使用 `Idle`。

<Demo name="AsyncRetry" />

```csharp
states.ActionInvoked += async (_, e) =>
{
    if (e.Phase == EmptyStatesPhase.Done)
    {
        states.Phase = EmptyStatesPhase.Idle;
        return;
    }
    states.Phase = EmptyStatesPhase.Loading;
    var ok = await sync.RetryAsync();
    states.Phase = ok ? EmptyStatesPhase.Done : EmptyStatesPhase.Idle;
};
```

要在一次失败的尝试之后更改文案，请在阶段仍为 `Loading` 时设置该项的 `Description`。新文字会随着回到 `Idle` 一同出现。示范即是如此：第一次尝试失败，描述随之变为一行提示用户应检查什么的文字。

每个场景都有自己的默认 `LoadingDescription`，例如 `Offline` 的 “Reaching sync.northwind.example…”。`NoResults` 场景没有默认值，因此搜索进行期间描述会保留在屏幕上。若要在任何场景中保留描述，请将 `LoadingDescription` 设为空字符串。

### 绑定到视图模型

`EmptyStates` 没有项的绑定，因此它的标签页在 XAML 或代码中固定。它的状态可以绑定：`SelectedIndex`、`SelectedItem` 与 `Phase` 为双向，`ActionCommand` 为单向。由视图模型持有阶段与工作，区块负责展示。

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TKCode.Arc;

public partial class InvoiceEmptyStateViewModel : ObservableObject
{
    private readonly IInvoiceService _invoices;

    public InvoiceEmptyStateViewModel(IInvoiceService invoices)
    {
        _invoices = invoices;
    }

    [ObservableProperty]
    private EmptyStatesPhase _phase = EmptyStatesPhase.Idle;

    [ObservableProperty]
    private int _selectedIndex;

    [RelayCommand]
    private async Task RunActionAsync(EmptyStatesActionEventArgs? args)
    {
        if (args is null)
        {
            return;
        }
        if (args.Phase == EmptyStatesPhase.Done)
        {
            Phase = EmptyStatesPhase.Idle;
            return;
        }
        Phase = EmptyStatesPhase.Loading;
        var result = await _invoices.RefreshAsync();
        Phase = result.IsSuccess ? EmptyStatesPhase.Done : EmptyStatesPhase.Idle;
    }
}
```

```xml
<arc:EmptyStates SelectedIndex="{Binding SelectedIndex, Mode=TwoWay}"
                 Phase="{Binding Phase, Mode=TwoWay}"
                 ActionCommand="{Binding RunActionCommand}"
                 Label="发票" />
```

- `Phase` 为双向绑定，因此区块会把它写回。切换到另一个标签页会将 `Phase` 设为 `Idle`，视图模型通过自己的 `Phase` 属性就能看到这一变化，并可以丢弃它原本持有的结果。
- 编译绑定需要在视图上设置 `x:DataType`。为简洁起见，这里的示例省略了它。

### 窄屏布局

区块跟随自身的宽度，而不是窗口的宽度。宽度在 420 px 及以下时，标签页使用更小的文字和更紧凑的内边距。从 560 px 起，标题使用较大的字号。放不下的标签保持其宽度，标签行会滑动，以保持选中的标签页可见。

<Demo name="Narrow" />

### 克制地使用动效

场景之间的变形是该区块的标志性效果，最适合在状态变化本身就是新闻的时候使用：筛选已被清除、连接已恢复、结果已到达。让图形承载这一变化，并保持文案简短。

- 为用户自己引起的状态变化（标签页、筛选或重试）使用场景切换。如果后台每隔几秒刷新一次，图形就会变形，这会把视线从它所属的列表上移开。
- 保持 `Loading` 阶段简短。每次阶段变化时图形都会移动，持续数分钟的加载指示器只给用户一幅动态画面，却没有任何信息；长时间的工作应在周围的页面中显示进度指示器。
- 在减弱动效下，图形会交叉淡化而不是变形，因此变化依然可见，却没有位移。鼠标倾斜在减弱动效下关闭；动效关闭时则完全禁用。

### 示例：三种空状态的发票页面

账单控制台的发票页面有三个标签页，对应团队最常遇到的三种状态。第一个标签页处于选中状态：月份筛选隐藏了所有发票，因此标题中写明月份，操作则清除已付款发票的筛选。第二个标签页显示同步状态，第三个显示所有提醒都已排定。每个标签页都设置了自己的标签文字，因此标签行读起来就是三个简短的标签。文案使用真实的对象（月份、客户），因为用户会先在标题中寻找原因。

![发票页上的三个空状态，选中的是无结果标签页](images/InvoicesEmptyStates.png)

```xml
<arc:EmptyStates Width="640"
                 Label="Invoices"
                 TabListLabel="Invoice empty states">
    <arc:EmptyStatesItem Scene="NoResults"
                         Header="Search"
                         Title="No invoices match “March 2026”"
                         Description="The status filter hides 14 paid invoices."
                         ActionLabel="Show paid invoices"
                         DoneTitle="26 invoices from March 2026"
                         DoneDescription="Paid and unpaid invoices are shown."
                         DoneActionLabel="Hide paid invoices" />
    <arc:EmptyStatesItem Scene="Offline"
                         Header="Sync"
                         Title="Invoices may be out of date"
                         Description="Edits are saved here and sync when you reconnect."
                         ActionLabel="Try again" />
    <arc:EmptyStatesItem Scene="CaughtUp"
                         Header="Reminders"
                         Title="No reminders due"
                         Description="Every overdue invoice has a reminder this week."
                         ActionLabel="Check again" />
</arc:EmptyStates>
```

### 文案规范

- **标题**：说明事实，并点明对象或筛选条件：“没有匹配“2026 年 3 月”的发票”，而不是“没有数据”。句子式大小写，不用感叹号。
- **描述**：一到两句话，说明原因与后果，例如“这里的编辑会被保存，重新连接后同步。”让它保持在区块约 350 px 的描述宽度之内。
- **操作标签**：动词加对象，与应用其他地方的按钮一致，例如“显示已付款的发票”“重试”。只有同一操作下次可能成功时，才使用“重试”。
- **完成文案**：说明发生了什么变化，给出新的数量或新的状态，例如“2026 年 3 月的 26 张发票”。只写“成功”的结果，没有告诉用户该检查什么。
- **加载文案**：说明当前正在发生什么，使用现在时并加省略号，例如“正在连接 api.example.com…”。

| 不要写 | 改为 |
|---|---|
| 没有数据 | 没有匹配“2026 年 3 月”的发票 |
| 错误 | 你已离线 |
| 这里没有内容 | 已全部处理完 |
| 点击这里 | 显示已付款的发票 |
| 成功 | 2026 年 3 月的 26 张发票 |

### 最佳实践

**推荐**

- 让场景与原因相符。用户先看图形再看标题；如果筛选隐藏了结果，却使用 `NotFound` 图形，就会把用户引向错误的解决办法。
- 为每个标签页都设置 `Header`。标题是标签行中唯一的文字，名为“标签 2”的标签对用户毫无意义。
- 让每个 `Loading` 都能回到 `Done` 或 `Idle`。`Loading` 期间会忽略按下，因此一个永远不结束的阶段会让按钮对用户来说卡住。
- 把 `Done` 的文案写成结果，让用户无需打开列表就能读出发生了什么变化。
- 对于有自己下一步的结果（“隐藏已付款的发票”）使用 `Done`；操作可以再次执行时使用 `Idle`。

**避免**

- 单纯的空列表。请使用 `EmptyState`：一个状态、一个按钮，没有阶段，也不需要解释标签行。
- 把破坏性操作作为空状态的按钮。`ActionInvoked` 在按下时就会触发，不做确认，因此这里的“删除所有草稿”按钮会立即执行。破坏性操作应放在对话框之后。
- 把标签页当作数据。区块只展示场景与文案，它不会筛选、获取或计数，因此应用必须根据自己的结果设置 `Phase`。
- 过长的描述。文案限制在约 350 px 之内，一段长文字会换成很高的区块，把下方的页面推走。每段描述保持在一到两句话。
- 为装饰而更改图形。场景应与用户所处的状态相符；在错误状态下使用 `CaughtUp` 图形，会暗示错误已经解决。

### 常见问题

**为什么按钮会忽略我的按下？** `Phase` 为 `Loading` 时，区块会忽略对操作按钮的按下，不会引发 `ActionInvoked`，也不会执行 `ActionCommand`。工作完成后，请将 `Phase` 设为 `Done` 或 `Idle`。

**为什么加载期间描述保持不变？** `LoadingDescription` 只有在有文字时才会替换描述。`Offline`、`CaughtUp` 与 `NotFound` 场景有默认的加载文字；`NoResults` 没有，因此描述会保持不变。若要选择加载文字，请在该项上设置 `LoadingDescription`。

**切换标签页会保留操作状态吗？** 不会。切换到另一个标签页会把 `Phase` 恢复为 `Idle`，新标签页显示它自己的 `Idle` 文案。若要让标签页直接显示某个结果，请同时设置 `SelectedIndex` 与 `Phase`，如窄屏示例中那样：`<arc:EmptyStates SelectedIndex="1" Phase="Done" />`。

**如何在运行时添加或移除标签页？** 修改 `Items`（使用 `Items.Add` 或 `Items.Remove`）。标签页会跟随列表变化；被选中的项被移除时，选中会保留在同一项上，或移到一个有效的索引。

## API 参考

<!-- api: EmptyStates -->

<!-- api: EmptyStatesItem -->

## 无障碍

- 区块是一个由 `Label` 命名的区域。标签行是一个由 `TabListLabel` 命名的标签页列表，每个标签页都会报告自己是否被选中。
- 只有选中的标签页可通过 Tab 键获得焦点。ArrowLeft 与 ArrowRight 在标签页之间移动，并首尾循环。Home 与 End 跳到第一个和最后一个标签页。移动即选中该标签页。
- 每个标签页以其 header 命名，并报告其在所有标签页中的位置，因此屏幕阅读器会读作“同步，第 2 个，共 3 个”。
- 图形是一幅以其所示内容命名的图像，例如“一朵云通过一条断开的线连接到一台设备”。设置 `IllustrationLabel`、`LoadingIllustrationLabel` 与 `DoneIllustrationLabel`，即可用其他语言为它命名。
- 每次阶段变化后，新的标题与描述，或加载文字，都会以礼貌式播报。切换标签页会清除上一次的播报，因此旧的结果不会被再次朗读。

### 键盘

| 按键 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 进入或离开标签行（选中的标签页），并移到其后的操作按钮 |
| `Left` / `Right` | 选中并聚焦上一个或下一个标签页；在两端循环 |
| `Home` / `End` | 选中并聚焦第一个或最后一个标签页 |
| `Enter` / `Space` | 选中已聚焦的标签页，与任何按钮相同 |

## 动效

- 切换标签页时，图形的每个形状都会以弹簧动画过渡到下一个场景。轮廓逐点变形，线条稍后再逐步绘出。颜色在 480 毫秒内交叉过渡。
- 阶段变化时，以较短的延迟轻推同一批形状。
- 旧文案带着模糊向上移开，新的标题与描述随之升起。文案高度以弹簧方式调整以适应内容，因此按钮不会跳动。标签页的高亮会滑动到选中的标签页。
- 使用鼠标时，图形的各层会略微朝指针方向倾斜。
- 动效等级为 Reduced 时，旧图形交叉淡化为新图形，文案淡入淡出。等级为 Off 时，立即切换。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 场景变化（切换到另一个标签页） | 形状以弹簧移动到新的位置、大小与颜色；轮廓变形；线条稍后绘出 | 旧图形交叉淡化为新图形 | 立即完成 |
| 阶段变化 | 形状以较短的延迟移动 | 图形交叉淡化 | 立即完成 |
| 文案变化 | 旧文案向上并模糊淡出；新文案穿过模糊从下方升起；高度以弹簧调整 | 文案淡入淡出 | 立即完成 |
| 指针在图形上移动 | 各层向指针方向略微倾斜 | 无倾斜 | 无倾斜 |

## 相关组件

- `EmptyState`：用于带图标、文字说明与操作的单个空列表。
- `Skeleton`：内容加载期间使用。
- `Alert`：已显示内容的页面中出现错误时使用。

| 组件 | 适用场景 |
|---|---|
| `EmptyState` | 一个列表或面板为空，一条消息与一个操作即可说明 |
| `Skeleton` | 内容正在加载，且其形状已知 |
| `Alert` | 错误或警告涉及仍在页面上的内容 |
| `Toast` | 保存或发送之后出现简短确认，无需操作 |
| `Tabs` | 用户在页面的各个分区之间切换，且每个分区都有内容 |
| `SearchResults` | 搜索返回带摘录与筛选条件的结果，而空结果是其中一种状态 |
