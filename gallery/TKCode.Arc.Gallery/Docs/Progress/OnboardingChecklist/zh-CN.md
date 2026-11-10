---
title: 新手引导清单
summary: 一步一步引导新工作区完成设置。
source-hash: 000b2cbf4093
---

<Demo name="Workspace" />

## 何时使用

新手引导清单带领使用者完成一次性的简短设置。圆环和计数跟随每次勾选，完成的步骤划掉后收进“已完成”行，下一步自动展开。

- **新账户或新工作区**：三到八个步骤，例如创建项目、邀请团队，顺序不限。
- **自身就能完成工作的步骤**：为步骤设置 `ActionLabel` 和 `Action`。任务运行时按钮显示进度，任务完成后自动勾选该步骤。
- **只有使用者自己能确认的步骤**：不设置操作，由圆形勾选框手动标记。
- **需要保留一段时间的清单**：`IsHidden` 把它收起到只剩标题栏，可以留在仪表盘上而不占据页面。

## 何时不用

- **顺序严格的流程**：使用 `Stepper`，它显示固定序列中的一个位置。
- **单个运行中任务的进度**：使用 `Progress`，它衡量已知的工作量。
- **不断增长的任务列表**：使用 `Table` 或列表；本控件用于有终点的简短设置。
- **单个问题**：使用 `Checkbox`。

## 用法

在控件内列出步骤，并为每一步设置稳定的 `Id`。设置 `Title`，也可以设置所有步骤完成时显示的 `DoneTitle`（默认是“You're all set”）。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:OnboardingChecklist Title="Before your first release" DoneTitle="Ready to ship">
        <arc:OnboardingChecklistStep Id="guide" Title="Read the release guide" Description="It covers versioning." />
        <arc:OnboardingChecklistStep Id="owner" Title="Add a second owner" />
    </arc:OnboardingChecklist>
</UserControl>
```

### 手动勾选的步骤

没有 `ActionLabel` 的步骤没有按钮，圆形勾选框是完成它的唯一方式。在步骤收起之前再次勾选，或者从“已完成”行勾选，都会把它标记为未完成并放回列表。

<Demo name="ByHand" />

### 操作与错误

`Action` 是 `Func<Task>`。运行期间按钮显示加载圈并忽略按压。任务完成后步骤被勾选；任务抛出异常时步骤保持展开，并在按钮上方显示异常消息，使用者可以重试。`Command` 也可以运行步骤，适用于视图模型。

<Demo name="FailingAction" />

### 保存进度

把 `Completed` 设为之前已完成步骤的键，这些步骤一开始就位于“已完成”行。每当使用者或操作改变它们时，`CompletedChanged` 按步骤顺序报告这些键，便于保存。`CompleteStep` 和 `ReopenStep` 在代码中做同样的事，例如工作在别处完成时。

<Demo name="PartlyDone" />

### 隐藏清单

Hide 按钮收起主体，保留带圆环和计数的标题栏。绑定 `IsHidden` 可以在多次访问之间保留这一选择；使用者更改时由 `HiddenChanged` 报告。

<Demo name="Hidden" />

### 其他语言

控件自身写出的文字（可见的和朗读的）都在 `Strings` 中。设置新的 `OnboardingChecklistStrings` 即可翻译。

## API 参考

<!-- api: OnboardingChecklist -->

<!-- api: OnboardingChecklistStep -->

`OnboardingChecklistStrings` 为每段固定文字提供一个文本属性：`Show`、`Hide`、`ShowChecklist`、`HideChecklist`、`CountFormat`、`Completed`、`Progress`、`ProgressFormat`、`OpenSteps`、`CompletedSteps`、`StepDoneFormat`、`StepUndoneFormat`、`AllDoneFormat` 和 `ActionFailed`。

## 无障碍

| 按键 | 操作 |
|---|---|
| Tab | 在勾选框、标题、操作、Hide 按钮和“已完成”行之间移动 |
| Space、Enter | 按下当前聚焦的勾选框、标题、操作或按钮 |
| Up、Down | 在步骤标题之间移动，到达两端时循环 |
| Home、End | 转到第一个或最后一个步骤标题 |

已聚焦的步骤完成时，焦点移到下一步的操作；没有操作时移到它的标题；没有剩余步骤时移到“已完成”行。从“已完成”行重新打开步骤时，焦点移到该步骤的勾选框。正在收起或已隐藏部分中的控件不获取焦点。

控件是一个具名分组，名称跟随标题，并以礼貌方式播报每次变化，例如“Create a project done. 1 of 4 complete.”。圆环把自己报告为名为“Setup progress”的进度条，状态类似“1 of 4 steps done”。每个勾选框都是以步骤命名的复选框，标题、Hide 按钮和“已完成”行会报告是否已展开。正在运行或已完成的操作仍可获得焦点，并报告其状态。

## 动效

勾选步骤时，填充以 snappy 弹簧从 0.6 放大，对勾在 240 ms 内绘制，一条 1 像素的删除线在 480 ms 内划过标题，标题换行时逐行划过。720 ms 后步骤收进“已完成”行。行以 smooth 弹簧展开，以稍快的弹簧收起；圆环以 morph 弹簧填充，最后一步完成时在中心绘制对勾。数字按计数变化的方向翻转，标题和 Hide 文字从柔和的模糊中升起，且不改变所在位置的尺寸。

减弱动效时，尺寸和圆环立即变化，只有透明度淡入淡出。关闭动效时，一切立即变化。720 ms 的停顿在任何情况下都保留，让使用者能看到自己完成了什么。

## 响应式

控件填满给定的宽度，长标题和说明会换行。宽度不超过 380 px 时，Hide 按钮只显示箭头并保留朗读名称，标题栏因此保持一行。每个勾选框有 40 px 的目标。悬停效果不作用于触摸。

## 相关组件

| 组件 | 适用场景 |
|---|---|
| `Stepper` | 步骤顺序严格，有一个当前位置 |
| `Progress` | 运行单个任务且进度已知 |
| `Accordion` | 各部分是要阅读的内容，而不是要完成的工作 |
| `Checkbox` | 只有一个独立的选择 |
