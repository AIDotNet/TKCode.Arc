# 浮层与输入约定

> 锚定浮层（菜单、弹层、提示、选择器下拉）与模态浮层（对话框、抽屉、底部面板）的共享基础设施（S8），以及 Toast 宿主、焦点管理、关闭规则、背景模糊、手势与键盘约定。

## 1. 浮层分类

| 类型 | 组件 | 宿主 | 动效原语 |
|---|---|---|---|
| 锚定浮层 | Tooltip、Popover、HoverCard、DropdownMenu、ContextMenu、UserMenu（宽屏）、SplitButton 菜单、ButtonGroup 菜单、Select / Combobox / MultiSelect / MorphSelect 下拉、DatePicker / DateRangePicker / TimePicker / ColorPicker 面板、MentionInput 建议、RichTextEditor 斜杠菜单、Reactions 选择器、ExpandingSearch 结果 | AtomUI `Popup` | P10（MorphSelect、ColorPicker 等为 P9 表面形变） |
| 模态浮层 | Dialog、Drawer、BottomSheet、Card 快速预览、UserMenu（窄屏底部面板）、CookieConsent 偏好面板 | `ScopeAwareOverlayLayer`（窗口内覆盖层） | P11、P12 |
| 通知宿主 | Toast、ToastStack | `ScopeAwareOverlayLayer` 中的 Arc 堆栈宿主 | P10 / P22 / P28 / P12 |

## 2. 锚定浮层（AnchoredOverlay）

### 2.1 宿主

- 使用 AtomUI 的 `Popup`（`src/AtomUI.Desktop.Controls/Popup/Popup.cs`）：
  - 它提供翻转检测、`MarginToAnchor`、`RequestedPlacement`、`CustomPlacementCallback`；
  - 支持窗口弹层与覆盖层弹层两种宿主；
  - 提供阴影属性（`PopupRootShadow`、`OverlayHostShadow`）。
- Arc 设置 `IsMotionEnabled = false`，关闭 AtomUI 自带的 Zoom 动画，由 Arc 的 `AnchoredPresence` 在内容层播放弹簧动画。
- 阴影使用 `ShadowFloating`。窗口弹层要为阴影留出边距（AtomUI 的阴影感知容器负责）；S0.10 验证 Arc 的大模糊阴影（48px）在两种宿主下都不被裁切。
- **背景模糊**：Arc 的部分菜单（如 Select、DropdownMenu）是半透明表面加背景模糊。Avalonia 无法模糊弹层背后的内容，锚定浮层统一使用 `SurfaceRaised` 实色表面，相当于 Arc 在"减少透明度"下的表现。
- **跨层形变**：MorphSelect、PhoneInput、DateRangePicker 等组件的表面从字段内的触发器长成弹层（P9）。实现方式：在浮层中绘制形变表面，起点取触发器在 TopLevel 中的矩形；动画期间隐藏原触发器表面。S0.10 验证这种跨越字段与浮层的形变是否可行。

### 2.2 进出场（P10）

| 规则 | 来源（Arc） |
|---|---|
| 以锚点为原点缩放：变换原点取触发器方向（下拉从上边缘、右键菜单从指针位置，即使被夹到屏幕内也保持原点） | dropdown-menu、context-menu |
| Popover：向触发器方向偏移 5px，从 0.97 缩放，退场 140ms | popover |
| HoverCard：从 0.96 缩放，偏移 4px，smooth | hover-card |
| Tooltip：250ms 延迟，从 0.97 上升 3px；Reduced 下为 90ms 淡入 | tooltip |
| 预热组：300ms 内再次打开同类浮层时立即显示（Tooltip 只淡入；HoverCard 80ms 后只淡入） | tooltip、hover-card |
| 菜单项交错进场：每项 `StaggerItem`；UserMenu 为 0.06s 延迟后每行 0.02s | dropdown-menu、user-menu |
| 退场更快，并可中途反向：关闭过程中再次打开，从当前状态继续 | 通用 |
| 锚点元素不做按压缩放，只用颜色响应（浮层按按下后的尺寸定位） | button.css 中 `[aria-haspopup]` 的规则 |

### 2.3 关闭与焦点

- 关闭方式：Escape、点击外部（`IsLightDismissEnabled`）、选择项、触发器再次点击。
- 打开时焦点移入浮层（菜单移到第一项或当前选中项）；关闭后焦点回到触发器。
- Tab 键：菜单与列表中，Tab 关闭浮层并把焦点移到下一个元素，不选择当前项（Arc 的 MorphSelect 规则）。
- 键盘快捷键不抢占文本框中的按键。
- **焦点留在字段中的列表弹层**（Combobox、ExpandingSearch、MentionInput、PhoneInput、MorphSelect 的搜索模式）：
  - 键盘焦点始终在输入框里，列表中的"活动项"只是虚拟高亮，对应 Arc 的 `aria-activedescendant`；
  - Avalonia 没有等价机制，统一做法是活动项变化时，通过一个隐藏的 `LiveSetting = Polite` 文本播报当前项，例如 "London, UTC+0, 3 of 12"；
  - 结果数量变化时同样播报（例如 "5 results"）。
  - 该做法在 Combobox（P6）规格中定型，其他组件复用。

### 2.4 共享菜单表面

- DropdownMenu、ContextMenu、UserMenu、SplitButton、ButtonGroup、SwipeActions（菜单路径）共用 Arc 程序集内的 `MenuSurface` presenter：
  - 菜单项、分组、分隔线、勾选项、快捷键提示；
  - 悬停高亮滑动（P7）；
  - 列表键盘导航（S9）。
- `MenuSurface` 在 DropdownMenu（P5）中首次落地并定义 API。

## 3. 模态浮层（ModalOverlay）

### 3.1 宿主

- 挂载到 `ScopeAwareOverlayLayer.GetLayer(topLevel)`。挂载与卸载由 Arc 的 `ModalPresence` 管理，参考 AtomUI Drawer 的做法：声明式控件留在逻辑树中，打开时创建容器挂到覆盖层。
- 每个模态浮层都由遮罩与面板组成：
  - 遮罩为 `Scrim` 颜色，用补间淡入；
  - 面板动画：Dialog 从 0.96 上升 8px，Drawer 从边缘滑入，BottomSheet 从底部升起，均用 smooth。

### 3.2 背景模糊

- Arc 原版使用 CSS `backdrop-filter`：对话框 7px，抽屉 4px。Avalonia 没有对任意元素背后内容做模糊的能力。
- 方案：模态打开时，对覆盖层**下方的窗口内容根**设置 `BlurEffect`，半径随遮罩透明度一起补间；关闭时移除。
- 风险：大面积模糊每帧重绘的开销。缓解措施：
  - 动画结束后模糊半径保持不变，内容静止时由合成器缓存；
  - `ReduceTransparency=true` 或性能不足时退化为只有遮罩。
- S0.10 与 Dialog 规格（P5）中给出实测结论。

### 3.3 焦点与关闭

- 打开时把焦点移入面板（第一个可聚焦元素或指定元素），并把 Tab 循环限制在面板内（焦点陷阱）。
- 关闭后焦点回到触发器。
- Escape 关闭；点击遮罩关闭（可配置）。
- 嵌套模态时只有最上层响应 Escape。

### 3.4 拖拽关闭（P12）

- BottomSheet 支持吸附点（peek / full），拖动时遮罩透明度随位置变化，抛掷阈值 320px/s。
- Drawer 的阈值为 500px/s 或面板宽度的三分之一。
- UserMenu 在窄屏下复用 BottomSheet 的拖拽逻辑。
- 实现基于 S10 手势服务（见 [非动效基元](primitives.md#4-手势s10p5)）。

### 3.5 实现（S8 已落地）

位于 Core `src/TKCode.Arc.Core/Primitives/Modal/`，命名空间 `TKCode.Arc.Primitives`：

```csharp
var modal = new ModalPresence(panel, anchor)        // panel 只做视觉宿主，保留自己的逻辑父级
{
    Entrance = ModalEntrance.Rise,                   // Rise | Slide(+Side) | Fade | Custom
    BackdropBlur = 7,                                // 0 = 不模糊；ReduceTransparency 时自动关闭
    ScrimBrush = null,                               // null = ArcDesign Scrim
    ContainmentRoot = owner,                         // 焦点陷阱视为"内部"的逻辑根（默认 panel）
};
modal.Dismissing += (_, e) => e.Cancel = …;          // Escape / 遮罩按下 / 手势，可取消
modal.Open();   modal.Dismiss(DismissReason.Escape);   modal.Close(reason, velocity);
modal.SetPanelOffset(px);  await modal.SettlePanelAsync(MotionSpec.Spring(ArcSpring.Snappy), velocity);   // Slide 拖拽
```

- 宿主：优先 `ScopeAwareOverlayLayer.GetLayer(anchor)`（在最近的 VisualLayerManager 或 `ScopeAwareOverlayLayerPanel` 中注入），退回 `OverlayLayer.GetOverlayLayer`；挂载 `ModalHost`（遮罩 Border + 面板宿主，按层尺寸铺满并随层尺寸变化）。没有 VisualLayerManager 的窗口（未套主题的 Avalonia `Window`）无法打开模态，`Open()` 返回 false；AtomUI `Window` 与 Fluent / Simple 窗口均可。
- 面板只作为 `ModalHost` 的视觉子级（不改逻辑父级），面板宿主承载 Rise / Slide 的变换与透明度；`ModalHost` 的空白区域不参与命中，按下会落到遮罩。打开时同步测量一次，使首帧与滑动距离使用真实尺寸。
- 动效（P11）：Rise = 面板 y 8→0、scale 0.96→1（smooth 弹簧）+ fast/enter 淡入，退出到 y 4 / 0.98（fast/standard）；Slide = 从边缘 smooth 弹簧滑入，点击或 Escape 关闭用 standard 补间滑出，Gesture 关闭用 visualDuration 0.24 的 smooth 弹簧继承速度，透明度在最后 35% 行程淡出（阴影随之离开）；遮罩 standard/enter 进、fast/standard 出。Reduced：只保留 instant 透明度；Off：直接呈现。
- 退出期间 `ModalHost.IsHitTestVisible = false`（点击穿透到触发器）；退出中再次 `Open()` 从当前值反向（弹簧保留速度）；退出完成后才移出覆盖层并触发 `Closed`。锚点离开可视树时立即移除。
- 堆叠：每个 TopLevel 一个 `ModalStack`。最上层的打开中模态独占 Escape（TopLevel 冒泡 KeyDown，已处理的不再响应）与焦点陷阱（TopLevel 的 `GettingFocus`：新焦点不在 `ModalHost` / 面板 / `ContainmentRoot` 逻辑子树内时改投到面板首个可聚焦元素或取消）；面板宿主 `TabNavigation=Cycle`。`ModalPresence.GetModals(topLevel)` 列出当前堆栈。
- 焦点：打开前记录焦点元素（若带 `:focus-visible` 则移入时用 Tab 方式），关闭开始时若焦点仍在模态内则归还（Escape 关闭以 Tab 方式归还）。
- 背景模糊：堆栈对 VisualLayerManager 的 `Child`（窗口内容根）设置一个可变 `BlurEffect`，半径 = max(各模态 BackdropBlur × 遮罩进度)，栈空时恢复原 Effect；`ScopeAwareOverlayLayerPanel` 作用域内不模糊（Arc 的 contained drawer 同样无模糊）。
- `ModalEntrance.Custom`：所有者通过 `CustomMotion(presence, entering)` 播放面板进出（返回 Task，退出任务完成后移除），并自行写 `ScrimOpacity`（BottomSheet 用面板位置计算遮罩）。

### 3.6 锚定浮层实现说明（Popover / HoverCard）

- Arc 内部 `OverlayAnchoredPresence`（Overlays/_Shared）：宿主 `ArcPopup`，以 Animation 优先级把 `IsMotionEnabled` 置 false，自己在弹层的 `BaseMotionActor`（AtomUI 公开类型）上写 `Opacity` 与 `MotionTransform`，因此阴影随表面一起淡入、位移和缩放；原点为落位一侧的边或角（读 `IsVerticalFlipped` / `IsHorizontalFlipped`）。Popover：5px + 0.97，transform 580ms CSS 弹簧曲线、透明度 fast/enter，退出到一半位移 + 0.98；HoverCard：4px + 0.96 同一 smooth 弹簧，热启动只淡入。
- 用 `RequestedPlacement`（而非 `Placement`）设置方向，保证默认的 Bottom 也走 AtomUI 的自定义放置（`MarginToAnchor` 生效）。Start / End 对齐用居中放置 + `HorizontalOffset` / `VerticalOffset`，规避 AtomUI EdgeAligned 放置在覆盖层宿主 + 大阴影时落到对侧的问题。
- 外部按下：在锚点 TopLevel 上以隧道 + handledEventsToo 监听，源不在表面或锚点内（视觉或逻辑，含嵌套弹层）即关闭；源是模态遮罩时吞掉这次按下，只关闭最上层。Escape 先在表面上处理（覆盖层宿主中键盘事件不会冒泡到窗口），再由 TopLevel 冒泡处理（焦点在触发器上时）。

## 4. Toast 宿主

AtomUI 的 Message / Notification 堆叠引擎（`FeedbackStack/*`）是 internal，Arc 自行实现：

| 能力 | 规则（来自 Arc） |
|---|---|
| 服务 | `ToastManager`：每个 TopLevel 一个，挂在 `ScopeAwareOverlayLayer` 中；`Show(ToastOptions)` 返回句柄，可更新或关闭 |
| 位置 | 屏幕边缘（上 / 下 × 左 / 中 / 右），从该边缘进入 |
| 进场 | 从 16px 外、0.96 缩放进入（morph） |
| 退场 | 下沉 8px 并淡出 |
| 堆叠（ToastStack） | 收起时每层露出 14px，逐层缩小 0.05；悬停或聚焦时展开（morph） |
| 寿命 | `PausableTimer`：悬停、聚焦、拖动、窗口隐藏时暂停 |
| 滑动关闭 | 拖动超过 80px 或速度超过 480px/s；向反方向拖动时有橡皮筋 |
| 播报 | `AutomationProperties.LiveSetting = Polite`；错误类为 Assertive |
| 使用规则 | Toast 不能是前台操作的唯一确认（Arc 规则）；保存成功等应在原处显示 |

`ToastManager` 在 Toast（P7）中落地，ToastStack 在其上增加堆叠与展开。

## 5. 输入约定

| 约定 | 说明 |
|---|---|
| 指针焦点不画焦点环 | 只有键盘焦点（`:focus-visible`）显示焦点环，见 [可访问性](accessibility.md) |
| 悬停只是增强 | 悬停才显示的内容必须也能通过点击或焦点到达；悬停样式只在支持悬停的精确指针上生效 |
| 触摸目标 | 至少 44px，间距至少 8px（`ControlSizeMD`） |
| 复合控件 | 一个 Tab 停靠点，内部方向键导航 |
| 手势 | 每个手势都有按钮或菜单的替代路径 |
| 重复的键盘动作 | 不播放动画（Arc：actions repeated by keyboard don't animate） |
| 文本框 | 快捷键不抢占文本框中的按键；全局快捷键（ShortcutRecorder、ScrubInput 等）只在控件启用并聚焦时监听 |
