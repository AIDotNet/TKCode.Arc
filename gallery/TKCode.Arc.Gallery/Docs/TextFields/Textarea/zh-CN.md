---
title: 多行文本框
summary: 用于备注、描述和较长文本的多行输入字段。
source-hash: 500366a42911
---

<Demo name="LiveCount" />

## 何时使用

`Textarea` 用于跨越多行的自由文本：对客户工单的回复、事件摘要、商品描述、发票上的备注。它填满所在列，以可用的高度开始，并允许用户将其拖高，因此较长的内容仍然易读，表单也不会因此跳动。辅助文字行和错误行与 `Input` 相同，因此多行字段无需特殊布局即可放入表单列。

- **支持控制台中的回复**：客服人员为工单写几句回复。对照回复上限的实时计数告诉他还剩多少空间，并且在他核对订单时保留草稿。
- **事件摘要与变更说明**：运维人员记录发生了什么、何时发生、改动了什么。多个段落需要换行和空间以便反复阅读，这是单行 `Input` 无法提供的。
- **商品与目录描述**：商品编辑输入会在店铺中展示的描述，并按店铺的限制统计字符数。
- **反馈与调查答复**：一个简短的自由文本问题，例如“什么会让导出更快？”，并附带在用户提交时检查的最小长度规则。
- **账户或发票上的内部备注**：计费人员记录退款获批的原因。备注随记录一起保存，备注较长时，可调整大小的字段会更有帮助。

## 何时不用

- **单行内容，例如姓名、邮箱或主题**：用 `Input`，它有相同的消息行，且无需管理换行。
- **机密**：用 `PasswordField`；当用户选择新密码时用 `PasswordStrength`。
- **带标题、列表、链接或强调的文本**：用 `RichTextEditor`。`Textarea` 只保存纯文本。
- **在页面上原位编辑标题或简短描述**：用 `InlineEdit`，它在编辑前看起来就是普通文字。
- **由简短值组成的列表，例如标签或收件人**：用 `TagInput`，它会把每一项变成一个词元。

## 用法

按 Enter 会添加一行。字段填满所在列，并且至少 110 px 高。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:Textarea Label="个人简介"
                  Placeholder="用一两句话介绍你自己"
                  Description="还剩 160 个字符"
                  MaxLength="160"
                  Width="420"
                  HorizontalAlignment="Left" />
</UserControl>
```

`Textarea` 派生自 Avalonia `TextBox` 之上的共享 Arc 字段基类。它开启 `AcceptsReturn`，并按外框宽度换行，因此换行就是普通的 Enter。`Text` 是双向的，`MaxLength` 在上限处阻止继续输入；`IsReadOnly`、光标、选择、撤销、输入法和剪贴板都是原生文本框的行为。字段在此之上加入标签、Arc 外框、辅助文字行和错误行、焦点环以及缩放手柄。

### 结构

1. **标签**（`Label`）：14 px、中等字重，位于外框上方 8 px，与 `Input` 相同。它是字段的无障碍名称。只有当可见标题已经为字段命名时，才用 `IsLabelVisible="False"` 隐藏它。
2. **外框**：1 px 边框、控件圆角、表面色背景，四周各 12 px 内边距。悬停与聚焦只加深边框。聚焦的字段会在边框外绘制 3 px 焦点环。
3. **文字与占位文字**（`Placeholder`）：文字在外框内宽度处换行。字段为空时，占位文字以弱化文字颜色显示。
4. **最小高度**：外框在所有尺寸下至少 110 px 高。外框不会随内容增长；当文字长于外框时，文字在其中滚动。
5. **缩放手柄**（`IsResizable`）：位于右下角的 12 px 区域，带两条短的斜线，拉入内边距中，使其紧贴边框。当 `IsResizable` 为 false 或字段被禁用时隐藏。
6. **辅助文字行**（`Description`）：外框下方 12 px、弱化颜色的辅助文案。“还剩 120 个字符”之类的计数显示在这里。
7. **错误行**（`ErrorMessage`）：12 px、危险色文案。只要它有内容，字段即处于无效状态。

### 选择高度

外框从 110 px 开始，并保持该高度，直到用户拖动手柄或你的代码设置 `FrameHeight`。手柄会以像素写入所选高度；低于 110 px 最小值的任何值都会被提升到最小值。

- **表单列中的简短备注**（评论、变更原因）：保留 110 px 最小值并保持手柄开启，使用户无需单独的页面即可为较长的备注腾出空间。
- **多段落的回复和摘要**：将 `FrameHeight` 设为约 200，增加初始高度，使第一屏就能显示整份草稿。
- **固定布局，例如仪表板卡片或侧边面板**：设置 `IsResizable="False"`，使卡片保持其几何尺寸。此时字段会滚动其文字。
- **必须保留用户所选尺寸的字段**：以 `Mode=TwoWay` 绑定 `FrameHeight`，并将该值保存在视图模型或用户设置中。`FrameHeight` 默认为单向。
- **紧凑卡片中的矮输入框**（评分下方的可选备注、一行提示词）：用 `FrameMinHeight` 降低最小值，它是外框连同边框和内边距的高度。默认尺寸下两行文字需要 66 px，三行需要 85 px。之后手柄也不会拖到新的最小值以下。

```xml
<arc:Textarea PlaceholderText="Add details (optional)" FrameMinHeight="66" IsResizable="False" />
```

### 绑定到视图模型

视图模型拥有回复及其规则。它统计字符数，在提交时报告错误，并在回复变化时清除错误。

```csharp
public partial class ReplyViewModel : ObservableObject
{
    private const int MaxReplyLength = 500;

    [ObservableProperty]
    private string _replyText = string.Empty;

    [ObservableProperty]
    private string? _replyError;

    public string CountText => $"还剩 {MaxReplyLength - ReplyText.Length} 个字符";

    partial void OnReplyTextChanged(string value)
    {
        ReplyError = null;
        OnPropertyChanged(nameof(CountText));
    }

    [RelayCommand]
    private void SendReply()
    {
        ReplyError = ReplyText.Trim().Length < 40
            ? "请再补充一些细节：至少 40 个字符。"
            : null;
        if (ReplyError is not null)
        {
            return;
        }
        // 在此处把回复发送到工单服务。
    }
}
```

```xml
<StackPanel Spacing="16">
    <arc:Textarea Label="回复客户"
                  Text="{Binding ReplyText}"
                  Description="{Binding CountText}"
                  ErrorMessage="{Binding ReplyError}"
                  MaxLength="500"
                  Width="520"
                  HorizontalAlignment="Left" />
    <arc:Button Content="发送回复"
                Command="{Binding SendReplyCommand}"
                HorizontalAlignment="Left" />
</StackPanel>
```

`Text` 是双向的。`Description`、`ErrorMessage`、`IsResizable` 和 `IsEnabled` 是单向的。`FrameHeight` 除非如上所述以 `Mode=TwoWay` 绑定，否则也是单向的。该控件没有自己的命令；发送操作是视图模型上的命令。

### 实时计数与校验

随着文字变化更新描述。只有变化的数字会滚动。设置 `ErrorMessage` 可将字段标记为无效；值为空时清除错误。

<Demo name="Validation" />

```csharp
const int limit = 160;
bio.TextChanged += (_, _) =>
{
    var length = bio.Text?.Length ?? 0;
    bio.Description = $"还剩 {limit - length} 个字符";
    bio.ErrorMessage = length < 40 ? "再多写一点吧：至少 40 个字符。" : null;
};
```

`MaxLength` 会在上限处阻止用户继续输入，但不显示任何内容，因此计数必须来自你的代码，如上所示。请按照店铺或服务端计算长度的方式计数：`TextBox` 按 UTF-16 字符计数，因此一个表情符号占两个字符。

校验规则与 `Input` 遵循相同的模式：在用户能够处理某条规则的时刻检查它。

| 规则 | 何时检查 | 显示在哪里 |
|---|---|---|
| 最大长度 | 输入时；`MaxLength` 阻止继续输入 | `Description` 倒数计数；无需错误 |
| 回复的最小长度 | 提交时，之后在回复仍然过短时 | `ErrorMessage`；把焦点移到该字段 |
| 必填 | 提交时 | `ErrorMessage` |
| 服务端的内容检查，例如屏蔽词 | 请求返回后 | `ErrorMessage`，文字变化时清除 |

### 绑定文本

`Text` 是双向属性。将其绑定到视图模型中的字符串。

```xml
<arc:Textarea Label="反馈"
              Text="{Binding Feedback}"
              ErrorMessage="{Binding FeedbackError}" />
```

按 Enter 会在 `Text` 中存入换行符。在 Windows 上，换行可能存为 `\r\n`，因此在比较两个值或把文本保存到需要 `\n` 的系统之前，请统一换行符。

### 缩放

拖动右下角手柄可使字段变高。高度从最小值开始，且不跟随内容；字段会改为滚动。使用 `IsResizable` 关闭手柄。

```xml
<arc:Textarea Label="备注"
              IsResizable="False"
              Text="固定高度。" />
```

用户拖动手柄时，`FrameHeight` 保存所选的高度（以像素为单位）。在代码中设置它，可使字段以指定高度开始。

```csharp
notes.FrameHeight = 200;
```

### 禁用

禁用的字段透明度降为 0.6，并且没有手柄。

<Demo name="Disabled" />

### 示例：支持回复表单

支持控制台中的回复表单。客服人员已写了 280 个字符的回复，上限为 500，因此计数显示还剩 220 个字符。字段比最小高度略高，因为回复通常是几句话，而右下角的手柄表明字段可以更高。字段下方的按钮使用客服人员需要的动词和对象：保存草稿，或发送回复。截图是稳定状态：没有错误，计数已定，文字适合外框。

![带实时计数和缩放手柄的支持回复表单](images/SupportReply.png)

```xml
<StackPanel Width="600" Spacing="20">
    <TextBlock Text="Reply to Northwind Logistics"
               FontSize="18"
               FontWeight="SemiBold"
               Foreground="{arc:ArcDesignTokenResource Foreground}" />
    <arc:Textarea Label="Reply"
                  Text="Thanks for the detail. The export stopped at 2 million rows because the workspace reached its row limit. We raised the limit to 5 million rows on your plan, so the export should now finish. If the file is still incomplete tomorrow morning, reply here and we will rerun it for you."
                  Description="220 characters left"
                  MaxLength="500"
                  FrameHeight="180" />
    <StackPanel Orientation="Horizontal"
                HorizontalAlignment="Right"
                Spacing="12">
        <arc:Button Content="Save draft" Variant="Secondary" />
        <arc:Button Content="Send reply" />
    </StackPanel>
</StackPanel>
```

### 文案规范

命名内容，而不是控件。标签说明备注是为了什么，错误说明需要补充什么。

| 不要写 | 改为 |
|---|---|
| 备注 | 退款原因 |
| 在此输入文字 | 描述出现错误前你尝试过的操作 |
| 输入无效 | 再多写一些细节：至少 40 个字符。 |
| 最多 500 | 还剩 220 个字符 |
| 提交 | 发送回复 |
| 此字段必填 | 请填写退款原因。 |

标签和消息使用句子式大小写，计数使用阿拉伯数字，每条错误使用一个完整的句子。描述中的计数在各处都使用相同的措辞，使回复在每个界面上都以同样的形式显示“还剩……个字符”。

### 最佳实践

**推荐**

- 在接近上限之前就显示计数，并按服务端的计数方式计算。能看到剩余空间的用户会在保存被拒绝之前缩短文字。
- 使用 `MaxLength` 作为硬性上限，使用描述作为反馈。只有 `MaxLength` 不会给出任何反馈，用户只会在文字停止变化时才得知存在上限。
- 短备注从 110 px 最小值开始，长内容则设置 `FrameHeight`。一个很高的空字段会让简短的表单显得未完成。
- 用户离开页面时，将草稿保留在视图模型中。多行草稿是最难重新输入的值。
- 在比较或保存文本之前统一换行符。Windows 的 `\r\n` 换行与其他系统的 `\n` 换行，否则会被当作不同的值。

**避免**

- 期望字段随内容增长。它会滚动，因此用户无法一次看到整份草稿；请给它一个适合任务的高度。
- 在用户仍在写第一句话时就显示最小长度错误。这读起来像是在指责用户；应在失去焦点或提交时检查。
- 用 `Textarea` 填写单行内容，例如姓名或主题。Enter 会添加一行，粘贴的文字也会保留其换行。
- 在用户需要阅读长文本的表单中关闭 `IsResizable`。字段将只有最小高度，文字会在一个小窗口中滚动。
- 依赖手柄服务键盘用户。手柄只响应指针，因此应使字段的高度在不缩放时也能正常工作。

### 常见问题

**为什么我输入更多行时字段高度保持不变？** 它保持高度，以免表单布局移动。文字在外框内滚动。让用户拖动手柄，或将 `FrameHeight` 设为任务所需的高度。

**为什么 `FrameHeight` 为 60 时字段仍是 110 px？** 低于 110 px 最小值的任何高度都会被提升到最小值。最小值适用于每个 `Size`，因此 `Small` 不会使字段变矮；需要更矮的字段时降低 `FrameMinHeight`。

**用户按 Tab 时字段会插入制表符吗？** 不会。`AcceptsTab` 为 false，因此 Tab 会把焦点移到下一个控件。只有当字段是代码或表格编辑器、且制表符本身就是值时，才设置 `AcceptsTab="True"`。

**键盘用户能调整字段大小吗？** 不能。手柄只响应指针。请在代码中设置 `FrameHeight`，或选择在最小高度下也能正常工作的固定布局。

## API 参考

<!-- api: Textarea -->

## 无障碍

- `Label` 为字段命名。`Description` 与 `ErrorMessage` 会合并为它的帮助文字。
- 错误出现时会以 assertive 实时区域立即播报。动画文案对辅助技术隐藏，辅助技术读取的是纯文本版本。
- 按 Enter 会插入换行。光标、选择、输入法、撤销和剪贴板快捷键都是原生 `TextBox` 的行为。
- 聚焦时会在外框周围绘制 3 px 焦点环。
- 缩放手柄只响应指针。键盘用户保持最小高度并滚动文字，因此布局在 110 px 下也必须可用。当内容需要更多空间时，请在代码中设置 `FrameHeight`，使所有人都能获得足够的空间。

### 键盘

| 按键 | 操作 |
|---|---|
| 文本按键 | 输入文字；`Enter` 插入换行 |
| `Up` / `Down` | 在行之间移动光标 |
| `Home` / `End`，`Ctrl+Home` / `Ctrl+End`（macOS 上为 `Cmd`） | 移到行首或行尾，或移到文字的开头或结尾 |
| `Shift` + 以上任一按键 | 扩展选区 |
| `Ctrl+A`、`Ctrl+C`、`Ctrl+X`、`Ctrl+V`、`Ctrl+Z`、`Ctrl+Y`（macOS 上为 `Cmd`） | 全选、复制、剪切、粘贴、撤销与重做 |
| `Tab` / `Shift+Tab` | 将焦点移到下一个或上一个控件；`AcceptsTab` 保持为 false，因此 Tab 不会插入制表符 |

## 动效

- 辅助文字行与错误行以平滑弹簧展开高度。变化的词带着柔和模糊升起。
- 字符计数只滚动变化的数字位。
- 拖动手柄会直接设置高度，因此外框无需过渡即可跟随指针。
- 减弱动效时，弹簧、数字滚动和模糊都会移除，文案立即改变。关闭动效时同样如此。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 消息行出现或消失 | 高度以平滑弹簧展开，文字淡入 | 以完整高度直接显示 | 立即完成 |
| 消息变化 | 只有变化的词带着柔和模糊从下方升起 | 瞬时淡入 | 立即完成 |
| 计数中的数字变化 | 只有变化的数字滚动，增大时向上，减小时向下 | 瞬时淡入 | 立即完成 |
| 悬停、聚焦、无效 | 边框颜色渐变 | 边框颜色渐变 | 立即完成 |

## 相关组件

- `Input`：单行文本。
- `InlineEdit`：无可见外框、原位编辑值。
- `TagInput`：由简短值组成的列表，而不是自由文本。
- `RichTextEditor`：需要标题或列表的文本。

| 组件 | 适用场景 |
|---|---|
| `Input` | 值只有一行，例如姓名、邮箱或主题 |
| `RichTextEditor` | 文本需要标题、列表、链接或强调 |
| `InlineEdit` | 标题或简短描述在显示位置直接重命名 |
| `TagInput` | 值是由简短词元组成的列表，例如标签或收件人 |
| `PasswordField` / `PasswordStrength` | 值是机密 |
| `SearchField` | 文字用于筛选列表，而不是保存 |
