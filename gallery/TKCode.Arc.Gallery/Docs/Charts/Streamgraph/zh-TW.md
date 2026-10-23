---
title: 串流圖
summary: 在擺動基線上堆疊的串流層，可在不同範圍之間變形；可單獨隔離某一層並逐週讀取。
source-hash: ba83187c6c3c
---

<Demo name="TopicsOverTime" />

## 何時使用

`Streamgraph` 呈現總量隨時間變化時，由哪些部分組成。每個部分是一層，各層沿共用的時間軸匯成一條帶狀的串流，讀者一眼就能同時看見總量的大小與每個部分所佔的份額。它適合回答「組成比例何時、如何改變？」的畫面，而不適合回答「每個部分的精確數值是多少？」。

- **營運檢討中依主題劃分的支援工單**：每週的工單量拆分為帳務、登入、效能、整合與行動版。發布之後，效能層膨脹，讀者既能看見組成的改變，也能看見總量的上升。
- **行銷儀表板中依獲客管道劃分的流量**：26 週內每天來自搜尋、社群、電子郵件、推薦與直接造訪的造訪量。串流能看出哪個管道帶動了某次活動，以及其他管道如何收縮讓出空間。
- **SaaS 財務檢討中依方案等級劃分的營收**：來自入門版、成長版與企業版的月度經常性收入。讀者能看見組成變化，例如一波升級潮，而不必比較五條獨立的折線。
- **可靠性報告中一季內的事故類別**：每週每個類別的事故數。季節性與高峰以流動的層呈現，比獨立的折線更清楚。
- **電商規劃視圖中依商品類別劃分的季節性需求**：每週每個類別的售出件數，帶狀的形狀顯示各類別何時達到高峰。

## 何時不用

- **精確數值或層與層之間的比較**，例如「三月的帳務成長是否快於登入？」：用 `LineChart`，它為每個系列提供獨立的基線與座標軸。
- **少量期間或離散類別**，例如四個季度或六個區域：用 `BarChart`，每個值是一根獨立的長條，讀者可以比較它們的高度。
- **包含負值的資料**，例如淨流量或各業務線的損益：各層從基線堆疊，串流便不再代表總量的組成部分。請用 `LineChart`。
- **某一時刻一個總量的組成部分**，例如今天各區域的營收占比：用 `DonutChart`。串流需要時間軸才有存在的價值。
- **許多都很小的層**：超過八層時，串流會變成一堆細條。請把最小的幾層合併為「其他」系列，或在每個系列都重要時改用 `LineChart`。
- **比較多個組的分布，而非它們隨時間的總量**，例如每個服務回應時間的離散情況：用 `Ridgeline`。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:Streamgraph Label="各主題的支援工單"
                     Unit="工單"
                     CategoryLabel="週"
                     ItemsSource="{Binding Weeks}">
        <arc:Streamgraph.Series>
            <arc:ChartSeries Key="billing" Name="帳務" Values="{Binding Billing}" />
            <arc:ChartSeries Key="login" Name="登入" Values="{Binding Login}" />
            <arc:ChartSeries Key="performance" Name="效能" Values="{Binding Performance}" />
        </arc:Streamgraph.Series>
    </arc:Streamgraph>
</UserControl>
```

每個 `ChartSeries` 是一層。`ItemsSource` 保存共用的 x 位置，例如每週的起始日期，每一層的 `Values` 在每個位置上都有一個數字。懸停或聚焦某一層即可將其隔離。

`Streamgraph` 派生自 `ChartBase`，即 Arc 自繪圖表的基底類別，與 `LineChart`、`BarChart` 共用資料管線、色盤、首次顯示動畫、讀數與無障礙摘要。串流在此之上加入堆疊、層隔離與直接標籤。由於各層是堆疊的，每一層的高度就是它的數值，而串流在每一週的總高度就是可見各層的總和。

### 結構

1. **圖例**（`ShowLegend`）：每層一個方形色塊與名稱，位於繪圖區上方，與繪圖區相隔 16 個單位。圖例向兩側各延伸 8 個單位進入邊界，使其懸停區域與繪圖區邊緣對齊。有超過一層時預設顯示。
2. **繪圖區**（`PlotHeight`，預設 260）：堆疊的各層，每層以其系列顏色填滿。相鄰的層之間以 2 個單位的表面顏色間隙分隔，因此無需外框也能看清邊界。
3. **直接標籤**（`ShowDirectLabels`，預設開啟）：層名稱顯示在該層最粗的位置，並帶有表面顏色的光暈，使文字在任何一層上都清晰可讀。只有當該處的層厚度至少為 19 個單位，且文字能放入繪圖區時才顯示標籤。
4. **十字準線**：位於目前週的 1 px 垂直線，使用前景色，不透明度 50%。
5. **提示**：本週的標題，以及每個可見層一列，依堆疊順序排列。被隔離層的那一列會被強調。
6. **時間軸**（`ShowXAxis`）：繪圖區下方 26 個單位高的一列，顯示分類標籤。範圍較長時，顯示的標籤較少。

### 層與系列

每一層是一個帶有 `Key`、`Name` 與 `Values` 的 `ChartSeries`。`Key` 在 `HiddenSeries`、`ActiveSeriesKey` 以及 `HiddenSeriesChanged`、`ActiveIndexChanged` 事件中標識該層，因此在資料重新整理時應保持穩定。`Name` 會出現在圖例、直接標籤、提示與播報中。

依讀者應當看見的內容排列各層。堆疊時，第一層位於串流的中央，其餘各層交替排在它的上方與下方，因此最大或最重要的層通常適合作為第一層。在共用圖例的多個畫面上保持相同的順序，使同一主題每次都在相同的位置。

### 偏移

`Offset` 決定各層圍繞基線的堆疊方式。預設值為 `Wiggle`。

| 值 | 堆疊方式 | 適用情境 |
|---|---|---|
| `Wiggle`（預設） | 使各層的斜率最小化，串流流動且保持水平 | 故事在於流動：各層一起起伏，讀者應跟隨這條帶狀 |
| `Silhouette` | 將整個堆疊圍繞一條平直的中線置中 | 總量的起伏是重點，各層是圍繞它的細節 |
| `Zero` | 從底部平直的基線依序堆疊各層 | 讀者需要穩定的基線，以比較最底層與其上各層 |

```xml
<arc:Streamgraph Label="主題" Offset="Silhouette" ItemsSource="{Binding Weeks}" />
```

改變 `Offset` 只會重新堆疊各層，不需要新資料。每個畫面只使用一種偏移，使讀者在兩個圖表之間切換時，同一層不會換位置。

### 範圍切換

範圍改變時，同時替換 `ItemsSource` 與各系列的數值。每一層都會從畫面上的目前形狀融合到新形狀；不同長度的範圍同樣會變形過渡，因為在融合之前，每一層都會被重新取樣到同一個網格上。

```csharp
void ShowRange(Streamgraph chart, IReadOnlyList<DateOnly> weeks, double[][] valuesByLayer)
{
    chart.ItemsSource = weeks;
    for (var i = 0; i < chart.Series.Count; i++)
    {
        chart.Series[i].Values = valuesByLayer[i];
    }
}
```

被隱藏的層會收縮至消失，其餘層會圍繞它合攏；讀者再次顯示該層時，它以相同的方式回來。

### 圖例、直接標籤與隱藏層

圖例與直接標籤用於命名各層。空間足夠時，直接標籤繪製在每一層最粗的部分，因此很細的層可能沒有標籤，但它的圖例項目仍會標明它。圖例已足夠、而繪圖區又很擁擠時，請關閉 `ShowDirectLabels`。

`HiddenSeries` 為雙向屬性：圖例會切換它，檢視模型也可以保存被隱藏的鍵。被隱藏的層會從堆疊、提示、播報與總量中移除，其餘各層圍繞它重新堆疊。

```xml
<arc:Streamgraph Label="主題"
                 ShowLegend="True"
                 ShowDirectLabels="True"
                 HiddenSeries="{Binding HiddenKeys}"
                 ItemsSource="{Binding Weeks}" />
```

### 讀取某一週

`ActiveIndexChanged` 回報指標或鍵盤所在的那一週。它的 `Index` 與 `Label` 標識該週，`SeriesKey` 指明被隔離的層，`Values` 列出該週每一層的數值。`ActiveSeriesKey` 隨時回傳被隔離的層；未隔離任何層，或被隔離的層已被隱藏時，回傳 `null`。

```csharp
chart.ActiveIndexChanged += (_, e) => readout.Text = e.Label ?? string.Empty;
```

### 繫結到檢視模型

在 MVVM 應用程式中，檢視模型擁有各週、各層的數值與被隱藏的鍵。使用 CommunityToolkit.Mvvm 時，檢視模型是 `ObservableObject`，每次範圍改變對應一個命令。

```csharp
public sealed record TicketReport(
    IReadOnlyList<DateOnly> Weeks,
    double[] Billing,
    double[] Login,
    double[] Performance);

public interface ITicketApi
{
    Task<TicketReport> GetTicketsByTopicAsync(int weeks);
}

public partial class TicketTrendViewModel : ObservableObject
{
    private readonly ITicketApi _api;

    public TicketTrendViewModel(ITicketApi api) => _api = api;

    [ObservableProperty]
    private IReadOnlyList<DateOnly> _weeks = [];

    [ObservableProperty]
    private double[] _billing = [];

    [ObservableProperty]
    private double[] _login = [];

    [ObservableProperty]
    private double[] _performance = [];

    [ObservableProperty]
    private IReadOnlyList<string> _hiddenKeys = [];

    [RelayCommand]
    private async Task LoadRangeAsync(int weeks)
    {
        // 各週的起始日期與各層的數值來自同一份報告，因此圖表一次接收它們的更新。
        var report = await _api.GetTicketsByTopicAsync(weeks);
        Weeks = report.Weeks;
        Billing = report.Billing;
        Login = report.Login;
        Performance = report.Performance;
    }
}
```

```xml
<arc:Streamgraph Label="依主題統計的支援工單"
                 Unit="工單"
                 CategoryLabel="週"
                 ItemsSource="{Binding Weeks}"
                 HiddenSeries="{Binding HiddenKeys, Mode=TwoWay}">
    <arc:Streamgraph.Series>
        <arc:ChartSeries Key="billing" Name="帳務" Values="{Binding Billing}" />
        <arc:ChartSeries Key="login" Name="登入" Values="{Binding Login}" />
        <arc:ChartSeries Key="performance" Name="效能" Values="{Binding Performance}" />
    </arc:Streamgraph.Series>
</arc:Streamgraph>
```

`HiddenSeries` 是雙向的，因此檢視模型能看見讀者隱藏了哪些層，並能在重新整理後保持它們隱藏。`ItemsSource` 與各層的數值是單向的。`HiddenSeriesChanged` 事件在其 `Hidden` 屬性中回報新的隱藏清單，供偏好事件的程式碼使用。`ActiveSeriesKey` 為唯讀；若要回應讀者的移動，請使用 `ActiveIndexChanged`。

### 資料結構與效能

資料改變時，每一層都會被重新取樣到固定的 161 個點的網格上；之後每一影格都會根據這些點重新計算堆疊、邊緣與標籤。因此成本更多取決於層數，而不是週數。請將層數控制在三到八層，並保持數值非負。

- **週數**：每週是一個類別。無論週數多少，曲線都會被重新取樣到固定網格上，時間軸會挑選不重疊的標籤。
- **各層的數值**：每一層在每週都需要一個數值，順序與 `ItemsSource` 相同。某週沒有活動時傳入 0，使各層在相同的週上對齊。
- **更新頻率**：每天或每週重新整理一次，讀起來像是範圍的改變。每隔幾秒更新一次的串流會讓各層持續移動，使隔離難以使用。
- **格式化**：數值由 `ValueFormat` 格式化（.NET 格式字串，en-US），`Unit` 則出現在提示與播報中的數值之後，例如「4 tickets」。若產品所用的日期格式不同於預設的短日期，請用 `XAxisFormatter` 格式化座標軸日期。

### 範例：發布後的工單組成

支援負責人檢視過去 26 週的工單。串流顯示：發布之後的幾週，效能層成長，而帳務層維持平穩。圖例保持可見，使每一層都可以被隱藏或隔離；直接標籤為較粗的層命名，時間軸則標示各週。讀者無需重新查詢，就能隱藏行動版層，查看其餘四層的組成。

```xml
<arc:Streamgraph Label="依主題統計的支援工單，26 週"
                 Unit="工單"
                 CategoryLabel="週"
                 PlotHeight="240"
                 ShowLegend="True"
                 ShowDirectLabels="True"
                 Offset="Wiggle"
                 ItemsSource="{Binding Weeks}">
    <arc:Streamgraph.Series>
        <arc:ChartSeries Key="billing" Name="帳務" Values="{Binding Billing}" />
        <arc:ChartSeries Key="login" Name="登入" Values="{Binding Login}" />
        <arc:ChartSeries Key="performance" Name="效能" Values="{Binding Performance}" />
        <arc:ChartSeries Key="integrations" Name="整合" Values="{Binding Integrations}" />
        <arc:ChartSeries Key="mobile" Name="行動版" Values="{Binding Mobile}" />
    </arc:Streamgraph.Series>
</arc:Streamgraph>
```

鍵是固定的字串，因此資料重新整理時，被隱藏的層會保持隱藏。`Name` 的值是讀者在圖例與提示中看見的文字，因此它是為人撰寫的，而不是為資料模型撰寫的。

### 文案規範

以層所計數的事物為各層命名，採句子式大小寫，並在 `Unit` 中寫明單位。圖例若寫著「帳務、登入、效能」，讀者無需對照說明，就能掃讀整條串流。

| 不要寫 | 改為 |
|---|---|
| `Label="Chart 1"` | `Label="依主題統計的支援工單"` |
| `Name="billing_v2"` | `Name="帳務"` |
| `Unit="count"` | `Unit="工單"` |
| `CategoryLabel="x"` | `CategoryLabel="週"` |
| `Name="Other (misc.)"` | `Name="其他"`，並把最小的類別併入其中 |
| `Name="ALL CAPS TOPIC"` | `Name="登入"` |

### 最佳做法

**建議**

- 保持各層的鍵穩定。在 `HiddenSeries` 與事件中，層是由鍵而非名稱標識的；鍵一旦改名，讀者隱藏過的層就會被重設。
- 在傳入資料之前，把最小的類別併入「其他」。太細、放不下直接標籤的層很難隔離，此時圖例與提示是它唯一的識別方式。
- 每個畫面只使用一種偏移。改變偏移會重新堆疊各層，使讀者難以在兩個圖表之間跟隨同一層。
- 範圍改變時，同時替換各週的起始日期與每一層的數值，使各層從畫面上的形狀變形為新形狀。
- 在 `Unit` 中寫明單位，使提示與播報讀作「4 tickets」，而不是一個光禿禿的數字。

**避免**

- 用於包含負值的資料。基線以下的堆疊沒有意義；請改用 `LineChart`。
- 超過八層。細層會變成讀者無法隔離的細條；請合併它們。
- 用於層與層之間的精確比較。堆疊的層是靠其帶狀來閱讀的，精確數值只在提示與播報中。
- 在小型繪圖區中放入許多層的直接標籤。細層上的標籤會被隱藏，因此圖例才是可靠的對照依據。
- 每隔幾秒重新整理一次資料。每次重新整理都會移動所有層，串流對讀者而言永遠不會穩定。

### 常見問題

**如何從程式碼隔離某一層？** 圖表沒有用於設定被隔離層的屬性。指標、鍵盤與圖例預覽會設定它，而 `ActiveSeriesKey` 用於讀回。若要驅動詳情面板，請在 `ActiveIndexChanged` 中讀取 `ActiveSeriesKey`，或在需要目前層時讀取。

**為什麼隱藏某一層後，一層消失而其他層會移動？** 被隱藏的層會從堆疊中移除，因此其餘各層會填滿它的空間並重新堆疊。它的圖例項目仍然保留，鍵也保存在 `HiddenSeries` 中，直到讀者再次顯示它。

**播報會說什麼？** 它會說出該週、被隔離層及其數值、可見層的數量，以及可見層的總量，例如「W5. Login 4 tickets, 3 layers, 30 tickets in total」。沒有被隔離的層時，只說出該週與總量。請設定 `Unit` 與 `Label`，使播報有意義。

**為什麼某一層沒有直接標籤？** 只有當該處的層厚度至少為 19 個單位，且文字能放入繪圖區時，才會繪製直接標籤。很細或很窄的層會保留它的圖例項目與提示列，當層變厚後標籤會重新出現。

**可以把 x 軸用於日期嗎？** 可以。`ItemsSource` 接受 `DateTime`、`DateTimeOffset` 或 `DateOnly`，分類標籤遵循預設的日期格式。將 `CategoryLabel` 設為表頭應使用的詞，例如「週」。

## API 參考

<!-- api: Streamgraph -->

## 無障礙

- `Label` 為圖表命名。繪圖區是一個以標籤與鍵盤操作說明命名的可聚焦群組，例如「依主題統計的支援工單。使用左右方向鍵沿時間移動，上下方向鍵切換層。」
- 每次讀取都會更新數值文字，並以禮貌播報（polite）的方式播報該週、被隔離層的數值與可見層的總量。使用螢幕閱讀器的讀者聽到的數字，與提示所顯示的一致。
- 圖例項目是切換開關。聚焦某個項目會隔離其所屬的層，按下 `Enter` 則顯示或隱藏該層。
- 隱藏的資料表列出每一週的每一層。
- 各層直接在繪圖區與圖例中標示名稱，因此不會只靠明暗來區分。相鄰的層之間以 2 個單位的表面顏色間隙分隔。
- 使用指標時，移到某一層上即會隔離它，離開繪圖區則清除讀數。使用觸控時，讀數停留在被觸碰的位置，直到接觸結束。

### 鍵盤

繪圖區可透過 `Tab` 取得焦點。焦點從鍵盤進入時，讀數從最新一週開始。

| 按鍵 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 在圖例項目與繪圖區之間移動焦點 |
| `Left` / `Right` | 移到上一週或下一週。靜止時，`Right` 停在最新一週 |
| `Up` / `Down` | 依系列順序隔離目前週中的下一個或上一個可見層。未隔離任何層時，`Up` 選取最後一個可見層，`Down` 選取第一個 |
| `PageUp` / `PageDown` | 向前或向後移動約六分之一的週 |
| `Home` / `End` | 移到第一週或最後一週 |
| `Escape` | 清除讀數與被隔離的層 |
| `Enter`（在圖例項目上） | 顯示或隱藏該層 |

## 動態效果

- 首次顯示時，串流從一根細線開始，沿中心線向外膨脹，使用不過衝的長彈簧。
- 新範圍或新資料集會在共用網格上融合每一層，因此各層保持其順序與速度。
- 被隱藏的層會收縮至消失，其餘層圍繞它重新堆疊。
- 十字準線與提示在彈簧上滑動；隔離某一層時，其餘層會變暗。
- 減少動態效果時立即顯示最終狀態，十字準線與提示則跳到新的一週，不使用彈簧。關閉動態效果時直接到達最終狀態。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 首次顯示 | 串流沿中心線以長彈簧膨脹，不過衝 | 直接顯示最終形狀 | 直接顯示最終形狀 |
| 新範圍或新資料集 | 各層以平滑彈簧融合為新形狀；新的層從無到有地長出 | 各層直接跳到新形狀 | 立即 |
| 隱藏某一層 | 該層收縮至消失，其餘層圍繞它重新堆疊 | 直接跳到重新堆疊後的形狀 | 立即 |
| 隔離某一層 | 其餘層以短促的標準補間淡出 | 同樣的短補間 | 立即 |
| 十字準線與提示 | 以彈簧滑動到新的一週 | 跳到新的一週，不使用彈簧 | 立即 |

## 相關元件

- `LineChart` 用於需要精確比較各系列數值的場合，`BarChart` 用於少量離散期間。
- `Ridgeline` 用於比較多個組的分布，而非它們隨時間的總量。
- `DonutChart` 用於顯示一個總量中各部分的占比。

| 元件 | 適用情境 |
|---|---|
| `LineChart` | 讀者必須比較精確數值，或資料含有負值 |
| `BarChart` | 有少量離散期間或類別需要比較 |
| `Ridgeline` | 比較多個組的分布，而不是它們隨時間的總量 |
| `DonutChart` | 在某一時刻顯示一個總量中各部分的占比 |
| `Sparkline` | 在單一總量的標題數值旁顯示它的趨勢 |
