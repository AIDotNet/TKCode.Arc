---
title: 文档抽取
summary: 把文档转为类型化数据，并显示每个值的来源：安静的文档查看器旁边是一个可编辑的表单，字段随模型阅读逐个填入，并带有着色的原文、曲线连线、低置信度复核，以及类型化的 JSON 结果。
source-hash: 9e50c2c8b02b
---

<Demo name="Preview" />

## 何时使用

文档抽取用于人必须确认模型从文档中读到了什么的时刻。复核者可以同时看到文档和抽取出的数据，准确看到每个值来自哪里，并在数据继续流转之前决定要修正什么。

- **发票、收据和订单录入系统**：模型阅读时字段逐个填入，明细表格逐行增长。
- **邮件与消息分拣**：同一个查看器阅读一封邮件，并提取出订单、承运商和日期。
- **查看模型的疑虑**：置信度低于阈值的值带有模型给出的理由和 "Looks right"（看着没问题）按钮；Review（复核）按钮会跳到下一个。
- **把类型化数据交给你的代码**：日期以 ISO 文本形式给出，金额为数字，IBAN 不含空格；与其类型不符的值为 null，并会阻止 "Accept all"（全部接受）。

## 何时不用

- **围绕文档的对话**：使用 `AiChat` 或 `AiAnswer`。
- **阅读很长的文档**：查看器一次显示一页，表单适合几十个字段。
- **在大量文档上训练或评估模型**：这是人对一份文档的复核。
- **展示文件**：查看器绘制的是你提供的文档模型，而不是 PDF 文件。

## 用法

`DocumentExtraction` 派生自 Avalonia 的 `TemplatedControl`，由数据驱动。为它提供 `Samples`（每个都包含一份文档及其模式）、用于页脚的 `Model`，以及调用你的模型的 `Extractor`。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:DocumentExtraction x:Name="Extraction" MaxWidth="1080" />
</UserControl>
```

```csharp
extraction.Samples.Add(new ExtractionSample { Document = invoice, Schema = invoiceSchema });
extraction.SelectedSample = extraction.Samples[0];
extraction.Model = new ExtractionModelInfo { Name = "My model", Id = "my-model", InputPricePerMillion = 0.12m, OutputPricePerMillion = 0.60m };
extraction.Extractor = new MyExtractor();
extraction.Accepting += (_, e) => e.Defer(SaveAsync(e.Output.ToJson()));

public sealed class MyExtractor : IDocumentExtractor
{
    public async IAsyncEnumerable<ExtractionEvent> ExtractAsync(
        ExtractionDocument document, ExtractionSchema schema, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // Send document.ToPlainText() and schema.ToJsonSchema() to your model, then report what comes back.
        await foreach (var found in model.ReadAsync(document.ToPlainText(), schema.ToJsonSchema(), cancellationToken))
        {
            yield return new ExtractionValueEvent(found.Path, found.Value, found.SourceId, found.Confidence, found.Note);
        }
    }
}
```

### 文档

`ExtractionDocument` 是一个名称加上一组块，绘制在一张白纸上（邮件则绘制为邮件页）：页眉、标题、段落、地址行、键值对、一组带标签的单元格、栏、表格、合计、分隔线和页脚。文本由文本段和 `DocumentSpan` 组成：片段是值可以指回的一段文本，其 id 由抽取器以 `SourceId` 引用。

### 模式

`ExtractionSchema` 列出要查找的字段：键、标签和类型（`Text`、`Date`、`Money`、`Currency`、`Iban`、`Integer`、`Number`）。表格字段有列，其值以路径 `key.row.column` 报告。字段归属于分组；每个分组依次取四种系列颜色之一，同一颜色同时为圆点、字段行、被引用的文字和连线着色。没有分组时，每个字段有自己的颜色。

<Demo name="Custom" />

### 一次运行

Extract（抽取）会读取文档：状态行闪烁显示 "Reading the document"（正在读取文档），一道扫光掠过页面，然后值一个接一个落入。每个落入的值填充其字段，为它来自的文字着色，并在页脚抬起一根条。查看器会跟随这些值，直到你手动滚动。按钮变为 Stop（停止）；停止会保留已找到的值。失败的运行同样保留它们，并提供 Retry（重试）。再次 Extract（抽取）会从头开始。

- **事件**：抽取器为每个值产出 `ExtractionValueEvent`（文字按文档原样写出，例如 "1,400.00"；日期为 `YYYY-MM-DD`），为费用产出带有词元总数的 `ExtractionUsageEvent`，失败时产出 `ExtractionErrorEvent` 或抛出异常。通过取消令牌取消。
- **费用与耗时**：页脚显示模型、根据词元总数和价格计算的费用，以及耗时。

### 复核

- **连线**：悬停或聚焦某个字段会点亮页面上的对应文字，并画出一条曲线连线；指向页面上的文字会点亮其字段，点击它则聚焦该字段。在窄容器中面板上下堆叠，不绘制连线。
- **低置信度**：低于 `ReviewThreshold`（0.8）的值会显示警告、模型的理由和 "Looks right"（看着没问题）。状态行中的 Review（复核）会跳到第一个这样的值。
- **编辑**：每个字段都是真正的输入框。编辑会把值标记为 "Edited"（已编辑）、清除其低置信度标记，并更新 JSON 页签，其中已编辑的行会闪烁。与类型不符的值会显示提示，在输出中为 null，并阻止 Accept all（全部接受）。
- **页签**：Fields（字段）、JSON（类型化输出，与字段相连）和 Schema（发送给模型的 JSON 模式）。

<Demo name="Narrow" />

### 接受

Accept all（全部接受）会以类型化输出触发 `Accepting`。用 `e.Defer(task)` 取得延迟，以异步保存；设置 `Cancel` 或让任务出错即可撤销接受。保存成功后，每个字段都显示为已接受。之后编辑某个值，只会使该值回到未接受状态。

### 缩放

查看器可以在 40% 到 200% 之间按档位缩放，使用 Ctrl 或 Cmd 加上加号、减号和 0 键。Fit（适应宽度）让页面宽度与窗格相同。缩放、`IsFitWidth` 和页签都是双向属性。

### 文案

所有文字都来自 `Labels`（`DocumentExtractionLabels`）：要翻译或改写，替换该实例即可；带格式的文字为函数。

### 内容指南

- **字段标签**：简短的名词，例如 "Invoice no."（发票编号）。
- **描述**：说明要查找什么；描述会在 JSON 模式中发送给模型。
- **理由**：抽取器的备注应用一句话说明它为什么不确定。

### 最佳实践

- 为每个能在文档中找到位置的值引用一个 `SourceId`，这样文字可以着色并被连线。
- 报告真实的置信度；复核流程依赖于它。
- 传递取消令牌，使 Stop 和新的运行真正停止模型。
- 在 `Accepting` 中保存，保存失败时抛出异常。

### 常见问题

**为什么没有连线？** 连线只在并排布局中绘制，并且仅当两端都可见时才绘制。

**为什么 JSON 中某个值为 null？** 它的文字与其类型不符，例如不是 `YYYY-MM-DD` 的日期。

**平均置信度计算什么？** 模型找到的每一个值，包括表格单元格。

## API 参考

<!-- api: DocumentExtraction -->

<!-- api: ExtractionDocument -->

<!-- api: ExtractionSchema -->

<!-- api: ExtractionValue -->

### 事件

| 事件 | 触发时机 | 参数 |
|---|---|---|
| `ExtractionStarted` | 一次运行开始 | |
| `ExtractionCompleted` | 一次运行结束：完成、停止或失败 | `Result`, `Error` |
| `ValueChanged` | 值被模型或人修改 | `Path`, `OldText`, `NewText`, `Origin` |
| `ValueChecked` | 人将值标记为 "Looks right"（看着没问题） | `Path` |
| `Accepting` | Accept all（全部接受）需要保存输出；设置 `Cancel` 或让延迟出错即可撤销 | `Output`, `Values`, `Cancel`, `Defer` |
| `Accepted` | 接受已保存 | |
| `SelectedPathChanged` | 相连的字段发生变化 | `Path` |

### 流式输出与结果

| 类型 | 说明 |
|---|---|
| `IDocumentExtractor` | `ExtractAsync(document, schema, cancellationToken)` 返回一次运行的事件 |
| `ExtractionValueEvent` | 一个值：路径、文字、来源片段 id、置信度和备注 |
| `ExtractionUsageEvent` | 截至目前的词元总数 |
| `ExtractionErrorEvent` | 模型无法完成 |
| `ExtractionOutput` | 按模式顺序排列的类型化结果；`ToJson()` 将其写出 |
| `ExtractionModelInfo` | 模型的名称、id 以及每百万词元的价格 |

## 无障碍

- 查看器是一个以文件名和缩放比例命名的区域，页面是一篇文章，表单的输入框是真正的、有名称的文本框；表格的单元格以列和行命名。
- 置信度用文字朗读（"Low confidence, 64 percent, check this value"，即"置信度低，64%，请检查此值"），状态从不只靠颜色：警告、勾选和编辑都有图标和文字。
- 无效值被标记为无效，并以其提示信息描述；低置信度值以模型的理由描述。
- 两个礼貌式实时区域播报运行（开始、找到多少个值及其中多少个需要检查、停止、失败）和复核（已检查、已接受、有值需要先修正）。失败的运行以强制式方式播报。
- 减少动效时保留每一种状态和每一条播报。

### 键盘

| 按键 | 操作 |
|---|---|
| `Tab` | 文件按钮、缩放按钮、页面、页签、操作按钮、按顺序排列的字段（低置信度字段之后是其 "Looks right"（看着没问题）按钮）、Accept all（全部接受） |
| 字段获得焦点 | 选中它：页面滚动到其文字，并绘制连线 |
| `Escape` | 停止运行；否则清除所选字段 |
| 页面上的 `Ctrl` 或 `Cmd` 加 `+`、`-`、`0` | 放大、缩小、适应宽度 |
| 页面上的 `ArrowUp` / `ArrowDown` | 滚动页面 |
| 页签上的 `ArrowLeft` / `ArrowRight` | 移动并选中 |
| `Enter` / `Space` | 激活按钮，或在菜单中选择文档 |

## 动效

- 值落入时文字和置信度列有短暂的淡入；被引用的文字闪烁一层更深的着色后稳定下来；页脚的条以弹簧升起，平均值与费用滚动更新。
- 表格行以弹簧展开；模型的理由以弹簧展开和收起；状态行以交叉淡化切换，高度跟随变化。
- 连线在五分之一秒内自行绘制，并有一次高光掠过；切换连线时交叉过渡。
- 页面以弹簧绕其中心缩放；扫光在运行开始时横过页面一次。
- Accept all（全部接受）按顺序标记各字段，每个间隔 35 毫秒。
- 减少动效会去掉扫光、连线绘制、弹簧和错落效果，保留交叉淡化；关闭（Off）则立即应用每一处变化。

| 变化 | 完整 | 减少 | 关闭 |
|---|---|---|---|
| 连线 | 绘制并高光 | 出现和消失 | 出现和消失 |
| 落入的值 | 淡入并闪烁 | 淡入 | 立即 |
| 缩放、行、条 | 弹簧 | 立即 | 立即 |
| Accept all（全部接受） | 错开 35 毫秒 | 同时完成，交叉淡化 | 立即 |

## 相关组件

- `ModelArena`：通过模型的回答来比较两个模型。
- `PromptStudio`：编辑抽取背后的提示。
- `AiAnswer`：带有引用来源的回答。

| 组件 | 何时选用 |
|---|---|
| `ModelArena` | 你比较模型，而不是阅读文档 |
| `PromptStudio` | 你维护提示 |
| `AiAnswer` | 结果是带引用的文字 |
