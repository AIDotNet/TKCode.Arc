---
title: Delivery tracker
summary: 訂單到了哪裡：預計送達時間、從下單到送達的五個狀態步驟、畫出車輛位置的路線圖，以及從新到舊的掃描記錄。
source-hash: 12e3c3b84b64
---

<Demo name="Preview" />

## 何時使用

`DeliveryTracker` 回答"我的訂單到哪了？"。頂部是訂單和承運商，以及大字顯示的預計送達時間（或實際送達時間）。五個步驟跟隨包裹從下單到送達。一張小地圖畫出從轉運中心到收貨地址的路線和車輛位置，歷史記錄按從新到舊列出每次掃描的時間和地點。

- **商店或平台的訂單頁。**
- **配送通知**：點開後顯示完整狀態。
- **客服工具**：需要一眼看清掃描記錄。

## 何時不用

- **真實地圖上的逐段導航**：使用帶實時坐標的地圖組件；這裡的路線只是示意圖。
- **多個包裹**：先列出它們，一次打開一個跟蹤器。
- **不是配送的流程**：使用 `Stepper` 和 `Timeline`。

## 用法

該區塊衍生自 Avalonia 的 `TemplatedControl`。設定 `Stage`、`RouteProgress`（0 到 1）、文字形式的 `EstimatedArrival` 和 `ArrivalWindow`，並用任意順序的 `DeliveryEvent` 填充 `Events`。

```xml
<arc:DeliveryTracker OrderNumber="4821-0937" Carrier="Larkpost" Stage="Shipped" RouteProgress="0.46"
                     EstimatedArrival="Thursday, Nov 12" ArrivalWindow="2 – 6 pm" />
```

### 階段與路線

修改 `Stage`（或調用 `Advance`）會讓步驟以彈簧前進，讀出 "Now: Out for delivery" 並引發 `StageChanged`。派送中時車輛會有脈衝效果；已送達時車輛畫在地址處。路線的形狀由訂單號決定，所以同一個訂單總是得到同樣的圖。`IsDelayed` 會顯示 Delayed 標籤，直到包裹送達；`IsException` 為 true 的事件顯示警告色的圓點。

<Demo name="Narrow" />

## API 參考

<!-- api: DeliveryTracker -->

<!-- api: DeliveryEvent -->

## 無障礙

- 步驟的名稱包含當前階段和它的位置（"Shipped, step 3 of 5"）。
- 路線的名稱包含兩端以及車輛走了多遠。
- 每一條歷史記錄的名稱包含標題、時間和地點。
- 禮貌的即時區域會讀出每一個新階段。

## 動效

- 步驟的填充以 smooth 彈簧移到新階段，車輛以 gentle 彈簧沿路線滑動。
- 派送中時，每 1.6 s 從車輛向外擴散一個圓環（使用 `LiveLoop`：只在可見且為完整動效時運行）。
- **減弱動效**：填充和車輛立即到位，沒有脈衝。**關閉**時相同。

## 響應式

- 低於 720 px 時，路線放在歷史記錄上方，而不是旁邊。
- 步驟名稱在圓點下方換行；第一個和最後一個與區塊邊緣對齊。

## 相關組件

- `Stepper` 用於任何流程的步驟，`Timeline` 用於任何歷史記錄。
- Cart drawer 和 Checkout summary 區塊：下單之前的步驟。
