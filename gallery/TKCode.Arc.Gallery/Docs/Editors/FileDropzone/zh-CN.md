---
title: 文件拖放区
summary: 面向拖放一个或多个文件的大面积投放区域。
source-hash: 7ddba2d27df4
---

<Demo name="Upload" />

## 何时使用

`FileDropzone` 是表单或页面收集文件时的附件目标区。它把拖放目标、系统文件选择器、剪贴板粘贴，以及显示每个文件上传状态的列表组合在一起，让用户无需离开页面，就能看到哪些文件已被接受、哪些仍在上传、哪些失败了。它应放在文件所属的字段下方，例如卡片或设置区块之内。

- **账单或报销页面中的发票与收据附件**：财务人员拖入一批 PDF 收据，看到每个文件的大小，并移除重复扫描的那一份。`MaxFiles` 与 `MaxFileSize` 会把限制显示在目标区旁边，因此没有人需要等到上传失败后才发现它们。
- **支持工单中的截图与日志**：支持人员复制截图，并在指针位于目标区上方时按下 Ctrl+V，图片就会作为一行添加，并带有缩略图。粘贴无需额外按钮。
- **CRM 或管理控制台中的批量数据导入**：单个 CSV 文件，`AllowMultiple` 设为 `False`，`Accept=".csv"`。新文件会替换旧文件，因此不存在导入使用的是哪个文件的疑问。
- **金融开户流程中的身份证件**：固定大小以内的图片与 PDF，上传通过 `UploadHandler` 交给应用自己的安全端点完成。被拒绝的上传会在其行中显示失败原因。
- **开发者工具中的发布资产**：应用运行自己的上传队列，并通过 `ReportProgress`、`CompleteUpload` 和 `FailUpload` 为每一行报告进度，因此列表反映的正是应用已有的队列。

## 何时不用

- **只需要系统文件对话框，不需要文件列表**：从自己的 `Button` 调用 `BrowseAsync`，或使用普通的 `Button` 在应用内打开选择器。没有列表的投放区会多出用户并不需要的区域。
- **页面为空，拖放是唯一操作**：使用 `EmptyState`，以更醒目的行动号召配合标题与简短说明，例如新工作区中的第一次导入。
- **从一组已知值中选择一个**：用 `Select` 或 `RadioGroup`。文件是带有上传生命周期的值，不要把投放区当作固定选项的选择器。
- **绘制签名而不是附加文件**：用 `SignaturePad`，它在应用内生成基于笔画的图像，不会从磁盘读取文件。
- **编写包含图片和格式的正文**：用 `RichTextEditor`，它把文字与格式保存在同一个文档中。投放区只是在文档旁边列出文件。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:FileDropzone Label="添加文件"
                      Description="将文件拖放到此处，或从设备中选择"
                      Accept=".pdf,image/*"
                      MaxFiles="6" />
</UserControl>
```

`FileDropzone` 派生自 `TemplatedControl`，而不是 `Button`。整个目标区都是点击区域：其内部按钮会打开系统文件选择器，因此在目标区任何位置点击、按 Enter 或空格键都会打开它。行、错误行和拖放反馈都属于同一个模板，因此控件在每种状态下都只保留一个标签、一段说明和一个列表。拖放、粘贴和选择器都汇入同一条验证路径，这意味着同一套规则（`Accept`、`MaxFiles`、`MaxFileSize`、`AllowMultiple`）适用于文件到达的每一种方式。

### 结构

1. **目标区表面**：绘制的区域，带有表面背景、虚线边缘与圆角。它使用表面圆角（默认尺寸下为 34 px），紧凑模式下收缩为面板圆角（26 px）。悬停与按下目标区时，背景变为弱化的表面色。
2. **纸张图标**：三张叠放的纸与一个箭头。悬停时纸张抬起 3 px；文件拖到目标区上方时，虚线边缘会闭合为带强调色的实线边缘。
3. **标签**（`Label`）：目标区的标题，使用前景色。文件悬停在目标区上方时，它改为显示拖放文案：若设置了 `DropLabel` 则显示它；否则，设置了上传处理程序时显示“松开即可上传”，多个文件显示“松开即可添加文件”，单个文件显示“松开即可添加文件”。
4. **说明**（`Description`）：标签下方的一行，说明该做什么。
5. **注释**（`Note`）：说明下方的一行小字。默认列出可接受的类型（“接受：.pdf,image/*”）或数量限制（“最多 6 个文件”）。紧凑模式下隐藏。
6. **错误行**（`ErrorMessage`，只读）：目标区下方带有危险色提示图标的一行。文件被拒绝时出现，直到下一次成功添加或移除才消失。
7. **文件列表**：目标区下方的行（默认 `ListPlacement="Below"`），或位于其边缘之内（`Inside`），两侧内边距为 12 px。行按添加顺序排列。
8. **行**：缩略图（解码后的 80 px 预览，圆角 10 px）或类型图标、文件名、大小、状态、4 px 高的进度条、30 px 高的重试按钮，以及 32 px 见方的移除按钮。每行高度至少 58 px。行宽小于 440 px 时，重试按钮只显示图标；小于 300 px 时隐藏类型图标。

### 选择接受的类型与限制

`Accept` 与 HTML 文件输入框的方式相同，接受以逗号分隔的扩展名（“.pdf”）和 MIME 模式（“image/*”“application/pdf”）。扩展名匹配文件名的结尾；带 `/*` 的模式匹配 MIME 类型的前缀；其他值必须与 MIME 类型完全相等。系统选择器的过滤条件也由同一列表生成。将 `Accept` 留空即接受所有文件。

请根据业务规则设置限制，而不是使用默认值：

- `MaxFiles`（默认 5）限制列表的数量。超过上限的一批文件会添加可以容纳的部分，并拒绝其余文件。
- `MaxFileSize`（以字节为单位，默认不限制）会使超过该大小的任何文件在其行中失败。该文件会列出失败原因，没有重试按钮，也绝不会上传。请移除它，改为添加更小的文件。
- `AllowMultiple`（默认 `True`）允许多个文件。设为 `False` 时，新文件会替换当前文件，并取消被替换文件的上传。

<Demo name="Images" />

Images 示例只接受图片，每个最大 2 MB（`MaxFileSize="2097152"`），最多三个文件。说明用用户需要的文字陈述限制：格式与大小。

### 行状态

每个 `FileDropzoneItem` 都有一个来自 `FileDropzoneStatus` 枚举的 `Status`，它决定该行显示什么。

| 状态 | 含义 | 行中显示的内容 |
|---|---|---|
| `None` | 普通选中项，没有上传 | 文件、其大小，没有进度条 |
| `Uploading` | 正在上传；`Progress` 从 0 变化到 100 | 进度条与百分比 |
| `Uploaded` | 上传完成；`Progress` 为 100 | 行上的对勾标记 |
| `Failed` | 上传或检查失败；`Error` 保存原因 | 失败原因、红色边缘，以及允许时的重试按钮 |

`FileDropzoneItem` 为每个属性引发 `PropertyChanged`，因此应用可以直接设置 `Status`、`Progress` 和 `Error`。投放区上的方法同样如此，并且还会把变化通知辅助技术，因此在应用驱动行时优先使用这些方法。

### 单个文件

将 `AllowMultiple` 设为 `False` 后，投放区只保存一个文件。新文件会替换当前文件，取消旧文件的上传，并引发 `FilesChanged`。

<Demo name="SingleFile" />

适用于合同或导入等场景，此时第二个文件属于误操作。此时拖放文案使用单数（“松开即可添加文件”）。

### 上传处理程序

设置 `UploadHandler` 可上传每个已添加的文件。处理程序接收该行、一个报告 0 到 100 的 `IProgress<double>`，以及一个 `CancellationToken`：

- 随着字节发出报告进度。进度条不会后退，因此从零开始的重试不会让用户觉得被重置；0 到 100 之外的值会被限制在该范围内。
- 正常返回即完成该行。抛出异常则使其失败：异常消息成为该行的原因；消息为空时显示“上传失败”。
- 观察该令牌。移除仍在上传的行会取消其令牌，被取消的上传既不会完成，也不会使该行失败。

```csharp
zone.UploadHandler = async (item, progress, token) =>
{
    await UploadToServerAsync(item.File!, progress, token);
};

zone.FilesChanged += (_, e) =>
{
    // e.Files 保存用户新增且仍列在清单中的文件。
};
```

只有设置了 `UploadHandler`，且行处于 `Failed` 状态、`IsRetryable` 为 `True` 时，才会提供重试。按下它会为该行重新启动处理程序。在文件添加之后以及移除之后，都会引发 `FilesChanged`；其 `Files` 列表保存的是用户的文件，而不是应用创建的行。

### 由应用驱动的行

没有上传处理程序时，由应用驱动每一行。调用 `ReportProgress`、`CompleteUpload` 和 `FailUpload`，或直接设置该项的 `Status` 与 `Progress`。移除可作用于任何一行，而 `Retry` 只能通过处理程序重新启动上传，因此由应用驱动的失败没有重试按钮。应用自己重新启动失败的上传，并在同一行上报告进度。

<Demo name="AppDriven" />

```csharp
zone.ReportProgress(item, 40);
zone.CompleteUpload(item);
zone.FailUpload(item, "服务器已关闭连接", retryable: true);
```

`FailUpload` 会把空原因视为“上传失败”。当失败无法通过再次发送同一文件解决时，例如服务器因格式而拒收的文件，请传入 `retryable: false`。此类行只显示原因和移除按钮。

### 目标区内的列表

`ListPlacement="Inside"` 会把行显示在目标区的边缘之内，两侧内边距为 12 px，底部为 12 px。当投放区就是整个面板时使用它，例如带有自身标题的上传卡片。设置 `CompactAt` 后，一旦列出的行数达到该值，提示会收缩为一条细栏：图标与注释隐藏，圆角收紧，内边距缩小。文件拖到已收缩的目标区上方时，图标会重新出现，因此目标区仍然会引导用户拖放。

<Demo name="ListInside" />

### 拒绝与限制

被拒绝的文件永远不会出现在列表中，目标区会在错误行中显示原因。同一批次中被接受的文件仍会添加。

| 规则 | 检查时机 | 用户看到的内容 |
|---|---|---|
| 类型不在 `Accept` 中 | 添加文件时 | 单个文件显示“<name> 不是可接受的文件类型。”，多个文件显示“部分文件未添加，因为其类型不被接受。”；目标区晃动 |
| 文件数量超过 `MaxFiles` | 添加文件时 | “最多只能添加 6 个文件。”（上限为 1 时显示“1 个文件”）；目标区晃动 |
| 大于 `MaxFileSize` | 添加文件时 | 该行失败并显示“文件超过 2 MB”，没有重试按钮；文件不会上传 |
| 同一文件添加两次 | 添加文件时 | 与清单中已有的某个文件名称、大小和修改时间都相同的文件会被静默跳过 |
| 上传失败 | 上传进行中或结束后 | 该行显示处理程序或 `FailUpload` 给出的原因；允许时显示重试 |

大小使用 `FileDropzoneItem.FormatSize` 格式化：字节、整 KB，或在 10 MB 以下保留一位小数的 MB。

### 绑定到视图模型

投放区是一个列表编辑器：应用拥有文件及其上传，控件负责显示它们。`UploadHandler` 是委托而不是命令，因此请在代码后置文件中设置它，并把上传逻辑保留在视图模型中。`Items` 是只读的集合属性，不可绑定，因此视图模型需要自行保存所需的状态，并响应 `FilesChanged`。

```csharp
public partial class ExpenseReportViewModel : ObservableObject
{
    private readonly IReceiptStore _receipts;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SubmitCommand))]
    private int _receiptCount;

    [ObservableProperty]
    private string? _submitError;

    public ExpenseReportViewModel(IReceiptStore receipts) => _receipts = receipts;

    public Task UploadReceiptAsync(FileDropzoneItem item, IProgress<double> progress, CancellationToken token) =>
        _receipts.UploadAsync(item.File!, progress, token);

    public void OnReceiptsChanged(int count) => ReceiptCount = count;

    private bool CanSubmit() => ReceiptCount > 0;

    [RelayCommand(CanExecute = nameof(CanSubmit))]
    private async Task SubmitAsync()
    {
        SubmitError = await _receipts.SubmitAsync() ? null : "无法提交报告，请重试。";
    }
}
```

```csharp
// 承载投放区的视图的代码后置文件。
public partial class ExpenseReportView : UserControl
{
    public ExpenseReportView()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is ExpenseReportViewModel vm)
        {
            Receipts.UploadHandler = vm.UploadReceiptAsync;
            Receipts.FilesChanged += (_, args) => vm.OnReceiptsChanged(args.Files.Count);
        }
    }
}
```

```xml
<StackPanel Spacing="16">
    <arc:FileDropzone x:Name="Receipts"
                      Label="添加收据"
                      Accept=".pdf,.jpg,.jpeg"
                      MaxFiles="8" />
    <arc:Button Content="提交费用报告"
                Command="{Binding SubmitCommand}"
                IsDefault="True" />
    <TextBlock Text="{Binding SubmitError}" />
</StackPanel>
```

- `UploadHandler`、`Accept`、`MaxFiles` 和 `MaxFileSize` 都是单向设置：视图模型只需设置一次，控件不会把它们写回。
- `Label`、`Description`、`Note` 和 `DropLabel` 是单向的：当文案取决于工作流程时（例如“请添加已签署的合同”），将它们绑定到视图模型中的字符串。
- 需要监听的事件是 `FilesChanged` 和 `FilesRejected`；`IsDragOver` 与 `ErrorMessage` 是只读的。
- 提交命令从 `FilesChanged` 读取 `ReceiptCount`。失败的上传会保留在列表中，因此如果应用只需要已完成的收据，应在提交前检查 `Items` 中每一项的 `Status`。

### 示例：审批页面中的费用收据

财务人员为费用报告附上收据。标签指明对象，说明写明最重要的规则（格式与大小），注释显示文件类型，列表则同时展示收据可能处于的三种状态：已上传、仍在上传、已失败。失败的行没有重试按钮，因为此场景没有上传处理程序；在运行中的页面里，处理程序会提供重试。

![一个收据投放区，包含一张已上传的收据、一张正在上传的收据和一张上传失败的收据](images/ExpenseReceipts.png)

```xml
<StackPanel Width="640" Spacing="16">
    <TextBlock Text="Expense report"
               FontSize="18"
               FontWeight="SemiBold"
               Foreground="{arc:ArcDesignTokenResource Foreground}" />
    <arc:FileDropzone Label="Add receipts"
                      Description="PDF or JPG, up to 10 MB each. Attach every receipt over 25 USD."
                      Accept=".pdf,.jpg,.jpeg"
                      MaxFiles="8"
                      MaxFileSize="10485760">
        <arc:FileDropzone.Items>
            <arc:FileDropzoneItem>
                <x:Arguments>
                    <x:String>taxi_2026-04-02.pdf</x:String>
                    <x:Int64>248000</x:Int64>
                    <arc:FileDropzoneStatus>Uploaded</arc:FileDropzoneStatus>
                </x:Arguments>
            </arc:FileDropzoneItem>
            <arc:FileDropzoneItem Progress="64">
                <x:Arguments>
                    <x:String>hotel_folio_march.pdf</x:String>
                    <x:Int64>1258291</x:Int64>
                    <arc:FileDropzoneStatus>Uploading</arc:FileDropzoneStatus>
                </x:Arguments>
            </arc:FileDropzoneItem>
            <arc:FileDropzoneItem Error="The server closed the connection">
                <x:Arguments>
                    <x:String>client_dinner_scan.jpg</x:String>
                    <x:Int64>3145728</x:Int64>
                    <arc:FileDropzoneStatus>Failed</arc:FileDropzoneStatus>
                </x:Arguments>
            </arc:FileDropzoneItem>
        </arc:FileDropzone.Items>
    </arc:FileDropzone>
</StackPanel>
```

### 文案规范

- **标签**：用用户期望的复数名词为对象命名，例如“添加收据”“附加合同”“添加图片”。目标区用动词“添加”，避免使用“上传”，因为它描述的是用户看不到的网络步骤。
- **说明**：写明用户必须满足的约束：格式与大小。在关键处说明后果，例如“附上所有超过 25 美元的收据”。
- **注释**：除非规则需要一句话说明，否则保留默认值。在说明中按用户的说法写出可接受的类型（“PDF 或 JPG”），在注释中写出扩展名。
- **行的原因**：说明发生了什么以及该怎么做，使用用户能理解的文字，而不是原始的服务器消息。

| 不要写 | 改为 |
|---|---|
| 上传文件 | 添加收据 |
| 无效文件 | 请使用 PDF 或 JPG 文件。 |
| 最多 8 | 最多 8 个文件 |
| 文件过大 | 文件大小超过 10 MB |
| 出错了 | 上传失败。请移除该文件后重新添加。 |

### 最佳实践

**推荐**

- 在用户拖放任何东西之前，就在说明和注释中把限制显示在目标区旁边。拖放之后再被拒绝是可以恢复的，但规则本应已经可见。
- 每个投放区只保留一个上传处理程序，并让它为每一行报告进度。停留在 0 的行看起来像是卡住了，即使上传实际上正在进行。
- 向 `FailUpload` 传入真实原因，或抛出用户能够处理的消息。原因是该行能提供的唯一帮助。
- 对于会替换之前文档的场景，将 `AllowMultiple` 设为 `False`，使列表中永远不会有同一字段的两份合同。
- 提交前检查每一行的 `Status`，确保失败的文件在表单提交前已被修正。

**避免**

- 把投放区作为附加文件的唯一方式。键盘用户可以通过 Enter 与空格键操作，但目标区旁边的可见按钮能帮助期望按钮的人，也让屏幕阅读器用户找到该操作。
- 设置了 `MaxFileSize`，却没有在说明中写明。因大小而失败、又没有任何提示的文件，会被误认为是程序错误。
- 把文件原始的异常文本显示为原因。堆栈跟踪和状态码对用户没有帮助；应为用户撰写消息，并记录详细信息。
- 在用户必须逐一检查每个文件的审阅步骤中使用 `CompactAt`。紧凑模式适用于拥挤的面板。
- 只依赖粘贴。粘贴只在指针位于目标区上方或焦点位于其中时有效，因此用户必须知道该在哪里粘贴。

### 常见问题

**为什么我的文件被跳过了，却没有任何消息？** 与列表中已有文件的名称、大小和修改时间都相同的文件属于重复项，会被跳过且不报错。因类型或数量限制而被拒绝的文件，总会在错误行上产生消息，并引发 `FilesRejected` 事件。

**例如用户要重新开始，如何移除所有文件？** 遍历 `Items` 的副本，并对每一行调用 `Remove`：`foreach (var item in zone.Items.ToList()) zone.Remove(item);`。移除正在上传的行会取消其上传，并且每个用户文件都会引发一次 `FilesChanged`。

**投放区会自行上传文件吗？** 不会。只有设置了 `UploadHandler` 才会上传；没有处理程序时，它只列出文件，由应用调用 `ReportProgress`、`CompleteUpload` 和 `FailUpload` 来完成上传。

**拖放文件经过目标区时，能否修改标签？** 可以。设置 `DropLabel` 为悬停时应显示的文案，例如“松开即可添加已签署的合同”。为空时，默认的拖放文案取决于是否设置了上传处理程序，以及 `AllowMultiple`。

**为什么失败的行没有重试按钮？** 只有设置了 `UploadHandler`、行处于 `Failed` 且 `IsRetryable` 为 `True` 时才会出现重试。文件过大会被标记为不可重试，而由应用驱动的失败没有可调用的处理程序。

## API 参考

<!-- api: FileDropzone -->

<!-- api: FileDropzoneItem -->

## 无障碍

- 目标区是一个按钮，其名称由 `Label` 提供。它的帮助文本合并了说明与注释，因此屏幕阅读器会读出目标区接受什么类型的文件，以及可以添加多少个。
- 列表是一个有名称的分组（“文件”）。列表为空时对辅助技术隐藏，添加第一行时出现。
- 每一行的进度条公开一个 0 到 100 的范围。重试与移除按钮的无障碍名称中包含文件名，因此在一串相同的按钮中，“移除 hotel_folio_march.pdf”可以区分开。
- 添加、上传、完成、失败和移除文件时，会以礼貌方式朗读。拒绝则以断言方式朗读，因为用户必须采取行动。
- 屏幕上的长文件名和失败原因会缩短为一行；完整文本位于该行的工具提示及其无障碍名称中。
- 移除一行时焦点保留在控件上：移到相邻行的移除按钮；若已无行，则移到目标区。

### 键盘

| 按键 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 先移过目标区，再依次经过各行的重试与移除按钮 |
| 目标区上的 `Enter` / `Space` | 打开系统文件选择器，并按 `Accept` 过滤 |
| 列表中的 `Up` / `Down` | 将焦点移到相邻行的同一操作（重试或移除）上 |
| 行上的 `Delete` / `Backspace` | 移除当前聚焦的行；焦点移到相邻行 |
| `Ctrl+V`（macOS 上为 `Cmd+V`） | 在指针位于目标区上方或焦点位于其中时，添加剪贴板中的文件（例如截图）；文本框获得焦点时忽略 |

## 动效

- 把文件拖到目标区上方时，纸张图标抬起，虚线边缘闭合为带强调色的实线边缘，并开始缓慢的呼吸脉冲（每 2.2 秒在 1 到 1.012 之间缩放）。拖动结束后脉冲立即停止。
- 行从上方滑入，并带有轻微模糊，依次进入；被移除的行会在约 0.3 秒内收起。
- 进度条以平滑的弹簧跟随其目标值，已完成的行会绘制对勾标记；失败的行或被拒绝的一批文件会晃动一次，约 0.42 秒。
- 减弱动效时，高度与抬起的变化改为短暂淡入淡出，进度变化立即生效；脉冲与晃动均不运行。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 文件拖到目标区上方 | 纸张抬起，虚线边缘闭合，目标区呼吸 | 边缘与色调立即变化；无脉冲 | 立即完成 |
| 添加一行 | 行滑入，从轻微模糊中放大出现，错落进入 | 短暂淡入 | 立即完成 |
| 移除一行 | 高度在约 0.3 秒内收起 | 短暂淡出 | 立即完成 |
| 上传进度变化 | 进度条以平滑弹簧跟随该值 | 立即完成 | 立即完成 |
| 一行失败，或一批被拒绝 | 目标区或行晃动一次 | 不晃动；仅颜色变化 | 立即完成 |

## 相关组件

- `EmptyState`：页面为空、拖放是主要行动号召时使用。
- `Button`：用于打开选择器的可见操作，或列表旁边的提交操作。
- `Input` 与 `Textarea`：用于非文件类的值。
- `SignaturePad`：用于手绘签名而非附加文件的场景。
- `RichTextEditor`：用于同时包含文字与图片的文档正文。

| 组件 | 适用场景 |
|---|---|
| `EmptyState` | 空页面需要一个更醒目、带标题的行动号召 |
| `Button` | 只需要系统选择器，不需要文件列表 |
| `Select` / `RadioGroup` | 用户从一组已知值中选择一个 |
| `SignaturePad` | 签名是在应用内绘制的，而不是从磁盘读取 |
| `RichTextEditor` | 内容是同一文档中的文字与图片 |
| `Input` / `Textarea` | 值是字符串，而不是文件 |
