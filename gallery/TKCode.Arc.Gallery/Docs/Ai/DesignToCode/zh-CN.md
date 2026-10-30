---
title: Design to code
summary: 截图转代码卡片：扫描线找出拖入的截图或设计文件画框中的布局区域，随后代码在旁边流式写出，并与区域一一对应，切换一个标签页即可看到实时预览。
source-hash: 0ad3fab6e8cd
---

<Demo name="Preview" />

## 何时使用

`DesignToCode` 是“截图转代码”功能的卡片。用户把界面截图拖到设计一侧，或者从设计文件导入一个画框。一条扫描线扫过截图，
它找到的每个布局区域都以带标签的方框出现。随后代码在旁边按区域逐段流式写出：正在编写的区域在截图上高亮，对应的代码行带有淡淡的底色。
指向方框或标签会点亮它的代码行，指向某一行代码也会点亮它的方框。切换一个标签页，“预览”就会展示可以实际操作的结果。

- **设计转代码功能**：上传一张截图或选择设计文件中的一个画框，得到一个组件。
- **展示 AI 视觉步骤**：当模型识别出了什么很重要时，例如生成之前的布局解析。
- **营销与新手引导**：一眼说明代码生成产品在做什么。给卡片一个 `Simulation`，它就会在可见期间循环播放一个设计及其代码。

## 何时不用

- **没有生成步骤的普通文件上传**：使用文件上传组件。
- **只展示完成的代码而不需要源设计**：使用 `CodeBlock`。
- **带文件、标签页和终端的完整编辑器**：搭建一个工作区布局，而不是把这张卡片撑大。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:DesignToCode />
</UserControl>
```

`DesignToCode` 是 Special 包中的 `TemplatedControl`。在 `UseArcControls()` 旁边用 `UseArcSpecial()` 注册这个包。
卡片填满容器的宽度；请把它放在页面或对话框中，宽度保持在 360 到 760px 之间。

既没有 `Generator` 也没有 `Simulation` 时，卡片以拖放区打开：拖入截图、选择文件或导入设计文件。接入 `Generator` 把设计转成代码，或者给卡片一个 `Simulation` 来播放。

### 模拟

`DesignToCodeSimulation` 是一个设计以及模型可能为它写出的代码：`Source`（一张图像，或用代码绘制的草图）、扫描找到的 `Regions`、
分段的代码 `Parts`（每个 `DesignToCodeSimulationPart` 标注它实现的区域），以及可选的署名模型 `Model`、`FileName`、模拟导入时带入的
`FrameName`、`PreviewFactory`，和循环时对预览调用的 `PreviewAction`。没有 `Generator` 时，`Source` 未设置期间卡片显示它的设计，
“Use sample” 会把它带回来。这次运行是模拟的：不会上传或发送任何内容，朗读的提示也会说明这一点。卡片在屏幕上时会循环播放（生成、
停留在代码上、显示预览、重新开始），直到卡片内第一次出现指针按下或按键。把 `Simulation` 设为 null 会停止模拟运行并回到拖放区。

```csharp
card.Simulation = new DesignToCodeSimulation
{
    Source = new DesignToCodeSource("pricing-card.png", screenshot),
    Regions = [new DesignToCodeRegion("price", "Price", 7.5, 42.5, 52, 14.5)],
    Parts =
    [
        new DesignToCodeSimulationPart("export function PricingCard() {\n  return (\n"),
        new DesignToCodeSimulationPart("    <p className=\"text-4xl\">$24</p>\n", "price"),
        new DesignToCodeSimulationPart("  );\n}\n"),
    ],
    Model = new AgentModel("Your model"),
    FileName = "PricingCard.tsx",
};
```

设计也可以用代码绘制，而不是提供图像：给来源设置 `Sketch`（一个 `DesignToCodeSketch`，用卡片的 `DesignToCodeSketchPalette`
在设计的矩形中绘制，因此会跟随浅色和深色主题）以及它的 `SketchSize`。本页的演示就是这样绘制一张小型价格卡片，所有内容都来自
`DesignToCodeDemoData`；署名的模型是真实的（核对于 2026-10-29），方案和价格是虚构的。

### 结构

1. **设计面板**：标题栏显示设计的名称、“Sample”或“Design file”标签以及“移除”按钮；下方是带区域方框和标签的截图，
   没有设计时则是拖放区。
2. **区域方框与标签**：每个区域一个 1px 的强调色轮廓，以及一个与其顶边对齐、位于其左侧留白中的小标签（没有空间时放在方框内）。
   标签是一个按钮，名称类似“Price: show its code”。
3. **输出面板**：标题栏显示文件名、模型的标记与名称、“代码 / 预览”切换以及“复制”；下方是带行号的代码，或实时预览。
4. **底栏**：状态（图标、简短标签、文件以及类似“52 lines in 3.4s”的附加信息）、“导入设计文件”以及主操作，
   主操作依次显示 Generate code、Stop、Regenerate 或 Try again。

### 一次运行

`Phase` 跟随运行进度：没有设计时为 `Empty`，有设计时为 `Ready`，然后是 `Analyzing`、`Writing` 和 `Done`，或者
`Stopped` 与 `Error`。Generate code（或 `Generate()`）开始一次运行，并把输出切回“代码”。Stop、Escape 或 `Stop()`
结束正在分析或编写的运行；已经写出的代码会保留。`Code` 保存屏幕上的代码，`Completed` 报告完成的代码以及行数和用时。

| 阶段 | 状态 | 主操作 |
|---|---|---|
| Empty | Add a design to start | Generate code（不可用） |
| Ready | Ready pricing-card.png | Generate code |
| Analyzing | Analyzing layout pricing-card.png · 3 regions | Stop |
| Writing | Writing PricingCard.tsx · 5 regions | Stop |
| Done | Done PricingCard.tsx · 52 lines in 3.4s | Regenerate |
| Stopped | Stopped PricingCard.tsx · Partial code kept | Regenerate |
| Error | Generation failed: the model timed out. Try again. | Try again |

### 接入你的模型

为卡片提供 `Generator`：一个接收设计与 `DesignToCodeContext` 的异步委托。每识别出一个布局区域就调用 `AddRegion`
（方框以设计尺寸的百分比表示），然后在代码流式到达时调用 `Write`，并传入该代码块所实现区域的 id，让这些行与方框关联。
完成时结束任务，抛出异常以显示错误，并在上下文的令牌取消时停止。代码块可以是任意大小：卡片会把它们整理成完整的单词逐批显示。

<Demo name="YourModel" />

```csharp
card.Model = new AgentModel("Your model");
card.Generator = async (source, context) =>
{
    await foreach (var e in backend.StreamAsync(source.File, context.CancellationToken))
    {
        if (e.Region is { } region)
        {
            context.AddRegion(new DesignToCodeRegion(region.Id, region.Label, region.X, region.Y, region.Width, region.Height));
        }
        else
        {
            context.Write(e.Text, e.RegionId);
        }
    }
};
card.Completed += (_, e) => drafts.Save(e.Code);
```

`AddRegion` 和 `Write` 可以在任意线程调用。设置了生成器的卡片会以拖放区打开，也不会循环播放；设置 `Source` 即可从某个设计开始。
`Model` 在代码标题栏中注明模型，并显示它的标记（24 × 24 的描边图形）；没有设置时卡片只显示闪光图标（播放模拟时显示模拟的模型）。

### 拖入截图

在设计一侧的任何位置都可以拖放，文件悬停时会显示“Drop to use this design”遮罩。每种拖放方式都有对应的按钮：
Choose file、Use sample 和 Import design file。`AcceptedFileTypes`（写法同输入框的 accept 属性：MIME 类型、`image/*`
或扩展名）与 `MaxFileSize`（10 MB）决定接受哪些文件；其他文件会显示一条说明问题与解决办法的错误，当前显示的设计保持不变。

<Demo name="DropZone" />

Choose file 会引发 `ChooseFileRequested`。在其中打开你的文件选择器，把文件交给 `LoadFileAsync`，并设置 `Handled`；
没有人处理时，卡片会改用模拟的设计（如果有），并说明没有打开任何文件。拖入或加载的文件会变成一个 `DesignToCodeSource`，包含它的名称、
解码后的图像以及 `IStorageFile`，你的生成器可以把它发送到后端。卡片自己解码的图像在被替换时会释放。

```csharp
card.ChooseFileRequested += async (_, e) =>
{
    e.Handled = true;
    var files = await TopLevel.GetTopLevel(card)!.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions());
    if (files.Count > 0)
    {
        await card.LoadFileAsync(files[0]);
    }
};
```

### 设计文件

Import design file 会运行 `DesignFileImporter`，也就是你的选择器或链接流程：返回带渲染图像的画框（`DesignToCodeSource`），
用户取消时返回 null。没有设置时，如果有模拟，卡片会以它的 `FrameName` 导入模拟的设计，并说明没有打开任何文件。没有图像的来源会绘制它的 `Sketch`。

### 实时预览

“预览”标签页展示完成的代码实际运行的样子。`PreviewFactory` 负责渲染，例如放在沙箱化的网页视图中；切勿在你的应用内运行生成的代码。
没有工厂时，模拟运行使用模拟的 `PreviewFactory`（演示渲染价格卡片，Monthly 与 Yearly 会切换价格），其他情况显示一条简短说明。“代码”和“预览”始终保持挂载，因此切换时会保留各自的滚动位置；
每次新的运行都会回到“代码”，让流式输出可见。

### 窄容器

布局跟随卡片自身的宽度。从 640px 起两个面板并排（设计 344px，代码占其余部分）；更窄时上下堆叠，各高 352px，
因此代码流式写出时卡片的尺寸不会变化。低于 520px 时，状态不再显示文件名，“导入设计文件”只显示图标，但保留其名称供辅助技术使用。
低于 440px 时，附加信息和模型名称隐藏，标记保留。较长的代码行以悬挂缩进换行，因此不会出现横向滚动。

<Demo name="Narrow" />

### 减少动态效果

在减少动态效果下没有扫描线，也没有代码块淡入：区域立即出现，代码一次到达一段（一个区域），模拟只播放一次而不再循环。

<Demo name="ReducedMotion" />

### 内容指南

区域标签是贴在小截图上的标签：用一两个词说明这个部分，而不是组件的实现。

| 不要写 | 应写 |
|---|---|
| div.pricing-header-container | Header |
| Segmented control (2 options) | Billing toggle |
| Generation completed successfully | Done |

`Labels` 接受一个 `DesignToCodeLabels` 对象，包含卡片显示或朗读的每一个词。带数值的句子是函数，因此其他语言可以调整语序：

```csharp
card.Labels = new DesignToCodeLabels
{
    Generate = "Write the code",
    DoneMeta = (lines, time) => $"{lines} lines, {time.TotalSeconds:0.0}s",
};
```

### 最佳实践

**建议**

- 先发送区域，再发送标注了所实现区域的代码，让方框与代码行相互关联。
- 在沙箱中渲染预览，并且每个页面或对话框只放一张卡片。
- 上下文的令牌取消时停止你的请求。

**避免**

- 把模拟当作真实的运行展示。它是虚构的，并且会如实说明。
- 为了预览而在应用内运行生成的代码。
- 发送没有 id 的区域：代码块通过 id 与它的方框关联。

### 常见问题

**卡片为什么以一个设计打开？** 没有设置 `Source` 也没有 `Generator` 时，它显示模拟的设计。把 `Source` 设为 null 即可以拖放区打开。

**为什么我的代码行没有关联到方框？** 一行属于它第一个字符所在的区域；请在实现该区域的每个代码块中传入区域的 id。

**Stop 之后到达的代码会怎样？** 会被丢弃。只保留屏幕上的代码，已停止的运行之后的写入都会被忽略。

## API 参考

<!-- api: DesignToCode -->

<!-- api: DesignToCodeSource -->

<!-- api: DesignToCodeRegion -->

<!-- api: DesignToCodeContext -->

<!-- api: DesignToCodeCompletedEventArgs -->

<!-- api: DesignToCodeSimulation -->

<!-- api: DesignToCodeSimulationPart -->

<!-- api: DesignToCodeSketchPalette -->

<!-- api: DesignToCodeLabels -->

枚举：

| 类型 | 值 |
|---|---|
| `DesignToCodePhase` | `Empty`、`Ready`、`Analyzing`、`Writing`、`Done`、`Stopped`、`Error` |
| `DesignToCodeView` | `Code`、`Preview` |
| `DesignToCodeSourceKind` | `Sample`、`File`、`DesignFile` |

## 无障碍

- 卡片名为“Design to code”，包含两个具名分组：Design 与 Output。一个礼貌型实时区域朗读每一步：正在分析、找到的区域、
  完成时的行数与用时、已停止以及错误。模拟会说明没有上传或打开任何内容。
- 状态从不只靠颜色表达：每种状态都配有图标和简短标签，错误会说明问题与解决办法。
- 区域标签是真正的按钮，名称类似“Price: show its code”。代码区是可聚焦的具名区域，代码流式写出时其状态读作“Writing”；
  它不是实时区域，因此屏幕阅读器不会被逐词朗读淹没。
- “代码 / 预览”中隐藏的那一层不可见，也不在 Tab 顺序中。用代码绘制的设计是装饰性的；拖入的图像以文件名命名。
- 每种拖放方式都有按钮：Choose file、Use sample 和 Import design file。

### 键盘

| 按键 | 操作 |
|---|---|
| `Tab` | 依次经过“移除”、区域标签、视图切换、“复制”、“导入设计文件”和主操作 |
| `Enter` / `Space` | 激活获得焦点的控件；在拖放区上，Choose file 请求一个文件 |
| `Left` / `Right` | 在视图切换上于“代码”和“预览”之间切换 |
| `Escape` | 停止正在分析或编写的运行；保留已写出的部分代码 |
| 聚焦某个区域标签 | 高亮该区域的方框及其代码行，并把它们滚动到可见位置 |

## 动效

- 分析：一条 1px 的扫描线用一秒扫过截图，扫描线到达某个区域的顶部时，该区域的方框和标签淡入（160ms 透明度，无缩放）。
- 编写：代码大约每 34ms 以一批完整单词流式写出（按阅读速度每批 3 到 8 个单词）；每批只做 140ms 的淡入。
  正在编写的区域在截图上高亮，它的代码行带有淡淡的底色。
- 状态、占位提示以及“代码”和“预览”两层交叉淡化：旧内容 100ms 淡出，新内容 160ms 淡入。主按钮的宽度跟随标签变化，没有回弹。
- 预览中，计费开关的滑块以 0.24s、无回弹的弹簧滑动，价格以等宽数字做 2px 的交叉淡化切换。
- 卡片不在屏幕上时，模拟的循环会暂停等待。

| 变化 | 完整 | 减少 | 关闭 |
|---|---|---|---|
| 扫描线 | 1s 扫过 | 无 | 无 |
| 区域 | 扫描线到达时淡入 | 立即出现 | 立即出现 |
| 代码 | 按单词分批，逐批淡入 | 一次一段 | 一次一段 |
| 代码与预览 | 交叉淡化 | 短暂交叉淡化 | 立即切换 |
| 模拟循环 | 循环 | 播放一次 | 播放一次 |

## 相关

- `CodeBlock`：带复制功能的完成代码。
- `ToolCall`：智能体运行中的一个步骤。
- `AgentStatus`：正在运行的智能体的紧凑实时状态。

| 组件 | 适用场景 |
|---|---|
| `CodeBlock` | 代码已经完成，设计不是要讲述的内容 |
| `ToolCall` | 生成只是智能体对话记录中的一步 |
| `AgentStatus` | 只关心一次运行的进度 |
