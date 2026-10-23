---
title: AI 侧边面板
summary: 位于人们阅读的页面旁边的助手：它读取整个页面，回答关于页面或关于选区的问题，并以文字差异的形式提出修改，由人们应用、放弃或撤销；应用时页面上会有扫过效果，并出现一个撤销胶囊。
source-hash: fd63b95acc6a
---

<Demo name="Preview" />

## 何时使用

AI 侧边面板用于人们阅读并偶尔修改的页面：规格说明、记录、知识库文章。助手位于页面旁边，读取全部内容，
当页面需要修改时，它不会在人们背后改写页面，而是把修改显示为一张卡片，由人们决定。

- **设计说明或规格说明**："缩短摘要"会返回一张卡片，其中新文本以文字差异显示；应用会把它写入页面。
- **带表格的记录或设置页面**：请求新增一列，在任何修改发生之前，以一个小表格的形式查看，其中新单元格会被着色。
- **人们对某段文字提问的帮助页面**：选中文本，按下 "Ask about selection"（就选区提问），提问会携带该段落作为上下文。
- **必须由人们掌控的页面**：每一项修改都可以撤销，没有应用就不会有任何修改生效。

## 何时不用

- **没有页面的对话**：使用 `AiChat`。
- **原地改写一小段文字**：内联编辑控件更适合；此面板处理的是由块组成的页面。
- **不是由标题、段落和表格组成的页面**：面板只渲染这三类块。更丰富的页面请自行展示，并在其旁边使用面板的线程。
- **运行模型**：面板会向你的 `IAiPanelResponder` 请求回答；它自己不调用任何东西。

## 用法

`AiSidePanel` 派生自 Avalonia 的 `TemplatedControl`，并填满其容器，因此请为它设置高度。设置 `Page`、`Responder`，以及空线程的 `Suggestions`。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:AiSidePanel Height="640" AssistantName="Helper" ModelName="Model X1" PanelWidth="360" />
</UserControl>
```

```csharp
var page = new AiPanelPage { Title = "Offline sync", Meta = "Design note, updated Mar 3" };
page.Blocks.Add(new AiPanelParagraph { Id = "summary", Label = "Summary", Text = "Offline sync is a planned change..." });
panel.Page = page;
panel.Responder = new MyResponder();
panel.ChangeApplied += (_, e) => Save(e.Page);
```

### 页面

`AiPanelPage` 有标题、一行元信息文字，以及 `Blocks`：`AiPanelHeading`、`AiPanelParagraph` 或 `AiPanelTable`（列和行）之一。每个块都有 `Id` 和 `Label`
（"Summary"、"Storage table"）；标签会在选区胶囊、修改卡片和占位符中为块命名，屏幕阅读器也会朗读它。面板处理的是你交给它的页面对象：应用修改时，页面中的块会被替换，
因此你持有的页面始终是最新的。替换 `Page` 会重置线程和撤销记录。

### 作答

`IAiPanelResponder` 接收问题、当前的页面（包括所有已应用的修改）和选区，并返回一个 `AiPanelReply`：文本，以及可选的修改。

```csharp
public async ValueTask<AiPanelReply> RespondAsync(AiPanelRequest request, CancellationToken cancellationToken)
{
    var summary = (AiPanelParagraph)request.Page.Find("summary")!;
    return AiPanelReply.FromText(
        "Here is a shorter summary.",
        new AiPanelChangeProposal("summary", new AiPanelParagraph { Id = "summary", Label = "Summary", Text = shorter }));
}
```

你的方法运行期间，面板显示 "Reading the page"（正在阅读页面）；随后文本以整词的形式每约 50 毫秒到达一次（每步四到七个词，积压较长时会加快追赶）。`AiPanelReply.FromText` 接收一整段文本，由面板自行切分成词；如需真正的流式输出，请传入 `IAsyncEnumerable<string>`。停止会取消令牌，并保留已显示的内容，附带 "Stopped"（已停止）的说明，且不出现卡片。异常会显示一张错误卡片，其中有 Retry（重试）按钮，它会把同一个问题再问一次。

### 修改

一个回复可以对**一个块**提出修改：该块的 id 以及整个新块。面板会显示一张卡片，包含标题（块的标签和诸如 "21 words, was 45"（21 个词，原为 45 个）或 "Adds Sync column"（新增 Sync 列）之类的摘要）、以文字差异形式呈现的修改（删除的词加删除线，新增的词着色；表格会显示带有着色新单元格的新表格），以及 Discard（放弃）和 Apply（应用）按钮。

- **Apply（应用）**会把修改写入页面。页面上的块会从左到右扫过一层着色，旧文字淡出、新文字淡入，块的高度以弹簧效果变为新尺寸；框的上方会出现一个撤销胶囊（"Summary updated, Undo"（摘要已更新，撤销）），焦点移到它上面。
  `ChangeApplied` 会携带更新后的页面被引发；请在那里保存。
- **Undo（撤销）**会以相同的扫过效果恢复该块，卡片显示 "Undone"（已撤销）；它可以再次应用。只有**最后一次**应用的修改可以撤销，撤销胶囊会一直保留到撤销为止。
- **Discard（放弃）**会使卡片变暗并结束它；它不能再被应用。

多张卡片可以同时等待，并且可以按任意顺序应用。

<Demo name="Programmatic" />

### 选区

在页面上拖选文字后，选区下方会出现一个按钮 "Ask about selection"（就选区提问）。按下它会清除选区，在标题中添加一个 "Selection: Summary"（选区：摘要）胶囊，并在该块左侧加上一条线作为标记；框会获得焦点，其占位符提及该块。胶囊会保留给接下来的问题，直到其 × 将它移除。选区属于它开始所在的块；表格选区以表格命名。`SelectionAsked` 会报告它，而 `ClearSelection()` 可以从代码中将其移除。

### 打开、关闭与窄容器

`IsOpen`（双向）、`Open()` 和 `Close()` 用于显示和隐藏助手。停靠时，它的宽度在 `PanelWidth`（360）与零之间以弹簧效果变化，页面列随之重新居中；关闭时，页面右上角会出现一个带有助手名称的胶囊，用于重新打开它。
在面板内按 Escape 会关闭它，并把焦点交还给该胶囊；打开时焦点会进入输入框。在宽度小于 `SheetBreakpoint`（640）的容器中，助手是覆盖在页面上的底部弹层（`SheetHeightRatio`，74%）：打开期间，页面会变暗并且不可操作；按下变暗的页面或关闭按钮即可关闭它。

<Demo name="Narrow" />

### 文案

所有文字都来自 `Labels`（`AiSidePanelLabels`）：要翻译或改写时，替换该实例即可；带格式的文字是函数。`AssistantName` 和 `ModelName` 填充标题；`AssistantMark` 可替换闪光图标（几何图形或图像）。

### 文案规范

- **块标签**：人们会说出口的简短名词（"Summary"、"Pricing table"）。
- **建议**：最多三条，每条都是页面允许助手去做的事（"Shorten the summary"）。
- **修改摘要**：让面板生成；它们是计数，而不是文章。
- **回答**：用一句话说明改了什么；卡片会展示如何改的。

### 最佳实践

- 在 `ChangeApplied` 和 `ChangeUndone` 中保存页面，而不是每次回答都保存。
- 为块提供稳定的 id；修改通过 id 指向它的块。
- 每次回答只提出一个块的修改；人们可以再次提问以获得下一个。
- 让响应器启动得快：最早的反馈是 "Reading the page"（正在阅读页面）。

### 常见问题

**为什么应用没有改变我的数据？** 它改变的是你交给面板的 `Page` 对象；请在 `ChangeApplied` 中保存它。

**为什么只能撤销最后一次修改？** 撤销胶囊属于最新应用的修改；应用另一项修改会替换它，而较早的卡片仍显示 "Applied"（已应用）。

**助手能一次修改多个块吗？** 每张卡片只针对一个块；请再次提问以获得下一个。

## API 参考

<!-- api: AiSidePanel -->

<!-- api: AiPanelPage -->

<!-- api: AiPanelHeading -->

<!-- api: AiPanelParagraph -->

<!-- api: AiPanelTable -->

<!-- api: AiPanelTurn -->

<!-- api: AiPanelChange -->

### 事件

| 事件 | 触发时机 | 参数 |
|---|---|---|
| `ChangeApplied` | 修改已应用，页面数据已更新 | `Change`、`Page` |
| `ChangeUndone` | 修改已撤销 | `Change`、`Page` |
| `ChangeDiscarded` | 修改已放弃 | `Change`、`Page` |
| `SelectionAsked` | 某个选区成为下一个问题的上下文 | `Selection` |

### 响应器

| 类型 | 说明 |
|---|---|
| `IAiPanelResponder` | `RespondAsync(request, cancellationToken)` 返回一个 `AiPanelReply` |
| `AiPanelRequest` | 提示、当前的页面，以及选区（如果有） |
| `AiPanelReply` | 回答文本（通过 `FromText` 整段提供，或以流的形式提供）以及可选的修改 |
| `AiPanelChangeProposal` | 块的 id 以及整个新块 |

## 无障碍

- 面板是一个以助手命名的区域；页面和线程是各自独立的区域。页面中的每个块都以其标签命名。
- 标题中的上下文胶囊说明助手读取的内容；选区胶囊是一个名为 "Remove selection"（移除选区）的按钮，其描述为所选文字。
- 一个礼貌式实时区域会播报：助手正在阅读页面、回答已就绪、已停止或失败、修改已建议、已应用、已撤销或已放弃、已添加选区，以及面板已打开或已关闭。
  流式输出的词不会被逐个播报。
- 修改卡片的按钮有名称并报告其状态；已应用的卡片不是按钮。
- 焦点会去往人们接下来会看的地方：应用后到撤销胶囊，撤销后回到该卡片的 Apply（应用）按钮，放弃或打开面板后进入输入框，关闭后到重新打开的胶囊。
- 在底部弹层模式下，弹层打开期间，其下方的页面不可操作。
- 减弱动效会保留每一种状态和每一条播报。

### 键盘

| 按键 | 操作 |
|---|---|
| `Tab` | 页面控件、标题、线程（卡片、Retry）、撤销胶囊、输入框 |
| `Enter` 或 `Space` | 按下获得焦点的按钮 |
| 输入框中的 `Enter` | 发送；回答写出期间，`Escape` 会停止它 |
| 其他位置的 `Escape` | 关闭助手，并把焦点移到重新打开的胶囊 |

## 动效

- 停靠面板的宽度在打开与关闭之间以弹簧变化；面板主体保持其宽度，并随边缘滑动，而页面重新居中。
- 流式输出的词以每步四到七个、每 50 毫秒一步的方式出现，每一步在 150 毫秒内淡入。
- 应用修改时，一层着色会扫过该块（约 420 毫秒），停留，然后淡出（约 630 毫秒）；旧文字在 100 毫秒内淡出，新文字在 80 毫秒后开始、用 180 毫秒淡入，并且高度以弹簧变化。
- 询问按钮、撤销胶囊和重新打开的胶囊会淡入淡出。
- 底部弹层以滑动和淡入的方式出现。
- 减弱动效会去掉弹簧和位移：扫过效果变为覆盖整个块的着色淡入淡出，弹层原地淡入，文字保持其节奏但不淡入。关闭动效时，每项修改立即生效。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 打开和关闭 | 宽度弹簧 | 120 毫秒淡入 | 立即 |
| 应用或撤销 | 扫过、交叉淡入、高度弹簧 | 整块着色淡入，无位移 | 立即 |
| 新文字 | 整词的步进，150 毫秒淡入 | 相同的步进，无淡入 | 相同的步进，无淡入 |
| 弹层 | 滑动和淡入 | 原地淡入 | 立即 |

## 相关组件

- `AiChat`：用于没有页面的对话。
- `AiComposer`：用于单独的提示框。
- `PromptStudio`：用于编辑提示，而不是页面。

| 组件 | 适用场景 |
|---|---|
| `AiChat` | 没有需要阅读或修改的页面 |
| `AiComposer` | 你只需要输入框 |
| `PromptStudio` | 被编辑的对象是提示模板 |
