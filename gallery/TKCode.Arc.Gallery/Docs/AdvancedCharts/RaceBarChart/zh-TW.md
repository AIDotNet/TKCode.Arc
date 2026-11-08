---
title: 競速長條圖
summary: 隨時間變化的排名：數值上升時長條彼此超越，時間軸可以播放、暫停或拖曳瀏覽各個時期。
source-hash: c411f827ec0e
---

<Demo name="TopRegions" />

## 何時使用

`RaceBarChart` 訴說的是「誰在什麼時候領先」。每個時期是一份排名，圖表從一份排名過渡到下一份，讀者能看到一方超過另一方的那一刻，而靜態的合計表格看不出這一點。它最適合用於展示：報告的開場、年度回顧，或讓人按下播放後觀看的頁面。

- **銷售或推廣回顧中依地區的累計合計**：想知道哪個地區在什麼時候拉開了差距。
- **媒體儀表板上依節目或頻道的受眾**：每週數字起起落落，排名不斷變化。
- **年度回顧中依產品的市占率**：並醒目提示頁面所討論的那一款產品。
- **工程回顧中依團隊的貢獻**：由群組提供圖例。
- **公開的排行榜**：訪客可以拖曳瀏覽各個時期。

## 何時不用

- **某一時刻的單份排名**：用 `BarChart`，不必等待就能比較數值。
- **需要查詢的精確數值**：用 `Table` 或 `DataGrid`。長條在移動，只有停下時數值才便於閱讀。
- **每個參賽者的趨勢**：用 `LineChart`，所有時期同時留在畫面上。
- **整體的組成隨時間變化**：用 `Streamgraph`，合計始終可見。
- **超過約二十個時期且沒有明確的故事**：過長的競速會變成螢幕保護程式。請彙總時期，或改用 `LineChart`。

## 用法

為圖表設定 `Items`（每個長條一項）和 `Frames`（每個時期一幀，依順序）。幀中的數值依 id 對應到項目，幀裡沒有提到的 id 計為零。圖表在每一幀中為各項目排名，顯示前 `BarCount` 名，並在每個長條上繪製名稱和數值。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:RaceBarChart Label="各鄉鎮的步道報名人數" IsAutoPlay="False">
        <arc:RaceBarChart.Items>
            <arc:RaceBarItem Id="elmford" Label="Elmford" />
            <arc:RaceBarItem Id="stonely" Label="Stonely" />
        </arc:RaceBarChart.Items>
        <arc:RaceBarChart.Frames>
            <arc:RaceBarFrame Period="春季">
                <arc:RaceBarValue Id="elmford" Value="120" />
                <arc:RaceBarValue Id="stonely" Value="90" />
            </arc:RaceBarFrame>
            <arc:RaceBarFrame Period="夏季">
                <arc:RaceBarValue Id="elmford" Value="180" />
                <arc:RaceBarValue Id="stonely" Value="240" />
            </arc:RaceBarFrame>
        </arc:RaceBarChart.Frames>
    </arc:RaceBarChart>
</UserControl>
```

在程式碼中，幀可以由字典建構，這也是資料來自查詢時的常見形式。

```csharp
chart.Items = towns.Select(t => new RaceBarItem(t.Id, t.Name)).ToList();
chart.Frames = seasons.Select(s => new RaceBarFrame(s.Name, s.SignUpsByTown)).ToList();
```

`RaceBarChart` 衍生自 `TemplatedControl`。一個自繪表面繪製長條、座標軸和圖例，大型時期標籤、播放按鈕和時間軸圍繞在它周圍。`Label` 為輔助技術命名圖表。

### 結構

1. **圖例**：設定 `Groups` 後出現在座標軸上方。
2. **數值軸**：長條上方的格線和標籤。刻度數量隨寬度變化，約每 84 px 一個刻度。
3. **長條**：高 28 px，間距 6 px，每個長條都帶名稱和數值。
4. **時期標籤**：右下角的大型文字，位於長條之後。
5. **播放按鈕**：36 px。顯示播放、暫停，在最後一個時期顯示重播。
6. **時間軸**：以時期為單位的滑桿，觸控目標 36 px，兩側是第一個和最後一個時期的名稱。

### 起伏的數值

幀不必是累計值。使用原始值時每個時期彼此獨立，因此參賽者既能上升也能下降。`IsLooping` 在最後一個時期之後重新開始，`BarCount` 決定畫面上有多少個長條：排名低於它的項目滑出，達到它的項目上升並淡入。

<Demo name="RisingAndFalling" />

### 控制時間軸

`FrameIndex` 是雙向的，每次變化都會平滑經過中間的數值，因此你自己的按鈕或滑桿可以驅動圖表。由頁面而不是圖表決定何時開始時，設定 `IsAutoPlay="False"`。`IsPlaying` 同樣是雙向的，`Play`、`Pause` 和 `Replay` 可在程式碼中完成同樣的操作。`FrameChanged` 回報每個時期及其領先者，`PlayingChanged` 回報播放的開始和停止。

<Demo name="ControlledTimeline" />

### 群組與醒目提示

`Groups` 賦予長條顏色意義。前四個群組使用 `Series1` 到 `Series4` 並出現在圖例中。後面的群組和沒有群組的項目繪製為中性色長條。沒有群組時所有長條都使用 `Series1`。`HighlightId` 強調頁面所討論項目的名稱和數值，但不改變其顏色。

```xml
<arc:RaceBarChart Label="各地區的安裝量" HighlightId="larkfield" BarCount="10" />
```

### 格式與時長

`ValueFormatter` 格式化長條和提示框。預設是精簡數字，例如 1.2K。`TickFormatter` 格式化座標軸，未設定時回退到 `ValueFormatter`。`FrameDuration` 設定播放時一個時期的時長。

```csharp
chart.ValueFormatter = value => value.ToString("N0");
chart.TickFormatter = value => ArcFormatting.Compact(value);
chart.FrameDuration = TimeSpan.FromSeconds(1);
```

### 讀取長條

將游標停在長條上、用手指按下，或使用上下方向鍵。其他長條退後，提示框顯示名稱、時期、數值和名次。讀數隨該項目移動，如果該項目跌出畫面上的長條，讀數會清除。

### 資料與效能

相等的數值保持 `Items` 中的順序，因此並列不會閃爍，向回拖曳也得到相同的順序。非有限數值計為零，負值繪製空長條，id 重複的第二項會被捨棄。可觀察的 `Items`、`Frames` 或 `Groups` 清單的變化會被追蹤；幀內部的變化不會，請改為指派新的幀。

一個迴圈驅動全部內容，在圖表暫停且靜止時停止，離開畫面時暫停。文字只配置一次，僅在顯示的數字變化時重建，因此數百個項目也能保持流暢；只繪製畫面上的長條。

### 內容指南

- **Label**：度量和範圍，例如「各地區的屋頂太陽能安裝量（累計）」。
- **時期**：簡短且長度一致，例如「3 月」或「2026 年第 2 季」。它會以大型文字繪製。
- **項目標籤**：簡短的名稱。過長的名稱以省略號截斷。

| 不要寫 | 應寫 |
|---|---|
| 資料 | 各城市的每月乘客量 |
| 時期 3 | 3 月 |
| 群組 A | 北部海岸 |

### 最佳實務

**建議**

- 讓 `BarCount` 接近人們能跟上的參賽者數量，通常為 5 到 12。
- 頁面圍繞某一參賽者時使用 `HighlightId`。
- 圖表只是頁面眾多內容之一時，從 `IsAutoPlay="False"` 開始。

**避免**

- 把顏色用於 `Groups` 以外的用途。色相必須有意義。
- 過短的 `FrameDuration`。低於約 500 ms 時沒有人能讀出數值。
- 依靠動畫來陳述結論。請在圖表旁用文字說明。

### 常見問題

**為什麼圖表不會自己開始？** 它在第一次捲動進入可視範圍時開始，並且只在完整動態下開始。減少動態時它會等待播放按鈕。

**為什麼圖表停在了兩個時期之間？** 不會這樣：暫停時會完成向下一時期的移動，逐步和拖曳都落在完整的時期上。

## API 參考

<!-- api: RaceBarChart -->

## 無障礙

- 繪圖區是由 `Label` 命名的可聚焦群組。它的說明文字是時期和領先者，或正在讀取的長條。
- 時間軸是滑桿，其值文字形如 "Mar, period 3 of 12"，播放按鈕會說明自己的操作。
- 逐步時播報時期和領先者。讀取長條時播報名稱、名次和數值。播放期間不播報，以免螢幕閱讀器被大量訊息淹沒；暫停時會再次播報目前狀態。
- 每個長條都顯示名稱和數值，因此色相不是唯一的線索。

### 鍵盤

| 按鍵 | 動作 |
|---|---|
| `Tab` / `Shift+Tab` | 在繪圖區、播放按鈕和時間軸之間移動 |
| `Space` / `Enter` | 播放或暫停。在最後一個時期從頭重播 |
| `Left` / `Right` | 平滑地前進或後退一個時期。逐步會暫停播放 |
| `Up` / `Down` | 依名次在長條之間移動讀數並顯示提示框 |
| `Home` / `End` | 前往第一個或最後一個時期 |
| `Escape` | 清除讀數 |

## 動態

- 數值在時期之間內插，每一列都在自己的彈簧上移動，因此超越看起來是一次連續的滑動。獲得新目標的列會保留速度。
- 進入前段的長條從下方上升並淡入。離開的長條滑出並淡出。
- 時間軸的滑桿沿臨界阻尼彈簧跟隨。座標軸刻度隨比例滑動，間距變化時交叉淡化。時期標籤捲動切換並帶有短暫的模糊。
- 離開畫面時播放暫停，回到畫面後繼續。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 首次顯示 | 設定 `IsAutoPlay` 時開始播放 | 等待播放按鈕 | 與減少相同 |
| 時期變化 | 數值平滑過渡，列在彈簧上滑動 | 立即就位，沒有內插 | 與減少相同 |
| 播放中 | 連續 | 每個 `FrameDuration` 一步 | 與減少相同 |
| 時期標籤 | 捲動切換並帶短暫模糊 | 短暫交叉淡化 | 立即替換 |

## 回應式行為

- `RaceBarChart` 依自身寬度判斷。低於 400 px 時名稱移入長條內部，長條放不下名稱時移到長條末端之後；數值跟在名稱後面。
- 低於 400 px 時時期標籤縮小一級，時間軸兩側的第一個和最後一個時期名稱隱藏。
- 刻度數量隨繪圖區寬度變化，從 2 到 8 個。
- 時間軸整條 36 px 的區域都回應手指，因此手機上也能拖曳。按下長條可讀取它。

## 相關元件

- `BarChart`：某一時刻的單份排名。
- `LineChart`：跟隨每個參賽者隨時間的變化。
- `Streamgraph`：合計的各層隨時間的變化。

| 元件 | 何時選擇 |
|---|---|
| `BarChart` | 只有一個時期需要比較 |
| `LineChart` | 所有時期都應同時可見 |
| `Streamgraph` | 合計與其組成部分同樣重要 |
| `DataGrid` | 需要查詢精確數值 |
