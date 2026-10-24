---
title: Onboarding illustration
summary: 一张紧凑的首次运行卡片，线条插画会随着人们逐步浏览而形变：从空盒子变成文件、团队和徽章。
source-hash: e287773de8c6
---

<Demo name="Preview" />

## 何时使用

`OnboardingIllustration` 是人们创建账户或工作区后首先看到的内容：一张简短的卡片，每一步解释一个概念，并配有一幅简单的插画。插画是同一套线条图，其中的图形会在各个场景之间移动，因此各步读起来像同一个故事（空白的空间、内容到达、人员加入、完成），而不是四张彼此独立的幻灯片。把它放在对话框或首次运行页面中，并让 "Get started" 把人带到设置工作区的流程。

- **首次启动的文档或工作区产品**：笔记应用、知识库或一开始为空的文件空间。各步说明内容存放在哪里、如何导入文件以及如何邀请他人，点击 "Get started" 会打开新的空间。
- **新项目、看板或频道**：项目工具可以带人创建第一块看板，团队聊天应用可以创建频道并邀请成员。通常三步就够了。
- **改变人们工作方式的功能**：共享审阅、对某一行添加评论或新的导入功能。在人们第一次使用前，用三到四步做一次简短导览。
- **人们会反复回来看的导览**：完成状态会提供 "Replay intro"，因此同一张卡片可以放在帮助菜单项之后，例如 "Show the tour again"。

## 何时不用

- **需要数日才能完成的任务**：请使用检查清单，跟踪每项任务的进度，并在最后一项完成前保持可见。这张卡片不会记住人们读过哪些步骤。
- **分多步收集信息的表单**：请使用 `Stepper` 或多步表单。这张卡片只展示信息；它没有字段，也没有校验。
- **营销页面中的功能区块**：请使用配有更大插画和行动号召按钮的功能区块。卡片按对话框尺寸设计（宽度最多 440 px）。
- **长篇解释**：每一步只包含一个标题和一两句话。需要小标题、列表或代码的内容应放在文档中，并在最后一步提供链接。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:OnboardingIllustration Label="Northdesk onboarding"
                                GetStartedCommand="{Binding CreateWorkspaceCommand}" />
</UserControl>
```

`OnboardingIllustration` 派生自 `TemplatedControl`。没有设置步骤时，它会显示四个示例步骤，每个场景一步，因此在你撰写文字之前它看起来已经完整。该区块本身不做额外工作：它负责切换步骤、引发事件，其余交给应用处理。处理 `GetStarted`（或绑定 `GetStartedCommand`）来创建工作区、关闭对话框或打开下一页。

### 结构

1. **卡片**：宽度最多 440 px（容器更窄时随之变窄），1 px 边框，20 px 圆角，使用凸起表面颜色和凸起阴影。`IsFramed="False"` 会去掉边框、圆角和阴影。
2. **舞台**：底部带 1 px 线条的弱化表面。上方内边距 40 px，两侧 20 px，下方 16 px。
3. **插画**：320 × 200 的画布按比例缩放以适应，最大为 352 × 220 px。包含七个圆角图形、一条虚线轨迹、三条短线和一个对勾。1.5 px 的描边随插画一起缩放。
4. **跳过**：一个 28 px 的胶囊按钮，位于舞台右上角向内 10 px 处，出现在除最后一步以外的每一步。
5. **文案**：步骤标题（展示字体，18 px，中等字重）位于句子（14 px，次要文字颜色）上方。文案区域至少 72 px 高，因此单行句子与两行句子的按钮位置相同。
6. **步骤圆点**：每步一个 6 px 的圆点；当前步的圆点宽 20 px。已完成的步骤使用弱化文字颜色，当前步使用强调色，之后的步骤使用强边框颜色。每个圆点都是一个按钮，点击区域为 32 px。
7. **返回**：带箭头的 36 px 描边按钮。在第一步和完成状态下它看起来是禁用的，但仍保留在 Tab 顺序中。
8. **主按钮**：宽 136 px，填充前景色。非最后一步显示 Next，最后一步显示 Get started，导览完成后显示 Replay intro。宽度不会随文字变化。

### 场景

每一步对应一个场景。各场景共享相同的图形，每个图形在每个场景中都扮演一个角色，因此从一步切换到下一步看起来像同一幅画面在变化，而不是幻灯片切换。

| 场景 | 展示内容 | 用于 |
|---|---|---|
| `Folder` | 一个敞开的空盒子，旁边有加号 | 新的空白空间；第一步 |
| `Files` | 三页纸落入盒子，同时一张便条沿虚线轨迹飞入 | 导入内容：导入、上传、连接的数据源 |
| `Team` | 三个人站在一块共享看板下方；一条虚线评论指向一行高亮文字 | 邀请他人、共享、共同审阅 |
| `Badge` | 一枚圆形徽章，带两条缎带、三道光芒，以及逐渐描出的对勾 | 完成：最后一步 |

场景可以出现在多步中；两步使用同一场景时，只有文字会变化。步骤不必按上表顺序排列，但按顺序时故事最易读，因为每个图形都会移动到它的下一个角色。

### 自定义步骤

<Demo name="CustomSteps" />

```xml
<arc:OnboardingIllustration Label="Fieldnote onboarding"
                            CompletedTitle="Study created"
                            CompletedDescription="Open the tour again from Help whenever you like.">
    <arc:OnboardingStep Scene="Folder"
                        Title="Name your research space"
                        Description="A space holds one study. Start with the April interviews and add more studies later."
                        SceneDescription="An empty open box with a plus sign beside it" />
    <arc:OnboardingStep Scene="Files"
                        Title="Import your recordings"
                        Description="Drop audio, transcripts and notes. Fieldnote tags each file by speaker and date."
                        SceneDescription="Recordings dropping into the open box while a note flies in" />
</arc:OnboardingIllustration>
```

`Steps` 是一个 `AvaloniaList<OnboardingStep>`，也是控件的内容，因此可以直接写在元素内部。视图模型也可以通过 `StepsSource` 提供步骤；它优先于 `Steps`，可观察集合的变化会立即显示。在显示过程中编辑某一步的 `Title`、`Description` 或 `Scene`，卡片会原地更新。

`SceneDescription` 是该步插画的无障碍名称。请用一个简短的短语描述插画展示的内容，例如 "Three people gathered around one shared board"。

按钮文字、圆点的无障碍名称以及朗读的状态文字都来自 `Labels`，这是一个带有英文默认值的 `OnboardingIllustrationLabels` 对象。包含数字的文字是函数，因此在其他语言中可以调整词序：

```csharp
card.Labels = new OnboardingIllustrationLabels
{
    Skip = "Skip tour",
    GetStarted = "Open study",
    ReplayIntro = "Show tour",
    StepStatus = (step, count) => $"Tour step {step} of {count}",
    DotName = (step, title) => $"Tour step {step}: {title}"
};
```

### 步骤、完成与事件

| 操作 | 结果 | 事件 |
|---|---|---|
| Next（主按钮，最后一步之前的步骤） | 下一步 | `StepChanged`（`Next`） |
| Back | 上一步；第一步时无变化 | `StepChanged`（`Back`） |
| 步骤圆点 | 跳到该步，即使相隔多步；退出完成状态 | `StepChanged`（`Dot`） |
| 左 / 右方向键 | 上一步或下一步；两端或完成后无变化 | `StepChanged`（`Keyboard`） |
| 跳过（Skip） | 最后一步（不是完成状态） | `StepChanged`（`Skip`），然后 `Skipped` |
| Get started（最后一步） | 完成状态，前提是 `IsCompletionVisible` | `GetStarted` |
| Replay intro | 第一步 | `StepChanged`（`Replay`），然后 `Replayed` |

`CurrentIndex`（从 0 开始计数）和 `IsCompleted` 是双向的，因此视图模型可以跟随卡片，也可以移动它。将 `IsCompleted` 设为 true 会移动到最后一步；在完成状态下设置 `CurrentIndex` 会退出完成状态。方法 `Next`、`Back`、`GoTo`、`Skip`、`Complete` 和 `Replay` 在代码中执行相同的操作，无事可做时返回 false。

<Demo name="Controlled" />

`StepChanged` 携带旧索引、新索引和原因，其中包括代码所做更改的 `Programmatic`。`GetStartedCommand` 和 `SkipCommand` 在对应事件之后运行，并以事件参数作为参数。

<Demo name="EventsLog" />

```csharp
card.GetStarted += async (_, _) =>
{
    await workspaces.CreateAsync(name);
    dialog.Close();
};
```

当应用自行关闭对话框时，请设置 `IsCompletionVisible="False"`：此时点击 Get started 会引发 `GetStarted`，卡片停留在最后一步，而不是显示完成状态。若卡片会一直留在屏幕上（帮助页面或侧边面板），则保持开启，使人们看到导览已完成并可以重播。

### 窄布局

卡片依据自身宽度而非窗口宽度调整。宽度小于 410 px 时切换为紧凑布局：插画上方 42 px、周围 12 px，文案周围 16 px，标题更小，文案区域至少 88 px（可容纳三行），Back 变为 36 px 的箭头按钮（屏幕阅读器仍朗读为 "Back"），主按钮宽 128 px。插画随卡片缩小，并保持比例。

<Demo name="Narrow" />

在手机上，卡片会从边缘到边缘铺满屏幕，此时请设置 `IsFramed="False"`。

### 克制地使用动效

形变是卡片的标志性特征：盒子变成纸张的去处，纸张变成人，人变成徽章。它之所以有效，是因为每一步都很短，并且节奏由读者掌控。

- 每一步只保留标题和一两句话。插画承载变化；冗长的文字会分散注意力，并迫使读者等待文字而不是看图。
- 让人们掌控步骤。不要用计时器自动前进：形变大约需要一秒，而一个在读者阅读时自行前进的步骤会被错过。
- 减少动效时，形变会替换为整幅插画 240 ms 的交叉淡化，文案淡入而不移动。

<Demo name="ReducedMotion" />

### 内容指南

- **标题**：说明人在此步骤中做什么或得到什么，四到七个词，句首大写，例如 "Bring the files you already use"。不使用句末标点。
- **句子**：一到两句话，附一个来自其空间的具体例子，例如 "Drag in slides, sheets, and PDFs."。应用已知时，请提及真实的名称（项目或队友）。
- **最后一步**：确认已经准备好的内容，若数量已知，请附上数量，例如 "Spring launch notes now has 5 files and 2 teammates."。
- **完成状态**：说明发生了什么以及如何再次查看导览，各用一句简短的话。
- **场景描述**：描述画面，而不是步骤："Pages dropping into the open box"，而不是 "Import step"。

| 避免写成 | 改为 |
|---|---|
| Welcome to our amazing platform! | Set up your first space |
| Step 2 | Bring the files you already use |
| You can do lots of things with teams. | Invite Ana and Ruben to the same draft. |
| Success! | You are all set |
| Finish | Get started |

### 最佳实践

**建议**

- 导览保持三到四步；步骤更多时，请把其余内容放到人们会遇到它的产品界面中。
- 按故事顺序使用场景（`Folder`、`Files`、`Team`、`Badge`），使每个图形都移动到它的下一个角色。
- 让 Get started 带人去一个有用的地方：新工作区、导入对话框或第一个项目。
- 保留跳过功能。已经熟悉产品的人应能一键到达终点。
- 在帮助菜单中提供再次查看导览的入口，并在那里显示完成状态，使 Replay intro 只需一次按下。

**避免**

- 在卡片中收集输入。它没有字段；设置和名称请使用表单。
- 通过计时器或滚动自动前进。应由人决定何时前进。
- 隐藏圆点。圆点显示导览有多长，并让人跳回想再读的步骤。
- 在导览中途使用徽章场景：它表示"完成"，会让正在导览的人感到困惑。

### 常见问题

**如何只显示一次卡片？** 卡片不会记住任何内容。在引发 `GetStarted`（或 `Skipped`）时保存一个标记，下次不再打开对话框。

**为什么 Back 在第一步仍可获得焦点？** Back 被标记为不可用，但仍留在 Tab 顺序中（Arc 的 `aria-disabled`），因此步骤使其不可用时焦点不会跳走。屏幕阅读器会将其朗读为已禁用，按下它不会有任何反应。

**跳过后焦点去向何方？** 跳过按钮在最后一步会消失。若它原本有焦点，焦点会移到主按钮（Get started），键盘用户可以按 Enter 继续。

**一个步骤能否复用场景？** 可以。两步使用同一场景时，在它们之间切换只会改变文字。

**卡片能否超过四步？** 可以，也可以少到一步（那时唯一的按钮是 Get started）。圆点会随步骤增加；超过五或六步时，通常应当缩短导览，而不是增加圆点。

## API 参考

<!-- api: OnboardingIllustration -->

<!-- api: OnboardingStep -->

<!-- api: OnboardingIllustrationLabels -->

<!-- api: OnboardingStepChangedEventArgs -->

## 无障碍

- 卡片是一个区域，名称由 `Label` 指定（"Northdesk onboarding"）。首次运行时如果它在对话框中打开，请为对话框设置自己的名称。
- 插画是一幅图片，名称为当前步骤的 `SceneDescription`；名称随步骤变化。
- 每次步骤切换，都会通过礼貌的实时区域朗读新的标题、句子和位置（"Step 2 of 4"）。完成状态会朗读其标题和句子，随后是 "Onboarding complete"。
- 圆点是一组，名称为 "Onboarding steps"。每个圆点是一个按钮，名称例如 "Step 2: Bring the files you already use"，当前圆点会报告 "Current step"。
- 在第一步和完成状态下，Back 会被报告为禁用，但仍可获得焦点。在紧凑布局中它只显示箭头，名称仍为 "Back"。
- 每次操作后焦点都留在原处：Next、Get started 和 Replay intro 之后在主按钮上，Back 之后在 Back 上，点击圆点之后在该圆点上。当跳过按钮在有焦点时消失，焦点会移到主按钮。

### 键盘

| 按键 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 依次经过跳过、每个步骤圆点、Back 和主按钮 |
| `Left` / `Right` | 上一步或下一步，在卡片内任何位置均可；焦点不移动。两端无变化，完成后也无变化（`Right` 从不按下 Get started） |
| `Enter` / `Space` | 按下获得焦点的按钮或圆点 |

## 动效

| 变化 | 完整 | 减少 | 关闭 |
|---|---|---|---|
| 步骤切换 | 每个图形以弹簧移动到下一个场景（中心约在半秒内到位，尺寸、圆角和转角稍慢一些），彼此错峰 30 ms 出发；返回时，最后一个图形先移动。轨迹在 100 ms 时弯入，其圆点在 160 ms 时出现；线条依次在 120、160 和 200 ms 跟随；颜色在 480 ms 内交叉过渡 | 旧画面淡出的同时新画面淡入，240 ms | 立即 |
| 徽章出现 | 对勾从 320 ms 开始，历时 480 ms 描画出现 | 直接显示为完成 | 直接显示为完成 |
| 大约 0.7 秒内的第二次切换 | 每个图形立即朝新场景转动，并保持其速度 | 交叉淡化 | 立即 |
| 文案变化 | 旧文字上移 4 px 并在 180 ms 内模糊淡出；新文字从 80 ms 起由模糊中上升 6 px，历时 480 ms | 文字淡变（180 ms 淡出，480 ms 淡入） | 立即 |
| 主按钮标签变化 | 旧标签在 180 ms 内上移淡出；新标签在 240 ms 内上升淡入 | 标签淡变 | 立即 |
| 步骤圆点 | 当前圆点以弹簧方式加宽至 20 px，旧圆点收窄至 6 px；颜色在 240 ms 内交叉过渡 | 宽度立即变化；颜色仍交叉过渡 | 立即 |
| 跳过出现或消失 | 160 ms 淡入 | 160 ms 淡入 | 立即 |
| 鼠标移到舞台上 | 图形朝指针方向漂移，水平最多 6 px，向下最多 4 px，位于前方的图形漂移更多 | 不漂移 | 不漂移 |

卡片没有入场动画，静止时也没有动效：插画只会在操作之后或鼠标移动时才运动。

## 相关组件

- `EmptyStates`，用于空视图，其插画在四种情况之间形变。
- `Stepper`，用于收集输入并逐步校验的多步任务。
- `Dialog`，用于在首次运行时承载卡片。

| 组件 | 适用场景 |
|---|---|
| `OnboardingIllustration` | 三到四个概念的简短首次运行导览，每个配一幅插画 |
| `EmptyStates` | 视图为空，插画说明原因 |
| `Stepper` | 人们分多步填写表单 |
| `Dialog` | 首次运行时卡片打开在应用之上 |
