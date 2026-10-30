---
title: Starter prompts
summary: AI 应用空状态里的问候语与起始卡片：带分类标签页、以交叉淡化切换卡片的“换一批”，以及由所选卡片填入的输入框。
source-hash: 6969be5a1813
---

<Demo name="Preview" />

## 何时使用

- 聊天、助手或智能体在第一条消息之前的空状态。
- 介绍助手能借助已连接的工具做什么，例如邮件、代码仓库或文档空间。
- 用几条量身定制、可以直接发送的提示词引导新的工作区。

## 何时不用

- 对话已经开始时；请改用会话里的后续建议标签。
- 带搜索的命令启动器；请使用命令面板。
- 文本框里的单条内联建议；请使用幽灵文本。

## 用法

### 空状态

没有数据时，控件只显示“全部”标签页和输入框，没有卡片，也没有问候语。为它提供问候语、一行说明、标签页和起始提示（预览中的 24 条分布在 Write、Code、Analyze、Plan 四个分类中，均为虚构），它每次显示 4 条。选择一张卡片会把它完整的 `Prompt` 填入输入框、聚焦输入框并引发 `PromptSelected`；按 Enter 发送并
引发 `SubmitRequested`。“换一批”以短暂的错开交叉淡化显示该标签页的下一批起始提示，并且至少带来一张原本不在屏幕上的卡片。

```xml
<arc:StarterPrompts Greeting="Good morning, Maya" ModelName="Qwen3.8-Max" />
```

人们浏览的是卡片标题，输入框里显示的是提示词。标题要简短并以动词开头，把完整、具体的指令放进 `Prompt`。

### 你自己的起始提示与标签页

<Demo name="OwnStarters" />

向 `Categories` 添加 `StarterPromptCategory`（一个 id、一个标签和可选的 24 × 24 描边 `Icon`），并把 `StarterPrompt`
作为内容添加（`Id`、`Category`、`Title`、`Description`、`Prompt`、`App`）。“全部”会自动加在最前面，没有起始提示的
标签页会隐藏。`Count` 决定屏幕上同时有几张卡片；网格始终保留这么多个位置，所以起始提示较少的标签页会留出空位，下方
内容不会移动。

`App` 标记使用了已连接应用的卡片：它的标志画在一个浅色底的方块里，名称显示在旁边，也会读入卡片的无障碍名称。预设
（`Mail`、`Repository`、`Docs`、`Issues`、`Calendar`）是中性的图标；真实的集成请传入它自己的名称与标志：

```csharp
var gmail = new StarterPromptApp("Gmail", myGmailGeometry);
```

没有 `Mark` 的 `StarterPromptApp` 显示其名称的首字母。

### 发送

<Demo name="Sending" />

`SubmitRequested` 交出去掉首尾空白的 `Message`、它来自的 `Prompt`（由人手写时为 null）以及一个 `CancellationToken`。
把请求的任务放进 `Sending`：任务完成之前，底栏显示“发送中”，发送按钮变成停止按钮。任务完成后输入框清空，底栏显示
“已发送”。任务抛出异常时，底栏显示警示图标、异常的消息和“重试”，文本保留。停止按钮与 Escape 会取消该令牌并保留文本；
控件离开窗口时也一样。

```csharp
prompts.SubmitRequested += (_, e) =>
    e.Sending = chat.SendAsync(e.Message, e.CancellationToken);
```

如果没有处理程序设置 `Sending`，发送是模拟的，底栏会注明这一点。`SendStatus` 与 `SendError` 报告状态，`Submit()` 与
`StopSending()` 的作用与按键相同，`Text` 是输入框的文本（双向）。

### 你自己的输入框

<Demo name="OwnComposer" />

`ShowsComposer="False"` 隐藏内置的输入框，让卡片为应用已有的输入框提供内容。选择卡片仍会设置 `Text`，并带着该起始
提示引发 `PromptSelected`。

### 加载

`IsLoading` 显示与真实卡片尺寸、节奏相同的占位卡片；它们缓慢呼吸、不能被选择，“换一批”也被禁用。关闭后，起始提示在
同样的框里淡入。

### 窄容器

<Demo name="Narrow" />

控件宽度达到 560px 时卡片排成两列，更窄时排成一列。标签页在自身内部滚动并带有边缘渐隐，宽度小于 440px 时“换一批”
只显示图标，小于 480px 时内边距、圆角与问候语都会缩小一档。控件填满它的容器，宽度从不跟随卡片或文本变化。

### 你自己的文字

`Greeting`、`Description`（设为 null 即隐藏）、`Placeholder` 以及 `ModelName` 与 `ModelMark` 都是属性。其余所有文字
都在 `Labels` 中：

```csharp
prompts.Labels = new StarterPromptsLabels
{
    All = "Alle",
    Shuffle = "Mischen",
    Send = "Nachricht senden",
    CardName = (title, app, category) => app is null ? title : $"{title}, mit {app}",
};
```

### 内容准则

- 标题只有几个词，并以动词开头（“规划我的一周”）；说明补充让它具体的细节。
- 只有提示词确实用到某个集成时才设置 `App`，这样标志才有意义。
- 问候语简短而亲切；下面一行安静的说明就够了。

### 最佳实践

- 把它用作新聊天或新智能体会话的第一屏，消息发出后换成对话本身。
- 遵守取消令牌，让停止真正停止请求。
- 让起始提示的总数多于 `Count`，这样“换一批”才有内容可换；数量不够时它会被禁用。

### 常见问题

**它会渲染每一条起始提示吗？** 不会。只有屏幕上的卡片存在；提示池可以容纳数百条起始提示。

**能用代码选择标签页吗？** 可以。`SelectedCategory` 是双向的；不对应任何可见标签页的 id 会显示“全部”。

## API 参考

<!-- api: StarterPrompts -->

<!-- api: StarterPrompt -->

<!-- api: StarterPromptCategory -->

<!-- api: StarterPromptApp -->

<!-- api: StarterPromptsLabels -->

<!-- api: StarterPromptsSubmitEventArgs -->

<!-- api: StarterPromptSelectedEventArgs -->

## 无障碍

- 问候语是标题。每张卡片都是按钮，名称依次读出标题、已连接的应用与分类（“Plan my week, uses Calendar, Plan”）。
- 一个礼貌（polite）的实时区域播报选择、换一批、发送中、已发送与错误，并说明发送是否为模拟。
- 状态从不只靠颜色表达：已发送显示对勾与文字，失败显示警示图标与消息。
- 发送按钮名为“Send message”，消息发送途中名为“Stop sending”。
- 所有文字都在 `StarterPromptsLabels` 或属性中。

### 键盘

| 按键 | 操作 |
|---|---|
| Tab | 依次经过标签页、换一批、每张卡片和输入框 |
| Left、Right | 在分类标签页之间移动 |
| Enter、Space | 在卡片上：用它的提示词填入输入框并聚焦输入框 |
| Enter | 在输入框中：发送消息 |
| Shift+Enter | 在输入框中：换行 |
| Escape | 发送途中：停止发送并保留文本 |

## 动效

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 换一批或切换标签页 | 每张卡片的内容在 100ms 内淡出，随后新内容在 160ms 内淡入并上升 2px；卡片之间相隔 35ms 开始（总共约 100ms），都在固定的框内 | 立即 | 立即 |
| 加载结束、起始提示到达 | 内容以同样的方式错开淡入 | 立即 | 立即 |
| 选择卡片 | 输入框获得焦点，新文本在 160ms 内淡入 | 立即 | 立即 |
| 底栏状态 | 原地交叉淡化：淡出 100ms，淡入 160ms | 120ms 淡化 | 立即 |
| 发送与停止图标 | 在 160ms 内交叉淡化；没有移动或尺寸变化 | 立即 | 立即 |
| 加载占位 | 每 1.6 秒呼吸一次 | 静止 | 静止 |
| 标签页选择 | 选中块沿标签页的弹簧滑动 | 立即 | 立即 |

## 相关组件

- `AgentStatus`：运行开始后智能体的实时状态。
- `ToolCall`：智能体发起的工具调用。
- `Tabs` 与 `SegmentedControl`：在其他地方切换视图。
- `EmptyState`：没有内容可显示时给出有用的下一步。
