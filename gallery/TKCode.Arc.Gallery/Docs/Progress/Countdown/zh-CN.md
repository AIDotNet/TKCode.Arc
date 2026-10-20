---
title: 倒计时
summary: 发布倒计时，数字逐位滚动，归零时转为实时状态。
source-hash: 7805982abd2e
---

## 何时使用

- 发布或活动页面，主要信息是距离某个固定时刻还剩多久。
- 横幅、表格行或促销窗口中的紧凑截止时间，开始时切换为"正在进行"。

## 何时不用

- 带倒计时与操作按钮的横幅：用 `AnnouncementBar`。
- 已完成工作的占比：用 `Progress`。
- 用户需要在实时区域中逐秒读取的计时器：`Countdown` 运行时不会被播报。

## 用法

将 `Target` 设为要倒数到的时刻，或将 `Duration` 设为从控件显示起倒数的固定时长。默认为大号变体，显示天、时、分、秒。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Countdown Label="距离发布" />
</UserControl>
```

```csharp
launch.Target = new DateTimeOffset(2030, 1, 1, 9, 0, 0, TimeSpan.Zero);
```

### 紧凑截止时间

将 `Variant` 设为 `Compact`，显示一个随字体大小调整的胶囊。适用于空间紧张的位置。

```xml
<arc:Countdown Variant="Compact"
               Units="Hours, Minutes"
               Label="优惠截止于" />
```

### 单位

`Units` 列出要显示的单位，从大到小排列。列出的最大单位会吸收未显示的单位，因此默认为天到秒。

```xml
<arc:Countdown Units="Minutes, Seconds"
               Duration="00:00:12" />
```

### 时区中的挂钟时间

当文本不含偏移量时，用 `ResolveTarget` 解析目标。带 `Z` 或偏移量的目标文本是精确的。挂钟时间按你传入的时区读取，因此每位访客都倒数到同一时刻。

```csharp
countdown.Target = Countdown.ResolveTarget("2030-01-01T09:00:00", timeZone);
```

### 暂停、继续与重新开始

时长倒计时在暂停期间保留剩余时间。目标倒计时仍然倒数到同一时刻，因此暂停只会冻结显示。

```xml
<arc:Countdown Duration="00:00:12"
               IsPaused="{Binding IsTimerPaused, Mode=TwoWay}" />
```

```csharp
timer.Pause();
timer.Resume();
timer.Restart(); // 从完整时长重新开始
```

### 完成

`CompleteContent` 在归零时替换数字，显示在一个脉动的实时圆点旁。默认为"正在进行"。处理 `Completed` 可在倒计时到达零的那一刻做出一次响应。

```csharp
timer.CompleteContent = "报名已开始";
timer.Completed += (_, _) => RefreshOffers();
```

## API 参考

<!-- api: Countdown -->

## 无障碍

- `Label` 为倒计时命名。默认为"剩余时间"。
- 控件没有实时区域。剩余时间超过一分钟时，朗读的文本会省略秒数；倒计时结束后朗读 `CompleteContent`。
- 可见数字变化时不会被播报，因此屏幕阅读器不会每秒重复。
- 控件不可获得焦点，也没有键盘交互。

## 动效

- 每个数字都是一个转轮，沿较短的方向旋转，窗口的顶部和底部边缘柔和过渡。
- 第一次显示时，数字依次滚入。之后每秒变化一次。
- 单位随容器以弹簧效果伸缩到新尺寸而出现或消失。
- 归零时，数字淡出并缩放进入完成标签，实时圆点脉动。
- 减弱动效时，转轮直接跳到新值，容器立即调整尺寸，内容以淡入淡出切换，并停止脉动。

## 相关组件

- `AnnouncementBar`：带倒计时和操作按钮的站点横幅。
- `Progress`：工作有已知进度而非截止时间时使用。
- `Stepper`：倒计时只是多步骤流程的一部分时使用。
