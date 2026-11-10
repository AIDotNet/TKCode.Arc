---
title: Seat map
summary: 在场地或飞机的座位图上选座：按价格档次着色的分区、已售出的座位、带价格的图例、用键盘和指针在上限内选座，以及带费用和合计的摘要。
source-hash: 402476070483
---

<Demo name="Preview" />

## 何时使用

`SeatMap` 让人们在平面图上选择座位：带舞台的剧场，或带过道的飞机。座位按分区绘制，颜色表示价格档次，已售出的座位是空心的，图例列出各档次及其价格。可以用指针或键盘选座，数量有上限；摘要列出所选座位、费用和合计，然后是 Continue。

- **音乐会、剧院和活动的售票。**
- **航班、火车和巴士的选座。**
- **按排布置的会议室或工位预订。**

## 何时不用

- **不对号入场**：一个数量字段就够了。
- **自由形状的平面图**（桌子、各种形状）：使用绘图或示意图。

## 用法

该区块派生自 Avalonia 的 `TemplatedControl`。用 `SeatMapTier` 填充 `Tiers`，用 `SeatMapSection` 填充 `Sections`，并把已售座位的编号（"ORC-C-12"：分区、排的字母和座号）设置给 `TakenSeats`。

```csharp
map.Tiers.Add(new SeatMapTier { Id = "premium", Name = "Premium", Price = 120, ColorIndex = 1 });
map.Sections.Add(new SeatMapSection
{
    Id = "ORC", Name = "Orchestra", TierId = "premium", Rows = 5, SeatsPerRow = 16, X = 4, Y = 1, Curve = 1.2, AisleAfter = [8]
});
```

### 布局

分区的排从 `FirstRow` 开始用字母表示，座位从 1 开始编号。`X` 和 `Y` 以地图单位放置分区（一个座位大约一个单位宽），`Curve` 让各排朝舞台弯曲，`AisleAfter` 在指定座号之后留出过道。地图会缩放以适应区块。

### 选座

`ToggleSeat`（或点按，或在光标处按 **Enter**）选择或释放一个座位；已售出的座位不能选，同时最多 `MaxSelection` 个（"You can choose up to 6 seats."）。`SelectedSeats` 和 `Total`（价格加上每个座位的 `ServiceFee`）随之更新，`SelectionChanged` 报告每次变化，Continue 引发 `ContinueRequested`。

### 飞机

同一个区块也能画飞机座位：过道两侧各是一个分区，排是座位字母，座号是飞机上的排号。文案把座位称为 "Seat 12C"。

<Demo name="Cabin" />

## API 参考

<!-- api: SeatMap -->

<!-- api: SeatMapSection -->

<!-- api: SeatMapTier -->

<!-- api: SeatMapSeat -->

## 无障碍

- 地图是一个可获得焦点的控件，名为 "Seat map"，帮助文本说明按键。光标移动时，它的名称变为该座位及其价格和状态（"Orchestra · Row C · Seat 12, $120, available"）。
- **方向键**移到该方向上最近的座位，**Home** 和 **End** 移到第一个和最后一个座位，**Enter** 或 **Space** 选择或释放座位。只有键盘焦点时光标才显示焦点环。
- 摘要中每个已选座位都有删除按钮，名为 "Remove Orchestra · Row C · Seat 12"。
- 礼貌的实时区域会读出每次选择和数量上限。

## 动效

- 座位状态立即变化；说明框跟随指针或光标。地图没有持续的动画。

## 响应式

- 低于 780 px 时，摘要移到地图下方。
- 座位随区块宽度缩放，每个座位最大 28 px。

## 相关组件

- Seat manager 区块：统计计划中的团队席位（而不是地图上的位置）。
- Checkout summary 区块：Continue 之后的支付步骤。
