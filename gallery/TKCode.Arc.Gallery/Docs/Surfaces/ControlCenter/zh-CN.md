---
title: Control center
summary: 一个快捷设置面板：磁贴可展开为各自的详情，带有专注时段转盘，以及在两端会被拉伸的音量与文字大小分段条。
source-hash: 0eb10cab1063
---

<Demo name="Preview" />

## 何时使用

`ControlCenter` 是一个紧凑的面板，汇集了工作区中的日常开关：一个宽的 Focus 磁贴、Notifications 与 Presenting 磁贴，以及两个分别控制提醒音量和文字大小的分段条。每个磁贴按一下即可切换，并说明它此刻的含义（"Until 12:13 PM"、"2 of 3 channels"、"Off"）。带箭头按钮的磁贴可以展开为铺满面板的详情，更细的设置都在其中；完成后它会缩回原位。面板尺寸为 360 × 396 像素，适合放在弹出层、侧边面板或设置页面的一角。

- **快捷设置弹出层**：位于状态栏或头像按钮之后，汇集人们一天中多次用到的少数几个开关，每个都只需按一下。
- **专注时段**：选择一种模式，在转盘上设定时长（转一圈是两小时，步长为 5 分钟），然后开始。之后磁贴会显示该模式及时段的结束时间。
- **两个层级的通知控制**：磁贴一次性暂停所有通知；详情中为每个频道提供一个开关，并显示哪些人的消息始终放行。
- **需要有实体手感的数值**：分段条一对一地跟随拖动，点按时直接跳到该位置，按住时会膨胀，到达两端时会略微拉伸，而不是戛然而止。

## 何时不用

- **完整的设置页面**：大量分组、搜索和说明需要一个由 `Switch`、`Slider` 与表单行组成的页面，而不是固定尺寸的面板。
- **单个开/关选项**：在对该选项有说明的地方使用 `Switch`。
- **精确的数值输入**：需要键入或精确读取到小数的值，应使用带输入框的 `Slider`，或数字输入框。
- **超过两个下钻区域或更深层的导航**：面板只有两个详情和一个层级；需要叠放的面板时，请使用底部面板或页面导航。
- **日程安排**：这里的时段从现在开始，最长持续两小时。更晚开始或更长的安排适合使用日历或时间选择器。

## 用法

为面板提供专注模式、频道以及（可选的）人员。第一个模式就是点按 Focus 磁贴时开始的模式，时长为该模式的默认时长。

```xml
<arc:ControlCenter Volume="{Binding AlertVolume}"
                   TextSize="{Binding TextSize}"
                   AreNotificationsOn="{Binding NotificationsOn}"
                   IsPresenting="{Binding Presenting}"
                   FocusSession="{Binding Session}" />
```

```csharp
center.FocusModes.Add(new ControlCenterFocusMode
{
    Id = "drafting", Label = "Drafting", Icon = ControlCenterSamples.HeadphonesIcon,
    Description = "Long stretches for writing and sketching", DefaultMinutes = 50
});
center.Channels.Add(new ControlCenterChannel { Id = "comments", Label = "Comments", IsOn = true });
center.People.Add(new ControlCenterPerson { Id = "mara", Name = "Mara Lind" });
```

`ControlCenter` 派生自 Avalonia 的 `TemplatedControl`。它位于 Special 包中：在 `UseArc()` 旁边用 `UseArcSpecial()` 注册它。`ControlCenterSamples.Apply(center)` 会用预览中的虚构内容填充一个面板。

用户可以更改的每个值都是双向属性，每次更改后 `StateChanged` 都会连同原因报告完整的状态。拖动分段条时，每经过一个完整的步长报告一次，而不是每移动一个像素报告一次。文字大小只会缩放面板中的 "Aa" 示例：请自行将其应用到你的应用中。

<Demo name="Events" />

### 结构

1. **面板**：宽 360（若容器更窄，则为容器宽度），高度始终为 396；凸起表面、1px 边框、圆角 30、浮动阴影、10px 内边距。
2. **Focus 磁贴**：占满整个宽度，高 84。包含图标圆圈、标题和状态行；右侧的箭头按钮用于打开 Focus 详情。
3. **Notifications 与 Presenting 磁贴**：半宽，高 104，图标在上，文字在下。Notifications 的角上有一个箭头按钮。
4. **分段条**：标签行（图标、标签、数值，文字大小还带有 "Aa" 示例）位于一条由 28 个分段组成的 30px 轨道之上。
5. **详情**：距面板边框内缩 8，圆角 22，覆盖在一层不透明度为 55% 的凸起表面遮罩之上。其标题栏包含图标、标题和 Close。

处于"开"状态的磁贴会在其表面混入 9% 的强调色，并用强调色填充图标圆圈。磁贴没有悬停变化；只有箭头按钮和 Close 会响应指针。

### 每种状态

<Demo name="States" />

| 磁贴 | 关 | 开 |
|---|---|---|
| Focus | "Focus"、"Off"、耳机图标 | 模式名称、"Until {time}"、模式的图标 |
| Notifications | "Paused"、带斜线的铃铛 | "{on} of {all} channels"、铃铛（开启 0 个频道时也是如此） |
| Presenting | "Off" | "Popups held back"（`Labels.PresentingOn`） |

状态变化时，图标和状态行会在一次短暂的模糊中交换：旧的淡出、变模糊并上移 8px，新的则从下方 8px 处升起。Focus 标题会立即改变。

### Focus 详情

- **模式切换**：每个模式一个分段，一个凸起的胶囊会滑动到选中的分段上。选择一种模式会把转盘设为该模式的默认时长，并滚动显示其说明。
- **转盘**：转满一圈是 120 分钟。在任意位置抓住并转动：它按你转动的角度移动，而不是跳到你按下的位置，因此点按不会改变任何东西。它在 12 点位置会继续计数，超出 5 分钟和 120 分钟时会产生阻力（弧线和数字停在极限值），松开时吸附到最接近的 5 分钟。数字下方显示若现在开始一个时段，它将在何时结束。
- **按钮**：没有时段时显示 Start session；有时段时显示 End session 和 Update session。

详情编辑的是一份草稿。Start 和 Update 会应用它（时段随后在当前时间加上其分钟数时结束）；Close、Escape 或按一下遮罩会丢弃草稿，再次打开时显示正在进行的时段的模式和时长。时段不会自行结束：时间到了之后，如有需要，请在代码中用 `EndFocus()` 结束时段。

### Notifications 详情

顶部的总开关与磁贴是同一个设置。总开关关闭时，各频道开关会变暗但仍然可用，磁贴也仍会记住每个频道的状态。频道行来自 `Channels`；列表较长时会在详情内滚动。设置了 `People` 时，详情底部会显示他们的头像（图片，或生成的渐变上的姓名首字母），以及一句点出他们名字的话。

### 你自己的标签和内容

面板显示的所有文字都来自 `ControlCenterLabels`：磁贴标题、状态、按钮文字、无障碍名称、时间格式，以及包含数字的句子（委托）。这个演示完全用 XAML 编写，只有一个专注模式、五个频道，没有人员。

<Demo name="Custom" />

### 窄容器

面板会采用更窄容器的宽度。当容器宽度不超过 312，或窗口宽度不超过 360 时，它会收紧间距（6px 内边距和间隙，更小的磁贴内边距），并使用 150px 的转盘。在宽度不超过 455 的窗口中，它还会去掉边框、圆角、阴影和填充，从而贴边嵌入页面，而打开的详情仍保留自己的表面。

<Demo name="Narrow" />

### 减少动效

<Demo name="ReducedMotion" />

### 内容准则

- 模式名称控制在一到两个词（"Drafting"、"Reviewing"）；它们要共享模式切换的宽度。
- 模式说明写成一个简短的句子，说明哪些内容仍会送达。过长的文字会在面板宽度处被截断。
- 按频道传递的内容为其命名（"Comments"、"Shared folders"），并用第二行说明何时传递。
- Presenting 的状态行应该用两三个词说明演示时会做什么。
- 传入人员时使用全名；句子中使用名字，头像使用姓名首字母。

### 最佳实践

- 持久化来自 `StateChanged`（或绑定属性）的状态，并通过设置相同的属性来恢复它。
- 为转盘的各个模式设定合理的默认值：最常用的时长应该一按即得。
- 使用 `TimeProvider` 为面板提供应用的时钟（也可用于测试时段的结束时间）。
- 用 `Labels.FormatTime` 为用户格式化时间；默认是美式短时间格式（"12:13 PM"）。
- 从代码中打开详情请使用 `OpenDetail`（例如来自深层链接），而不是模拟点击。

### 常见问题

**为什么时段不会自行结束？** 面板会显示时段何时结束，但不会据此采取行动，这样宿主可以按自己的需要延长时段、发出通知或结束时段。请调用 `EndFocus()`，或将 `FocusSession` 设为 null。

**可以添加第三个详情或更多磁贴吗？** 不可以：布局是固定的。请使用多个面板，或用 `Switch` 和图库中的其他控件自行构建。

**为什么点按转盘会被忽略？** 转盘按你转动的角度移动，因此一次误触永远不会改变时长。相比之下，分段条在你松开时会跳到点按的位置。

## API 参考

<!-- api: ControlCenter -->

<!-- api: ControlCenterFocusMode -->

<!-- api: ControlCenterChannel -->

<!-- api: ControlCenterPerson -->

<!-- api: ControlCenterFocusSession -->

<!-- api: ControlCenterState -->

<!-- api: ControlCenterLabels -->

<!-- api: ControlCenterStateChangedEventArgs -->

<!-- api: ControlCenterDetailEventArgs -->

<!-- api: ControlCenterSamples -->

枚举：

| 类型 | 取值 |
|---|---|
| `ControlCenterDetail` | `None`、`Focus`、`Notifications` |
| `ControlCenterChangeReason` | `FocusStarted`、`FocusUpdated`、`FocusEnded`、`NotificationsToggled`、`ChannelToggled`、`PresentingToggled`、`VolumeChanged`、`TextSizeChanged` |

## 无障碍

- 面板是一个以 `Labels.PanelName` 命名的组。每个磁贴都是一个切换按钮，其名称读出标题和状态（"Notifications, 2 of 3 channels"）；箭头按钮分别命名为 "Focus session settings" 和 "Notification channels"。
- 打开的详情以其标题命名。其后方的磁贴和分段条会退出 Tab 顺序、命中测试和自动化树，焦点会一直留在详情内，直到详情关闭。
- 模式切换是一个名为 "Mode" 的单选组。转盘是一个名为 "Session length" 的滑块，范围 5 到 120，其帮助文本读出分钟数和结束时间（"50 minutes, until 12:13 PM"）。分段条是以各自标签命名的滑块，数值读作 "64%"。
- 开关以所在行的标签命名。头像仅作装饰：名字已在旁边的句子中。
- 键盘焦点在磁贴、按钮、分段、转盘或轨道外侧 2px 处显示一个 2px 的环；指针焦点不绘制任何环。

### 键盘

| 按键 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | Focus 磁贴、其箭头按钮、Notifications 磁贴、其箭头按钮、Presenting 磁贴、音量、文字大小。在详情内：Close、选中的模式、转盘、各按钮，然后回到 Close |
| 磁贴上的 `Enter` / `Space` | 切换它（Focus 磁贴会开始第一个模式或结束时段） |
| 箭头按钮上的 `Enter` / `Space` | 打开其详情；焦点移到 Close |
| 模式切换中的 `←` `→` `↑` `↓` / `Home` / `End` | 选择上一个、下一个、第一个或最后一个模式 |
| 转盘上的 `←` `↓` / `→` `↑` | 缩短或延长 5 分钟 |
| 转盘上的 `Page Down` / `Page Up` | 缩短或延长 15 分钟 |
| 转盘上的 `Home` / `End` | 5 分钟或 120 分钟 |
| 分段条上的 `←` `↓` / `→` `↑` | 一个步长（音量 1%，文字大小 5%） |
| 分段条上的 `Page Down` / `Page Up` | 四个步长 |
| 分段条上的 `Home` / `End` | 最小值或最大值 |
| `Escape` | 关闭详情；焦点回到其箭头按钮 |

每个磁贴是一个 Tab 停靠点，模式切换则是一个用方向键操作的单一 Tab 停靠点，这正是单选组应有的行为。

## 动效

- 打开详情时，磁贴的外框会以一个无回弹的弹簧（约 0.75 s 稳定）扩展为详情的外框。圆角全程保持 22，内容按最终尺寸布局，因此扩展的外框是逐步显露内容，而不是拉伸内容；图标和标题会从磁贴滑入标题栏。遮罩在 230ms 内淡入，内容在延迟 60ms 后于 240ms 内淡入。
- 关闭时以同一个弹簧反向运行。内容在 110ms 内淡出；磁贴在前半程淡入回来，详情则在半程到 95% 之间淡出。
- 按下磁贴时，它会以同一个弹簧缩小到 0.975。磁贴颜色在 240ms 内变化。
- 模式胶囊以同一个弹簧滑动；转盘在切换到新模式、按键或吸附时快速弹动，从两端之外回弹时则较慢。分段条的数值在点按或按键时弹动到位，数字随之计数；按住时轨道会膨胀到其高度的 1.18 倍，被拉伸后会弹回。

| 变化 | 完整 | 减少 | 关闭 |
|---|---|---|---|
| 详情打开与关闭 | 形变、交叉淡化、遮罩与内容淡入淡出 | 原地出现；遮罩与内容淡入淡出 | 立即 |
| 图标与状态切换 | 模糊、位移（图标还有缩放） | 淡入淡出与模糊，无位移 | 立即 |
| 转盘与分段条数值 | 弹簧，数字计数 | 跳变 | 跳变 |
| 分段条膨胀 | 按住时 1.18 | 无 | 无 |
| 磁贴按下 | 缩放 0.975 | 无 | 无 |

## 响应式

- 面板与所在位置同宽，最宽 360 px，高度固定 396 px；磁贴在这个宽度内重新排布。
- 视口（最近的视口宿主，否则为窗口）不超过 360 px，或面板本身不超过 312 px 时，间距收紧，专注转盘缩小到 150 px。
- 视口不超过 455 px 时，面板去掉边框、圆角、阴影和底色，平铺在手机屏幕上；打开的详情保留自己的底面，保证可读。
- 触控时，磁贴、转盘和音量条都支持点按与拖动；每个数值也能用键盘修改（音量条和转盘上用方向键），没有功能依赖悬停。

## 相关组件

- `Switch`：用于带标签的单个开/关选项。
- `Slider`：用于带刻度或输入框的精确数值。
- `SegmentedControl`：用于在页面中从少数几个选项中选择一个。
- `Popover`：用于从按钮显示该面板。

| 组件 | 适用场景 |
|---|---|
| `ControlCenter` | 几个日常设置应一按即得，并可按需深入 |
| `Switch` | 单个选项，在原处加以说明 |
| `Slider` | 精确的数值 |
| `SegmentedControl` | 在页面或工具栏中从少数几个选项中选择一个 |
