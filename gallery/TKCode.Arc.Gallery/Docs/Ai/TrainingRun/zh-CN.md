---
title: Training run
summary: 微调任务卡片：训练与验证损失曲线沿着固定的步数轴实时绘制，这条轴同时也是轮次进度条；卡片还显示学习率、已用时间、预计剩余时间、最近事件、暂停与取消，以及带 Deploy 的结果。
source-hash: c7fd2a8ac687
---

<Demo name="Preview" />

## 何时使用

`TrainingRun` 把一个微调或训练任务放在一张卡片上。损失曲线沿着覆盖整个运行的步数轴实时绘制，这条轴同时也是轮次进度条，所以训练过程中什么都不会重新缩放。旁边是步数、学习率、已用时间和预计剩余时间、最新的三条事件，以及暂停和取消。运行完成后，结果取代实时统计，Deploy 把模型部署到一个端点。

- **AI 平台里的微调或训练任务页**：人们在这里查看损失、暂停、取消和部署。
- **评测或蒸馏运行**：报告损失曲线、检查点和最终分数。
- **新手引导中的一步**：用客户的数据训练自定义模型，最后部署。

## 何时不用

- **有多条序列、没有任务生命周期的图表**：使用 `LineChart`。
- **由步骤组成的简短代理任务**，而不是损失曲线：使用 `AgentStatus`。
- **任务列表**：每个任务渲染一行紧凑的条目，选中时再打开这张卡片。

## 用法

根据任务配置设置 `Status`、`TotalSteps` 和 `Epochs`，这样从第一个样本起坐标轴就是正确的。然后持续更新 `Step`、`LearningRate`、`Elapsed` 和 `Eta`，并在数据流入时追加到 `Loss` 和 `Events`。无需节流：曲线会缓动到每个新样本，所以成批到达的数据也会匀速绘制。

```csharp
run.TotalSteps = job.TotalSteps;
run.Epochs = job.Epochs;
run.Status = TrainingRunStatus.Running;

// On every report from the job:
run.Loss.Add(new TrainingRunLossPoint(report.Step, report.TrainLoss, report.ValidationLoss));
run.Step = report.Step;
run.LearningRate = report.LearningRate;
run.Elapsed = report.Elapsed;
run.Eta = report.Eta;
```

`Apply` 接受一个只包含变化内容的 `TrainingRunProgress`，`FollowAsync` 则在 UI 线程上依次应用整条更新流：

```csharp
await run.FollowAsync(jobs.WatchAsync(job.Id, token), token);
```

既不设置 `Status` 也没有 `Simulation` 时，卡片显示一个排队中、尚无数据的任务：名称为 “Fine-tuning job”，统计为空，图表为空。不设置 `Status` 时，卡片会改为播放它的 `Simulation`，方便在接入真实任务 API 之前先行开发：`TrainingRunSimulation` 为任务命名（`JobName`、`BaseModel`、`Details`、`Dataset`），给出它等待和训练所用的 `Hardware`，以及模拟部署返回的 `Endpoint`。卡片让它排队、启动，并以程序生成的损失曲线、检查点和评估，在三个 epoch 中训练 2,400 步。`Scenario="Oom"` 让它在第 940 步失败一次，以便查看重试流程。卡片离开屏幕时模拟会暂停，你自己设置的 `Status` 优先于它，把 `Simulation` 设为 null 会停止它。

```csharp
run.Simulation = new TrainingRunSimulation
{
    JobName = "support-v4",
    BaseModel = new AgentModel("Base model"),
    Details = ["LoRA r16", "8× H200"],
    Dataset = new TrainingRunDataset("your-org/support-tickets", 48210),
    Hardware = "8× H200",
    Endpoint = "https://inference.example.com/v1/support-v4",
};
```

本页的演示从 `TrainingRunDemoData` 获取任务。基础模型使用中性的标签；任务、数据集和所有损失值都是虚构的。

### 结构

1. **头部**：基础模型 24px 的标识（`BaseModel` 没有标识时显示首字母），任务名称，模型及其 `Details`，以及用图标加文字表示的状态。
2. **图例**：最新样本（或正在查看的样本）的训练损失和验证损失，以及当前轮次。
3. **图表**：每隔一个轮次一条浅色背景带，原始训练损失（浅色）、平滑后的训练损失，以及每个样本带一个圆点的验证损失。绘图区下方的步数轴就是轮次进度条，每个轮次一段。
4. **统计**：步数、学习率、已用时间和预计剩余时间，数字滚动变化。完成后由结果取代。
5. **事件**：最新的三条，每种类型一个图标，并标出步数。
6. **底部**：数据集、失败原因或已部署的端点，以及操作按钮。

### 由应用驱动

<Demo name="HostDriven" />

按钮会引发 `PauseRequested`、`ResumeRequested`、`CancelRequested`、`RetryRequested` 和 `RestartRequested`。在你的任务上执行相应操作，然后设置 `Status`。取消需要按两次：第一次按下后它会变成 Confirm cancel，持续 3 秒。

`Actions` 决定哪些按钮可以显示（默认全部）。只读监控设置 `Actions="None"`。每个按钮还需要相应的状态：运行中显示 Pause，暂停时显示 Resume，排队、运行或暂停时显示 Cancel，失败后显示 Retry from checkpoint，运行结束后显示 Run again。

### 结果与部署

<Demo name="Finished" />

完成后，结果取代实时统计。默认是最终训练损失和验证损失、步数以及时长。`Metrics` 可以设置最多四项自定义结果。设置了 `DeployHandler` 时显示 Deploy：

```csharp
run.DeployHandler = async token =>
{
    var deployment = await api.DeployAsync(job.Id, token);
    return deployment.Endpoint;
};
```

任务运行期间按钮显示加载状态并保持焦点。之后底部显示端点和一个复制按钮。如果任务抛出异常，则显示异常消息和 Retry deploy。卡片卸载或运行离开 Completed 时，令牌会被取消。

### 窄布局

<Demo name="Narrow" />

卡片填满容器，最宽 640px。宽度小于 440px 时，内边距降为 16px，统计改为两列，图例把 Validation 缩写为 Val，底部把数据集放在操作按钮上方。

### 自定义文案

`Labels` 包含卡片显示或朗读的所有文字。替换整个对象即可本地化或改写：

```csharp
run.Labels = new TrainingRunLabels
{
    Training = "Entrenando",
    Pause = "Pausar",
    StepAt = step => $"paso {step:N0}",
};
```

### 内容准则

- 用任务产出的模型来命名任务，例如 `support-v4`。
- `Details` 只放两三个简短事实：方法和硬件。
- 事件标签写成简短的已发生事实："Checkpoint saved"、"Loss spike, gradients clipped"。
- `ErrorText` 写明原因和下一步，而不是堆栈跟踪。

### 最佳实践

- 在第一个样本之前传入 `TotalSteps` 和 `Epochs`，坐标轴就永远不会移动。
- 超过几千个样本的运行，把 `Loss` 降采样到约 500 个点。
- 回滚到某个检查点时，删除该检查点之后的样本；曲线会直接跳回，而不是缓动。

### 常见问题

**损失突然升高时，为什么 y 轴不扩大？** 它根据最初的几个样本固定下来，所以曲线在运行中途不会重新缩放。更高的值画在顶边。

**图例显示什么？** 最新的平滑训练损失和最新的验证损失，或者你用指针或方向键查看的那个样本的值。

## API 参考

<!-- api: TrainingRun -->

<!-- api: TrainingRunStatus -->

<!-- api: TrainingRunActions -->

<!-- api: TrainingRunScenario -->

<!-- api: TrainingRunLossPoint -->

<!-- api: TrainingRunEvent -->

<!-- api: TrainingRunEventKind -->

<!-- api: TrainingRunMetric -->

<!-- api: TrainingRunDataset -->

<!-- api: TrainingRunProgress -->

<!-- api: TrainingRunLabels -->

<!-- api: TrainingRunSimulation -->

## 无障碍

- 一个礼貌的实时区域播报状态变化（Queued、Training、Paused、Completed、Failed、Cancelled、Deployed）和 `Notice`，从不逐步播报。
- 状态由图标和文字共同表示，绝不只靠颜色。失败原因以文字形式出现在底部和事件日志中。
- 图表是一个带标签的分组，其名称包含步数和两个损失。方向键可逐个查看样本，图例显示其数值。
- 取消是两步操作，误按一次不会停止耗时很长的运行。Deploy 在加载期间保持焦点。
- 统计是一组术语及其数值。步数和数字使用等宽数字。

### 键盘

| 按键 | 操作 |
|---|---|
| Tab | 移到损失图表，再到底部的操作按钮 |
| Left、Right | 在图表上逐个查看样本；图例显示该步的损失 |
| Shift + Left、Shift + Right | 跳过十个样本 |
| Home、End | 跳到第一个或最新的样本 |
| Escape | 清除正在查看的样本 |
| Enter、Space | 按下获得焦点的操作；Cancel 需要再按一次 Confirm cancel |

## 动效

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 新样本 | 曲线和进度条缓动到它（指数，120ms） | 立即 | 立即 |
| 回滚或重置 | 立即 | 立即 | 立即 |
| 新事件 | 列表以 0.24s 无回弹弹簧下移，新行淡入 | 立即 | 立即 |
| 状态与底部文字 | 旧文字 100ms 淡出，新文字 160ms 淡入并上升 2px | 立即 | 立即 |
| 计数数值 | 数字以弹簧滚动 | 立即 | 立即 |
| 加载圈与活动文字微光 | 旋转、闪烁 | 静止 | 静止 |

x 轴覆盖整个运行，y 轴根据最初的样本固定，所以运行中途什么都不会重新缩放。模拟在减弱动效下继续运行（它是内容），但卡片离开屏幕时会暂停。

## 相关组件

- `AgentStatus`：代理运行的实时状态。
- `ToolCall`：代理记录中的一次工具调用。
- `LineChart`：通用的多序列图表。
- `CopyButton`：复制一个值并给出确认。
