---
title: 即時資料串流
summary: 隨讀數到達而捲動的即時折線圖，指標懸停時靜止以便讀取某個點，並標記被判定為異常的讀數。
source-hash: 0178c9de6d7f
---

<Demo name="ServiceLatency" />

## 何時使用

`RealtimeStream` 呈現正在被量測的數值。最新讀數從右側進入，折線向左捲動，讀者隨時可以讓它停下來讀取某個點。適合有人正盯著這個數字、想知道它走向的情境。

- **服務健康**：維運畫面上的請求延遲、錯誤率或吞吐量，並在折線上標出告警系統標記的部署與重試高峰。
- **佇列與工作程序**：後台主控台裡工作佇列目前的深度，搭配固定座標軸，讓折線的高度整天含義不變。
- **感測器與設備**：工業或設施儀表板裡的溫度、壓力或土壤濕度，大約每秒一個讀數。
- **進行中的工作階段**：交易、音訊或直播工具裡工作階段期間的位元率、價格或觀看人數。

## 何時不用

- **供人回顧的歷史資料**，例如上個月的營收：用 `LineChart`，它一次繪製整個範圍。
- **需要縮放與範圍選取的長序列**：用 `BrushChart`。
- **表格列或卡片裡的小趨勢**：用 `Sparkline`。資料串流需要足夠的寬度來捲動。
- **沒有趨勢的單一目前數字**：用 `Gauge` 或 `AnimatedCounter`。
- **找出異常**：圖表只標記你標註的內容，本身不判斷讀數。請在自己的系統裡做偵測，並為它標註的讀數設定 `Anomaly`。

## 用法

為圖表設定 `Label`、`Unit` 和讀數。`Append` 是最省資源的饋入方式：它接收時間、數值和選用的異常文字，本身不配置記憶體。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:RealtimeStream x:Name="Latency"
                        Label="Checkout latency"
                        Unit=" ms" />
</UserControl>
```

```csharp
// 每個讀數呼叫一次，在 UI 執行緒上。
Latency.Append(reading.Time, reading.Milliseconds);

// 告警系統標註了這個讀數：文字成為它的標記。
Latency.Append(reading.Time, reading.Milliseconds, "Retry burst");
```

### 結構

- **頁首**：`Label`、以滾動數字顯示的最新值和 `Unit`。
- **狀態按鈕**：檢視跟隨資料時顯示圓點和 `LiveLabel`，靜止時顯示暫停圖示和 `PausedLabel`。狀態由形狀和文字表達，而不只靠顏色。按下時切換 `IsPaused`。
- **繪圖區**：折線及其下方的淺色面積、水平格線和數值標籤。一個圓點跟隨折線前端。
- **時間標籤**：繪圖區下方隨折線捲動的整點時刻。繪圖區較窄時數量會減少。
- **標記**：每個被標註的讀數上有一個菱形，文字放在小標籤裡。菱形和文字讓它不依賴顏色也能讀懂。
- **工具提示**：指標或鍵盤所在讀數的時間和數值，以及它的異常文字。

### 從集合饋入

`Points` 接受任何 `RealtimeStreamPoint` 清單。`ObservableCollection` 會被監看：新增項目會被附加，其他內容不會重新讀取。資料來源為控制自身記憶體而移除最舊的項目不會影響圖表，圖表保留所需部分並自行裁剪其餘部分。取代或清空集合會重新讀取。

<Demo name="QueueDepth" />

### 時間視窗與間隔

`WindowDuration` 是繪圖區顯示的時長。`Interval` 是讀數通常到達的頻率，檢視比最新讀數落後這麼多。這樣折線總有前進的餘地，會平滑地生長到下一個讀數，而不是在它到達時跳變。請把 `Interval` 設得接近資料的實際節奏。如果它短得多，折線會停在最新讀數處，等下一個讀數到達。

### 數值軸

預設情況下，座標軸跟隨檢視內的讀數，留有少量餘地並取整，然後緩動到每個新範圍。設定 `Minimum` 和 `Maximum` 可固定座標軸，也可以只設一個來固定那一端。固定座標軸適合有已知上下限的數值，例如百分比或佇列容量。

### 異常

為 `RealtimeStreamPoint` 設定 `Anomaly`，或把文字傳給 `Append`。折線走到該讀數時標記彈出。螢幕報讀器在每個被標註的讀數到達時朗讀一次。初始 `Points` 中的讀數屬於歷史：會被標記但不會朗讀。

### 暫停

當 `IsPaused` 為 true、指標停留在繪圖區上（`IsPauseOnHover`）、觸控期間以及鍵盤正在讀取點時，檢視保持靜止。期間到達的讀數會被保留，頁首保持最新。恢復時折線滑回即時位置。`IsHeld` 表示檢視是否因任何原因靜止，`PausedChanged` 回報每次變化及其 `Reason`。

當介面有自己的控制項時，繫結 `IsPaused`。關閉 `IsPauseOnHover` 後圖表在指標下也繼續移動，適合大螢幕展示或游標常駐其上的圖表。

<Demo name="ControlledPause" />

### 格式

`ValueFormatter` 在所有地方取代預設的數字格式，此時頁首顯示它的文字而不是滾動數字，因為只有純數字才能滾動。`TimeFormatter` 收到的時間採用讀數所帶的時區位移；預設格式是 `HH:mm:ss`。

```csharp
Latency.ValueFormatter = value => $"{value / 1000:0.00} s";
Latency.TimeFormatter = time => time.ToString("mm:ss");
```

### 繫結到檢視模型

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:RealtimeStream Label="Render queue"
                        Unit=" jobs"
                        Points="{Binding Readings}"
                        IsPaused="{Binding IsReviewing}"
                        Minimum="0"
                        Maximum="500" />
</UserControl>
```

```csharp
public ObservableCollection<RealtimeStreamPoint> Readings { get; } = new();

// 先切換到 UI 執行緒：圖表在變化發生時讀取它。
Dispatcher.UIThread.Post(() => Readings.Add(new RealtimeStreamPoint(time, depth)));
```

### 資料形狀與效能

- 讀數按時間遞增到達。比最新讀數更早的讀數以及不是有限數的值會被忽略。
- 只繪製視窗內的讀數，透過二分搜尋定位。更早的讀數在超過兩個視窗後被捨棄，因此無盡的資料串流佔用的記憶體保持穩定。檢視靜止期間，圖表最多保留 200,000 個讀數。
- 捲動的一格畫面只是移動上次讀數到達時建立好的幾何圖形，本身不配置記憶體，也不重新解析顏色。圖表移出螢幕或沒有內容需要移動時，迴圈會停止。
- 傳入時間和數值的 `Append` 不會配置點物件。資料很快時優先用它，而不是集合。

### 內容準則

- 在 `Label` 裡寫明量測什麼、在哪裡：寫「Checkout API, p95 latency」，而不是「Latency」。
- 標記文字控制在兩三個詞，例如「Cache flush」。工具提示和螢幕報讀器會重複它。
- 單位按它跟在數字後面應有的樣子寫。它直接接在數值後面，所以 `" jobs"` 帶空格，`"%"` 不帶。

### 最佳實務

- 只標註需要有人處理的情況。滿是標記的折線就失去了意義。
- 高度本身有含義時固定座標軸，形狀更重要時讓它跟隨資料。
- 透過 `Points` 載入近期歷史，讓繪圖區首次顯示時不是空的。
- 需要仔細讀數的情境保持 `IsPauseOnHover` 開啟。移動的目標很難指中。

### 常見問題

**為什麼折線停在最新讀數之前？** 這是 `Interval` 的作用：檢視落後於最新讀數，以便持續移動到下一個讀數。減少動態效果時不捲動，所以會立即顯示最新讀數。

**懸停會設定 `IsPaused` 嗎？** 不會。`IsPaused` 是你的請求，保持為 false。要了解檢視狀態，請讀取 `IsHeld` 或 `PausedChanged`。

**能繪製多個序列嗎？** 每個圖表一條線。需要比較多個即時數值時，請堆疊多個圖表。

## API 參考

<!-- api: RealtimeStream -->

<!-- api: RealtimeStreamPoint -->

## 無障礙

- 繪圖區是一個可聚焦的群組，以 `Label` 命名，名稱中包含按鍵說明。繪製內容本身對輔助技術隱藏，其資料透過說明文字朗讀。
- 頁首的數值是真實文字：無論數字如何滾動，螢幕報讀器都能讀出標籤和目前值。
- 狀態按鈕的名稱包含狀態和按下後的效果，例如「Live. Pause updates」。
- 被標註的讀數在到達時以禮貌方式朗讀一次。鍵盤移動時朗讀各點的讀數。
- 狀態從不只靠顏色表達：按鈕上是圓點或暫停圖示加文字，標記上是菱形加文字。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 把焦點移入或移出繪圖區。移入時檢視靜止並讀取最新可見點，移出時解除。沒有讀數時略過繪圖區 |
| `Left` / `Right` | 讀取上一個或下一個點 |
| `PageUp` / `PageDown` | 向前或向後移動十個點 |
| `Home` / `End` | 讀取最早或最新的可見點 |
| `Space` / `Enter` | 暫停或恢復 |
| `Escape` | 清除讀取並恢復 |

## 動態效果

- 折線按螢幕更新率捲動，前端在讀數之間連續移動，而不是逐步跳變。
- 數值軸緩動到新範圍。
- 折線走到標記時，標記帶一次過衝彈出。
- 恢復時滑回即時位置，頁首的數字滾動。
- 減少動態效果時停止捲動，繪圖區只在讀數到達時重繪。關閉動態效果時相同。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 新讀數 | 折線繼續向它捲動 | 立即繪製在右邊緣 | 與減少相同 |
| 座標軸範圍變化 | 緩動到新範圍 | 直接跳變 | 與減少相同 |
| 被標註的讀數 | 標記帶一次過衝彈出 | 標記直接出現 | 與減少相同 |
| 恢復 | 滑回即時位置 | 直接跳到即時位置 | 與減少相同 |

## 響應式

- `RealtimeStream` 填滿寬度，調整大小時重繪。`PlotHeight` 保持你設定的值。
- 時間標籤會減少以保持間距。30 秒的視窗在寬繪圖區每 5 秒一個標籤，在手機上每 10 秒或更久一個。
- 寬度小於 420 px 時，數值欄變窄並顯示更少的標籤。
- 與較新標記重疊的標記文字會被省略，工具提示仍會顯示它。
- 觸控時按住並拖曳可讀取並保持靜止，放開手指後恢復。懸停只對滑鼠或觸控筆生效。

## 相關元件

- `LineChart`：供人回顧的完整序列。
- `BrushChart`：需要縮放與範圍選取的密集序列。
- `Sparkline`：列或卡片裡的微型趨勢。
- `Gauge`：目前值相對其上下限的情況。

| 元件 | 何時選擇 |
|---|---|
| `LineChart` | 資料已完整，需要比較多個序列 |
| `BrushChart` | 一條長序列需要縮放與範圍選取 |
| `Sparkline` | 趨勢只是其他內容旁邊的小細節 |
| `Gauge` | 只關心目前值相對上下限的情況 |
| `AnimatedCounter` | 只關心目前數字，且沒有上下限 |
