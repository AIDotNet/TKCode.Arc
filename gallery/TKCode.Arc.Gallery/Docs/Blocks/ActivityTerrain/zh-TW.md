---
title: 活動地形
summary: 把一週每小時的活動畫成三維地形：可以轉動與縮放，將指標移到某個小時看它的數字，飛到一根長條旁，或把它壓平成熱度圖；旁邊則讀得到總計、高峰時段與最忙的一天。
source-hash: d106cd4feb20
---

<Demo name="Preview" />

## 何時使用

`ActivityTerrain` 把一週的每小時數字呈現為 168 根立在地面上的長條：七列為星期，二十四欄為小時。長條越高、顏色越深，代表該小時越忙碌，因此一週的節奏一眼就能讀懂：早晨的爬升、午餐的低谷、寧靜的週末。將指標懸停在長條上會將它抬起，並顯示它的數值、前一週同一小時的數值，以及那一天的走勢；點擊它則讓相機飛到該處。繪圖區旁邊的摘要會列出一週總計與其變化、最忙的幾個小時，以及最忙的一天。

- **產品分析**：每小時的造訪、註冊或搜尋次數，看出人們究竟何時使用產品，以及發布或維護時段會造成最小的影響。
- **工程活動**：每小時的建置、部署或合併的變更，顯示團隊何時出貨，以及週五下午是否仍然安靜。
- **營運與值班**：每小時的警示或事件，使用危險色調；一個糟糕的下午會呈現為單一的紅色尖峰，而比上週高的走勢則清楚表示情況惡化。
- **客服與銷售櫃台**：每小時的對話、工單或來電，依摘要列出的高峰來規劃人力。
- **儀表板首頁的展示**：動態效果（開場、量度切換時的波浪、飛行進場）會邀請人們探索一週的資料，而這些資料若放在表格中，人們往往只會略過。

## 何時不用

- **不是七天乘以二十四小時的資料。** 這個形狀是固定的；一整年的日資料請使用 `ActivityHeatmap`，其他期間則使用折線圖或長條圖。
- **精確比較比整體形狀更重要時。** 透視會讓遠處的長條看起來較小；熱度圖有幫助，但表格或長條圖能更精確地比較數值。
- **同一頁面有許多圖表。** 地形很大且會動，每頁放一個就夠了。小型的多圖並排請使用 Sparkline 或 `MetricCard`。
- **資料不是每小時的計數或金額**（比率、可能為負的平均值）：長條高度從零開始，並隨數值增長。

## 用法

### 結構

從上到下依序是：可選的標題與副標題；工具列，內含量度切換器（有兩個以上量度時顯示）與檢視切換器（地形或熱度圖）；繪圖區，內含提示卡，以及相機移動後才出現的「重設檢視」按鈕；從少到多的圖例，附一行手勢提示；以及摘要，包含三個區塊：一週總計與其變化、高峰時段、最忙的一天。繪圖區的高度為區塊寬度的 44%，介於 280 至 460 之間。

### 量度

每個 `ActivityTerrainMetric` 是一種量度：`Values` 中有 168 個數值，依週一 00:00 開始，逐小時、逐日排列（`day × 24 + hour`）。`PreviousValues` 保存前一週，順序相同；沒有它時，變化與「上一週」列都會隱藏。`Label` 是切換器上的名稱，`Unit` 會接在卡片與摘要的數值後面，`Key` 則讓你以 `SelectedMetricKey` 選取它。

```csharp
var visits = new ActivityTerrainMetric
{
    Key = "visits",
    Label = "Visits",
    Unit = "visits",
    Values = thisWeek,            // 168 numbers: Monday 00:00, Monday 01:00, … Sunday 23:00
    PreviousValues = lastWeek     // optional
};
terrain.Metrics.Add(visits);
```

以 7 × 24 方格保存的資料，可以用 `ActivityTerrainMetric.Flatten(grid)` 轉換（列為從週一開始的日，欄為小時）。缺少的項目以零計算。其餘的計算都由地形自行完成：總計、與上一週的變化、高峰時段（由大到小排列，同值時依日與小時排序）、各日總計、最忙的一天，以及一週中落在工作時段內的比例。

### 色調與好消息

`Tone` 決定長條、圖例與提示卡中的目前小時所用的顏色：`Accent`（預設）、`Neutral`、`Success`、`Warning` 或 `Danger`。`HigherIsBetter` 決定上升時顯示綠色還是紅色；對於錯誤、事件或等待時間，請設為 false。以下是一個使用危險色調、只有一個糟糕下午的量度：

<Demo name="Incidents" />

```xml
<arc:ActivityTerrain PeakCount="5"
                     Title="Incident load"
                     Subtitle="Alerts that paged someone, per hour.">
    <arc:ActivityTerrainMetric Key="incidents" Label="Incidents" Unit="incidents" Tone="Danger" HigherIsBetter="False" />
</arc:ActivityTerrain>
```

### 地形與熱度圖

`ViewMode` 在地形（`Perspective`）與從上方俯視的平面熱度圖（`Top`）之間切換。在熱度圖中，長條會壓平成方格，每一天都有標籤，拖曳也不再轉動視圖；懸停、點擊與縮放仍然可用。工具列中的檢視切換器設定的是同一個屬性，`ViewModeChanged` 會回報變更。

<Demo name="HeatMap" />

### 懸停與飛入

懸停在長條上，或用方向鍵移到它，會醒目標示該小時：長條抬起，一條色帶沿著它所在的列與欄延伸到軸標籤，其餘的一週淡出，提示卡則顯示日、小時、數值、上一週、變化，以及該小時佔當天的比例，並以小長條列出當天二十四小時。`HighlightedCell` 保存該小時，`CellHovered` 會回報它。

點擊長條會讓相機飛到它（至少放大到 1.5 倍）並保持它抬起；點擊空白地面或按下 Escape 則回到整週。`FocusedCell` 保存該小時，`CellFocused` 會回報它，而 `FlyTo`、`ShowWeek`、`FlyToCommand` 可以從程式碼做到同樣的事。摘要中的高峰時段按鈕會飛到該小時，再飛回來。

```csharp
terrain.CellFocused += (_, e) =>
{
    if (e.Cell is { } cell)
    {
        ShowDetails(cell.Day, cell.Hour, e.Value);
    }
};
terrain.FlyTo(new ActivityTerrainCell(Day: 2, Hour: 14));   // Wednesday 14:00
```

### 轉動與縮放

拖曳即可轉動視圖（偏航角沒有限制，傾角保持在 10° 至 84° 之間，過度拖曳後會彈回），放開後會滑行停下。按住 Ctrl 或 ⌘ 搭配滾輪，或以捏合手勢，可在 0.7 至 2.8 之間縮放；單純的滾輪則捲動頁面。`Yaw`、`Pitch` 與 `Zoom` 回報相機的狀態，`IsViewDefault` 在視圖被移動後變為 false；「重設檢視」按鈕（或 `ResetView`、`ResetViewCommand`）會讓視圖回到原位，並引發 `ViewReset`。

### 窄版容器

版面依區塊自己的寬度變化。寬度低於 560 時，切換器會堆疊排列，繪圖區高度為 280 並多轉一些角度（50°），小時標籤縮短為「09」，摘要則分為兩欄。寬度達到 880 時，摘要會移到繪圖區旁邊的欄位中。

<Demo name="Narrow" />

### 你自己的資料與即時更新

每當有新數字進來，就替換 `Values`（以及 `PreviousValues`）：長條會以與量度切換相同的波浪升到新高度，摘要的數字也會滾動更新。星期名稱取自 `DayNames` 與 `DayAbbreviations`，`WorkingHours` 則定義哪些小時計入工作時間。

<Demo name="LiveData" />

```xml
<arc:ActivityTerrain Title="Support desk"
                     SelectedMetricKey="tickets">
    <arc:ActivityTerrain.WorkingHours>
        <arc:ActivityTerrainWorkingHours FirstDay="0" LastDay="4" FirstHour="8" LastHour="18" />
    </arc:ActivityTerrain.WorkingHours>
    <arc:ActivityTerrainMetric Key="chats" Label="Chats" Unit="chats" />
    <arc:ActivityTerrainMetric Key="tickets" Label="Tickets" Unit="tickets" Tone="Neutral" />
</arc:ActivityTerrain>
```

### 文字

每段可見或會被朗讀的文字，都是一個有英文預設值的屬性：圖例（`LessText`、`MoreText`）、切換器（`PerspectiveText`、`TopViewText`）、`ResetViewText`、提示（`HintText`、`TouchHintText`、`KeyboardHintText`）、提示卡（`PreviousWeekText`、`ChangeText`、`ShareFormat`、`HourRangeFormat`）、摘要（`SummaryTitleFormat`、`ChangeSuffixText`、`PeakHoursTitle`、`BusiestDayTitle`、`BusiestDayFormat`），以及所有播報文字。格式字串使用編號的預留位置；某個量度自己的 `SummaryTitle` 會取代該量度的 `SummaryTitleFormat`，而 `ValueFormat`（例如 `"N1"` 這類 .NET 數字格式）可以改變它的數值顯示方式。

## API 參考

<!-- api: ActivityTerrain -->

<!-- api: ActivityTerrainMetric -->

## 無障礙

- 繪圖區是一個可取得焦點的元素。螢幕閱讀器會朗讀一句關於這一週的摘要（量度、總計、變化、最忙的小時與日），並將鍵盤說明作為其描述。
- 這一週也以表格的形式提供給螢幕閱讀器：每天一列，以星期命名，每個小時一個儲存格（「14:00: 1,000 visits」），並以量度作為標題。
- 一個禮貌模式的即時區域會播報醒目的小時、其數值與變化、飛入與回到整週、檢視切換、量度切換與重設。
- 量度與檢視切換器是分段控制項，各自只有一個 Tab 停駐點。高峰時段的列是切換按鈕，名稱包含日、小時與數值，並在相機位於該小時時保持按下狀態。「重設檢視」只有在顯示時才在 Tab 順序中。
- 鍵盤焦點會在繪圖區外側 2 px 處顯示 2 px 的焦點環；以指標取得焦點時則不顯示。
- 顏色從來不是唯一的訊號：數值會出現在卡片、表格與播報中，變化則帶有正負號與箭頭。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| ← → ↑ ↓ | 第一次按下會醒目標示最忙的小時；之後以小時（← →）或以天（↑ ↓）移動，到達邊緣時停止 |
| Home / End | 醒目標示那一天的第一個 / 最後一個小時 |
| Enter / Space | 飛到醒目標示的小時 |
| Escape | 回到整週；再按一次則清除醒目標示 |
| Shift + ← → | 將視圖轉動 15° |
| Shift + ↑ ↓ | 將視圖傾斜 8°（10° 至 84°） |
| + / − | 以 1.25 倍放大 / 縮小（0.7 至 2.8） |

## 動態效果

- **開場。** 繪圖區第一次出現在畫面上時，相機會從高空向下擺動（約 3 秒），同時長條從地面長出，先從最後一排與最左一欄開始。
- **量度切換。** 每根長條的高度與顏色都會彈簧式地變為新數值，各自比前一根稍晚開始，從週一 00:00 朝週日 23:00 呈對角線推進（波浪約三分之一秒）；摘要的數字逐位滾動，其文字交叉淡化；圖例的顏色在 480 ms 內變化。
- **相機。** 飛入、回到、重設與檢視切換都是 1 至 1.4 秒的彈簧，被打斷時會保留速度；縮放較快；放開的拖曳會滑行，過度拖曳則會彈回傾角限制之內。傾角變化時，地形與繪圖區的貼合會以自己的彈簧跟隨。
- **懸停。** 長條約在四分之一秒內抬起，其餘的一週淡出；提示卡以 120 ms 淡入，之後隨相機移動，以彈簧跟隨長條。
- **減少動態效果。** 沒有開場與波浪；相機移動、檢視切換與縮放立即完成；拖曳跟隨指標但不滑行；提示卡只淡入淡出。關閉動態效果時，什麼都不會動。
- 靜止的繪圖區不繪製任何內容；只有在有東西移動、且繪圖區位於畫面上時，才會繪製影格。

## 相關元件

- `ActivityHeatmap`：以日曆熱度圖呈現一整年的日子。
- `BarChart`：每個類別的數值，可精確比較。
- `MetricCard`：一個數字及其變化，用於有許多量度的儀表板。
