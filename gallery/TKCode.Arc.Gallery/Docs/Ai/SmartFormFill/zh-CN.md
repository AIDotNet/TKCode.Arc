---
title: Smart form fill
summary: 粘贴即填的表单卡片：AI 读取邮件签名或零散笔记，逐个字段填写联系人表单，每个值带置信度圆点，来源在文本中高亮，猜测的值需要复核，并可撤销。
source-hash: 1f6fca89440b
---

<Demo name="Preview" />

## 何时使用

`SmartFormFill` 把粘贴的文本变成一条记录。用户把邮件签名、转发的邮件或笔记粘贴到左侧区域，AI 读取后逐个字段填写表单。每个值淡入时带一个置信度圆点，它所来自的文字在粘贴文本中高亮；猜测的值在保存前需要复核，"撤销"会恢复原来的值。

- **从邮件签名或通话笔记创建联系人或线索**。
- **入职与 CRM 流程**，手动输入六个字段是主要负担。
- **以非结构化文本形式到来的供应商、公司或收货记录**；传入你自己的字段。
- **任何 AI 填写的数据需要先核对再保存的表单**，置信度可见，复核路径简单。

## 何时不用

- **没有非结构化来源的普通录入**：直接使用 `Input` 等字段。
- **很长的多页申请**：使用分步表单，并在其中一页加入粘贴步骤。
- **批量导入大量记录**：使用表格导入流程，而不是一张卡片。

## 用法

通过 `Extract` 把你的模型交给卡片。它接收粘贴的文本和一个 `CancellationToken`，返回 `SmartFormFillResult`：按字段 id 给出一个 `SmartFormFillValue`，包含值、0 到 1 的置信度、该值在文本中的原文子串，以及猜测时说明原因的备注。结果中没有的字段保持原值。`Submit` 保存表单；任务运行期间按钮显示加载状态。

```csharp
card.Extract = async (text, token) =>
{
    // Ask your model for structured output: { field: { value, confidence, source, note } }.
    var contact = await contacts.ExtractAsync(text, token);
    var result = new SmartFormFillResult();
    foreach (var field in contact.Fields)
    {
        result[field.Id] = new SmartFormFillValue(field.Value, field.Confidence, field.Source, field.Note);
    }
    return result;
};
card.Submit = values => crm.SaveContactAsync(values);
```

没有提取器时，卡片使用模拟读取器：粘贴的文本如果是某个示例的文本，就返回该示例的 `Result`，其他文本由一个基于规则的小型解析器处理（不会发送任何内容）。既没有 `Extract` 也没有 `Simulation` 时，表单一开始是空的，没有 Try sample 按钮，也不显示模型。

### 模拟

想在没有模型的情况下展示卡片（演示、设计评审、新手引导），就给它一个 `SmartFormFillSimulation`：Try sample 背后的示例，每个都带有读取时返回的结果（`SmartFormFillSample.Result`），以及要显示的模型。动效为完整时，卡片会自动循环演示（粘贴、填写、核对、撤销），直到有人在卡片内点击或输入。你自己的 `Extract` 优先于模拟；把 `Simulation` 设为 null 会停止模拟并清除它填写的内容。产品界面中请设置 `AutoPlay="False"`。

```csharp
card.Simulation = new SmartFormFillSimulation
{
    ModelName = "DeepSeek-V4.1-Flash-1017",
    Samples =
    [
        new SmartFormFillSample("Email signature", signature)
        {
            Result = new SmartFormFillResult
            {
                ["name"] = new SmartFormFillValue("Maya Okafor", 0.98, "Maya Okafor"),
                ["email"] = new SmartFormFillValue("maya.okafor@example.com", 0.99, "maya.okafor@example.com"),
            },
        },
    ],
};
```

上方的预览播放的就是一个模拟，其文本和结果来自图库自己的演示数据。

### 结构

1. **标题区**：标题及其下方的一行说明（`Title`、`Description`；说明为空时隐藏这一行）。
2. **粘贴区**：标题行显示负责读取的模型（"Read by DeepSeek-V4.1-Flash-1017"），然后是粘贴框，以及 Try sample、Edit text 和 Fill。开始读取后，粘贴框变为粘贴的文本，每个字段的来源都在其中高亮。
3. **表单**：按顺序排列的字段，两个半宽字段占一行。每个字段有标签、置信度标记（圆点加百分比，或 Review、Checked、Edited）、一个 Arc `Input`；猜测的值还有警告色描边、核对按钮和其下方的原因。公司字段在开头显示公司的标志或首字母。
4. **底栏**：带图标的状态，然后是"撤销"和提交按钮。

### 读取与填写

在粘贴框中粘贴（Cmd/Ctrl+V）会立即开始读取；Cmd/Ctrl+Enter 或 Fill 读取手动输入的文本；Try sample 粘贴下一个示例。读取期间，状态以微光显示 "Reading with DeepSeek-V4.1-Flash-1017"，Fill 变为 Stop。Stop 或 Escape 会取消提取器的令牌，什么都不会改变。

结果到达后，字段按表单顺序每隔 140ms 填写一个。正在填写的字段，其来源在粘贴文本中显示更深的底色；之后悬停或聚焦某个字段也会如此，让因果关系在卡片上一目了然。一次读取的所有字段都填好后触发 `Filled`；每次值变化（无论输入还是填写）都触发 `ValuesChanged`。

```csharp
card.Filled += (_, e) => analytics.Track("contact-filled", e.FilledCount, e.ReviewCount);
card.ValuesChanged += (_, e) => draft.Save(e.Values);
```

`ReadAsync(text)`、`Stop()`、`TrySampleAsync()`、`Check(id)`、`Undo()` 和 `SubmitAsync()` 在代码中完成同样的操作；`GetFieldValue` 和 `SetFieldValue` 读写单个字段。

<Demo name="Extractor" />

### 复核与撤销

置信度低于 `ReviewThreshold`（默认 0.8）的值需要复核：标记显示 Review，输入框外有一圈警告色描边，下方显示原因，末尾有一个核对按钮。核对它或修改其值即可结束复核。仍有字段需要复核时无法保存：卡片会说明还剩几个并聚焦第一个。"撤销"恢复上一次填写之前的值，并以这些值触发 `Undone`。

### 自定义字段

<Demo name="CustomFields" />

`Fields` 替换默认的联系人表单。每个字段有 `Id`（值和结果中的键）、`Label`、`Kind`（`Text`、`Email`、`Phone`、`Url` 或 `Company`，后者带标志位）、`Placeholder` 和 `Span`（`Half` 或 `Full`）。`DefaultValues` 在读取之前预先填写字段。`Samples` 设置 Try sample 背后的文本（为每个示例提供 `Result`，供模拟读取器返回）；没有示例时会隐藏该按钮并关闭自动演示。

```xml
<arc:SmartFormFill Title="New vendor" SubmitLabel="Add vendor" AutoPlay="False">
    <arc:SmartFormFillField Id="company" Label="Vendor" Kind="Company" Span="Full" />
    <arc:SmartFormFillField Id="email" Label="Billing email" Kind="Email" />
</arc:SmartFormFill>
```

`ResolveLogo` 为公司字段提供你自己的标志：按公司名返回一个控件，返回 null 则显示首字母。`ModelName` 和 `ModelMark` 指定粘贴区旁显示的模型。

### 窄栏

<Demo name="Narrow" />

卡片跟随自身宽度，而不是窗口宽度。内容宽度达到 720px 时粘贴区位于表单左侧；更窄时粘贴区以固定高度叠在表单上方。低于 440px 时每个字段独占一行，底栏把状态放在操作按钮上方。宽度为 360px 时也不会出现横向滚动。

### 自定义文案

`Labels` 包含卡片显示或朗读的其他所有文字；替换整个对象即可本地化或改写：

```csharp
card.Labels = new SmartFormFillLabels
{
    TrySample = "Beispiel",
    Fill = "Ausfüllen",
    Reading = model => $"{model} liest",
    Filled = (filled, review) => $"{filled} Felder ausgefüllt",
};
```

### 内容指南

- `Description` 用一句平实的话说明要粘贴什么。
- 备注简短具体："Two numbers in the text; picked the mobile."，而不是"置信度低"。
- 返回原文中的确切子串；改写过的来源无法高亮。

### 最佳实践

- 把 `ReviewThreshold` 保持在 0.8 左右，让推断出的州名、改写过的职位、多个电话号码中选出的一个被标记出来，而不是被悄悄保存。
- 把令牌传给你的 HTTP 调用，这样 Stop、Escape 和新的粘贴才能真正取消请求。
- 在产品界面中关闭 `AutoPlay`；循环演示是为文档和营销页面准备的。

### 常见问题

**用户可以手动输入而不是粘贴吗？** 可以。每个字段都是普通的输入框；在粘贴框中输入的文本可以用 Fill 或 Cmd/Ctrl+Enter 读取。

**读取之前用户已输入的值会怎样？** 一次读取只修改它返回的字段；"撤销"会恢复读取之前的内容。

## API 参考

<!-- api: SmartFormFill -->

<!-- api: SmartFormFillField -->

<!-- api: SmartFormFillValue -->

<!-- api: SmartFormFillSample -->

<!-- api: SmartFormFillSimulation -->

<!-- api: SmartFormFillLabels -->

## 无障碍

- 底栏状态是一个礼貌的实时区域：会播报读取中、填写了多少字段及其中多少需要复核、核对、撤销和保存。使用内置读取器时会附加 "Simulated"。
- 每个已填写的输入框都描述其置信度（"Filled by AI, 94% confident"）；猜测的值还会给出原因，并在核对或修改之前报告 Review 状态。
- 状态从不只靠颜色表达：置信度显示为圆点加百分比或文字，底栏的图标总是配有文字。
- 需要复核的字段内的核对按钮以字段命名（"Mark Phone as checked"）。保存会聚焦第一个仍需复核的字段。
- 粘贴文本视图是一个可聚焦、带标签的区域（"Pasted text"），键盘用户可以滚动它；键盘聚焦时会在其周围显示焦点环。

### 键盘

| 按键 | 操作 |
|---|---|
| Cmd/Ctrl + V | 粘贴到文本框；立即开始读取 |
| Cmd/Ctrl + Enter | 读取文本框中输入的文本 |
| Escape | 停止正在进行的读取；什么都不会改变 |
| Tab | 依次经过文本框、Try sample、Fill、每个字段、核对按钮、撤销和保存 |
| Enter、Space | 按下获得焦点的按钮 |

## 动效

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 值到达 | 按表单顺序，每隔 140ms 一个 | 全部同时 | 全部同时 |
| 一个值落位 | 200ms 淡入；输入框泛起 900ms 的强调色底色；没有位移 | 立即，没有底色 | 立即 |
| 文本中当前的来源 | 高亮在 160ms 内变为完整底色 | 相同 | 立即 |
| 标记、标志与状态 | 旧内容 100ms 淡出，新内容 160ms 淡入并上升 2px | 短暂交叉淡化 | 立即 |
| 读取标签 | 平缓的微光 | 静止 | 静止 |
| 复核描边 | 160ms 淡入 | 立即 | 立即 |
| 自动演示循环 | 卡片在屏幕上时播放 | 从不播放 | 从不播放 |

没有任何布局动画：值、标记和高亮都在原位变化，所以卡片填写时不会跳动。卡片离开屏幕时循环暂停，卡片被移除时正在进行的读取会被取消。

## 相关组件

- `Input` 和 `Textarea`：卡片所用的字段。
- `ToolCall`：展示智能体对工具做了什么。
- `ContextWindow`：展示一段对话占用了模型上下文的多少。
