---
title: Image generation
summary: 一张图像生成卡片：四张图像以像素马赛克的方式逐步成形，从粗糙的色块到完整细节，每张都有自己的进度环；选中其中一张即可放大、生成变体或下载。
source-hash: 11e47e45be40
---

<Demo name="Preview" />

## 何时使用

`ImageGeneration` 是一张文生图卡片。用户写下提示词，选择模型和画幅，四张图像便并排以像素马赛克的方式成形：先是粗糙的色块，然后在一道柔和的光带下变成越来越细的色块，最后呈现完整图像，每张都有自己的进度环。用户选中其中一张继续操作：Upscale 把它放大到铺满整个网格，并让一道清晰像素带扫过画面；Vary 以它为基础生成四张新图；Download 把它交给你的应用。

- **产品内的文生图功能**：营销素材、商品图片、幻灯片或文档的插图。
- **任何需要比较几个候选再继续其中一个的流程**（放大、变体、下载）。
- **在后端就绪之前为图像模型集成做原型**，使用一个模拟。

## 何时不用

- **完整的画布编辑器**，带图层、蒙版和局部重绘：请基于绘图表面来构建。
- **只显示一张带加载状态的图像**，没有生成控件：用图像自己的占位符即可。
- **历史结果的图库**：卡片只保存当前这一批；请搭配你自己的素材列表。

## 用法

没有生成器时，卡片播放它的 `Simulation`；两者都没有时，“生成”不可用。`ImageGenerationSimulation` 保存卡片起始的 `Prompt`、可选的 `Models` 和 `Suggestions`，以及一个为提示词绘制图片的 `Painter`（`ImageGenerationPainter`）；卡片在两到三秒内逐帧报告进度，每个图块从绘制的预览中逐步呈现，并在第一次滚动进入视口时自动生成一次。不会下载或发送任何内容。你自己设置的 `Prompt`、`Models` 和 `Suggestions` 优先于模拟中的值，把 `Simulation` 设为 null 会停止它的运行。

```csharp
card.Simulation = new ImageGenerationSimulation
{
    Prompt = "A quiet mountain lake at golden hour",
    Models = [new ImageGenerationModel { Id = "your-model", Name = "Your image model", Mark = ArcIcons.Sparkles }],
    Suggestions = ["A neon city street after rain at night"],
    // Opaque BGRA bytes, width × height × 4; the same prompt and seed should paint the same picture.
    Painter = (prompt, seed, width, height) => MyPainter.Paint(prompt, seed, width, height),
};
```

本页的演示从 `ImageGenerationDemoData` 绘制黄金时刻的湖泊、雨后的城市街道、阳光窗台上的杯子或粉彩色的房间。图库其他 AI 演示所用的模型目录里没有图像生成模型，因此这些图像模型保留虚构的名称。

用 `Generator` 把它连接到你的图像服务。每个格子调用一次，并行执行，用于 Generate、Vary 和 Retry：

```csharp
card.Generator = async (request, context) =>
{
    var job = await images.CreateAsync(request.Prompt, request.Model.Id, request.Aspect.Width, request.Aspect.Height,
        seedFrom: request.VariationOf?.Id, context.CancellationToken);
    await foreach (var step in job.ProgressAsync(context.CancellationToken))
    {
        context.ReportProgress(step.Fraction, step.Preview);
    }
    return new ImageGenerationImage
    {
        Id = job.Id,
        Source = await job.DownloadBitmapAsync(context.CancellationToken),
        Description = request.Prompt,
        Width = request.Aspect.Width,
        Height = request.Aspect.Height,
    };
};
```

以 0 到 1 报告进度。提供预览位图时，马赛克会从你的图像中成形；没有预览时，一个安静的占位网格会闪烁，直到图像到达后淡入；完全不报告时，进度环会自行估算。抛出异常即把该格子标记为失败；Stop 会取消 `context.CancellationToken`。`Upscaler` 对选中的图像以同样的方式工作。

### 结构

1. **提示词**：一个文本框；Cmd/Ctrl + Enter 开始生成。
2. **标签**：模型（选中的显示名称，其余收起为带提示的 16px 标志）、画幅（各带一个小轮廓）以及 Generate，批次运行时它显示为 Stop。
3. **网格**：四个格子保持本批次的画幅，高度不超过 420px。每个运行中的格子有一个玻璃质感的小胶囊，内含进度环和百分比；选中的格子有强调色描边和一个对勾徽章。
4. **底栏**：一行状态，然后是 Upscale、Vary 和 Download。

### 选中、放大、变体与下载

点按一张完成的图像（或聚焦后按 Enter 或 Space）即可选中；再按一次取消选中。Upscale 会把选中的图像从它的格子放大到整个网格，并让一道清晰像素带从左向右扫过：前方柔和，后方清晰。按 Escape 或角落里的按钮回到网格。Vary 以选中的图像作为 `VariationOf` 生成四张新图。Download 触发 `DownloadRequested`，附带图像和一个由提示词、图像编号与尺寸组成的文件名；请在那里保存它。

失败的格子显示警示图标和 "Failed"，被停止的格子显示方块和 "Stopped"；点按任意一个都会重新生成该图像。

### 竖向画幅

<Demo name="Portrait" />

每一批都保持自己的画幅：选择另一个画幅标签只对下一次生成生效。网格以弹簧动画过渡到新画幅，并把高度限制在 420px，因此竖向批次依然紧凑。

### 你自己的生成器

<Demo name="YourGenerator" />

这里用卡片的模拟代替图像服务（`Card.Simulation.Generate`），第 3 张图在第一次尝试时失败。设置了生成器后 `AutoGenerate` 默认关闭；把它设为 `True`，卡片会在滚动进入视口时自动生成一次。

### 监听卡片

<Demo name="Events" />

`SelectionChanged` 报告选中的图像（放大完成后报告放大后的图像），`SelectedImage` 保存它，`DownloadRequested` 携带图像和文件名。

```csharp
card.DownloadRequested += async (_, e) =>
{
    var file = await TopLevel.GetTopLevel(card)!.StorageProvider.SaveFilePickerAsync(new() { SuggestedFileName = e.FileName });
    if (file is not null && e.Image.Source is { } bitmap)
    {
        await using var stream = await file.OpenWriteAsync();
        bitmap.Save(stream);
    }
};
```

### 窄容器

<Demo name="Narrow" />

卡片填满容器，最宽 640px。宽度小于 420px 时，画幅标签移到第二行；小于 520px 时，底栏改为上下排列：状态在上，下面是三个等宽按钮。

### 你自己的文案与模型

`Models` 和 `AspectRatios` 替换标签（`ImageGenerationModel` 有 id、名称和可选的标志；`ImageGenerationAspect` 有 id、显示文字和一次生成返回的像素尺寸）。`Suggestions` 替换空网格中提供的提示词。`Labels` 保存卡片显示或播报的每一个词：

```csharp
card.Labels = new ImageGenerationLabels
{
    Generate = "Créer",
    Stop = "Arrêter",
    Generating = (count, model) => $"Création de {count} images avec {model}",
};
```

### 减弱动效

减弱动效时，进度环、分步的马赛克（它跟随进度而非时间）以及最终状态保留；光带、放大扫描、悬停缩放和展开位移被去掉。关闭动效时一切立即变化。

### 内容准则

- 模型名称只写你的服务实际提供的；演示中的名称是虚构的。
- 返回真实的宽高，让底栏和文件名显示真实尺寸。
- 在 `Description` 中描述每张图像（通常就是提示词）：屏幕阅读器会在格子上朗读它。

### 最佳实践

- 把 `context.CancellationToken` 传给每一次调用，这样 Stop 才会真正停止工作。
- 尽早报告预览：马赛克从第一步起就会显示真实的颜色。
- 一次调用返回四张图的服务，可以缓存同一个请求，再按序号分别取出每一张。

### 常见问题

**批次运行时用户能修改提示词吗？** 可以；修改会在下一次 Generate 时生效。

**卡片会保留之前的批次吗？** 不会。它只保存当前批次；监听 `SelectionChanged` 来保留结果。

## API 参考

<!-- api: ImageGeneration -->

<!-- api: ImageGenerationModel -->

<!-- api: ImageGenerationAspect -->

<!-- api: ImageGenerationImage -->

<!-- api: ImageGenerationRequest -->

<!-- api: ImageGenerationContext -->

<!-- api: ImageGenerationLabels -->

<!-- api: ImageGenerationSelectionChangedEventArgs -->

<!-- api: ImageGenerationDownloadEventArgs -->

<!-- api: ImageGenerationSimulation -->

<!-- api: ImageGenerationPainter -->

## 无障碍

- 模型标签和画幅标签各是一个单选组，每组只有一个 Tab 停靠点；收起的模型标签仍以名称作为无障碍名称，并显示在提示中。
- 每个格子都是一个切换按钮，会报告是否被选中；它的名称朗读图像编号和描述，或说明正在生成、已失败或已停止。
- 一个礼貌的实时区域会播报开始、完成、失败、放大和下载，并说明结果是否为模拟。
- 状态从不只靠颜色表达：失败配有危险图标和 "Failed"，停止配有方块和 "Stopped"，选中配有强调色描边和对勾徽章。
- 图像展开时，其余格子不接收指针或焦点。

### 键盘

| 按键 | 操作 |
|---|---|
| Cmd/Ctrl + Enter | 从提示词输入框开始生成 |
| 方向键 | 在模型标签或画幅标签之间移动（并选择），以及在网格中的图像之间移动 |
| Enter、Space | 选中或取消选中当前聚焦的图像；重试失败或已停止的图像 |
| Escape | 关闭展开的图像并回到网格 |
| Tab | 提示词、模型、画幅、Generate、选中的图像，然后是 Upscale、Vary 和 Download |

## 动效

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 图像成形 | 4 列色块按带种子的顺序从中心淡入，然后 8、16、32、64 列依次落在其上，最后交叉淡入完整图像；同一个进度值驱动马赛克、进度环和百分比 | 同样的步骤，由进度驱动 | 同样的步骤 |
| 运行中的格子 | 一道柔和的光带沿对角线扫过成形中网格的色块，随着细节出现而淡去，每遍 1.6s，离开屏幕时暂停 | 无 | 无 |
| 完成的格子 | 悬停时画面放大 2%；按下时格子缩到 98.5% | 无 | 无 |
| 放大 | 格子以无回弹的 240ms 弹簧从它的格位长到整个网格，其余格子淡出；一道清晰像素带从左向右扫过 | 立即，无扫描 | 立即 |
| 新批次的画幅 | 网格以弹簧动画过渡到新画幅；容器尺寸变化则直接切换到新尺寸 | 立即 | 立即 |
| 按钮文字 | 叠放在同一格中交叉淡化（淡出 100ms，淡入 160ms），按钮宽度不变 | 交叉淡化 | 立即 |
| 底栏状态 | 旧的一行在 100ms 内淡出，新的一行在 160ms 内淡入并上移 2px | 交叉淡化 | 立即 |
| 模型标签 | 选中的标签按测得的宽度以弹簧展开名称 | 立即 | 立即 |

## 相关组件

- `ContextWindow`：一次会话用了模型窗口的多少。
- `ToolCall`：智能体发起的工具调用。
- `AgentStatus`：运行中智能体的实时状态。
