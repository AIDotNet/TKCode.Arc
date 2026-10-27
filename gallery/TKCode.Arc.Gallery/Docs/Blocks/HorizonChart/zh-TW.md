---
title: Horizon chart
summary: 依每個系列偏離自身基線的程度來比較許多日序列，每個系列摺疊成一條細色帶行：色帶數量用同一根彈簧摺疊與展開所有行，各行可依名稱、峰值或最新值重新排序，一條十字線用游標、觸控與鍵盤在所有行上讀出同一天。
source-hash: f61e270a0109
---

<Demo name="Preview" />

## 何時使用

`HorizonChart` 在一個畫面上回答 "這麼多序列裡哪個偏離了、什麼時候、偏了多少"。每一行都是一個日序列，以它自己的基線（預測、目標、平均值）為參照；高於基線的距離用一種顏色填滿，低於基線的距離用另一種顏色填滿，距離被摺疊成多條色帶，疊放在一條細長的帶上，所以一行只占 36px，而不是一張完整的圖表。十四行、119 天大約占 550px。

- **一批相似的對象。** 門市、樞紐、地區、服務或行銷活動對照各自的計畫：眼睛會找到帶有深色色帶的行，以及它發生的那一天。
- **發現共同事件。** 十字線穿過所有行，所以一次故障、一場促銷或一個假日如果影響了多行，就會呈現為一條直線，並在每行末尾寫出同一天的值。
- **高密度儀表板。** 每行一張折線圖會太高，而一張數字表格又太平淡。

## 何時不用

- **只有幾個系列。** 只有三四行時，折線圖呈現的內容相同，而且有可以讀數的刻度。
- **絕對水準。** 各行是相對基線的偏差；基線本身的水準並沒有畫出來。請用折線圖或面積圖呈現水準。
- **從形狀上讀取精確數值。** 色帶用來發現問題；數字來自十字線與數值欄。
- **數百天以上或超過約 40 行。** 呈現更多之前請先彙總（用週代替天）：此區塊不會代你彙總。

## 用法

此區塊衍生自 Avalonia 的 `TemplatedControl`。它的資料是一組 `HorizonSeries`（一個名稱，加上每天一個帶正負號的值：正數高於基線，負數低於基線）以及與之對齊的 `Days`，最早的在前。`HorizonDay` 帶有 `Label`（"Sep 27"）、`LongLabel`（"Sunday, September 27"）與選用的 `Date`；只設定日期時，標籤會根據它格式化。

```xml
<arc:HorizonChart Title="Orders against forecast"
                  Description="Daily orders at each hub compared with its winter forecast"
                  BaselineLabel="winter forecast"
                  Days="{Binding Days}">
    <arc:HorizonSeries Name="Northgate" Values="{Binding NorthgateValues}" />
    <arc:HorizonSeries Name="Harbor Point" Values="{Binding HarborPointValues}" />
</arc:HorizonChart>
```

```csharp
chart.Days = Enumerable.Range(0, 119).Select(i => new HorizonDay(start.AddDays(i))).ToArray();
chart.Series.Add(new HorizonSeries("Northgate", values, key: "northgate"));
chart.Bands = 2;
chart.Sort = HorizonSort.Latest;
```

`SeriesSource` 可以改為接收你自己持有的集合（會跟隨 `ObservableCollection` 的變化）。對於帶有日期的天，座標軸會在第一天與每月一號顯示刻度；設定 `Ticks`（索引與標籤）可以自行放置。值為 NaN 時畫成零，讀作短橫線。

### 色帶如何運作

每一行是一個多邊形，色帶有多少條就畫多少次。`Extent` 是填滿所有色帶的距離：預設是最大絕對值向上取整到 12 的倍數（最大波動為 35 時取 36）。有三條色帶時，每條色帶跨越範圍的三分之一，所以 12 填滿第一條，24 填滿第二條，36 填滿第三條；每一層以 alpha `1 − 0.08^(1/bands)` 繪製，所有色帶都填滿時疊加為 0.92。因此顏色越深表示離基線越遠，而一條色帶就是一般的面積圖。

摺疊過程中，色帶數量是一個連續的值：每一行的四個圖層與 alpha 共同跟隨一根彈簧（一根約 600ms 內穩定的臨界阻尼彈簧），所以在摺疊中途改變數量，會從目前狀態繼續。

<Demo name="Palette" />

### 排序

`Sort` 為 `Name`（A 到 Z）、`Peak`（該行最大絕對值大的在前，預設值）或 `Latest`（最後一天的值，高的在前）。值相同時保持資料順序。變化時，每一行同時用同一根彈簧移到新位置，數字隨之移動；十字線與色帶數量不受影響。

### 十字線

把游標移到色帶欄上（名稱欄與數值欄沒有反應）即可把十字線放到某一天：圖表上方的日期與每行末尾的值會立即變成那一天，狀態行寫出 "Picked day"。線出現在游標所在處，然後以一根快速的彈簧跟隨；游標離開時它淡出，讀數回到最近的一天。在觸控螢幕上，點擊或水平拖曳會設定十字線，抬起手指後它仍然保留；點擊圖表旁邊則清除它。`SelectedDayIndex` 是雙向的，`SelectDay` 與 `ClearSelection` 可在程式碼中驅動它，`DayChanged` 與 `DayCommand` 回報每一次變化。

<Demo name="Events" />

### 格式化數值與其他單位

`ValueFormatter` 格式化各行的值、刻度條兩端的標籤與朗讀的文字；預設寫成帶正負號的整數百分比，使用真正的減號。如果想要一個不依賴資料的整齊色帶邊緣，請設定 `Extent`。

```csharp
chart.ValueFormatter = v => v switch { > 0 => $"+{v:0} ms", < 0 => $"−{-v:0} ms", _ => "0 ms" };
chart.Extent = 60;
```

<Demo name="Latency" />

### 窄容器

區塊會讀取自己的寬度。寬度不超過 900 時，名稱欄與數值欄變窄（116 和 52）；不超過 560 時，內距降到 16，行高為 34px，文字為 12px，兩欄寬度為 84 / 44px，兩個開關共用一行（放不下時各占一行），顏色刻度條占滿整行。

<Demo name="Compact" />

### 文字

`Labels` 保存區塊寫出或朗讀的每一個詞（開關標籤、狀態行、說明及其格式、播報以及表格的欄名），帶有英文預設值；指定一個新的 `HorizonChartLabels` 即可翻譯或改寫。`AboveColor` 與 `BelowColor` 取代 Series1 與 Series3 的顏色，當頁面自帶控制項時，可用 `IsSortSwitchVisible`、`IsBandSwitchVisible` 與 `IsLegendVisible` 隱藏相應部分。

## API 參考

<!-- api: HorizonChart -->

<!-- api: HorizonSeries -->

<!-- api: HorizonDay -->

<!-- api: HorizonTick -->

<!-- api: HorizonChartLabels -->

<!-- api: HorizonDayChangedEventArgs -->

<!-- api: HorizonSortChangedEventArgs -->

<!-- api: HorizonBandsChangedEventArgs -->

<!-- api: HorizonSort -->

## 無障礙

- 圖表是一個可取得焦點的群組，名為 "{title}. {n} rows"，並以按鍵提示作為說明文字。焦點環繪製在圖表內部，區塊的圓角裁切不會把它切掉。
- 用鍵盤移動十字線時，會禮貌地播報日期以及最高與最低的行："Tuesday, March 16. Top Foxhollow +8%, bottom Northgate −31%."。排序與色帶數量的變化也會播報；移動游標則不會。
- 一張隱藏的表格依目前順序列出每一行，包含其最新值、最高值與最低值以及各自的日期，螢幕閱讀器因此無需形狀就能讀出數字。
- 顏色刻度條是一張帶有朗讀描述的影像（"Color scale from −36% under the forecast to +36% over it, 3 bands per side"），它的說明寫明一條色帶代表什麼。方向同樣由帶正負號的數字傳達，而不只靠顏色。
- 兩個開關都是有名稱的群組；其中的按鈕是切換按鈕。

### 鍵盤

- **Tab** 依序前往排序開關、色帶開關與圖表，各一個停靠點。Tab 進入圖表時，十字線會放在最新的一天。
- **← →** 把十字線移動一天；**Shift + ← →** 移動一週。它在第一天與最後一天停住。
- **Home / End** 跳到第一天與最後一天。
- **Escape** 隱藏十字線；焦點留在圖表上，下一次按方向鍵從最新的一天開始。
- 在兩個開關中，**← ↑** 與 **→ ↓** 立即改變選擇並循環；**Home / End** 跳到兩端。

## 動態效果

- **摺疊**：一根彈簧（臨界阻尼，600ms 達到靜止）把每一行的色帶數量帶到新值；圖層及其 alpha 在每一影格跟隨它，什麼都不會重建。中途出現新的目標時保留速度。顏色刻度條的格子用 480ms 的先慢後快再慢曲線增長或縮小並改變不透明度，與摺疊同時開始；它的說明立即變化。
- **排序**：每一行從原位置彈到應在的位置（約 700ms，無過衝），所有行同時開始；各行繪製不透明的底色，所以一行滑過另一行時會遮住它。
- **十字線**：它在原地出現並用 160ms 淡入，以一根快速的臨界阻尼彈簧（約 250ms 達到靜止）跟隨游標或按鍵，離開時在原位用 160ms 淡出。文字始終立即變化。
- **開關**：被選中的膠囊在一根無過衝的彈簧上滑動。
- 區塊首次出現時不播放任何動畫。減少動態效果與關閉動態效果會讓每一次摺疊、重新排序、十字線移動與刻度條變化立即完成。

## 相關

- `Metric explorer` 與 `Revenue explorer`，其他在時間軸上帶有十字線的 Analytics 區塊。
- `Cohort retention`，同樣用顏色刻度比較隨時間變化的許多行。
- `SegmentedControl`，此區塊使用的開關。
