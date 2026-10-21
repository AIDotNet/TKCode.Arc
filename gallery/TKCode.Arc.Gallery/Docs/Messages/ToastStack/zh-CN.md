---
title: 轻提示堆叠
summary: 将简短结果堆叠在边缘，直到你需要查看时再展开。
source-hash: 34f8d540e9d9
---

<Demo name="Stack" />

## 何时使用

`ToastStack` 承载用户发起、且无需等待的工作的简短结果：导出已完成、邀请已发送、文件上传失败。它位于窗口边缘、覆盖在页面之上，始终把最新的结果放在最前，较早的结果则收在它后面。窗口中的任何控件都可以向它添加内容，因此后台任务、保存处理程序或命令面板都能报告自己做了什么，而无需了解页面布局。

- **管理后台中的后台任务结果**：账单导出完成后显示“导出已就绪”，或显示带有行数的“导入失败”，操作人员可以继续在表格中工作。
- **带撤销时限的确认**：项目列表中显示带撤销按钮的“项目已归档”，该操作可在几秒内撤回；撤回后，轻提示会变为“项目已恢复”。
- **由加载转为结果的提示**：加载状态中显示“正在上传 report.pdf”，完成后原地更新为“报告已上传”，或更新为带原因的错误，而不是两条独立的消息。
- **来自多个来源的类型化通知**：存储或配额警告、结账流程的成功消息以及同步错误，全部集中在一处，具有统一的阅读顺序和同一种关闭方式。

## 何时不用

- **页面用自身状态控制的一条确认**：用 `Toast`。它没有队列，通过 `IsOpen` 设置，只有一条消息时更简单。
- **需要保留在页面上、直到问题解决的消息**，例如超出限额的套餐或失败的付款：用 `Alert`。堆叠会把较早的消息藏在最前面的消息之后，计时器也会关闭其余消息。
- **轮换消息的站点级横幅**，例如维护通知或促销：用 `AnnouncementBar`。
- **用户必须先回应才能继续的消息**，例如删除确认或权限请求：用 `Dialog`，它会获得焦点并阻止页面操作，直到做出选择。

## 用法

将 `ToastStack` 放在内容之上，例如作为窗口根面板的最后一个子元素。在窗口中的任何控件里调用 `Toast.Show` 即可添加轻提示。如果窗口没有 `ToastStack`，`ToastHost` 会在浮层上创建一个。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <Grid>
        <!-- 页面内容放在这里。 -->
        <arc:ToastStack Label="通知" />
    </Grid>
</UserControl>
```

`ToastStack` 派生自 Avalonia 的 `TemplatedControl`，并实现了 `IToastService`，即应用级 API 的接口：`Show`、`Update`、`Dismiss` 和 `Count`。持有堆叠的代码可以直接调用这些成员。只知道窗口的代码则使用 `Toast.Show`、`Toast.Update` 和 `Toast.Dismiss`，或使用 `ToastHost.GetService`，它为窗口的堆叠返回同一个接口。一个窗口只有一个活动堆叠：放置好的 `ToastStack` 优先于由 `ToastHost` 创建的堆叠。

### 结构

1. **区域**（堆叠本身）：一个由 `Label` 命名的区域地标，默认名称为“通知”。启用 `IsHotkeyEnabled` 时，名称末尾会附加“(Alt+T)”，使屏幕阅读器用户了解该快捷键。
2. **列表**（`PART_List`）：容纳卡片的区域。只有列表矩形接收指针输入，因此堆叠其余部分之下的页面仍可点击。列表宽 360 px（22.5rem），或窗口宽度减去 32 px，取两者中较小的值。
3. **前卡片**：最新的轻提示，带有浮动阴影（堆叠展开时也始终使用同样的阴影）。
4. **收起的卡片**：位于前卡片后面的较早轻提示。每往后一层，会在前面的卡片之外露出 14 px，缩小 5%，使用弱化的表面色，并隐藏其内容。`VisibleToasts`（默认 3）设置显示的数量，其余的在后面等待。
5. **图标槽**（`PART_Icon`）：18 × 20 px 的槽位，内含 18 px、描边为 1.75 的 Lucide 图标，之后是 12 px 的间距。颜色随类型变化：成功、强调、警告或危险。加载中的轻提示改为显示 15 px 的旋转指示器。
6. **文案**（`PART_Title` 与 `PART_Description`）：标题为 14 px 中等字重，描述使用次要文字颜色，两者都按 20 px 的行高换行。描述位于标题下方 2 px。
7. **操作按钮**（`PART_Action`）：28 px 高的胶囊按钮，左右内边距 12 px，小尺寸的次要变体。它位于文案右侧；在宽度小于 336 px 的卡片中则位于文案下方。
8. **关闭按钮**（`PART_Close`）：28 px 的圆形幽灵按钮，带 16 px 的 X 图形。其无障碍名称为 "Dismiss notification"。

卡片内容左侧内边距 16 px，其他方向 12 px。堆叠距窗口边缘 24 px（启用 `IsContained` 时为 16 px），在宽度小于 480 px 的窗口中居中于底边。

### 显示轻提示

`Show` 接收一个 `ToastOptions` 并返回轻提示的 id。`Kind` 选择图标、颜色和朗读前缀，`Title` 为必填项。`Toast.Show` 以参数形式接收相同的值，并将其发送到拥有 `owner` 视觉对象的窗口的堆叠。

```csharp
stack.Show(new ToastOptions
{
    Kind = ToastKind.Warning,
    Title = "存储空间即将占满",
    Description = "已使用 10 GB 中的 92%。"
});

Toast.Show(button, "邀请已发送", ToastKind.Success, "Maya 很快会收到一封电子邮件。");
```

每条轻提示都会以其类型与文案播报，堆叠始终把最新的轻提示放在最前面。`Count` 报告排队的轻提示数量，包括显示中的和等待在后面的；每当轻提示被显示、更新或关闭时，都会引发 `ToastsChanged`。可以使用该事件更新标题栏中的计数或徽标。

### 类型与生命周期

`ToastKind` 设置图标、颜色以及默认生命周期。堆叠的默认生命周期为 5 秒。

| 类型 | 图标 | 默认生命周期 | 朗读前缀 |
|---|---|---|---|
| `Success` | 成功色的圆形对勾 | `Duration`（5 秒） | “成功：” |
| `Info` | 强调色的信息图标 | `Duration`（5 秒） | “信息：” |
| `Warning` | 警告色的三角警示 | `Duration` 的 1.6 倍（8 秒） | “警告：” |
| `Error` | 危险色的圆形叉号 | `Duration` 的 1.6 倍（8 秒） | “错误：”（以强提示方式播报） |
| `Loading` | 旋转的圆环 | 无：等待更新 | “进行中：” |

警告与错误的停留时间更长，因为用户可能需要阅读并采取行动。加载中的轻提示没有计时器，会一直保留，直到工作更新它或堆叠将其关闭。设置 `ToastOptions.Duration` 或 `ToastUpdate.Duration` 可为单条轻提示覆盖默认值。`Timeout.InfiniteTimeSpan` 会使轻提示保留到被关闭，而零或更小的值同样使其保持打开。

### 加载与更新

在 `ToastOptions` 中传入 id，再用相同的 id 调用 `Update`，即可原地修改轻提示。以已在堆叠中的 id 调用 `Show`，会更新该轻提示而不是新增一条。将 `Duration` 设为 `Timeout.InfiniteTimeSpan`，可使轻提示保留到被更新或关闭。轻提示的图标、文字和高度会变形为新状态，计时器则以新的生命周期重新开始。

```csharp
var id = stack.Show(new ToastOptions
{
    Kind = ToastKind.Loading,
    Title = "正在上传 report.pdf",
    Duration = Timeout.InfiniteTimeSpan
});

await UploadAsync();

stack.Update(id, new ToastUpdate
{
    Kind = ToastKind.Success,
    Title = "报告已上传",
    Description = "已与 4 人共享。"
});
```

`ToastUpdate` 只改变它所设置的成员。`Kind`、`Title`、`Description` 或 `Action` 为 null 时保持当前值。`Duration` 为 null 时，轻提示以其类型的默认生命周期重新开始。使用 `RemoveDescription` 或 `RemoveAction` 可以移除描述或操作。
对已不在堆叠中的 id 进行更新不会产生任何效果，因此发送一个迟到的、针对已关闭轻提示的结果是安全的。

### 操作

`ToastAction` 会为轻提示添加一个按钮。按钮接收文字标签以及一个接收轻提示 id 的回调。回调执行后，除非回调更新了轻提示，否则轻提示会关闭，因此“查看”这类简单操作无需额外代码。撤销操作可以把轻提示更新为结果，并通过 `RemoveAction` 移除按钮。

```csharp
stack.Show(new ToastOptions
{
    Title = "项目已归档",
    Action = new ToastAction("撤销", id => stack.Update(id, new ToastUpdate
    {
        Kind = ToastKind.Success,
        Title = "项目已恢复",
        RemoveAction = true
    }))
});
```

回调决定轻提示接下来做什么。如果它既没有更新也没有关闭轻提示，堆叠会在回调返回后将其关闭。回调在 UI 线程上运行，因此应从回调中启动耗时的工作，而不是在回调里直接执行。更新轻提示并移除按钮后，轻提示只剩关闭按钮；键盘焦点会移到该按钮，使用户可以继续用键盘操作。

### 位置与数量限制

`Position` 将堆叠移到左下、居中或右侧边缘，默认为 `BottomRight`。无论位置如何设置，宽度小于 480 px 的窗口始终让堆叠居中。`VisibleToasts` 设置同时显示的轻提示数量，`Limit` 会丢弃超出数量的最早轻提示。`Limit` 的值不会低于 1。

<Demo name="Position" />

```xml
<arc:ToastStack Position="BottomCenter" VisibleToasts="3" Limit="12" />
```

`IsContained` 会把堆叠固定在其自身边界内 16 px 处，而不是距窗口边缘 24 px。在预览、面板或任何位于窗口较小区域内而非覆盖整个窗口的堆叠中启用它。

### 由代码驱动

`ToastStack` 是一种服务，而不是带有可绑定值的控件，因此视图模型接收的是接口，而不是控件。在视图中获取窗口的服务一次，然后将其传递下去：

```csharp
public partial class ExportViewModel : ObservableObject
{
    private readonly IToastService _toasts;

    public ExportViewModel(IToastService toasts) => _toasts = toasts;

    [RelayCommand]
    private async Task ExportAsync()
    {
        var id = _toasts.Show(new ToastOptions
        {
            Kind = ToastKind.Loading,
            Title = "正在导出发票",
            Duration = Timeout.InfiniteTimeSpan
        });

        var result = await _exports.CreateCsvAsync(Range);
        _toasts.Update(id, result.IsSuccess
            ? new ToastUpdate { Kind = ToastKind.Success, Title = "导出已就绪", Description = result.FileName }
            : new ToastUpdate { Kind = ToastKind.Error, Title = "导出失败", Description = result.Error });
    }
}
```

```csharp
// 在视图的代码后置中，视图附加到窗口后：
var toasts = ToastHost.GetService(this);
DataContext = new ExportViewModel(toasts);
```

视图模型与导出服务属于应用程序自身。视图中无需放置 `ToastStack`：`ToastHost` 会在第一次调用时在窗口的浮层上创建一个。只有当位置、数量限制或 `IsContained` 需要与默认值不同时，才自行放置 `ToastStack`。

### 示例：账单后台中的发票导出

一个账单后台允许财务操作人员将一个月的发票导出为 CSV。导出需要几秒钟，而操作人员应继续工作，因此请求开始时显示一条没有计时器的加载轻提示。文件就绪后，轻提示变为带“下载”按钮的“导出已就绪”；失败时，轻提示变为带原因和“重试”按钮的错误。错误的停留时间比默认值更长，而且只有堆叠的列表矩形接收指针输入，因此周围的发票表格仍可正常使用。

堆叠放置在页面内容之上，数量限制为八条，使长时间的导出会话不会让堆叠无限增长：

```xml
<Grid>
    <!-- 发票表格和筛选器放在这里。 -->
    <arc:ToastStack Label="账单通知"
                    Position="BottomRight"
                    VisibleToasts="3"
                    Limit="8" />
</Grid>
```

```csharp
var id = _toasts.Show(new ToastOptions
{
    Kind = ToastKind.Loading,
    Title = "正在导出 3 月的发票",
    Duration = Timeout.InfiniteTimeSpan
});

var result = await _exports.CreateCsvAsync(March2026);

_toasts.Update(id, result.IsSuccess
    ? new ToastUpdate
    {
        Kind = ToastKind.Success,
        Title = "导出已就绪",
        Description = "412 张发票，1.8 MB。",
        Action = new ToastAction("下载", () => _files.Open(result.FileName))
    }
    : new ToastUpdate
    {
        Kind = ToastKind.Error,
        Title = "导出失败",
        Description = result.Error,
        Action = new ToastAction("重试", () => _ = ExportAgainAsync())
    });
```

### 文案规范

- **标题**：用过去时简述结果，或用一个状态描述加载中：“导出已就绪”“邀请已发送”“正在上传 report.pdf”。类型本身已经说明是否成功，因此不要在标题中重复。
- **描述**：一句话，包含数字、名称或下一步：“412 张发票，1.8 MB。”“请检查地址后重试。”数字使用阿拉伯数字。
- **操作标签**：一个动词，一到两个字：“撤销”“重试”“下载”“查看”。不要用“确定”或“关闭”，因为关闭按钮已经存在。
- **错误**：说明失败的内容以及用户能做什么：原因与下一步的办法。不要指责用户。
- **句子式大小写，不用感叹号**：通知报告的是事实。

| 不要写 | 改为 |
|---|---|
| 成功！！！文件已上传 | 报告已上传 |
| 错误 | 导出失败 |
| 出了点问题，请稍后再试。 | 无法发送邀请，请检查地址后重试。 |
| 点击“确定”继续 | 项目已恢复 |

### 最佳实践

**推荐**

- 每项工作使用一条轻提示并更新它，使加载状态变为其结果，而不是留下两条消息。
- 为每条加载中的轻提示提供结束路径：成功时更新、失败时更新，或由代码有意设定的超时。没有任何更新的加载轻提示会一直停留在屏幕上。
- 将 `Limit` 设为操作人员能够扫视的数量，例如八条，使旧结果不会堆积在新结果后面。
- 对用户必须处理的事项使用 `Warning` 和 `Error`，使其较长的生命周期与用户需要完成的工作相匹配。

**避免**

- 用堆叠显示必须保留到解决为止的消息，例如套餐限额。堆叠中的轻提示会自行关闭；这类消息应使用 `Alert`。
- 长描述。轻提示是一眼读完的，而卡片最宽只有 360 px。
- 悄悄执行主要任务的操作，例如没有撤销的“全部删除”。破坏性工作应在执行前使用 `Dialog` 确认，执行后提供撤销。
- 对每次按键或每个轮询周期都显示轻提示。每次 `Show` 和 `Update` 都会向辅助技术播报，频繁的消息会变成噪音。应将它们合并为一条结果。

### 常见问题

**为什么轻提示在我读完之前就消失了？** 计时器在堆叠收起时运行，只在堆叠展开、被拖动或窗口最小化时才会停止。可以为该轻提示设置更长的 `Duration`，或在阅读时将鼠标悬停在堆叠上以暂停计时器。

**如何让轻提示一直保留到用户将其关闭？** 在 `ToastOptions` 或 `ToastUpdate` 中把 `Duration` 设为 `Timeout.InfiniteTimeSpan`。加载中的轻提示在被更新之前已经是这种方式。

**窗口没有 `ToastStack` 时会发生什么？** 第一次调用 `Toast.Show` 或 `ToastHost.GetService` 时，会在窗口的浮层上创建一个。如果窗口没有浮层，`ToastHost` 会引发 `InvalidOperationException`，因此在这种情况下请在窗口中放置一个 `ToastStack`。从未附加到窗口的控件调用也会引发异常。

**如何阻止堆叠占用 Alt+T 快捷键？** 将 `IsHotkeyEnabled` 设为 `False`。在 Windows 上，Alt+T 可能与菜单的访问键冲突，因此拥有自身菜单的应用程序可能会将其关闭。你仍可以用 `FocusInto()` 自行把焦点移入堆叠，例如从“通知”菜单项中调用。

**能否统计或读取排队中的轻提示？** 读取 `Count` 可获得排队中的轻提示数量（包括显示中的和等待中的），读取 `IsExpanded` 可判断列表是否已展开。订阅 `ToastsChanged` 可使徽标与堆叠保持同步。

## API 参考

<!-- api: ToastStack -->

<!-- api: ToastOptions -->

配套类型，摘自源代码：

`ToastUpdate` 用于更改已显示的轻提示。为 null 的成员保持不变。

| 成员 | 含义 |
|---|---|
| `Kind` | 新的类型；图标会变形 |
| `Title` | 新消息 |
| `Description`、`RemoveDescription` | 新的第二行文字，或将其移除 |
| `Action`、`RemoveAction` | 新的操作，或收起操作按钮 |
| `Duration` | 新的生命周期；为 null 时以该类型的默认值重新开始 |

`ToastKind` 是轻提示的类型：`Success`、`Info`、`Warning`、`Error` 和 `Loading`。`ToastStackPosition` 是堆叠所在的边缘：`BottomRight`（默认）、`BottomCenter` 和 `BottomLeft`。

`ToastHost` 用于查找窗口的堆叠，是应用级 API 的入口：

| 成员 | 含义 |
|---|---|
| `GetService(owner)` | 窗口的 `IToastService`；没有堆叠时在浮层上创建一个 |
| `GetStack(owner)` | 窗口的 `ToastStack`，创建规则相同 |
| `Find(topLevel)` | 窗口已注册的堆叠，或 null；放置的堆叠优先于自动创建的堆叠 |

## 无障碍

- 通知区域是一个由 `Label` 命名的区域地标。启用 `IsHotkeyEnabled` 时，名称末尾为“(Alt+T)”。Alt+T 会将焦点移入堆叠并将其展开。
- Tab 在显示中的轻提示的操作按钮与关闭按钮之间移动。Escape 关闭当前聚焦的轻提示。
- 新增和更新的轻提示以“类型：标题。描述”的形式播报。错误以强提示方式播报，其他类型以礼貌方式播报。
- 每张卡片的名称为“类型：标题”，屏幕阅读器可借此区分不同类型，其描述即为帮助文字。关闭按钮的名称为 "Dismiss notification"。
- 堆叠展开、轻提示被拖动以及窗口最小化时，计时器会暂停。堆叠被其他窗口遮挡时，计时器仍会继续运行。
- `VisibleToasts` 之外的轻提示以及正在离开的轻提示处于非活动状态：无法通过指针或 Tab 到达。
- 聚焦的轻提示关闭时，键盘焦点会移交出去：移到下一条轻提示的第一个按钮；如果没有剩余的轻提示，则回到进入堆叠之前拥有焦点的元素。

### 键盘

| 按键 | 操作 |
|---|---|
| `Alt+T` | 将焦点移到最前面的轻提示（有操作时为其操作按钮，否则为关闭按钮）并展开堆叠。将 `IsHotkeyEnabled` 设为 `False` 可关闭该快捷键 |
| `Tab` / `Shift+Tab` | 在显示中的轻提示的操作按钮与关闭按钮之间移动 |
| `Enter` 或 `Space` | 按下聚焦的按钮：执行操作，或关闭轻提示 |
| `Escape` | 关闭聚焦的轻提示，不执行任何操作 |

键盘焦点会展开堆叠，离开堆叠则将其收起。鼠标点击按钮不会使堆叠保持展开。

## 动效

- 新轻提示从其自身高度的下方升起并淡入。较早的轻提示向后收起、缩小，并失去内容的色调。悬停或聚焦堆叠时，轻提示展开为列表；离开后再收合回去。
- 向右滑动轻提示时，它会跟随指针移动。较长或较快的滑动会关闭它，卡片在移开时淡出。
- 更新时，图标、文案与卡片高度会原地变形，操作按钮的宽度会展开或收起。工作进行时，加载圆环持续转动。
- 减弱动效时，位置直接跳转，轻提示以快速淡入淡出的方式出现和消失。关闭动效时，每次变化立即显示。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 新轻提示到达 | 从下方升起，并带有标准的进入淡入效果 | 原位出现，快速淡入 | 立即完成 |
| 较早的轻提示向后收起 | 每一层在变形弹簧上后退 14 px 并缩小 5%；内容淡出 | 位置直接跳转；快速淡入 | 立即完成 |
| 堆叠展开或收起 | 在变形弹簧上展开为列表；其他变化使用平滑弹簧 | 位置直接跳转 | 立即完成 |
| 轻提示被关闭 | 下沉 10 px，缩小到 96%，同时淡出 | 快速淡出 | 立即完成 |
| 轻提示被滑走 | 跟随指针；滑动 280 px 时淡出至 25%；快速放开时在 0.22 秒内淡出 | 滑动仍会关闭轻提示；淡出很快，且不会甩出 | 立即完成 |
| 轻提示被更新 | 图标、文案与卡片高度变形；操作槽展开或收起；加载圆环转动 | 加载圆环停止；新文案与高度直接生效，不使用弹簧 | 立即完成 |

## 相关组件

- `Toast`：由页面通过 `IsOpen` 控制的一条本地确认。
- `Alert`：页面内持续存在的消息。
- `AnnouncementBar`：带倒计时的轮播横幅。
- `Dialog`：页面继续前必须做出的决定。

| 组件 | 适用场景 |
|---|---|
| `Toast` | 页面掌控一条确认，并设置其文字与打开状态 |
| `Alert` | 消息保留在页面上，直到用户或数据将其解决 |
| `AnnouncementBar` | 站点级通知轮流展示多条消息 |
| `Dialog` | 用户必须先做出回应，页面才能继续 |
| `ToastStack` + `Toast.Show` | 后台工作的结果可能来自任何控件，并且可能相互重叠 |
