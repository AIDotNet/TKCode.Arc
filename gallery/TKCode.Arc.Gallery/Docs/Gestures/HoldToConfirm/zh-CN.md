---
title: 长按确认
summary: 通过长按而不是点击来确认破坏性操作。
source-hash: 2474b37fba46
---

## 何时使用

- 具有破坏性或难以撤销的操作，且其后果已在屏幕上可见，例如删除项目。
- 紧凑的设置行，弹出确认对话框会显得过于笨重。
- 触屏设备上，误触不应执行操作的场合。

## 何时不用

- 安全或容易撤销的小操作：用 `Button` 或 `ActionButton`。
- 需要用户先阅读后果的操作：用 `Dialog`。
- 在触控列表中删除项目：用 `SwipeActions`，它通过滑动显示操作。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:HoldToConfirm Label="长按删除项目"
                       ConfirmedLabel="已删除"
                       Command="{Binding DeleteProject}" />
</UserControl>
```

`Command` 在长按完成后只执行一次。普通点击或自动化调用都不会触发确认。

### 色调与长按时长

`Tone` 设置填充颜色：`Danger` 用于不可逆的操作，`Accent`（默认）和 `Neutral` 用于其他情况。`HoldDuration` 设置需要按住的时长，默认为 1.2 秒。

```xml
<arc:HoldToConfirm Label="长按删除"
                   Tone="Danger"
                   ConfirmedLabel="已删除" />
<arc:HoldToConfirm Label="长按撤销"
                   Tone="Neutral"
                   HoldDuration="0:0:2"
                   ConfirmedLabel="已撤销" />
```

### 图标

`Icon` 是 24 × 24 的 Lucide 图形，显示在标签之前。长按完成后，图标会变为绘制出的对勾。

```csharp
var archive = new HoldToConfirm
{
    Label = "长按归档",
    Tone = HoldToConfirmTone.Neutral,
    Icon = ArcIcons.Archive,
    ConfirmedLabel = "已归档"
};
```

### 按住时的提示

`HoldingChanged` 在长按开始和结束时触发。`IsHolding` 表示当前状态。可以借助它们在按钮旁显示提示。

```csharp
project.HoldingChanged += (_, e) =>
{
    hint.Text = e.IsHolding
        ? "请继续按住…"
        : project.IsConfirmed ? "已删除。" : "提前松开会让填充回退。";
};
```

提前松开时，填充会以弹簧动画回退，且不会确认任何操作。

### 完成状态

`IsConfirmed` 为双向绑定。长按完成时它变为 `true`。将其设回 `false` 即可重置按钮；标签变回原样时，填充会随之回退。

```xml
<arc:HoldToConfirm Label="长按删除项目"
                   IsConfirmed="{Binding IsDeleted}" />
```

```csharp
reset.Click += (_, _) => project.IsConfirmed = false;
```

## API 参考

<!-- api: HoldToConfirm -->

## 无障碍

- `Label` 是按钮的名称。长按完成后，名称改用 `ConfirmedLabel`。
- 帮助文本说明如何确认：按住指定时长，或者用键盘按住 Space 或 Enter。
- 完成状态会通过礼貌的状态区域播报一次，并告知按钮已不可用，但焦点仍保留在按钮上。
- 用 Space 或 Enter 长按时走键盘路径；按键的自动重复不会重新开始长按。焦点移开会取消长按，指针移离按钮超过 24 px 也会取消。

## 动效

- 按住期间，填充以匀速从按钮一端推进到另一端，所用时间即剩余时长。提前松开时，填充以不带过冲的平滑弹簧回退。
- 长按完成时，标签与图标以短暂的模糊和滑动切换，图标同时绘制出对勾。
- 减弱动效时，去掉按压缩放与回退弹簧。标签和图标改为淡入淡出，但填充仍跟随长按进度，因为它用于显示进度。
- 关闭动效时，只保留填充进度，其他内容一次性切换。

## 相关组件

- `Button` 与 `ActionButton`：不需要长按的操作。
- `Dialog`：用户需要先阅读后果再确认的场合。
- `SwipeActions`：删除触控列表中的某一行。
