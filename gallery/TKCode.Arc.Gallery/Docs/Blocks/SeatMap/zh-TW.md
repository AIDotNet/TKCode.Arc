---
title: Seat map
summary: 在場地或飞機的座位圖上選座：按價格檔次著色的分區、已售出的座位、帶價格的圖例、用鍵盤和指標在上限內選座，以及帶費用和合計的摘要。
source-hash: 402476070483
---

<Demo name="Preview" />

## 何時使用

`SeatMap` 讓人們在平面圖上選擇座位：帶舞台的劇場，或帶過道的飞機。座位按分區繪製，顏色表示價格檔次，已售出的座位是空心的，圖例列出各檔次及其價格。可以用指標或鍵盤選座，數量有上限；摘要列出所選座位、費用和合計，然後是 Continue。

- **音樂會、劇院和活動的售票。**
- **航班、火車和巴士的選座。**
- **按排布置的會議室或工位預訂。**

## 何時不用

- **不對號入場**：一個數量欄位就夠了。
- **自由形狀的平面圖**（桌子、各種形狀）：使用繪圖或示意圖。

## 用法

該區塊衍生自 Avalonia 的 `TemplatedControl`。用 `SeatMapTier` 填充 `Tiers`，用 `SeatMapSection` 填充 `Sections`，並把已售座位的編號（"ORC-C-12"：分區、排的字母和座號）設定給 `TakenSeats`。

```csharp
map.Tiers.Add(new SeatMapTier { Id = "premium", Name = "Premium", Price = 120, ColorIndex = 1 });
map.Sections.Add(new SeatMapSection
{
    Id = "ORC", Name = "Orchestra", TierId = "premium", Rows = 5, SeatsPerRow = 16, X = 4, Y = 1, Curve = 1.2, AisleAfter = [8]
});
```

### 版面配置

分區的排從 `FirstRow` 開始用字母表示，座位從 1 開始編號。`X` 和 `Y` 以地圖單位放置分區（一個座位大約一個單位寬），`Curve` 讓各排朝舞台彎曲，`AisleAfter` 在指定座號之後留出過道。地圖會縮放以適應區塊。

### 選座

`ToggleSeat`（或點按，或在光標處按 **Enter**）選擇或釋放一個座位；已售出的座位不能選，同時最多 `MaxSelection` 個（"You can choose up to 6 seats."）。`SelectedSeats` 和 `Total`（價格加上每個座位的 `ServiceFee`）隨之更新，`SelectionChanged` 報告每次變化，Continue 引發 `ContinueRequested`。

### 飞機

同一個區塊也能畫飞機座位：過道兩側各是一個分區，排是座位字母，座號是飞機上的排號。文字把座位稱為 "Seat 12C"。

<Demo name="Cabin" />

## API 參考

<!-- api: SeatMap -->

<!-- api: SeatMapSection -->

<!-- api: SeatMapTier -->

<!-- api: SeatMapSeat -->

## 無障礙

- 地圖是一個可獲得焦點的控制項，名為 "Seat map"，幫助文字說明按鍵。光標移動時，它的名稱變為該座位及其價格和狀態（"Orchestra · Row C · Seat 12, $120, available"）。
- **方向鍵**移到該方向上最近的座位，**Home** 和 **End** 移到第一個和最後一個座位，**Enter** 或 **Space** 選擇或釋放座位。只有鍵盤焦點時光標才顯示焦點環。
- 摘要中每個已選座位都有刪除按鈕，名為 "Remove Orchestra · Row C · Seat 12"。
- 禮貌的即時區域會讀出每次選擇和數量上限。

## 動效

- 座位狀態立即變化；說明框跟隨指標或光標。地圖沒有持續的動畫。

## 響應式

- 低於 780 px 時，摘要移到地圖下方。
- 座位隨區塊寬度縮放，每個座位最大 28 px。

## 相關組件

- Seat manager 區塊：統計計劃中的團隊席位（而不是地圖上的位置）。
- Checkout summary 區塊：Continue 之後的付款步驟。
