---
title: Branch graph
summary: 逐个提交绘制的 git 风格历史；分支分出到各自的泳道再合并回来，带有分支和标签名称，选中某个提交时显示它的历史。
source-hash: 841346a92975
---

<Demo name="History" />

## 何时使用

`BranchGraph` 像 git 工具那样绘制版本历史：每个提交占一行，每个分支有自己带颜色的泳道，分支分出和合并回来的地方各画一条曲线，合并提交画成圆环，
每个分支的最新提交和每个标签都有一个胶囊标签。作为故事播放时，它逐个提交地绘制历史（连线画出，圆点弹出，分支标签向上移动），同时说明文字讲出每个提交
做了什么：第一个提交、分支上的提交、新的分支、带两个父提交的合并。选中一个提交时，它的所有祖先都会亮起，其余部分变暗，一眼就能回答
“这个修复是否包含在那个版本中？”。

- **关于版本控制的文档与课程**：用图展示分支、合并和发布分支，而不是只用文字讲。
- **发布说明与更新日志**：一个版本是如何形成的，以及它的标签。
- **开发者工具**：放在拉取请求或部署旁边的紧凑历史。

## 何时不用

- **很长的历史**：每个提交都占一行，而且没有虚拟化；请控制在几十个提交以内。完整的仓库请使用 `Table` 或专门的日志视图。
- **差异和文件改动**：图只显示历史的形状，不显示改了什么。
- **同一页面上有多张卡片**：每张卡片在播放时都会做动画。只需要静态图的卡片请设置 `StartsFinished`。

## 用法

卡片无需数据：它播放一段虚构的历史，包含 `main`、一个功能分支和一个热修复分支上的九个提交，其中有两次合并和一个发布标签。

```xml
<arc:BranchGraph />
```

`BranchGraph` 派生自 `Explainer`，基类提供故事时钟（`IsPlaying`、`Speed`、`IsLooping`、`Play()`、`Pause()`、`StepForward()`、`Restart()`）、
进入可视区域即开始播放的行为、播放按钮以及礼貌的实时区域；本控件添加速度控件和图。

### 你自己的历史

按从旧到新的顺序添加 `BranchGraphCommit` 项。`Branch` 选择泳道（默认 `main`）。没有设置 `Parents` 时，提交的父提交是同一分支上的前一个提交；
如果是分支的第一个提交，则是任意分支上的前一个提交。设置 `Parents` 可以从别处分出分支，或者给出两个 id（分支自己的放在前面）表示合并。
泳道按分支出现的顺序分配，分支合并后它的泳道会被重新使用。

<Demo name="PlainGraph" />

```xml
<arc:BranchGraph Title="Release branch" StartsFinished="True" IsNewestFirst="True" AreControlsVisible="False">
    <arc:BranchGraphCommit Id="5e1a20" Branch="main" Message="Start the 2.0 cycle" Author="Rhea" />
    <arc:BranchGraphCommit Id="6f2b31" Branch="release/2.0" Message="Freeze features for 2.0" Author="Rhea" />
    <arc:BranchGraphCommit Id="7a3c42" Branch="main" Message="Add dark mode settings" Author="Sam" />
    <arc:BranchGraphCommit Id="8b4d53" Branch="release/2.0" Message="Fix crash on startup" Author="Rhea" Tag="v2.0.0" />
    <arc:BranchGraphCommit Id="9c5e64" Branch="main" Message="Merge release/2.0" Parents="7a3c42, 8b4d53" Author="Sam" />
</arc:BranchGraph>
```

`StartsFinished` 让卡片打开时显示完整历史（播放仍会重放），`IsNewestFirst` 像 `git log` 一样把最新的提交放在最上面，`ItemsSource` 从视图模型获取提交。
`RevealedCount` 报告故事进行到哪里，每个提交绘制时都会引发 `CommitDrawn`，并带上它的 `Kind`：`Commit`、`Branch` 或 `Merge`。

### 选中

点击某个提交，或在图获得焦点时按方向键，会设置 `SelectedCommit`（双向）。该提交所在的行被高亮，它的所有祖先保持亮起，其余部分变暗。
再次点击它或按 Escape 可以清除选中。

<Demo name="Selection" />

`Labels` 包含卡片显示或朗读的所有文字；替换它即可翻译或改写。

### 减弱动效

在减弱动效下，卡片打开时直接显示完整历史。播放和 Next step 仍然可用，每个提交在原位直接出现。

<Demo name="ReducedMotion" />

## API 参考

<!-- api: BranchGraph -->

<!-- api: BranchGraphCommit -->

<!-- api: BranchGraphLabels -->

<!-- api: BranchGraphCommitEventArgs -->

`BranchGraphSamples.CreateCommits()` 返回一份新的示例历史副本。

## 无障碍

- 卡片是一个以 `Title` 命名的分组（"Branch history"）。状态行说明 Ready、Drawing、Paused 或 Up to date。
- 图是一个 Tab 停靠点。上下键在提交之间移动选中，Home 和 End 跳到第一行和最后一行，Escape 清除选中。它的无障碍名称是选中的提交
  （"Merge feature/search, 2c8d0e on main by Mira, v1.2.0"），没有选中时是已绘制的最新提交。
- 只有读者主动推进故事（单步、暂停、重新开始）时，实时区域才会读出说明文字。
- 泳道除了颜色，还靠位置区分；合并提交靠圆环区分；分支名称写在胶囊标签里。

## 动效

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 连向新提交的连线 | 在 0.5 s 内画出 | 直接出现 | 直接出现 |
| 提交圆点 | 弹出，略有回弹 | 直接出现 | 直接出现 |
| 行文字和胶囊标签 | 淡入 | 直接出现 | 直接出现 |
| 说明文字 | 交叉淡入淡出 | 原位更换 | 原位更换 |

在速度 1 下，每个提交需要 1.6 秒。所有动画都跟随故事时钟，因此会随之暂停、单步，并按 `Speed` 缩放。

## 响应式

- 每条泳道占 18 px；提交说明占据这一行剩下的宽度，过长时以省略号截断。
- 宽度达到 480 px 时，作者和短 id 显示在每行右侧；更窄时不显示（屏幕阅读器仍会读出）。
- 卡片最宽 760 px；窄于 560 px 时内边距收紧，窄于 420 px 时隐藏 "Speed" 文字。
- 轻点会选中一个提交，与点击效果相同。

## 相关组件

| 组件 | 何时选择 |
|---|---|
| `Timeline` | 按顺序排列的事件，没有分支 |
| `ReasoningTree` | 故事是一次会分支和剪枝的搜索 |
| `AgentTimeline` | 多个智能体随时间工作 |
