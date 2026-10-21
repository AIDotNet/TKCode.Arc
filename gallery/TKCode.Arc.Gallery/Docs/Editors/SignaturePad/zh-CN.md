---
title: 签名板
summary: 笔迹平滑，速度越快越细；支持撤销、回放，并可导出 PNG 或 SVG。
source-hash: 9430c6029c5c
---

<Demo name="Sign" />

## 何时使用

`SignaturePad` 为需要在应用内签署的文件采集手写签名。它在画板上绘制墨迹，并自带工具栏（颜色、笔尖、撤销、重做、回放、清除和导出），保留笔画以便复核和修正签名，并将结果导出为图片或矢量文件，由记录保存。

- **销售或账单控制台中的订单与合同**：客户在通话结束时用平板签署订单表单。墨迹导出为 PNG，与签署人姓名一起归档，姓名由表单另外保存。
- **物流应用中的签收确认**：司机在手持设备屏幕上为包裹签收。签名以 PNG 保存在运单记录上，细笔尖让墨迹在小画板上依然清晰。
- **诊所或患者登记应用中的同意书**：患者签署治疗同意书。SVG 导出在以任何尺寸打印时都保持清晰。
- **金融科技开户流程中的确认书**：客户签署披露声明，笔画随账户一同保存，因此可以用 `Reset` 再次显示同一个签名。
- **现场服务验收**：技术人员签署工单，并在记录关闭前用撤销修正一笔。

## 何时不用

- **输入姓名即可**：用 `Input`，它以文字保存姓名，无需绘制。
- **只需一个明确的确认，无需保留签名**：用 `HoldToConfirm`，它需要按住确认，只记录决定。
- **已经是文件的已签署文档**：用 `FileDropzone` 附加该文件，例如签名服务返回的 PDF。
- **对条款的同意与否**：用 `Checkbox`，并在附近链接条款。手绘签名增加了条款并不需要的一步。
- **长篇备注或文档正文**：用 `RichTextEditor`，它编辑文字，不采集签名。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:SignaturePad Signer="张三"
                      Hint="请在此签名" />
</UserControl>
```

`SignaturePad` 派生自 `TemplatedControl`，自行绘制墨迹，因此它不是文本框，没有插入点，也没有文字。墨迹位于画板单位中，即宽高比为 600:260 的坐标空间，画板会把该空间缩放到其宽度。笔画是普通数据（`InkStroke` 记录，包含其点、颜色、笔尖和开始时间），因此同一个签名无需屏幕位图即可导出、保存和回放。

### 结构

1. **画板**：绘图画布。在任何宽度下都保持 600:260 的宽高比，因此宽 560 px 的画板约高 243 px。它的圆角与面板圆角一致，并有一条 1.5 px 的虚线基线，从宽度的 7% 延伸到 93%，位于高度的 72% 处。一个 X 标记签名线。
2. **提示**（`Hint`，默认“在此签名”）：基线上方的大号提示文字，在第一笔落下前显示。开始绘制时淡出，清除后重新出现。
3. **签署人**（`Signer`）：基线下方打印的姓名，使用超小文字尺寸。它只绘制在画板上；导出内容只包含墨迹，因此应将姓名与记录一同保存。
4. **墨迹**：笔画。黑色墨迹跟随前景色，蓝色与紫色墨迹是随浅色和深色模式变化的主题值。导出使用固定的打印颜色，因此 PNG 在任一主题下都相同。
5. **颜色组**：三个 36 px 选项（`Black`、`Blue`、`Violet`），为单选组。
6. **笔尖组**：三个 36 px 选项（`Fine`、`Medium`、`Bold`）。工具栏分成两组，分别对齐两端，宽度不足时换行。
7. **操作**：撤销、重做和回放为 36 px 的胶囊图标按钮（回放运行时图标切换为停止），然后是“另存为”标签下的导出按钮（PNG 与 SVG），以及清除按钮。导出成功后，导出按钮会在短时间内显示“已保存”。
8. **状态**：实时区域，播报每一次变化，例如“已撤销”或“签名已清除。撤销可将其恢复。”

### 墨迹颜色与笔尖

`InkColor`（默认 `Black`）与 `InkWidth`（默认 `Medium`）设置新笔画的默认值。每一笔都保留绘制时所用的颜色与笔尖，因此之后更改选择不会重绘先前的笔画。

<Demo name="InkAndNib" />

需要打印或作为合同归档的签名使用黑色。蓝色或紫色墨迹可以让第二位签署人或见证人在同一页上区分开。小型手持画板用 `Fine`，大多数表单用 `Medium`，大屏幕上用手指书写的自助终端用 `Bold`。笔尖以画板单位设置宽度（3.4、5.6 和 8.4），因此粗笔画在小画板上依然保持比例。

设备报告压力时，笔压决定墨迹的宽度。鼠标或手指没有压力，因此画板会根据笔画的速度模拟压力：快速的笔画细，慢速的笔画饱满。

### 读取笔画

每一笔之后，以及撤销、重做、清除和重置之后，都会执行 `StrokesChanged`。`Strokes` 为只读，以画板单位保存笔画。第一笔之前以及清除之后，`IsEmpty` 为 true；`CanUndo` 与 `CanRedo` 报告历史状态。

```csharp
pad.StrokesChanged += (_, e) =>
{
    saveButton.IsEnabled = e.Strokes.Count > 0;
};
```

每个 `InkStroke` 都有 `Id`、一组 `InkPoint` 值（`X`、`Y`、`Pressure`，取值 0 到 1，无压力时为 -1，以及 `Time`，即距笔画起点的毫秒数）、`Color`、`Width` 和 `At`（笔画开始的时间）。

### 导出

`ExportPng` 与 `ExportSvg` 返回裁剪到墨迹边界的内容。`ExportPng` 接受可选的 `scale` 参数（默认 3），用于放大画板的分辨率；`ExportBitmap` 返回同一图像的 `RenderTargetBitmap`，用于预览。`SaveAsync` 会打开系统保存对话框，并以 `FileName`（默认为“签名”）作为建议的文件名。当用户取消、没有任何笔画或写入失败时，它返回 `false`。

<Demo name="UseTheSignature" />

```csharp
var png = pad.ExportPng();
var svg = pad.ExportSvg();

var saved = await pad.SaveAsync(SignatureFileKind.Png);
```

`ExportSvg` 在没有笔画时返回空字符串，因此在保存导出结果之前应检查 `IsEmpty`。打印时使用 `SignatureFileKind.Svg`，用于邮件或 PDF 页面时使用 `SignatureFileKind.Png`。

### 保存与还原签名

`Reset` 会替换笔画，不播放清除动画，并清空撤销历史，因此从存储加载的签名不会被视为一次编辑。传入之前保存的笔画即可重新显示；不带参数调用则会重新开始。

```csharp
// 保存：笔画是普通记录，任何序列化器都可以使用。使用应用自己的选项；当应用被裁剪或以 AOT 发布时，使用源生成的上下文。
var json = JsonSerializer.Serialize(pad.Strokes);

// 还原：画板显示已保存的墨迹，并且历史为空。
pad.Reset(JsonSerializer.Deserialize<List<InkStroke>>(json));
```

保存的笔画保留其画板单位，因此在 560 px 画板上保存的签名在 400 px 画板上也能正确显示。

### 绑定到视图模型

签名是视图模型保存的值，画板只负责采集。通过 `StrokesChanged` 读取笔画，因为 `Strokes` 是只读的，无法反向绑定。`Signer`、`Hint`、`InkColor` 与 `InkWidth` 是单向设置：由视图模型设置，画板不会将其写回。

```csharp
public partial class SignOffViewModel : ObservableObject
{
    private readonly ISignatureStore _store;

    [ObservableProperty]
    private string _signerName = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SubmitCommand))]
    private bool _hasSignature;

    public IReadOnlyList<InkStroke> Strokes { get; private set; } = Array.Empty<InkStroke>();

    public SignOffViewModel(ISignatureStore store) => _store = store;

    public void OnStrokesChanged(IReadOnlyList<InkStroke> strokes)
    {
        Strokes = strokes;
        HasSignature = strokes.Count > 0;
    }

    [RelayCommand(CanExecute = nameof(HasSignature))]
    private async Task SubmitAsync()
    {
        await _store.SaveAsync(SignerName, Strokes);
    }
}
```

```xml
<StackPanel Spacing="16">
    <arc:Input Label="打印姓名" Text="{Binding SignerName, Mode=TwoWay}" />
    <arc:SignaturePad x:Name="Pad"
                      Signer="{Binding SignerName}"
                      Hint="签名以接受订单" />
    <arc:Button Content="接受并签署"
                Command="{Binding SubmitCommand}"
                IsDefault="True" />
</StackPanel>
```

```csharp
// Code behind: 视图把画板的事件连接到视图模型。
Pad.StrokesChanged += (_, args) => vm.OnStrokesChanged(args.Strokes);
```

- `Signer` 是单向的：打印的姓名会随用户输入而跟随姓名字段。更改姓名不会清除笔画。
- 应监听 `StrokesChanged`；`IsEmpty`、`CanUndo` 与 `CanRedo` 是只读的，用于更新视图的状态。
- 画板没有提交命令。提交应使用视图模型的命令，启用状态则使用画板的 `IsEmpty` 或 `HasSignature` 标志。

### 校验

签名在用户提交时检查，画板为空时提示文字保持可见。

| 规则 | 何时检查 | 显示在哪里 |
|---|---|---|
| 存在签名 | 提交时 | 表单自己的消息；画板为空时提示文字保持可见 |
| 已填写打印姓名 | 提交时，或离开姓名字段时 | 姓名字段的错误行，来自表单自己的规则 |
| 再次显示已保存的签名 | 记录打开时 | 使用已保存的笔画调用 `Reset`，不增加撤销步骤 |

这些规则属于表单而非画板，因此消息使用表单自己的组件。表单打开期间保持提交按钮可用，并在用户按下后显示“未签名”的消息。在画板有墨迹之前禁用它，会让用户无从得知这种状态的原因。

### 示例：订单表单确认

客户在销售电话结束时签署订单表单。标题和一句话摘要说明接受的是什么，画板的提示用平实的语言重复该操作，签署人姓名打印在签名线下方，让客户看清这条签名线属于谁。在该场景中画板没有任何笔画，因为它是在客户签署之前显示的。

![订单表单，包含摘要、一个带提示的空白签名板，以及打印的签署人姓名](images/OrderFormSignature.png)

```xml
<StackPanel Width="640" Spacing="16">
    <TextBlock Text="Accept order form"
               FontSize="18"
               FontWeight="SemiBold"
               Foreground="{arc:ArcDesignTokenResource Foreground}" />
    <TextBlock Text="Northwind Logistics agrees to the 12-month Analytics Pro plan at 4,800 USD per month, billed quarterly."
               TextWrapping="Wrap"
               Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
    <arc:SignaturePad Signer="Maya Chen, Head of Finance"
                      Hint="Sign to accept the order" />
    <arc:Button Content="Accept and sign" />
</StackPanel>
```

### 文案规范

- **提示**：用平实的语言说明操作，例如“签名以接受订单”。避免使用“签名处”，因为用户不知道这个签名接受的是什么。
- **签署人**：打印法定姓名与职务，例如“陈美雅，财务主管”，并将同样的文字与记录一同保存，因为导出的墨迹中不包含姓名。
- **提交按钮**：使用动词加宾语，例如“接受并签署”“签署协议”。避免使用“完成”，因为它没有说明确认的是什么。
- **状态消息**：说明发生了什么变化以及接下来可以做什么，例如“签名已清除。撤销可将其恢复。”

| 不要写 | 改为 |
|---|---|
| 签名 | 签名以接受订单 |
| 请在此签名！！ | 在此签名 |
| 完成 | 接受并签署 |
| 错误：未签名 | 提交前请添加你的签名。 |

### 最佳实践

**推荐**

- 为每个签名将 `Signer` 设为法定姓名，让签署人清楚是谁在签署，并将该姓名与笔画一同保存。
- 将 `Strokes` 与记录一同保存，并用 `Reset` 重新加载，而不是只保存画板的图片。
- 对于以多种尺寸打印的文档使用 `ExportSvg`，对于邮件和屏幕显示使用 `ExportPng`。
- 为签署人保留撤销历史，并在基线可见的宽度下显示画板。
- 提交时检查 `IsEmpty`，并在用户修正签名时保留表单数据。

**避免**

- 保存空的导出结果。没有笔画时 `SaveAsync` 返回 false，而空的 SVG 不是签名。
- 依赖颜色来传达含义。蓝色与紫色墨迹只是视觉选择，打印的记录需要姓名。
- 在表单中无理由地让签名变为可选。应说明它是否必填以及原因。
- 在用户仍在绘制时用 `Reset` 替换笔画。Reset 会立即清空历史和墨迹。
- 在狭窄的列中显示画板。基线与工具栏分组会换行，狭窄的画板很难签名。

### 常见问题

**为什么 Delete 会清除画板，用户能恢复吗？** Delete 与 Backspace 会以擦除动画清除画板。清除是一个撤销步骤，因此 `Ctrl+Z`（macOS 上为 `Cmd+Z`）可以把笔画恢复。

**为什么我导出的 PNG 是空的？** `ExportPng` 会裁剪到墨迹，没有笔画时就没有可导出的内容。检查 `IsEmpty`，或使用 `SaveAsync` 的结果（画板为空时它返回 false）。

**更改墨迹颜色会重绘已有的笔画吗？** 不会。每一笔都保留绘制时所用的颜色与笔尖。`InkColor` 与 `InkWidth` 只为新笔画设置选择。

**鼠标或触控时压力如何运作？** 触控笔会报告压力，压力直接决定宽度。鼠标或手指不报告压力，画板会根据速度推算宽度。

**如何再次显示签名，例如作为回放？** 调用 `Replay()` 以原有节奏重绘笔画，大约需要 3.2 秒；调用 `StopReplay()`（或按 Escape）停止回放。使用 `Reset(strokes)` 可以显示已保存的笔画而不回放。

## API 参考

<!-- api: SignaturePad -->

## 无障碍

- 画板是一个可聚焦的图像，名称为“签名板”。其无障碍名称包含笔画数量；没有墨迹时，描述为“签名板。空。请用指针、触控笔或手指书写。”
- 键盘快捷键写在画板的说明文字中，因此画板获得焦点时屏幕阅读器会播报它们。
- 墨迹颜色与笔尖是单选组。只有选中的选项在 Tab 顺序中，方向键在各选项之间移动。
- 工具栏按钮带有名称与工具提示。笔画、撤销、重做、清除、回放与导出结果都会以礼貌方式播报，因此用户能听到每一次变化而不会失去焦点。

### 键盘

| 按键 | 操作 |
|---|---|
| `Ctrl+Z`（macOS 上为 `Cmd+Z`） | 画板获得焦点时撤销上一次变化 |
| `Ctrl+Shift+Z`（macOS 上为 `Cmd+Shift+Z`），或 `Ctrl+Y` | 重做 |
| `Delete` 或 `Backspace` | 以擦除动画清除画板；清除可以撤销 |
| `Escape` | 停止正在进行的回放 |
| 颜色或笔尖组中的 `Left` / `Right`（或 `Up` / `Down`） | 移到上一个或下一个选项并选中它 |
| `Tab` / `Shift+Tab` | 在画板、各组和工具栏按钮之间移动 |

## 动效

- 清除时，一个刮板横扫画板，在约半秒内将墨迹一并刮去。
- 回放会以原有节奏重绘每一笔，笔尖处带有一个圆点，整个回放约在 3.2 秒内完成。笔画之间的停顿有上限。
- 提示文字在上浮的同时淡入，颜色与笔尖的高亮会滑动到所选选项。
- 减弱动效时，取消擦除与模糊效果，改用短暂的淡入淡出。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 清除 | 刮板横扫，约 0.56 秒内刮去墨迹 | 墨迹立即清除 | 立即 |
| 回放 | 笔画以原有节奏在约 3.2 秒内重绘 | 笔画以其节奏重绘 | 回放立即结束 |
| 提示出现或消失 | 淡入并上浮；开始绘制时模糊淡出 | 约 120 ms 的短暂淡入淡出 | 立即 |
| 颜色或笔尖选择变化 | 高亮滑动到所选选项 | 高亮直接跳到该选项 | 立即 |
| 回放与停止图标 | 图标缩放并切换 | 图标切换 | 立即 |

## 相关组件

- `Input`：与手写签名并列或代替手写签名时，用于输入姓名。
- `HoldToConfirm`：无需签名的确认操作。
- `FileDropzone`：附加已签署文档等文件。

| 组件 | 适用场景 |
|---|---|
| `Input` | 姓名是输入的，且不需要手绘签名 |
| `HoldToConfirm` | 按住确认的决定即为记录，不保留签名 |
| `Checkbox` | 用户以是或否的选择接受条款 |
| `FileDropzone` | 必须附加其他服务已签署的文件 |
| `RichTextEditor` | 内容是用户撰写的文字，而不是签名 |
