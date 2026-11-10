---
title: Metric ticker
summary: 一條即時指標帶，可以像股票跑馬燈一樣捲動，也可以分欄靜止顯示；每個數值逐位滾動，並顯示變動多少、往哪個方向。
source-hash: 712593f19cc6
---

<Demo name="LiveTicker" />

## 何時使用

`MetricTicker` 用很小的空間持續顯示幾個即時數字。每個指標有一個名稱和一個數值；數值每次改變時，數字會滾動到新值，旁邊以向上或向下的箭頭顯示
與上一次讀數相比的變動，儲存格會以好或壞的色調閃一下。上升算不算好消息由指標決定：每秒請求數上升是好事，延遲上升則不是。指標帶可以在一行中
捲動經過，像兩端淡出的股票跑馬燈，也可以靜止排成等寬的欄，在窄螢幕上換行。

- **狀態列與頁面標題區**：維運頁面上的服務健康狀態、佇列深度與錯誤率。
- **即時儀表板**：圖表上方的一排關鍵數字，隨著資料流入而變化。
- **活動與銷售看板**：發表期間不斷增加的訂單、營收與註冊數。

## 何時不用

- **只有一個重要數字**：請使用 `AnimatedCounter` 或大型統計數字，而不是指標帶。
- **歷史與趨勢**：指標帶只顯示最近一次變動；若要顯示數值隨時間的變化，請使用 `Sparkline` 或 `LineChart`。
- **需要仔細比較或複製的數字**：捲動的文字不易細讀；請使用 `Mode="Roll"` 或 `Table`。
- **警示**：指標帶只是背景資訊。需要處理的事情應放在通知或橫幅中。

## 用法

加入帶有 `Label` 與初始 `Value` 的 `MetricTickerItem` 項目，之後在新讀數到達時設定 `Value`。第一個值是起點，之後的每個值都算一次變動。

```xml
<arc:MetricTicker Label="Service metrics">
    <arc:MetricTickerItem Label="Requests / s" Value="1240" />
    <arc:MetricTickerItem Label="p95 latency" Value="182" Suffix=" ms" IsHigherBetter="False" />
    <arc:MetricTickerItem Label="Error rate" Value="0.42" Decimals="2" Suffix="%" ChangeFormat="Absolute" />
</arc:MetricTicker>
```

```csharp
ticker.Items[0].Value = 1310; // rolls to 1,310 and shows +5.6% in the success color
```

`Decimals`、`Prefix` 與 `Suffix` 用來格式化數值。`ChangeFormat` 決定變動的顯示方式：相對於上一個值的百分比（預設）、指標本身的單位（`Absolute`），
或不顯示（`None`）。`IsHigherBetter="False"` 會讓上升變成壞消息。`Change` 與 `PreviousValue` 回報最近一次變動；`ItemsSource` 可從檢視模型取得指標，
取代 `Items`。

### 捲動或分欄

在 `Mode="Scroll"`（預設）下，指標以每秒 `Speed` 像素的速度經過。滑鼠指標停在上面或焦點在其中時，這一行會停住（`PausesOnHover`）；圓形暫停按鈕會讓它
一直停住，直到再次按下（`IsPaused`，雙向）。所有指標都放得下時，這一行不會移動。`Mode="Roll"` 會把指標排成每欄至少 150 px 的欄，放不下時換到更多行。

<Demo name="Columns" />

### 減弱動效

在減弱動效下，捲動的指標帶會保持靜止，並以分欄方式顯示指標。數值仍會改變，但不再滾動，閃爍改為短暫的淡出。

<Demo name="ReducedMotion" />

## API 參考

<!-- api: MetricTicker -->

<!-- api: MetricTickerItem -->

## 無障礙

- 指標帶是以 `Label` 命名的清單（"Live metrics"）。每個指標是一個清單項目，名稱會以文字說出名稱、數值與最近一次變動
  （"p95 latency, 182 ms, down 3.1%"）。用來填滿捲動行的副本對螢幕報讀程式隱藏，因此每個指標只會讀一次。
- 數值改變時不會朗讀，忙碌的資料流不會打斷閱讀。
- 暫停按鈕是真正的按鈕，名稱為 "Pause ticker" 或 "Play ticker"；Enter 與空白鍵可以按下它。移動的內容隨時都能停止，焦點進入指標帶時也會讓它停住。
- 變動除了色彩之外，還帶有箭頭與正負號。

## 動效

| 變化 | 完整 | 減弱 | 關閉 |
|---|---|---|---|
| 捲動行 | 以 `Speed` px/s 滑動，滑鼠懸停與取得焦點時停住 | 分欄，不移動 | 分欄，不移動 |
| 新數值 | 數字滾動 | 立即改變 | 立即改變 |
| 變動閃爍 | 儲存格的色調在 1.2 s 內淡出 | 相同的淡出 | 無 |

只有當指標帶在螢幕上且有內容需要移動時，捲動迴圈才會執行。

## 響應式

- 捲動時，指標帶填滿可用寬度，兩端淡出；沒有最小寬度。
- 分欄時，欄數依寬度而定（每欄至少 150 px），多出的指標換到新行；欄較窄時，名稱以省略號截斷。
- 觸控不會讓這一行停住（沒有懸停）；暫停按鈕可以。

## 相關元件

| 元件 | 何時選擇 |
|---|---|
| `AnimatedCounter` | 單獨一個數字滾動 |
| `Sparkline` | 顯示數值隨時間的變化 |
| `SplitBar` | 這些數字是單一總量的各部分 |
| `LiveVisitors` | 需要一張完整的即時訪客卡片 |
