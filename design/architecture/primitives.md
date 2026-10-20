# 非动效基元与服务

> Core 中与动效无直接关系、但被大量组件共享的服务：尺寸观察（S5）、视口可见性（S6）、列表键盘导航（S9）、手势（S10）、可暂停计时器（S12），以及剪贴板、偏好存储、文件与拖放、文本测量等辅助能力。浮层（S8）见 [浮层与输入](overlays-and-input.md)；格式化（S11）见 [本地化](localization.md)。

## 1. 尺寸观察（S5，P0）

Arc 原版约 75 个组件使用 ResizeObserver。Avalonia 中对应 `Control.SizeChanged` 与布局事件。

```csharp
public sealed class SizeObserver : IDisposable
{
    public SizeObserver(Control target, Action<SizeChangedInfo> onChanged);
    public bool IsWithinContentChangeWindow { get; }   // 最近一次内容变化后 700ms 内为 true
    public void NotifyContentChanged();                 // 控件在数据或文案变化时调用
}

public readonly record struct SizeChangedInfo(Size Previous, Size Current, bool IsContentDriven);
```

- **区分主动与被动变化**：只有内容驱动的变化（`IsContentDriven = true`）才触发宽度弹簧（P5）与 FLIP（P19）。窗口缩放、字体加载等被动变化直接跳变。
- **像素对齐**：报告的尺寸按设备像素取整，避免亚像素抖动导致的反复动画。
- **祖先变换修正**：位置换算使用 `TranslatePoint`，忽略祖先的 RenderTransform（例如父元素正在按压缩放时不应引起子元素重新测量）。

## 2. 视口可见性（S6，P1）

基于 Avalonia 的 `Layoutable.EffectiveViewportChanged`。

```csharp
public sealed class ViewportVisibility : IDisposable
{
    public ViewportVisibility(Control target, double threshold = 0.5, double margin = 0, bool once = true);
    public event EventHandler<bool>? VisibilityChanged;   // 可见比例越过阈值时触发
    public double VisibleRatio { get; }
}
```

- Arc 使用的阈值：UsageMeter 40%，InViewTitle 45%，Gauge 50%，AnimatedCounter 60%；边距 64px。
- `once = true`：首次揭示（P18）只运行一次。
- 窗口最小化或隐藏时视为不可见，持续循环（P21）据此暂停。

## 3. 列表键盘导航（S9，P1）

```csharp
public sealed class ListKeyboardNavigator
{
    public ListKeyboardNavigator(INavigableList list, ListNavigationOptions options);
    public bool HandleKeyDown(KeyEventArgs e);       // 控件在 OnKeyDown 中调用
    public int ActiveIndex { get; set; }
    public event EventHandler<int>? ActiveIndexChanged;
}
```

| 能力 | 规则（来自 Arc） |
|---|---|
| 焦点模型 | 复合控件只有一个 Tab 停靠点，内部用方向键移动。两种实现：移动焦点（roving tabindex），或保持焦点在宿主、移动"活动项"（active descendant） |
| 方向键 | 上下 / 左右按方向移动，可选循环，跳过禁用项 |
| Home / End | 第一项 / 最后一项 |
| PageUp / PageDown | 移动 8 项 |
| 类型跳转 | 输入可打印字符时跳到匹配的标签，500ms 内连续输入合并为前缀 |
| 高亮动画 | 键盘导致的高亮直接跳到目标（不动画）；指针悬停导致的高亮滑动（P7） |
| 二维网格 | Calendar 等网格控件使用网格模式：上下移动一行，左右移动一格 |

## 4. 手势（S10，P5）

```csharp
public sealed class DragTracker : IDisposable
{
    public DragTracker(Control target, DragOptions options);   // 阈值、轴锁定、垂直滚动穿透
    public event EventHandler<DragDelta>? Started, Moved, Completed, Cancelled;
}

public sealed class VelocityTracker  { public Vector Velocity { get; } public void AddSample(Point p, TimeSpan t); }
public static class RubberBand       { public static double Apply(double overflow, double dimension, double factor = 0.55); }
public static class MomentumProjector { public static double Project(double velocity, double rate = 0.99); }
```

| 公式 / 规则 | 来源 |
|---|---|
| 橡皮筋：`(1 − 1/(x·0.55/d + 1))·d`，x 为越界距离，d 为参考尺寸 | Arc 拖拽组件 |
| 惯性投影：`v/1000 × rate/(1 − rate)`，rate 取 0.99 或 0.995，约等于 0.1–0.2s 的行进距离 | 同上 |
| 抛掷阈值：SwipeActions 900px/s、Carousel 360px/s、BottomSheet 320px/s、Drawer 500px/s 或面板的三分之一、Toast 80px 或 480px/s | 同上 |
| 速度采样：最近 100ms 内的指针样本做加权估计 | 实现约定 |
| 指针捕获：拖动开始后捕获指针；`PointerCaptureLost` 视为取消并回弹 | Avalonia |
| 垂直滚动穿透：水平拖动组件在识别出垂直意图时放弃，交给外层 ScrollViewer | Arc 响应式规则 |

- 鼠标、触控板、触屏、笔统一处理。
- 每个手势都必须有按钮或菜单路径（Arc 规则），组件规格中写明。
- 桌面端没有触觉反馈；Arc 中的振动（HoldToConfirm 12ms、ElasticSlider 6ms）省略。


### 4.1 实现（已落地）

位于 Core `src/TKCode.Arc.Core/Gestures/`，命名空间 `TKCode.Arc.Gestures`（纯代码，不进 XAML）：

```csharp
var drag = new DragTracker(handle, new DragOptions { Axis = DragAxis.Horizontal, Threshold = 4 })
{
    CanBegin = e => !IsOnButton(e.Source)            // 按下时过滤
};
drag.Starting  += (_, e) => e.Cancel = …;            // 越过阈值时可拒绝（随后忽略到松开）
drag.Moved     += (_, e) => offset.Jump(start + RubberBand.Clamp(e.Offset.X, min, max, 120));
drag.Completed += (_, e) =>
{
    var i = MomentumProjector.SelectSnapPoint(points, offset.Value, e.Velocity.X,
        SnapProjection.Linear(TimeSpan.FromSeconds(0.2), fromIndex, flickVelocity: 360));
    _ = MomentumProjector.SettleTo(offset, points[i], e.Velocity.X, ArcSpring.Smooth);   // 速度交给弹簧
};
drag.Cancelled += (_, _) => _ = offset.AnimateTo(start, MotionSpec.Spring(ArcSpring.Snappy));
```

| 类型 | 说明 |
|---|---|
| `DragTracker` | 挂在控件上：`Pressed / Starting / Started / Moved / Completed / Cancelled / Released`；坐标取 TopLevel 坐标系（跟手移动的面板不会反馈到自身偏移）；越过阈值后才捕获指针并 `PreventGestureRecognition`（按钮下的按下仍可点击）；`PointerCaptureLost` 视为取消；鼠标在窗外松开（移动时左键已抬起）视为结束；`HandledEventsToo` 允许从已处理的子元素（如握把按钮）起拖；`IsEnabled` / `CanBegin` 用于 Reduced 下关闭拖拽。时间优先用事件时间戳，不单调时退回 Stopwatch。 |
| `DragRecognizer` | 与平台无关的状态机（Idle / Pending / Dragging / Rejected）：阈值、轴锁定（另一轴偏移置 0）、`RejectCrossAxis`（首个有效位移更偏向另一轴时放弃，让外层 ScrollViewer 滚动）、释放速度。可在自定义指针代码与单元测试中直接使用。 |
| `VelocityTracker` | 定长环形缓冲（无逐帧分配），最近 `Window`（默认 100ms）内样本的加权最小二乘斜率（越新权重越大）；松手时距最后样本超过 `StaleAfter`（默认 60ms）则速度为 0（Arc：停顿后松手不带惯性）。 |
| `RubberBand` | `Apply`（iOS 曲线，保留符号，渐近于 dimension）、`Invert`（越界中途抓取时还原手指距离）、`Clamp`（区间内自由、区间外阻尼）、`Sqrt`（Arc Drawer 的反向平方根衰减）。 |
| `MomentumProjector` | `Project(v, rate)` 指数衰减投影、`Project(v, horizon)` 线性投影、`NearestIndex`、`SelectSnapPoint`（投影后最近点；超过 `FlickVelocity` 而仍落回 `FromIndex` 时沿速度方向前进一个点，点集无需排序）、`SettleTo`（`MotionValue.AnimateTo(target, Spring, velocity)`，Reduced / Off 时直接跳到终值）。 |

单元测试：`tests/TKCode.Arc.Core.Tests/Gestures/`（速度估计、窗口、停顿、环形缓冲、橡皮筋往返、投影公式、甩动换档、轴锁定与拒绝）。

## 5. 可暂停计时器（S12，P1）

```csharp
public sealed class PausableTimer : IDisposable
{
    public PausableTimer(TimeSpan duration, Action elapsed, PausableTimerOptions options);
    public void Start(); public void Pause(); public void Resume(); public void Reset();
    public TimeSpan Remaining { get; }
    public double Progress { get; }   // 0–1，驱动 P22 时间线
}
```

| 选项 | 用途 |
|---|---|
| `AlignToWholeSeconds` | Countdown 在每个整秒唤醒 |
| `PauseWhenHidden` | 窗口隐藏或控件离屏时暂停（Toast、Carousel 自动播放、AnnouncementBar 轮播） |
| `PauseOnHoverOrFocus` | Toast / ToastStack 悬停、聚焦、拖动时暂停 |
| `RepeatRamp` | NumberField 按住连发：间隔从 400ms 到 150ms 再到 40ms |

基于 `DispatcherTimer`，所有回调都在 UI 线程。

## 6. 辅助能力

| 能力 | 方案 | 使用者 |
|---|---|---|
| 剪贴板 | `ArcClipboard.CopyTextAsync(Visual, string)`：封装 `TopLevel.Clipboard`；复制反馈在 1.9s 后复位 | CopyButton、CodeBlock、JsonViewer（Shift 复制路径） |
| 偏好存储 | `IArcPreferenceStore`（Get / Set 字符串）：默认实现为内存存储；应用可以替换为文件或设置系统 | AnnouncementBar（已关闭）、CookieConsent（用户选择） |
| 文件选择 | `TopLevel.StorageProvider` | FileDropzone、ChatThread 附件、SignaturePad 导出 |
| 文件拖放 | Avalonia 12 的拖放 API（`DragDrop` 事件、`IAsyncDataTransfer`），跟踪拖入深度以避免子元素闪烁 | FileDropzone、ChatThread |
| 文本测量 | `TextLayout` / `FormattedText` 获取字形位置与行信息 | TextMorph（P4）、TextReveal（P20）、ExpandableText（行数裁剪）、MoneyInput（缩放适配）、MentionInput（光标定位） |
| 等宽数字 | 字体特性 `tnum`；字体不支持时用固定宽度的数字单元格（以 "0" 的宽度为准） | P14 全部使用者 |
| 取色器 | Avalonia 没有 EyeDropper；ColorPicker 的吸管功能需要平台实现或省略（在 ColorPicker 规格中决定） | ColorPicker |

## 7. 归属与落地

| 服务 | 程序集 | 落地阶段 |
|---|---|---|
| S5 尺寸观察 | Core | P0 |
| S6 视口可见性、S9 列表导航、S12 计时器、剪贴板、文本测量 | Core | P1 |
| 偏好存储 | Core | P7（AnnouncementBar、CookieConsent） |
| S10 手势 | Core | P5 |
| 文件选择与拖放 | Arc（组件内）或 Core（如有复用） | P9 |
