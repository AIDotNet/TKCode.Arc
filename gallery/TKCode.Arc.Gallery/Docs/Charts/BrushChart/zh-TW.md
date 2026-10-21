---
title: 刷選圖表
summary: 帶概覽條的密集時間序列：拖曳視窗即可縮放，透過控點調整大小，並在原處查看事件。
source-hash: e7a4445fb8ad
---

<Demo name="OverviewAndDetail" />

## 何時使用

`BrushChart` 在一個繪圖區中繪製較長的每日序列，並在其下方的概覽條中保留完整歷史。讀者在概覽條上拖曳一個視窗來選擇所關心的時段，繪圖區隨之變化。它適合放在分析或檢討頁面的主體部分，此時序列是頁面的主要內容，探索它本身就是任務。

- **SaaS 管理後台中的每日活躍使用者**：產品團隊查看一年的每日工作階段，然後把視窗縮小到某次定價調整前後的幾週。概覽條讓全年其餘部分維持可見，讀者不會迷失目前位置。
- **事件檢討時間軸**：可靠性負責人為一個季度繪製 p95 延遲，並用 `Annotations` 標出部署與故障。每個事件同時顯示在曲線和概覽條上，檢討時能同時讀到因果關係。
- **財務儀表板中的營收或訂單量**：財務人員打開一個包含數千個資料點的每日營收序列，點選會設定 `Range` 的「最近 30 天」等預設，並在由 `RangeChanged` 更新的說明文字中讀取新視窗。
- **帳單頁中的計量用量**：客戶打開目前計費週期內每日的 API 呼叫次數，平移到一個峰值處，讀取其日期和數值，然後再決定是否送出支援工單。

## 何時不用

- **只有幾週的資料，或需要比較多個資料系列**：使用 `LineChart`。它能以圖例繪製多個資料系列，並顯示整個範圍，沒有概覽條。
- **表格儲存格或 KPI 卡片中的小型趨勢**：使用 `Sparkline`，它沒有座標軸，也沒有互動。
- **包含負值，或數值軸不能從零開始**：使用 `LineChart`，它提供 `YMinimum` 和 `YMaximum`。刷選圖表的數值軸始終從零開始，因此無法顯示帶正負號的序列。
- **隨時間變化的構成分層，例如各管道的流量**：使用 `Streamgraph`，它會堆疊各層。刷選圖表只繪製一個資料系列。
- **隨計數增加而變化的單一總數**：使用 `AnimatedCounter`，它為數字本身加上動畫。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:BrushChart Label="每日活躍使用者"
                    Unit="使用者"
                    PlotHeight="240"
                    OverviewHeight="52"
                    ItemsSource="{Binding Points}" />
</UserControl>
```

`BrushChart` 衍生自 Arc 圖表家族的基底類別 `ChartBase`。該基底類別提供共用的外殼：`Label`、`Unit`、`PlotHeight`、`EmptyLabel`、`ValueFormat`，帶有 `XBinding` 與 `YBinding` 的 `ItemsSource`，以及提示規則。刷選圖表額外提供長歷史所需的功能：概覽條、可見視窗（`Range`）、其最小跨度以及事件標記。它採用自繪方式：繪圖區和概覽條都由程式碼根據資料繪製，而不是由子元素拼裝而成，這使長序列依然保持流暢。

每個資料項目提供一個日期和一個數值。日期可以是 `DateTimeOffset`、`DateTime` 或 `DateOnly`，控制項會在內部將其轉換為時間。資料變更時，控制項會依日期對資料點排序，因此來源集合的順序無關緊要，不過依遞增排列的資料來源可以省去這部分工作。

### 結構

1. **繪圖區**：目前視圖內資料點的原始折線；當視圖縮放得很遠時，改為顯示七點平均值及其由低到高的範圍帶。繪圖區預設高 240 px（`PlotHeight`）。
2. **數值軸**：位於右側，佔用 44 px 的邊界（權杖的 `GutterWidth`）。它從零開始，其頂端以彈簧動畫調整到可見範圍內的最高點，刻度標籤採用精簡格式，例如 1.2K，除非設定了 `TickFormatter`。
3. **網格線**：在數值刻度處繪製的水平線，遵循 `ShowGrid`，預設開啟。
4. **日期軸列**：位於繪圖區下方，高 28 px（權杖的 `AxisRowHeight`）。其刻度標籤隨視窗寬度變化，從天到週、月和年。
5. **事件標記**：一條細線加一個圓點，使用第二資料系列顏色，並從左到右依序標註。與其他標籤衝突的標籤會被略過，因此即使文字放不下，標記依然保留。
6. **十字線與提示**：十字線位於目前的資料點上。提示顯示日期、帶 `Unit` 的數值以及當天的任何事件；空間不足時它位於資料點上方，否則位於下方，並且始終不會離開繪圖區。
7. **概覽條**（範本中的概覽部分）：繪製完整歷史，每個寬度繪製一次，視窗之外的部分會被壓暗。高 `OverviewHeight`（預設 52 px），起點位於日期軸下方 8 px，終點與數值邊界對齊，因此兩個圖表共用同一條水平刻度。
8. **視窗與控點**：概覽條上的一個環，兩端各有一個握把，握把的命中區域為 28 × 44 px。拖曳環可平移，拖曳握把可調整大小，在空白軌道上拖曳可繪製新視窗，單擊可重新置中，雙擊可重設。

### 選擇視窗與預設

`Range` 是以 `ChartTimeRange` 表示的可見視窗（起點與終點都包含在內）。為 null 時視窗即為整個序列。`Range` 是雙向的：拖曳或按鍵會把新視窗寫回它，程式碼也可以透過指定新值來移動視窗。程式碼修改會在完整動態效果下以彈簧動畫滑動到新視窗，在減少或關閉動態效果下則直接跳轉。

```csharp
chart.Range = new ChartTimeRange(last.AddDays(-30), last);
chart.RangeChanged += (_, e) => range.Text = $"視窗：{e.Range.Start:MMM d, yyyy} – {e.Range.End:MMM d, yyyy}";
```

預設適用於讀者最常詢問的視窗：最近 30 天、最近一個季度、最近一年以及完整序列。預設只是一般的指派，因此同樣的程式碼也適用於檢視模型。重設手勢（在視窗上按 Escape 或 0）始終回到完整序列，無論預設如何設定。

### 標註

`Annotations` 在繪圖區和概覽條上標記事件。每個 `ChartAnnotation` 包含一個日期、一段簡短的標籤以及可選的描述。空間足夠時，標籤會印在標記旁邊；描述則顯示在提示中，並由螢幕閱讀器播報，因此它可以承載簡短標籤容納不下的細節。

```csharp
chart.Annotations =
[
    new ChartAnnotation(start.AddDays(120), "v2 上線", "新的導覽流程已開放給所有使用者。"),
    new ChartAnnotation(start.AddDays(410), "媒體提及", "被每週電子報刊登。"),
    new ChartAnnotation(start.AddDays(530), "定價調整")
];
```

清單只保留能夠解釋序列走勢的事件。事件是讀者解讀資料時的一部分，因此不在清單中的發布無法從圖表中找到；應在發布時加入，而不是等有人詢問時才補上。

### 最小跨度

`MinSpan` 防止視窗縮小到某個跨度以下。預設值為七天（`TimeSpan.FromDays(7)`）。此限制適用於拖曳、控點、鍵盤和程式碼：比最小值更窄的範圍會擴展到最小值，起點或終點超出資料範圍的範圍會被拉回資料之內。跨度同時受序列長度限制，因此短於最小跨度的序列會顯示整個序列。

```xml
<arc:BrushChart Label="要求數" MinSpan="14.00:00:00" ItemsSource="{Binding Points}" />
```

選擇讀者應能檢視的最小跨度。每週檢討需要 14 天，這樣視窗總能顯示兩個完整的週週期，星期模式就不會隱藏在資料點之間。

### 格式化

`DateFormatter` 格式化提示和螢幕閱讀器播報中的日期，`TickFormatter` 格式化數值軸。日期文字的預設格式為英文（en-US），例如 "Tue, Sep 15, 2026"。視窗文字則使用較短的形式，例如 "Jan 1, 2026 to Apr 10, 2026"，並且不會隨 `DateFormatter` 改變。當頁面使用其他文化或帶單位的數字時，應替換預設值。

```xml
<arc:BrushChart Label="營收"
                Unit="USD"
                TickFormatter="{Binding FormatMoney}"
                ItemsSource="{Binding Points}" />
```

`ValueFormat`（.NET 格式字串，例如 `N0` 或 `C0`）作用於提示和播報中的數值，`ShowGrid` 可關閉網格線，讓繪圖區更清爽。

### 繫結到檢視模型

檢視模型擁有序列、視窗和事件。刷選圖表只負責繪製它們，並在讀者移動視窗時把視窗寫回。使用 CommunityToolkit.Mvvm 時：

```csharp
public sealed record DailyUsage(DateTimeOffset Date, double Calls);

public partial class UsageViewModel : ObservableObject
{
    private readonly DateTimeOffset _lastDay = new(2026, 10, 20, 0, 0, 0, TimeSpan.Zero);

    [ObservableProperty]
    private IReadOnlyList<DailyUsage> _usage = [];

    [ObservableProperty]
    private ChartTimeRange? _window;

    [ObservableProperty]
    private ObservableCollection<ChartAnnotation> _releases = [];

    [RelayCommand]
    private void ShowLast30Days() => Window = new ChartTimeRange(_lastDay.AddDays(-30), _lastDay);

    [RelayCommand]
    private void ShowAll() => Window = null;
}
```


- `Range` 是雙向的，因此拖曳或按鍵會把 `Window` 寫回，預設則會移動繪圖區。程式碼對 `Window` 的修改會以彈簧動畫滑動視窗，因此應在使用者操作時設定它，而不要放在計時器中。
- `Annotations` 接受任何 `IEnumerable<ChartAnnotation>`。指派該屬性時標記會重繪，因此清單變動後應重新指派（新清單或同一集合）以觸發重繪。
- `ItemsSource` 是整體取代而不是附加。序列重新載入時，應指派一個新的 `IReadOnlyList<DailyUsage>`。
- `XBinding` 與 `YBinding` 讀取日期和數值。編譯形式 `{CompiledBinding Date, DataType=...}` 是對 AOT 安全的路徑；使用它時需在檢視上加入 `x:DataType`。範例為求簡潔而省略了它。

### 範例：帳單頁上的 API 用量

帳單頁向客戶顯示其在目前週期內發出的 API 呼叫。讀者需要一眼看清週期的形態，跳到最近 30 天，並在導致峰值的發布旁邊讀取該峰值。兩個預設按鈕呼叫檢視模型的命令，這些命令會寫入 `Window`；圖表讀寫的是同一個屬性，因此預設與拖曳始終保持一致。發布標記來自產品團隊用於更新日誌的同一份清單，因此標註都是真實事件，而不是裝飾。此處不顯示截圖：只有檢視模型提供資料點後，圖表才會繪製資料。

```xml
<StackPanel Width="720" Spacing="12">
    <StackPanel Orientation="Horizontal" Spacing="8">
        <arc:Button Content="最近 30 天" Variant="Secondary" Size="Small" Command="{Binding ShowLast30DaysCommand}" />
        <arc:Button Content="全部" Variant="Secondary" Size="Small" Command="{Binding ShowAllCommand}" />
    </StackPanel>
    <arc:BrushChart Label="每日 API 呼叫次數"
                    Unit="次"
                    ValueFormat="N0"
                    ItemsSource="{Binding Usage}"
                    XBinding="{Binding Date}"
                    YBinding="{Binding Calls}"
                    Range="{Binding Window, Mode=TwoWay}"
                    Annotations="{Binding Releases}" />
</StackPanel>
```

### 資料結構與效能

控制項要求每個日期只有一個數值。它透過 `XBinding`（或 `XSelector`）讀取每個資料項目的日期，透過 `YBinding`（或 `YSelector`）讀取其數值。日期無法轉換的資料項目會被略過。

資料變更時，控制項會對資料點排序，並對整個序列一次計算七點平均值及其範圍帶，然後重建概覽條。每次資料變更都是對序列的一次完整走訪，因此應透過一次指派取代整個集合，而不是逐一加入資料點。繪圖區只繪製視窗內的資料點，以及兩側各多出的一個。當超過兩個資料點落在同一像素欄中時，折線只保留該欄的最低與最高值，因此峰值得以保留，路徑也保持精簡。預期範圍是一年的每日資料或數千個資料點。

資料變更時，目前視窗會被保留，並被限制在新資料和最小跨度之內。繫結的 `Range` 優先於目前視窗，因此應把 `Range` 設為想要的時段，重新載入後會跟隨它。數值軸會針對視圖內的資料點重新計算，並以彈簧動畫調整到新的頂端。

### 文案規範

以序列計量的內容及其頻率為它命名，因為讀者會先看到標籤，然後才看到座標軸。單位應保持簡短，事件細節則放在標註描述中，而不要放在標籤裡。

| 不要寫 | 改為 |
|---|---|
| Chart | Daily active users |
| Users | Active users per day |
| Release | v2 launch |
| Click the chart to zoom | Drag the window to zoom |
| Long description of the release in the label | Short label, with the detail in the description |
| 12000.0 calls | 12,000 calls (`ValueFormat="N0"`) |

視窗文字和說明文字保持句首大寫，日期使用頁面其餘部分所用的格式，例如 "Oct 1, 2026"。不要把視窗讀數放在標題中：圖表下方的說明文字已經足夠。

### 最佳做法

**建議**

- 將 `Range` 設為頁面打開時所顯示的時段。超過每像素兩個資料點後，折線只保留每個像素欄的最低與最高值，因此需要查看個別資料點的讀者應使用更短的視窗；完整序列只作為後備。
- 將視窗預設放在圖表旁邊，並把 `Range` 設為雙向繫結，使預設與拖曳保持同步。
- 將 `MinSpan` 設為檢討所需的最短時段。每週檢討需要 14 天，這樣視窗就不會隱藏每週的模式。
- 加入能夠解釋序列走勢的標註。每個標註都是讀者無需閱讀表格即可找到的標記。
- 為圖表設定 `Label` 和 `Unit`，使螢幕閱讀器使用者能正確聽到摘要和提示。

**避免**

- 負值，或數值軸需要從非零值開始。刷選圖表始終從零開始，因此帶正負號的序列會看起來像貼近底部的一條平線。
- 兩週或更短的序列。資料點過少會使概覽條難以閱讀，視窗也沒有意義；應使用 `LineChart`。
- 同一天有許多重疊的標註。發生衝突的標籤會被略過，讀者會錯過本應看到的事件。
- 從計時器或每個畫格都在變化的值中寫入 `Range`。每次寫入都會讓視窗滑動，因此繪圖區永遠無法穩定。
- 為一個很小的更新重新載入整個序列。每次資料變更都會重建平均值與概覽條，因此應把變更合併為一次指派。

### 常見問題

**設定 `Range` 時，為什麼視窗會貼齊到七天？** `MinSpan` 預設為七天。比最小跨度更窄的視窗會擴展到該跨度，並移動終點以維持跨度。如需允許更短的跨度，請把 `MinSpan` 設為你希望允許的最小值。

**從程式碼設定 `Range` 會有動畫嗎？** 在完整動態效果下會以彈簧動畫滑動；在減少或關閉動態效果下則直接跳轉。拖曳和按鍵始終與指標一對一跟隨，因此只有程式碼修改會使用彈簧。

**數值軸從哪裡開始？** 始終從零開始。其頂端會以彈簧動畫調整到可見範圍內的最高點，因此縮放到一段平緩的區間時，繪圖區會被充分填滿。若要更改刻度標籤而非刻度範圍，請使用 `TickFormatter`。

**為什麼只有一個資料點時圖表顯示「尚無資料」？** 刷選圖表至少需要兩個資料點才能繪製折線和概覽條。`EmptyLabel` 可取代預設文字，該圖表的預設文字為「尚無資料」。

**如何在自己的文字中顯示視窗？** 處理 `RangeChanged` 並讀取 `e.Range`，其中的 `Start` 與 `End` 都是 `DateTimeOffset` 值。像上面的範例那樣，使用你自己的格式模式進行格式化。同一視窗也可透過 `Range` 取得。

**如何修改提示中的日期格式？** 將 `DateFormatter` 設為 `Func<DateTimeOffset, string>`。它會格式化提示和播報；視窗文字仍保持較短的形式。

## API 參考

<!-- api: BrushChart -->

### 配套型別

| 型別 | 用途 |
|---|---|
| `ChartAnnotation(DateTimeOffset Date, string Label, string? Description = null)` | 時間軸上的一個事件，以帶標籤和提示說明的標記顯示 |
| `ChartTimeRange(DateTimeOffset Start, DateTimeOffset End)` | 可見視窗；`Span` 傳回其長度 |
| `ChartTimeRangeChangedEventArgs` | `RangeChanged` 的酬載；其 `Range` 屬性即為新視窗 |

## 無障礙

- `Label` 為圖表命名。視窗及其兩個控點為滑桿，其數值文字即為日期。
- 繪圖區是一個可聚焦的群組。其自動化名稱由標籤、視窗讀數以及一則提示組成，提示說明可使用左右方向鍵讀取數值。
- 對某個資料點的讀取會播報日期、帶單位的數值以及當天的任何事件，例如 "Sun, Jan 11, 2026, 110 users. Launch"。
- 一張隱藏的資料表列出視窗內的每個資料點及其事件。事件以第二種資料系列顏色標記，並附帶文字標籤或提示，因此不會僅靠顏色來區分。
- 視窗及其控點的焦點環僅在鍵盤焦點時繪製。

### 鍵盤

概覽條共有四個鍵盤停駐點，順序為：繪圖區、視窗、起點控點和終點控點。Tab 與 Shift+Tab 在它們之間移動。

| 按鍵 | 操作 |
|---|---|
| 繪圖區：`Left` / `Right` | 將十字線向前或向後移動一個資料點；第一次 `Right` 讀取可見的最後一個資料點 |
| 繪圖區：`PageUp` / `PageDown` | 將十字線移動可見資料點的八分之一 |
| 繪圖區：`Home` / `End` | 將十字線移到第一個或最後一個可見資料點 |
| 繪圖區：`Escape` | 清除十字線 |
| 視窗：`Left` / `Down`、`Right` / `Up` | 向前或向後平移視窗的十分之一 |
| 視窗：`Shift` + 方向鍵、`PageUp` / `PageDown` | 平移視窗的一半 |
| 視窗：`+` / `-` | 以中心為基準放大或縮小 |
| 視窗：`Home` / `End` | 將視窗移到序列的開頭或結尾 |
| 起點或終點控點：`Left` / `Right` | 將邊緣移動一個資料點（在每日序列中為一天） |
| 起點或終點控點：`Shift` + 方向鍵 | 將邊緣移動十個資料點 |
| 起點控點：`Home` / `End` | 將起點移到第一個資料點，或移到終點之前的最小跨度處 |
| 終點控點：`Home` / `End` | 將終點移到起點之後的最小跨度處，或移到最後一個資料點 |
| 視窗或控點：`Escape` / `0` | 將視窗重設為完整序列 |

## 動態效果

- 首次顯示時，折線從左側繪製出來一次，使用較長且無回彈的彈簧。
- 拖曳時視窗在各動態效果級別下都與指標一對一跟隨。預設、重設以及 `Range` 的變更，在完整動態效果下以彈簧滑動視窗，在減少或關閉動態效果下則直接跳轉。
- 完整動態效果下，數值軸以彈簧調整到可見範圍內的最高點。隨著密度變化，原始折線與平滑趨勢線交叉淡化。
- 完整動態效果下，十字線在資料點之間平滑滑動。
- 減少動態效果時視窗立即到位，十字線保持原處。關閉動態效果時直接到達最終狀態。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 首次顯示 | 折線從左側繪製一次，使用較長的彈簧 | 直接顯示完成的折線 | 直接顯示完成的折線 |
| 拖曳視窗或控點 | 與指標一對一跟隨 | 與指標一對一跟隨 | 與指標一對一跟隨 |
| 預設、重設或新的 `Range` | 視窗以彈簧滑動 | 視窗立即移動 | 視窗立即移動 |
| 數值軸頂端 | 以彈簧調整到可見範圍內的最高點 | 立即移動 | 立即移動 |
| 十字線 | 在資料點之間滑動 | 立即移動，保持原處 | 跳到資料點 |

## 相關元件

- `LineChart`：幾週的資料、多個資料系列或帶正負號的數值軸。
- `Sparkline`：無互動的小型趨勢。
- `Streamgraph`：多個圖層隨時間的組成。
- `AnimatedCounter`：變動中的單一總數。

| 元件 | 適用情境 |
|---|---|
| `LineChart` | 時段較短、需要比較多個資料系列，或數值可能為負 |
| `Sparkline` | 趨勢位於表格儲存格或 KPI 卡片中，且無需互動 |
| `Streamgraph` | 問題是整體各部分如何隨時間變化 |
| `AnimatedCounter` | 一個總數在變化，數字本身就是重點 |
| `BarChart` | 依時段比較數值，而不是沿連續時間軸閱讀 |
