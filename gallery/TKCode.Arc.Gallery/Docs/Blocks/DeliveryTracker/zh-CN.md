---
title: Delivery tracker
summary: 订单到了哪里：预计送达时间、从下单到送达的五个状态步骤、画出车辆位置的路线图，以及从新到旧的扫描记录。
source-hash: 12e3c3b84b64
---

<Demo name="Preview" />

## 何时使用

`DeliveryTracker` 回答"我的订单到哪了？"。顶部是订单和承运商，以及大字显示的预计送达时间（或实际送达时间）。五个步骤跟随包裹从下单到送达。一张小地图画出从转运中心到收货地址的路线和车辆位置，历史记录按从新到旧列出每次扫描的时间和地点。

- **商店或平台的订单页。**
- **配送通知**：点开后显示完整状态。
- **客服工具**：需要一眼看清扫描记录。

## 何时不用

- **真实地图上的逐段导航**：使用带实时坐标的地图组件；这里的路线只是示意图。
- **多个包裹**：先列出它们，一次打开一个跟踪器。
- **不是配送的流程**：使用 `Stepper` 和 `Timeline`。

## 用法

该区块派生自 Avalonia 的 `TemplatedControl`。设置 `Stage`、`RouteProgress`（0 到 1）、文字形式的 `EstimatedArrival` 和 `ArrivalWindow`，并用任意顺序的 `DeliveryEvent` 填充 `Events`。

```xml
<arc:DeliveryTracker OrderNumber="4821-0937" Carrier="Larkpost" Stage="Shipped" RouteProgress="0.46"
                     EstimatedArrival="Thursday, Nov 12" ArrivalWindow="2 – 6 pm" />
```

### 阶段与路线

修改 `Stage`（或调用 `Advance`）会让步骤以弹簧前进，读出 "Now: Out for delivery" 并引发 `StageChanged`。派送中时车辆会有脉冲效果；已送达时车辆画在地址处。路线的形状由订单号决定，所以同一个订单总是得到同样的图。`IsDelayed` 会显示 Delayed 标签，直到包裹送达；`IsException` 为 true 的事件显示警告色的圆点。

<Demo name="Narrow" />

## API 参考

<!-- api: DeliveryTracker -->

<!-- api: DeliveryEvent -->

## 无障碍

- 步骤的名称包含当前阶段和它的位置（"Shipped, step 3 of 5"）。
- 路线的名称包含两端以及车辆走了多远。
- 每一条历史记录的名称包含标题、时间和地点。
- 礼貌的实时区域会读出每一个新阶段。

## 动效

- 步骤的填充以 smooth 弹簧移到新阶段，车辆以 gentle 弹簧沿路线滑动。
- 派送中时，每 1.6 s 从车辆向外扩散一个圆环（使用 `LiveLoop`：只在可见且为完整动效时运行）。
- **减弱动效**：填充和车辆立即到位，没有脉冲。**关闭**时相同。

## 响应式

- 低于 720 px 时，路线放在历史记录上方，而不是旁边。
- 步骤名称在圆点下方换行；第一个和最后一个与区块边缘对齐。

## 相关组件

- `Stepper` 用于任何流程的步骤，`Timeline` 用于任何历史记录。
- Cart drawer 和 Checkout summary 区块：下单之前的步骤。
