---
title: 底部面板
summary: 停靠在预览高度或完整高度的面板，跟随手指移动。
source-hash: cdd9c3628e9f
---

<Demo name="Detents" />

## 何时使用

- 次要的移动端优先任务，例如行程详情、筛选或分享选项。
- 先显示一小部分内容、再展开到接近全高的内容。
- 地图或媒体视图中，用户在不同高度之间拖动面板。

## 何时不用

- 需要打断整个屏幕的决定：用 `Dialog`。
- 宽屏布局中保持页面可见的侧边面板：用 `Drawer`。
- 锚定在某个控件上的小块内容：用 `Popover`。

## 用法

`BottomSheet` 从底边升起，并停靠在一个或多个高度上，这些高度称为停靠点（detent）。默认停靠点为窗口高度的 0.45 与 0.92。

### 基础面板

```xml
<arc:BottomSheet Title="里斯本之旅"
                 Description="3 晚 · 6 月 12 日至 15 日"
                 IsOpen="{Binding ShowTrip}">
    <StackPanel Spacing="12">
        <TextBlock Text="周五 · 14:05 抵达 LIS，打车前往 Alfama" />
        <TextBlock Text="周六 · 28 路电车与城堡" />
    </StackPanel>
</arc:BottomSheet>
```

### 停靠点

`Detents` 以窗口高度的比例列出停靠高度。使用 `BottomSheetDetents.Parse` 从字符串设置它们，并用 `InitialDetentIndex` 选择面板打开时所在的停靠点。只有一个停靠点时，面板高度固定。

<Demo name="ThreeDetents" />

```csharp
share.Detents = BottomSheetDetents.Parse("0.3, 0.6, 0.95");
share.InitialDetentIndex = 1;
```

### 打开并在停靠点之间移动

`OpenCommand` 可从按钮打开面板。`Expand`、`Collapse` 与 `SnapTo` 可在停靠点之间移动面板，`DetentChanged` 会报告每个停靠位置。

```csharp
openTrip.Command = trip.OpenCommand;

trip.DetentChanged += (_, e) => detent.Text = $"停在 {e.Fraction:P0}";
trip.Expand();
```

### 长内容

内容不做虚拟化。长列表请分页加载，待面板完全展开后，由面板滚动内容。

```xml
<arc:BottomSheet Title="行程安排" IsOpen="{Binding ShowItinerary}">
    <ItemsControl ItemsSource="{Binding Items}" />
</arc:BottomSheet>
```

### 关闭

`Closed` 会报告面板关闭的原因，例如因快速滑动或向下拖动而关闭的 `Gesture`、`PressOutside`，或 `Escape`。

```csharp
trip.Closed += (_, e) => detent.Text = $"关闭方式：{e.Reason}";
```

## API 参考

<!-- api: BottomSheet -->

<!-- api: BottomSheetDetents -->

## 无障碍

- 面板的名称来自 `Title`，辅助文字来自 `Description`。焦点首先落在面板本身。
- 拖动手柄是一个按钮，会公开其展开状态。其名称为“展开面板”或“收起面板”。
- 在拖动手柄上，ArrowUp 与 ArrowDown 每次移动一个停靠点，Home 与 End 分别跳到最高与最低的停靠点，Enter 或空格键则在最低与最高之间切换。
- 停靠点的变化以礼貌方式播报。Escape 关闭面板。关闭按钮默认名为“关闭”；设置 `CloseButtonName` 可更改它。

## 动效

- 一个动效值驱动面板。拖动时面板跟随指针；超过最高停靠点后，面板会以最多 120px 的橡皮筋效果拉伸。
- 松开后，面板会停靠到最近的停靠点；向下拖动时则关闭。速度超过 320px/s 的快速滑动，始终至少移动一个停靠点。
- 面板背后的遮罩取决于面板位置。在最低停靠点时保留完整遮罩的 78%，随面板下移而淡出。
- 减弱动效时，停靠点直接跳转，面板淡入淡出。关闭动效时，面板立即显示或移除。

## 相关组件

- `Drawer`：宽屏布局中的侧边面板。
- `Dialog`：必须先回应才能进行其他操作的决定。
- `Popover`：锚定在某个控件上的小块内容。
