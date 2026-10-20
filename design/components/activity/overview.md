# 动态与会话（activity）

> Arc 中围绕时间与人的活动组件：活动流、评审讨论与实时会话，新内容以平滑的进入与高度变化融入列表。

## 1. 大类定位
- 展示随时间累积的人与系统活动：项目与系统的活动流（Timeline），附着在某个对象上的评审讨论（CommentThread），以及实时的双向会话（ChatThread）。
- 边界：需要排序、比较的记录归 [表格与数据](../tables/overview.md)（SortableDataTable）；固定步骤的进度归 [进度](../progress/overview.md)（Stepper）；只需要输入框时用 [文本输入](../text-fields/overview.md) 的 Textarea 或 [特殊输入](../special-inputs/overview.md) 的 MentionInput；单独的表情反应按钮归 [手势](../gestures/overview.md)（Reactions）。
- 共同特征：都是派生自 ItemsControl 的长列表，新项进入（P19）与内容高度变化（P6）是主要动效；时间与日期按区域格式化；作者以头像或首字母标识；新内容经实时区域播报；Arc 都不虚拟化，长历史需要分页、裁剪或分块加载。

## 2. 组件一览
| 组件 | 类名 | Arc 页面 | 阶段 | Arc 依赖 | 建议基类 | 原语 |
|---|---|---|---|---|---|---|
| Timeline | Timeline | [timeline](https://uiarc.dev/components/timeline) | P9 | 无 | ItemsControl | P6 P15 P18 P19 P26 |
| Comment thread | CommentThread | [comment-thread](https://uiarc.dev/components/comment-thread) | P9 | 无 | ItemsControl | P6 P9 P10 P14 P19 P25 |
| Chat thread | ChatThread | [chat-thread](https://uiarc.dev/components/chat-thread) | P9 | 无 | ItemsControl | P2 P3 P6 P8 P10 P14 P19 P21 P26 P30 |

## 3. 共享约定
- 时间：Timeline 依赖外部传入的 `now`（Arc 建议约每分钟更新一次）并按 `timeZone` 分天；ChatThread 按 `groupWindow`（默认 5 分钟）把同一人的相邻消息分组，并插入日期分隔（today、yesterday 默认取区域自身的说法）；CommentThread 的 `createdAt` 是现成的展示文本（如 "2h"）。Avalonia 版用 S11 统一格式化相对时间与日期，`locale` 映射为 `Culture`。
- 文案：CommentThread 与 ChatThread 用 `labels` 覆盖每个可见词与可访问名称，带计数或姓名的词是函数；Avalonia 版用本地化目录（xlf 复数形式）提供默认值，再允许按实例覆盖。
- 身份：作者没有头像时显示首字母，统一复用 [头像与徽标](../avatars/overview.md) 中的 Avatar。
- 表情反应：CommentThread 与 ChatThread 都有可配置的表情列表、Toggle 语义的反应 chip（名称形如 "<表情> 3, including you"）与滚动计数；与 Gestures 大类的 Reactions 行为相近，规格阶段评估是否共享 chip 与选择器。
- 播报：新动态经 Status 区域、新消息经 log 区域（List 加 `LiveSetting=Polite`）播报；正在输入、复制等辅助状态使用独立的 Status 区域。
- 动效分工：新增项进入用 P19、内容高度变化用 P6、计数滚动用 P14；Reduced 统一改为短淡入淡出（fast 160ms），高度立即变化，Off 立即终态。
- 窄屏与触摸：小于 420px 时收紧内边距（Timeline 行内边距、CommentThread 嵌套缩进 12px、ChatThread 内容内边距 12px）；只在悬停时出现的操作在触摸设备上常显。
- 规模：三者都不虚拟化，长历史需要分页、裁剪或分块加载。

## 4. 组件

### 4.1 Timeline（Timeline）
`slug: timeline` · 阶段 P9 · Arc 依赖：无 · 建议基类：ItemsControl

**Arc 行为摘要**：最新在前、按天分组的竖向活动流，日期标签吸顶，有详情的行可以原位展开，实时到达的新动态从顶部滑入。用于项目历史、审计日志、部署流等重视顺序与新近程度的场景。需要排序或比较时用 SortableDataTable，固定步骤的进度用 Stepper。

**Arc API 要点**：
| Arc 属性 | 类型 / 默认值 | 建议 Avalonia 属性（类型） |
|---|---|---|
| `events`（必填） | `{ id, at, actor?, title, meta?, detail?, avatar?, icon?, tone? }[]`，任意顺序，有 detail 的行可展开 | `ItemsSource`（`TimelineEvent`：Id、At、Actor、Title、Meta、Detail、Avatar、Icon、Tone 为 Neutral、Success、Danger） |
| `now`（必填） | number（epoch 毫秒） | `Now`（DateTimeOffset，相对时间与日期分组的参照） |
| `label`（必填） | string | `Label`（string，区域的可访问名称） |
| `timeZone` / `locale` | `"UTC"` / `"en-US"` | `TimeZone`（TimeZoneInfo）/ `Culture`（CultureInfo?） |
| `maxHeight` / `scrollToNew` | 无 / `true` | `MaxHeight`（继承，设置后内部滚动）/ `IsScrollToNewEnabled`（bool，新动态到达时滚回顶部） |
| `defaultExpanded` | `[]` | `ExpandedIds`（`IList<string>`，初始展开的事件） |
| `headingLevel` | `3`（2 到 6） | `HeadingLevel`（int，写入日期标签的 `AutomationProperties.HeadingLevel`） |

**交互与键盘**：
- ↓/↑ 在可展开的行之间移动；Home / End 到首末可展开行；Enter / Space 展开或收起当前行。
- 没有详情的行不可交互，也不在 Tab 序列中。

**可访问性**：
- 整体是带名称的区域（`LandmarkType=Region`）；日期标签是标题（`HeadingLevel`），附带隐藏的更新数量；时间附带完整日期的隐藏文本。
- 可展开行是 Button 并提供 `IExpandCollapseProvider`；新动态经 Status 区域播报；tone 只是颜色，结果必须写进 title（Arc 规则），Gallery 与示例照此书写。

**动效**：
- 行进入视口时连接线随之描绘（P15、P18；standard + enter），标记点弹出（P26；缩放 snappy，透明度 fast）。
- 新动态从顶部滑入，其余行下移（P19；morph）；详情在弹簧上展开（P6；smooth）。
- Reduced：位移改为短淡入淡出（fast 160ms），高度立即变化；Off：立即终态。

**Avalonia 实现要点**：
- 日期标签吸顶：Avalonia 没有 position: sticky，需要在 ScrollViewer 内用自定义面板或叠加层实现分组标题吸顶。
- 相对时间与分组按 `Now`、`TimeZone`、`Culture` 计算（S11）；Arc 提醒 `now` 每次变化都会重渲染所有行，Avalonia 侧只刷新时间文本，不重建行。
- 连接线描绘与标记点按行进入视口触发（S6）；头像复用 Avatar；小于 420px 时行内边距收紧，文字保持宽度。

**AtomUI 参考**：Timeline（仅供学习行为与边界，不复用其实现）

**风险与未决**：
- 吸顶日期标签与"新动态到达时滚回顶部"在同一个 ScrollViewer 中的配合需在规格中细化。
- 不虚拟化，长流需要设置 `MaxHeight` 并裁掉旧动态；`Detail` 是任意内容，展开高度测量依赖 S5 与 SpringSizeDecorator。

### 4.2 CommentThread（Comment thread）
`slug: comment-thread` · 阶段 P9 · Arc 依赖：无 · 建议基类：ItemsControl

**Arc 行为摘要**：树状评论讨论：嵌套回复可折叠，表情反应带滚动计数，可原位编辑与删除，@提及自动补全，解决后整个线程折叠为一个 chip。用于文档、设计、工单或 PR 上的评审讨论，以及附着在某个对象上的反馈侧栏。实时往来的对话用 ChatThread，只需要输入框时用 MentionInput。

**Arc API 要点**：
| Arc 属性 | 类型 / 默认值 | 建议 Avalonia 属性（类型） |
|---|---|---|
| `comments` / `defaultComments` / `onCommentsChange` | `ThreadComment[]` / `[]`；评论含 id、author、body、createdAt、edited、deleted、reactions、replies | `Comments`（`IList<ThreadComment>`，双向）+ `CommentsChanged` 事件（携带变更：Reply、Edit、Delete、React） |
| `currentUser`（必填） | `{ id, name, avatar? }` | `CurrentUser`（`CommentAuthor`，决定哪些评论可编辑和删除） |
| `people` | 默认为线程中的所有人 | `People`（`IList<CommentAuthor>?`，可被提及的人） |
| `resolved` / `defaultResolved` / `onResolvedChange` | `false` | `IsResolved`（bool，双向）+ `ResolvedChanged` 事件 |
| `title` / `placeholder` | ReactNode / string | `Title`（object，线程主题，如 "Hero headline"）/ `Placeholder`（string） |
| `reactions` / `maxDepth` | 6 个默认表情 / `2` | `Reactions`（`IList<string>`）/ `MaxDepth`（int，更深的回复挂到允许的最深父级） |
| `labels` / `nowLabel` | 英文默认文案，带计数的词是函数 | 本地化资源（xlf 复数形式）加可选的按实例覆盖 |

**交互与键盘**：
- Cmd / Ctrl + Enter 发送；@ 打开提及建议，↑/↓ 在建议中移动，Enter / Tab 插入当前提及；Escape 依次关闭建议、取消编辑或回复，也用于关闭表情选择器或删除确认。

**可访问性**：
- 输入框是组合框语义（人员列表加活动项），Avalonia 需要自定义 peer 暴露展开状态与当前建议项；每条评论是以作者与时间命名的 Group（Arc 为 article），已删除的评论读作 "Deleted comment"。
- 反应 chip 是 Toggle；回复折叠按钮提供 `IExpandCollapseProvider`；只有图标的操作都有具体名称。

**动效**：
- 展开回复、新评论到达、线程折叠为已解决 chip 时，线程高度弹簧变化（P6；smooth）。
- 反应计数滚动到新值（P14；smooth），chip 增删时以布局动画移动（P19；morph）。
- 编辑、删除确认与表情选择行从侧边滑入（P25；smooth，位移 8px）。
- Reduced：内容原地替换、不移动，并关闭过渡；Off：立即终态。

**Avalonia 实现要点**：
- 每次变化不可变地重建评论树并渲染全部评论；`CommentsChanged` 携带具体变更，调用方据此持久化（Arc 建议不要比对整棵树），服务端也要执行"只能编辑和删除自己的评论"。
- 输入框基于 TextBox，@ 提及建议用 S8 锚定浮层与 S9 列表导航；正文中 "@Full Name" 形式的已知人员用 Inlines 高亮。
- 头像复用 Avatar；删除仍有回复的评论时保留安静的占位，维持线程结构；小于 420px 时输入框隐藏头像、嵌套缩进减到 12px；触摸设备上所有操作都是可见按钮。

**AtomUI 参考**：—（AtomUI 无同类控件）

**风险与未决**：
- 提及建议是否复用 MentionInput（SpecialInputs，阶段 P6）的弹层与 token 逻辑，主数据未列依赖；若自建弹层，还需要 P10（主数据未列）。
- "折叠为已解决 chip"更接近 P9 表面形变，主数据只列 P6，需在规格中确认。
- 复核结论（汇总评审）：新增 P9、P10，已同步到[组件总清单](../overview.md)。

### 4.3 ChatThread（Chat thread）
`slug: chat-thread` · 阶段 P9 · Arc 依赖：无 · 建议基类：ItemsControl

**Arc 行为摘要**：聊天或客服会话：按人和时间分组的消息、日期分隔、表情反应、已读回执、正在输入提示和可带附件的输入框；始终钉在最新消息，新消息落在视野外时显示提示胶囊。用于客服挂件、私信与团队聊天。活动历史用 Timeline，只需要输入框时用 Textarea 或 MentionInput。

**Arc API 要点**：
| Arc 属性 | 类型 / 默认值 | 建议 Avalonia 属性（类型） |
|---|---|---|
| `participants` / `currentUserId`（必填） | `{ id, name, avatar? }[]` / string | `Participants`（`IList<ChatParticipant>`）/ `CurrentUserId`（string，本人消息靠右并用 accent 色） |
| `messages` / `defaultMessages` | `ChatMessage[]`，旧到新；消息含 id、authorId、text、createdAt、attachments、reactions、status | `Messages`（`IList<ChatMessage>`，未绑定时内部维护列表；Status 为 Sending、Sent、Delivered、Read、Failed） |
| `typing` / `readBy` | `string[]` / 参与者 id 到最后已读消息 id 的映射 | `TypingParticipantIds`（`IList<string>`）/ `ReadBy`（`IDictionary<string, string>`） |
| `onSend` / `onReact` / `onRetry` | 回调，`onSend` 可返回 Promise | `SendRequested` 事件（Draft：Text、Files）/ `ReactionToggled` 事件 / `RetryRequested` 事件（最后一条本人消息失败时显示 Retry） |
| `composer` / `allowAttachments` / `accept` | `true` / `true` / 无 | `IsComposerVisible` / `IsAttachmentEnabled`（bool）/ `AcceptedFileTypes`（`IReadOnlyList<FilePickerFileType>?`） |
| `groupWindow` / `locale` | `300000`（5 分钟）/ `"en-US"` | `GroupWindow`（TimeSpan）/ `Culture`（CultureInfo?） |
| `reactions` / `placeholder` / `label` / `labels` | 6 个默认表情 / `"Message"` / `"Conversation"` / 英文默认 | `Reactions` / `Placeholder` / `Label`（string）/ 本地化资源 |
| ChatThreadHandle：`scrollToBottom` / `focusComposer` | 方法 | `ScrollToBottom(bool smooth = true)` / `FocusComposer()` 方法 |

**交互与键盘**：
- Enter 发送（输入法组合中忽略），Shift + Enter 换行；Tab 可以到达每条消息的添加反应按钮（行内获得焦点时显示）；表情选择器内 ←/→ 移动并在两端循环，Escape 关闭并把焦点还给按钮。

**可访问性**：
- 消息列表是 log 语义：List 角色加 `LiveSetting=Polite`，只播报新增消息；正在输入在独立的 Status 区域播报（如 "Sam is typing"），跳动的点为 Raw。
- 反应 chip 是 Toggle；已读回执的名称列出已读的人，头像是装饰；选择器打开时聚焦第一个表情并使用 roving tabindex。

**动效**：
- 新消息从各自一侧带小缩放升入（P19；smooth），列表按新增高度上滑而不是跳动（P30；smooth）。
- 读者头像以共享元素飞到新的已读消息下方（P8；morph）。
- 反应 chip 弹出（P26；snappy），计数上下滚动（P14；smooth），反应行与附件托盘弹簧展开（P6；smooth）；发送时箭头飞出、新箭头升入（P2；图标缩放 snappy，BlurSubtle），跳到最新的胶囊从下方弹入（P26；snappy）；正在输入的点循环跳动（P21，只在可见时运行）。
- Reduced：改为短淡入淡出，停止输入点动画，滚到底部立即完成；Off：立即终态。

**Avalonia 实现要点**：
- 根是高度 100% 的纵向布局并自带 ScrollViewer，父级必须给定高度；S5 观察内容高度以保持钉底并驱动上滑（`ScrollLinked`，P30）；已读头像用 `SharedElementScope`（P8）；`ScrollToBottom` 在 Reduced 下立即完成。
- 输入框作为独立公开控件 `ChatComposer`（对应 Arc 导出）：自适应高度至 6 行，附件来自按钮（StorageProvider 文件选择）、粘贴（剪贴板）与拖放（DragDrop），一次最多 10 个。
- 图片附件最多 4 张网格加 "+N" 格，单张图用宽高预留空间（AsyncImage）；文件行用与 `formatBytes` 等价的字节格式化（S11）；气泡宽度上限 min(78%, 34rem)，小于 420px 时最宽 84%；表情选择器默认在气泡上方，会越过滚动区顶部时改到下方（S8）；触摸设备上添加反应按钮以较低不透明度常显。

**AtomUI 参考**：—（AtomUI 无同类控件）

**风险与未决**：
- 表情选择器是锚定弹层，可能还需要 P10；发送箭头"飞出、升入"更接近 P3；两者主数据都未列。
- 不虚拟化，数百条之后需要分块加载历史，向上插入历史时保持滚动锚点需专门处理；Arc 的非受控发送为附件创建的临时 URL 从不释放，Avalonia 版应在消息移除时释放位图与文件句柄。
- 复核结论（汇总评审）：新增 P3、P10，已同步到[组件总清单](../overview.md)。

## 5. 实现顺序与依赖
- 三者都在阶段 P9（数据、活动与重型编辑器），依赖的原语都已更早落地：P21 随 Button（阶段 P0），P6、P25 随 Calendar，P15 随 CopyButton，P18 随 AnimatedCounter，P26 随 Avatar（以上阶段 P1），P19 随 DatePicker（阶段 P2），P30 随 Tabs（阶段 P4），P8 随 Card（阶段 P5）。
- 建议顺序：Timeline（列表进入、高度展开与吸顶分组的基础）→ CommentThread（加回复树、编辑与提及）→ ChatThread（加钉底滚动、已读回执的共享元素飞行与附件输入）。
- 服务：S5 尺寸观察、S6 视口可见性、S8 锚定浮层、S9 列表导航、S11 时间格式化需在阶段 P9 之前可用。
- 跨大类：头像复用 Avatar（阶段 P1）；提及建议可参考或复用 MentionInput（阶段 P6）；表情反应可参考 Reactions（阶段 P5）；附件拖放可参考 FileDropzone（同为阶段 P9）；输入框可参考 Textarea（阶段 P3）。

## 6. 相关文档
- [组件总清单](../overview.md) · [规格模板](../spec-template.md)
- [动效原语目录](../../architecture/motion/primitives-catalog.md) · [Token 目录](../../architecture/theming/token-catalog.md)
- [浮层与输入](../../architecture/overlays-and-input.md) · [字段通用结构](../../architecture/forms-and-fields.md) · [路线图](../../roadmap/overview.md)
- 相邻大类：[头像与徽标](../avatars/overview.md) · [特殊输入](../special-inputs/overview.md) · [手势](../gestures/overview.md) · [表格与数据](../tables/overview.md)
