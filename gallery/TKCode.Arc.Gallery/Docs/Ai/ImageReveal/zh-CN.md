---
title: Image reveal
summary: 图像生成画布：由图像自身颜色生成的柔和模糊预览，在一条边缘柔和的光带后方自上而下逐渐变清晰，带有细腻的胶片颗粒、半透明的进度胶囊，以及安静的排队与错误状态。
source-hash: 11bcd64820da
---

<Demo name="Preview" />

## 何时使用

`ImageReveal` 以当下图像工具的方式展示图像模型的工作过程。一张由图片自身颜色生成、高度模糊的柔和预览带着细腻的胶片颗粒淡入，随后一条边缘柔和的光带自上而下移动：光带上方的图片已经清晰，光带内部混入一份接近清晰的副本，光带下方的预览保持柔和，并慢慢对焦。一个半透明的胶囊显示进度环与步数。排队中的任务是一块安静的表面，上面掠过一道宽阔的微光；失败的任务显示警示图标、错误信息与 Retry。

- **图像画布**，或模型渲染图片时聊天回复中的内联图片。
- **一组变体图片**，一张接一张地生成完成。
- **排队中的任务**，在开始之前也应当显得有生气。
- **空状态**，第一张图片仍在渲染时。

## 何时不用

- **普通的图片加载**（来自服务器或磁盘）：使用模糊占位或骨架屏。
- **文本生成或工具调用**：使用文字微光、`ToolCall` 或 `AgentStatus`。
- **进度确定的文件上传**：使用进度条。

## 用法

传入任务的 `Status` 与 `Progress`（当前步数除以总步数），并在预览图或最终图片一出现时就把它交给 `Source`：柔和预览从它取样，在它出现之前画布保持安静的排队外观。粗粒度的进度更新（每个模型步骤一次）会被平滑成一段连续的显现。

```xml
<arc:ImageReveal Source="{Binding Image}"
                 AltText="A calm alpine lake at golden hour"
                 Status="{Binding Status}"
                 Progress="{Binding Progress}"
                 Steps="30"
                 AspectRatio="0.8" />
```

没有状态也没有生成器时，`Source` 中的图片会直接以完成状态显示。什么都没有设置时，画布停在它的素色表面上：没有胶囊、没有微光，名称为 "No image"（上方的 Empty 选项卡）。用于演示时，可以让模拟在你提供的图片上循环播放生成过程（见下文）。

### 结构

1. **画布**：填满容器，最宽 560px，并保持 `AspectRatio`（宽度除以高度）；图片按覆盖方式裁剪。圆角随 `Size` 变化。
2. **柔和预览**：图片宽 6 到 48 像素的副本，平滑放大并模糊，按进度沿这条模糊链交叉淡化，带有细腻的单色颗粒与极其缓慢的漂移。
3. **光带**：一条约为画布四分之一高、边缘柔和的条带，向下移动时让图片变清晰。
4. **胶囊**：位于左上角，一个加载圈、进度环或对勾，配一个简短标签与步数（"18 / 30"，等宽数字，宽度预留）。图片显现完成后不久淡出。
5. **错误状态**：画布中央的警示图标、错误信息与 Retry 按钮。

### 状态

| `ActualStatus` | 画布 | 胶囊 |
|---|---|---|
| `Queued` | 表面底色，掠过一道宽阔柔和的微光 | 加载圈与 `QueueLabel`（"Queued, 2 ahead"） |
| `Generating` | 柔和预览在光带后方自上而下变清晰 | 进度环、"Generating" 与 "18 / 30" |
| `Done` | 清晰的图片；一道柔和的光泽横扫而过 | 对勾与 "Done"，随后胶囊淡出 |
| `Error` | 清晰的图层在变暗的预览上重新模糊淡出 | 隐藏；显示警示图标、`ErrorMessage` 与 Retry |

每次变化都从当前姿态出发：重新开始、失败或更换图片时，清晰图层会先重新模糊淡出，底下的状态才会重置；新图片从旧画面的快照交叉淡入。

### 生成器

<Demo name="Generator" />

`Generator` 接受一个运行一次任务的异步委托：报告 `ImageRevealProgress` 值（进度，以及一旦拿到就附上的图片），并返回最终图片。抛出异常会显示错误状态，Retry 会再次运行生成器。`Restart()`（或新的 `RunKey`）会取消正在运行的任务并开始新的任务。

```csharp
reveal.Generator = async (progress, cancellationToken) =>
{
    var job = await images.StartAsync(prompt, cancellationToken);
    await foreach (var update in job.UpdatesAsync(cancellationToken))
    {
        progress.Report(new ImageRevealProgress(update.Step / 30.0, update.Preview));
    }
    return job.Image;
};
```

### 没有进度事件

<Demo name="Estimated" />

服务不报告进度时，只设置 `Status` 和预估的 `Duration`。胶囊会缓缓走向终点并在那里等待；当你设置 `Source` 与 `Done` 后，光带会把图片扫入。

### 模拟

用于演示或原型时，不要设置 `Status`、`Generator` 与 `Source`，而是给画布一个 `ImageRevealSimulation`，并传入你自己的图片（每张是一个 `ImageRevealSimulationPicture`：图片及其替代文本）。每次运行先在队列中等待 `QueueDuration`（900ms），再在 `Duration`（5.2s）内生成并揭示下一张图片；开启 `IsLooping` 时，完成的图片会停留 `HoldDuration`（2.6s），然后重新模糊淡出并再次运行。`Seed` 决定第一张图片，播报会附加 "simulated"。不会生成或下载任何内容。把 `Simulation` 设为 null 会停止模拟并清空画布。

```csharp
reveal.Simulation = new ImageRevealSimulation
{
    Pictures =
    [
        new ImageRevealSimulationPicture { Image = lakeBitmap, AltText = "A calm alpine lake at golden hour" },
        new ImageRevealSimulationPicture { Image = dunesBitmap, AltText = "Desert dunes at dusk" },
    ],
};
```

给每个控件各自的模拟实例：它会记住自己播放到哪张图片。上方预览揭示的是 Gallery 在代码中绘制的三幅场景（高山湖泊、沙漠沙丘、灯塔），它们是演示数据，不属于组件库。

### 一组变体

<Demo name="Grid" />

在网格中使用 `Size="Small"`：胶囊只显示进度环与步数。给每个图块不同的 `Seed`，让颗粒与漂移各不相同（这里每个图块还在同一组图片上播放各自的模拟）；用 `StartDelay` 错开各次运行，关闭 `IsLooping`，并设置 `MaxWidth="Infinity"` 让图块填满各自的单元格。修改 `RunKey`（或调用 `Restart()`）会让每个图块重新模糊淡出并再次运行。

### 自定义文字

`Labels` 包含控件显示或朗读的每一个词；`QueueLabel` 与 `ErrorMessage` 修改单个任务的标签与错误信息，`AltText` 为完成的图片命名。

```csharp
reveal.Labels = new ImageRevealLabels
{
    Generating = "Génération",
    Done = "Terminé",
    Count = (step, total) => $"{step} sur {total}",
};
```

`RingBrush` 设置进度环的颜色（默认为强调色），`ShowsProgress` 隐藏胶囊，`ShowsSheen` 隐藏完成时的光泽。

### 内容准则

- `AltText` 描述图片内容（"A calm alpine lake at golden hour"），而不是任务本身。
- `QueueLabel` 保持简短："Queued"、"Queued, 2 ahead"。
- `ErrorMessage` 用一句话说明发生了什么、该怎么做。

### 最佳实践

- 只要有任何预览图就立即传入；显现效果从它取样。
- 报告每一个模型步骤：200ms 的跟随会把它们变成一段平滑的显现。
- 在网格中使用 Small 图块、不同的种子，并错开约 400ms。

### 常见问题

**它会读取图片的像素吗？** 不会。副本只被绘制，从不回读，因此任何 `IImage` 都可以使用。

**为什么生成中仍保持排队外观？** 还没有图片；传入 `Source`（或由生成器报告一张图片），预览就会淡入。

## API 参考

<!-- api: ImageReveal -->

<!-- api: ImageRevealStatus -->

<!-- api: ImageRevealProgress -->

<!-- api: ImageRevealStatusChangedEventArgs -->

<!-- api: ImageRevealSimulation -->

<!-- api: ImageRevealSimulationPicture -->

<!-- api: ImageRevealLabels -->

## 无障碍

- 画布报告为图像；工作期间它的名称说明当前状态（"Generating image"），完成后变为 `AltText`。空画布的名称为 "No image"，不播报任何内容。
- 一个礼貌型实时区域会播报 Queued、Generating image、Image ready 与错误信息，而不是每一步。模拟模式会附加 "simulated"。
- 生成期间，胶囊是一个只读进度条，其值为当前步数，名称为 "Step 18 of 30"。
- 状态从不只靠颜色表达：胶囊把加载圈、进度环或对勾与简短标签配对，错误状态把警示图标与错误信息配对。

### 键盘

| 按键 | 操作 |
|---|---|
| Tab | 在错误状态下移到 Retry 按钮。画布本身不可聚焦。 |
| Enter、Space | 激活 Retry。 |

## 动效

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 排队 | 一道宽阔柔和的微光，每 2.6s 从画布外掠到画布外 | 只有表面底色 | 只有表面底色 |
| 预览出现 | 从 6px 副本开始在 420ms 内淡入，按进度沿模糊链交叉淡化 | 同样的淡入，没有漂移 | 立即 |
| 生成中 | 边缘柔和的光带自上而下移动；颗粒每秒刷新 24 次；极其缓慢的漂移随显现逐渐停下 | 没有光带：接近清晰的副本与图片按进度依次交叉淡入；颗粒静止 | 同样的交叉淡化，立即跟随进度 |
| 进度更新 | 200ms 的指数跟随；模拟模式使用缓出曲线，让构图更早成形 | 相同 | 立即 |
| 显现完成 | 一道柔和的光泽横扫而过（1.1s），随后胶囊淡出（240ms） | 没有光泽 | 立即 |
| 重新开始、失败、新图片 | 清晰图层先重新模糊淡出（500ms），显现才会重置；新图片从快照交叉淡入（450ms） | 相同 | 立即 |
| 胶囊标签 | 旧文字在 100ms 内淡出，间隔 60ms 后新文字在 160ms 内淡入；胶囊宽度以无回弹的弹簧变化 | 120ms 交叉淡化，宽度立即变化 | 立即 |

帧循环在画布滚出屏幕或窗口隐藏时暂停，图片显现完成后不再绘制。

## 相关组件

- `AgentStatus`：智能体运行的实时状态。
- `ToolCall`：智能体发起的工具调用。
- `ReasoningBlock`：AI 回复中的思考部分。
