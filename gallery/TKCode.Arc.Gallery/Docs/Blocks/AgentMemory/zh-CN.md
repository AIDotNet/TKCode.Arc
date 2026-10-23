---
title: 助手记忆
summary: 一张展示助手记住了什么的卡片：按分组筛选、搜索、原地编辑一句话、忘记时有几秒钟可以撤销，并可归档刚记下的记忆、手动添加一条记忆。
source-hash: 2f0de5bce8d3
---

<Demo name="Preview" />

## 何时使用

助手记忆是人们查看、更正和删除助手为他们保存的内容的页面。它是一张卡片：
一个容量计量条、两个开关、筛选胶囊、一个搜索、按类型分组的记忆，以及一个用于添加记忆的字段。

- **会学习的助手的设置页**："Reference saved memories" 和 "Reference chat history" 是人们会去找的两个开关，开关下方的列表就是已保存内容的证明。
- **对话之后的信任界面**：助手记下了一条内容（"Noted: you run builds with the dotnet CLI"）。已捕获胶囊会显示它，按一下即可把它归档。
- **必须可以复查的记忆**：每一句话都可以原地编辑或忘记，而忘记操作在生效之前会保留几秒钟的撤销机会。
- **有上限的记忆**：计量条显示容量已用了多少，并在快要占满之前发出警告。

## 何时不用

- **事件或消息的日志**：一条记忆是一句不带句号结尾的简短句子。历史记录请使用 `Timeline` 或表格。
- **长篇笔记或文档**：每条记忆最多 `MaxLength` 个字符（默认 200）。长文本请使用编辑器。
- **并非记忆的设置**：开关和选项请放在使用 `Switch` 和 `Select` 的设置页中。
- **无法撤回的删除**：撤销条是设计的一部分。如果你的数据无法在几秒钟内恢复，请在说明文字中写明，并缩短 `UndoDuration`。
- **保存记忆**：卡片只会修改列表并告知你。保存、同步以及助手如何使用这些记忆，都由你的代码负责。

## 用法

`AgentMemory` 派生自 Avalonia 的 `TemplatedControl`。一条记忆是一个 `MemoryItem`，它是一个 `AvaloniaObject`，包含 id、一句话、所属分组的键、来源以及日期。分组是 `MemoryGroup`；若未设置分组，则使用四个默认分组（"About you"、"Preferences"、"Projects"、"People"）。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:AgentMemory AssistantName="Orbit" Capacity="2000">
        <arc:AgentMemory.Memories>
            <arc:MemoryItem Id="1" GroupKey="preferences" SourceKind="Chat"
                            Text="Prefers tables to long bullet lists" />
            <arc:MemoryItem Id="2" GroupKey="people" PersonName="Mina Okafor"
                            Text="Mina Okafor reviews every database migration" />
        </arc:AgentMemory.Memories>
    </arc:AgentMemory>
</UserControl>
```

### 结构

- **标题区**：标题、点明助手名称的副标题，以及容量计量条（细条上方显示 "62% full"）。
- **开关**："Reference saved memories" 和 "Reference chat history"，每个开关下方都有一行说明其当前状态。
- **工具栏**：筛选胶囊，末尾是搜索按钮。
- **已捕获胶囊**：助手最新记下的内容。按下它即可把该记忆归入其分组。
- **列表**：各条记忆，按带数量的标题分组；超过 `ListMaxHeight` 后，卡片内部滚动。
- **添加字段**：输入一句话并按 Enter；卡片会把它归入某个分组。

### 筛选与搜索

胶囊按分组筛选；「全部」视图会显示每个在其标题下有记忆的分组，而选中单个分组时，只显示该分组的记忆，不显示标题（若该分组为空，则显示它自己的一句话）。搜索按钮会在胶囊上方打开一个搜索框。它匹配句子中的任意位置，不区分大小写，并高亮每一处命中。结果按分组显示，并附上每组的命中数；无论当前筛选是什么，搜索都覆盖所有分组。按 Escape 或关闭按钮会清除搜索，并恢复你之前的筛选。

`SelectedGroupKey`、`SearchText` 和 `IsSearchOpen` 都是双向的，因此你可以在代码中设置它们。

<Demo name="Bindings" />

通过 `MemoriesSource` 可以为卡片提供一个要跟随的列表（`ObservableCollection<MemoryItem>` 即可）：卡片会对该列表做出更改，而你在列表中所做的修改也会出现在卡片中。

### 编辑与忘记

行上的编辑按钮（悬停或获得焦点时可见）会打开一个小编辑器：Enter 保存，Shift+Enter 添加一行，Escape 取消。空白或过长的句子会被拒绝并给出提示；超过 `MaxLength` 后会出现计数（"212 of 200 characters"）。忘记会把该行变成一条提示条 "Forgot “…”"，其中带有 **Undo** 按钮，并在 `UndoDuration` 内倒计时。计数和计量条会立即下降；只有在时间耗尽后，更改才会正式提交并触发 `MemoryChanged`。多条提示条可以同时倒计时。

### 保存更改

设置 `CommitAsync`，即可在更改生效之前先保存它。任务运行期间，编辑器会显示保存中的状态。如果任务抛出异常，错误信息会显示在原处，并且更改会被回滚；如果任务被取消，则静默回滚更改。忘记操作会在其撤销窗口结束后才提交。

<Demo name="Commit" />

```csharp
memory.CommitAsync = async (change, cancellation) =>
{
    await api.SaveAsync(change.Kind, change.Memory.Id, change.Memory.Text, cancellation);
};
```

`MemoryChange` 包含 `Kind`（`Capture`、`Add`、`Edit` 或 `Forget`）、`Memory`，以及编辑时的 `PreviousText`。`MemoryChanged` 会报告该次更改以及更改之后的整个列表。

### 已捕获的记忆

把助手刚记下的内容放入 `Captured`：一个 `CapturedMemory`，包含简短的 `Note` 和要归档的 `Memory`。第一条尚未归档的记录会显示为一个胶囊。按下它时，如有需要会先把筛选切换到该记忆所在的分组，再添加该记忆，让它所在的行短暂着色，并把胶囊的文字改为 "Saved to Preferences"，然后收起。`FileCaptured()` 可以用代码完成同样的操作。

### 手动添加

底部的字段用于添加记忆。卡片会把它归入某个分组：若没有设置 `ClassifyAsync`，它会用关键词猜测（两个大写开头的名字归为 People，"prefer" 或 "want" 归为 Preferences，"project" 或 "moving" 归为 Projects，其余归入第一个分组）。设置 `ClassifyAsync` 即可交由你自己的代码或模型来判断。当添加会超出容量时，添加会被拒绝并显示 "Memory is full"，文字保留在字段中。将 `IsAddEnabled` 设为 false 即可得到只读卡片。

### 容量

`Capacity` 统计所有记忆的字符总数。超过 `WarningRatio`（默认 90%）时，计量条会以颜色和图标发出警告，而不只依靠颜色。容量为零时，计量条和上限都会隐藏。

<Demo name="Capacity" />

### 暂停

关闭 "Reference saved memories" 会让列表变暗，并用一句说明记忆已暂停的文字取代已捕获胶囊。列表仍可编辑；添加和忘记依然有效。`IsSavedMemoriesEnabled` 和 `IsChatHistoryEnabled` 是双向的，`SettingToggled` 会报告更改。

<Demo name="Paused" />

### 文案

所有文字都来自 `Labels`（`AgentMemoryLabels`）：替换该实例即可翻译或改写。格式化文字是函数，因此可以调整词序。

```csharp
memory.Labels = new AgentMemoryLabels
{
    Title = "Souvenirs",
    SavedTo = group => $"Rangé dans {group}",
    NoMatches = query => $"Aucun souvenir ne correspond à « {query} »"
};
```

在控件上设置 `Title` 和 `Subtitle` 时，它们优先于 labels 中的文字。

### 内容规范

- **一条记忆是一句不带句号结尾的话**，用第三人称或平实的陈述来写，例如 "Prefers tables to long bullet lists"（偏好表格而非长列表）。
- **具体且中立**：写 "Is training for a spring half marathon"（正在为春季半程马拉松训练），而不是 "Loves running!"。
- **人物**：把名字写进句子和 `PersonName`；该行会显示一个带首字母的小头像（或 `PersonImage`）。
- **来源**：`Chat` 显示 "From a chat"，`Manual` 显示 "Added by you"，并附上简短日期。设置 `SourceUri` 即可把来源变为链接，并处理 `SourceRequested` 或 `SourceCommand` 以打开对话。

### 最佳实践

- 使用 `CommitAsync` 保存，并显示真实的错误；绝不要吞掉失败，卡片会替你回滚更改。
- 将 `UndoDuration` 保持在五秒或以上；更短的窗口对屏幕阅读器用户很难使用。
- 把卡片显示的同一份列表也提供给助手：人们看不到的内容，就无法更正。
- 保持四到六个分组；更多的话，胶囊会滚动。

### 常见问题

**为什么我的忘记操作没有立即调用 `CommitAsync`？** 它会在撤销窗口结束时提交；在此之前仍可撤销。

**能使用我自己的分组吗？** 可以。添加带有唯一键的 `MemoryGroup`，并为每条记忆设置 `GroupKey`。键与任何分组都不匹配的记忆会显示在第一个分组中。

**这张卡片也适合窄的侧边栏吗？** 宽度低于 440 px 时，它会收紧内边距，让副标题换行，并允许筛选项滚动。

## API 参考

<!-- api: AgentMemory -->

<!-- api: MemoryItem -->

<!-- api: MemoryGroup -->

<!-- api: CapturedMemory -->

### MemoryChange

| 属性 | 说明 |
|---|---|
| `Kind` | `Capture`、`Add`、`Edit` 或 `Forget` |
| `Memory` | 发生变化的记忆（忘记时为变更前的样子） |
| `PreviousText` | 编辑前的文本；其他类型为 null |

### 事件

| 事件 | 触发时机 | 参数 |
|---|---|---|
| `MemoryChanged` | 一次更改被提交 | `Change`、`Memories`（更改之后的整个列表） |
| `SettingToggled` | 两个开关之一发生变化 | `Setting`（`SavedMemories` 或 `ChatHistory`）、`IsOn` |
| `SourceRequested` | 按下了带链接的来源 | `Memory` |

## 无障碍

- 计量条会被朗读为 "Memory used, 62% full"，并读取真实的值，而不是动画中的值。
- 开关具有名称，其说明会随状态更新。筛选胶囊构成一组，名称为 "Filter memories"，报告哪一个被按下，并且只占一个 Tab 停靠点；左右方向键用于移动并选中。
- 行是分组内的列表项，分组以其标题命名。编辑和忘记按钮以句子命名（"Edit memory: …"），并带有工具提示；它们在悬停和键盘焦点时出现，且不占用额外空间。
- 礼貌的实时区域会播报搜索结果、已保存的编辑、带撤销时间的忘记、撤销窗口结束、恢复、归档和添加、记忆已满，以及每一个开关。
- 焦点会移到人们接下来会看的地方：进入编辑器，保存或取消后回到编辑按钮；忘记后移到 Undo，撤销后回到忘记按钮；移到搜索框，关闭后回到搜索按钮；用键盘归档已捕获的记忆后，移到新行的编辑按钮。
- 状态不只依靠颜色：警告计量条会添加图标，错误会添加图标和一句话，已保存的状态则显示对勾。
- 减弱动效会保留每一种状态、每一条播报以及撤销时间。

### 键盘

| 按键 | 操作 |
|---|---|
| `Tab` | 开关、当前选中的筛选胶囊、搜索按钮、已捕获胶囊、每行的编辑和忘记、添加字段 |
| 胶囊上的 `ArrowLeft` / `ArrowRight` | 移到上一个或下一个胶囊并选中它；首尾循环 |
| 胶囊上的 `Home` / `End` | 第一个或最后一个胶囊 |
| `Enter` 或 `Space` | 按下获得焦点的按钮、开关或胶囊 |
| 编辑器中的 `Enter` | 保存；`Shift+Enter` 添加一行 |
| 编辑器中的 `Escape` | 取消 |
| 搜索中的 `Escape` | 清除、关闭搜索，并回到搜索按钮 |
| 添加字段中的 `Enter` | 归类并添加记忆 |

## 动效

- 筛选时，离开的分组和行在 100 ms 内淡出，进入的分组和行在短暂延迟后于 160 ms 内淡入，它们的高度则跟随临界阻尼弹簧变化。选中胶囊的背景会在各胶囊之间滑动。
- 搜索框在 240 ms 内于胶囊上方展开，同时胶囊淡出；其输入框和关闭按钮淡入。
- 一行、它的编辑器和撤销条之间做交叉淡化（淡出 100 ms，淡入 160 ms），同时该行的高度在两者之间以弹簧变化。
- 撤销圆环沿撤销窗口匀速耗尽；秒数每秒递减一次。
- 归档或添加的行先以平滑弹簧展开其位置，然后该行着色 1.6 秒并淡出。
- 计量条的填充和数字跟随同一个弹簧；警告颜色立即切换。
- 说明、已捕获胶囊的文字以及添加字段的状态会交叉淡化，出现时上移 2 px。
- 减弱动效会去掉弹簧、移动和耗尽动画：高度直接跳变，交叉淡化变为短暂淡化，圆环保持满格，秒数仍然递减。关闭动效时，每项变化都立即生效。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 筛选或搜索 | 100 ms 淡出，160 ms 淡入；高度弹簧 | 短淡化；高度直接跳变 | 立即 |
| 行切换为编辑器或撤销条 | 交叉淡化；高度弹簧 | 120 ms 交叉淡化 | 立即 |
| 选中的胶囊 | 弹簧滑动 | 直接跳变 | 立即 |
| 搜索展开 | 240 ms 内展开宽度 | 立即 | 立即 |
| 撤销圆环 | 沿撤销窗口耗尽 | 保持满格 | 保持满格 |
| 归档或添加的行 | 位置以弹簧展开；1.6 秒着色 | 无位置动画；着色保留 | 立即 |

## 相关组件

- `ModelPicker`：用于选择使用这些记忆的模型。
- `Switch`：用于与记忆无关的设置。
- `Timeline`：用于事件的历史记录。

| 组件 | 适用场景 |
|---|---|
| `Switch` | 一个设置需要开或关 |
| `Timeline` | 列表是按时间顺序排列的历史 |
| `Textarea` | 人们需要写较长的文本 |
| `Toast` | 一条短消息确认某个操作，并自动消失 |
