---
title: Multi-step form
summary: 在一张卡片中每次只询问一个步骤的简短表单，配有进度列表、"Back"和"Continue"按钮、每一步前的校验，以及最后的成功状态。
source-hash: dc7c21c7ca0d
---

<Demo name="Preview" />

## 何时使用

`MultiStepForm` 将一个简短的表单拆分为几个重点突出的步骤，并在同一张卡片中引导用户逐步完成。标题区列出每一个步骤并用圆形标记显示，同时统计当前所在位置（"Step 1 of 2"）；中间是当前步骤的字段；底部是"Back"和"Continue"，最后一步时"Continue"会变为完成操作。提交最后一步后，字段会被一段简短的成功信息取代，按钮随之收起。

- **注册与入门。** 为工作区命名、选择负责人、挑选套餐：两到四个小决定，若放在一张长表单里会显得沉重。
- **收集信息与申请。** 支持请求、预订或申请，每一步询问一类问题，最后一步统一提交。
- **按固定顺序填写的设置。** 个人资料、地址、配送：用户可以回到前面的步骤修改答案，而不会丢失其他步骤的内容。

## 何时不用

- **只需要进度指示。** 当你只需要步骤列表，而不需要卡片、按钮和字段时，请使用 `Stepper`。
- **长流程或整页流程。** 五个或更多步骤、步骤自带导航，或跨越多个页面的流程（例如结账），应当使用各自的页面布局。步骤放不下时进度列表会滚动，但列表很长通常说明流程应当缩短。
- **单个简短表单。** 三个字段不需要分步；把它们放在一个表单里，配一个提交按钮即可。
- **在别处完成的任务。** 需要用户在其他地方完成的事项（连接日历、邀请团队成员）属于检查清单，而不是表单。

## 用法

该区块派生自 Avalonia 的 `TemplatedControl`。每个步骤都是一个 `MultiStepFormStep`，它是 `AvaloniaObject`，包含 `Title`（字段上方的标题，也是进度列表中的标签）、可选的 `Description` 说明行，以及作为 `Content` 的步骤字段。将步骤作为子元素编写；字段就是普通控件。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:MultiStepForm CompleteLabel="Create workspace"
                       SuccessTitle="You're in"
                       SuccessDescription="Your workspace is ready.">
        <arc:MultiStepFormStep Id="workspace"
                               Title="Workspace basics"
                               Description="Give it a short, memorable name.">
            <arc:Input Label="Workspace name" Text="Northwind studio" />
        </arc:MultiStepFormStep>
        <arc:MultiStepFormStep Id="lead" Title="Pick a lead">
            <arc:Combobox Label="Lead" SelectedIndex="0">
                <arc:SelectItem Content="Priya Raman" />
                <arc:SelectItem Content="Tomás Ortega" />
                <arc:SelectItem Content="June Whitfield" />
            </arc:Combobox>
        </arc:MultiStepFormStep>
    </arc:MultiStepForm>
</UserControl>
```

`Completed` 在最后一步提交且成功区块显示后引发（`CompleteCommand` 紧随其后以相同参数运行）。参数中包含各个步骤，其内容仍保留着用户输入的值。

```csharp
form.Completed += (_, e) => workspaces.Create(nameBox.Text, (string?)leadPicker.SelectedValue);
```

### 结构

- **进度标题**：每个步骤一项，包括 20 px 的标记和步骤标题。尚未到达的步骤显示为弱化样式，当前步骤使用强调色，已完成的步骤显示绿色对勾。右侧的计数显示 `CountFormat`（"Step {0} of {1}"）。
- **步骤**：步骤标题作为标题，然后是可选的说明，再是字段。卡片宽 540（若容器更窄则与容器同宽），步骤区域至少高 224，因此较短的步骤不会使卡片跳动。
- **按钮**：Back（第一步时禁用）和提交按钮；提交按钮显示 `NextLabel`（"Continue"），在最后一步显示 `CompleteLabel`（"Finish"）。
- **成功区块**：一个对勾、`SuccessTitle` 和 `SuccessDescription`，居中显示在原先字段的位置。

### 保留输入的值

每个步骤在其他步骤显示时都会保持其字段存活：未处于当前位置的步骤会被隐藏但仍留在表单中，因此其中输入的文字、选择项、绑定和数据上下文在"Back"和"Continue"之间都会保留。可以在 `Completed` 触发时读取这些值，将它们绑定到视图模型，或两者兼用。`Restart()` 会重新显示第一步；它不会清空字段，因此如果希望每次都从空白开始，请重置你的模型（或控件）。

### 前进前检查步骤

步骤通过"Continue"、完成按钮，或在步骤的单行文本框中按 Enter 提交。表单前进之前会依次检查：

1. **数据校验错误。** 步骤中显示校验错误的字段（来自绑定、`INotifyDataErrorInfo` 或 `DataValidationErrors.SetError`）会阻止表单前进，并获得焦点。
2. **`StepValidating`。** 在参数上设置 `Cancel` 即可停留在当前步骤；适合无需等待的快速检查。
3. **`MultiStepFormStep.ValidateAsync`。** 你自己的检查，会被等待。返回 false 则停留（通常在此之前为某个字段设置 `ErrorMessage`）。运行期间 `IsBusy` 为 true，提交按钮显示加载动画并保持焦点，Back 被禁用。Back、其他任何步骤切换、`Restart()` 或离开窗口都会取消其令牌。抛出异常的检查视同返回 false。

<Demo name="Validation" />

```csharp
// isEmailTaken is your own check, for example a call to your API.
form.Steps[0].ValidateAsync = async cancellation =>
{
    var taken = await isEmailTaken(emailBox.Text, cancellation);
    emailBox.ErrorMessage = taken ? "That email already has an account." : null;
    return !taken;
};
```

### 提交最后一步

`CompleteAsync` 在最后一步提交并通过其检查后运行。在任务完成之前，提交按钮会显示加载动画；随后显示成功区块并引发 `Completed`。如果任务抛出异常，表单停留在最后一步，`CompleteFailed` 报告该异常，按钮可再次尝试。没有 `CompleteAsync` 时，表单会立即完成。

<Demo name="AsyncSubmit" />

```csharp
// sendRequest is your own call that stores or sends the answers.
form.CompleteAsync = cancellation => sendRequest(request, cancellation);
form.CompleteFailed += (_, e) => status.Text = e.Exception.Message;
```

### 自行驱动步骤

`CurrentIndex`（从零开始，双向）是当前显示的步骤。设置它可以从其他步骤开始或跳转；表单会沿行进方向做动画，并引发 `StepChanged`，其中包含旧索引和新索引。超出步骤范围的值会在步骤已知后被限制在有效范围内，因此 XAML 中的 `CurrentIndex="1"` 在步骤写入之前也能生效。`NextAsync()` 执行"Continue"的操作（包括检查），并返回表单是否已移动；`Back()` 执行 Back 的操作。`IsComplete` 同样是双向的：设置为 true 可显示成功区块而不引发 `Completed`，设置为 false 则重新显示步骤。

<Demo name="Controlled" />

### 窄容器

宽度不超过 540 时，卡片跟随其容器；在更宽的容器中，卡片保持 540 宽并居中。该区块读取的是自身的宽度，而不是窗口的宽度：当宽度为 388 或更小时，计数会移到进度列表上方，内边距从 20 收紧为 16，步骤区域增长至至少 240。无论宽度多少，只要步骤放不进一行，列表就会横向滚动（不显示滚动条），并使当前步骤保持在可视范围内。

<Demo name="Narrow" />

### 其他文字

每段文字都是一个属性，并带有英文默认值。屏幕上可见的有 `NextLabel`、`CompleteLabel`、`BackLabel`、`SuccessTitle`、`SuccessDescription` 和 `CountFormat`；供屏幕阅读器使用的有 `FormName`、`ProgressNameFormat`、`AnnouncementFormat`、`CurrentStepText` 和 `CompletedStepText`。在格式字符串中，`{0}` 是从一开始计数的步骤编号，`{1}` 是步骤总数；步骤切换时，计数中只有 `{0}` 部分会滚动。

<Demo name="Labels" />

## API 参考

<!-- api: MultiStepForm -->

<!-- api: MultiStepFormStep -->

<!-- api: MultiStepFormStepChangedEventArgs -->

<!-- api: MultiStepFormStepValidatingEventArgs -->

<!-- api: MultiStepFormCompletedEventArgs -->

<!-- api: MultiStepFormCompleteFailedEventArgs -->

## 无障碍

- 每次步骤切换后，焦点移到新步骤的标题；最后一步完成后，焦点移到成功标题。两者都只能由代码设置焦点（它们不是 Tab 停靠点，也不绘制焦点环），因此屏幕阅读器会从正确的位置开始朗读，下一次 Tab 会进入第一个字段。只有当焦点原本位于表单内时焦点才会移动，因此从其他地方发起的步骤切换（例如 `CurrentIndex` 绑定）不会改变焦点。
- Back 在第一步时才被禁用，且这一操作发生在焦点移动之后，因此焦点不会丢失。
- 卡片是一个表单地标，由 `FormName` 命名。进度标题是导航地标，列表是列表，二者均由 `ProgressNameFormat`（"Progress, step 1 of 2"）命名；每一项以其步骤标题命名，并以 `CurrentStepText` 或 `CompletedStepText` 作为状态。可见的计数对屏幕阅读器隐藏。
- 每个步骤是以其标题命名的分组。步骤切换时以 `AnnouncementFormat`（"Step 2 of 2"）进行礼貌播报；完成时播报成功标题和文字。
- 提交按钮以其当前标签命名；在检查或最终提交运行期间，它保持焦点并忽略按下操作。
- 键盘焦点环为 2 px，位于按钮外侧 2 px 处；指针按下时不绘制焦点环。

### 键盘

| Keys | Action |
| --- | --- |
| Enter | 在步骤的单行文本框中：提交该步骤（继续，或在最后一步完成）。自身使用 Enter 的字段（展开且有高亮选项的组合框列表、多行文本区域）会保留该按键。 |
| Enter / Space | 在 Back 或提交按钮上：按下该按钮。 |
| Tab / Shift+Tab | 依次经过步骤的字段，然后是 Back（禁用时跳过）和提交按钮。 |

## 动效

- **切换步骤**：新步骤从行进方向以 20 px 滑入（前进时从右侧，后退时从左侧），使用约 0.4 s 的临界阻尼弹簧，同时在 240 ms 内淡入；旧步骤向相反方向滑动 20 px，并在 120 ms 内淡出。两者同时开始。切换过程中途后退时，旧步骤会从当前位置掉头，并保持其速度。
- **高度**：步骤区域以相同的弹簧调整到新步骤的高度，较高的步骤使其增长，较矮的步骤使其收缩（不会低于最小高度）。仅在高度变化期间，内容才会在上下方向被裁剪。
- **进度**：已完成的步骤会将其编号替换为对勾：对勾从 0.6 开始，以较短的临界阻尼弹簧放大，然后在 320 ms 内绘制完成；后退时，编号会从轻微模糊中重新弹出。标记和标签颜色在 240 ms 内过渡。
- **计数和标签**：步骤编号在前进时向上滚动 0.3em，后退时向下滚动，在 240 ms 内从柔和的模糊中升起，而旧编号在 120 ms 内离开。提交按钮的标签以相同方式切换（始终向上），同时按钮宽度以弹簧过渡到新标签的宽度。
- **完成**：最后一步滑出，按钮在 120 ms 内淡出，其所在行则随弹簧收起；成功区块以 8 px 上升并淡入，其对勾从 80 ms 开始绘制，标题从 100 ms 开始从模糊中升起，文字从 140 ms 开始。
- **按钮**：悬停颜色在 160 ms 内过渡；按下时按钮缩放至 0.98（耗时 100 ms），松开时以弹簧弹回。
- 减弱动效和关闭动效时，步骤、高度、标签、标记和成功区块都会立即切换，且按下时不做缩放；焦点和播报保持不变。

## 相关组件

- `Stepper`：用于单独的进度指示器。
- `Input`、`Combobox`、`Textarea` 和 `RadioCards`：示例中使用的字段。
- 其他认证类区块：Login、OTP input 和 Onboarding illustration。
