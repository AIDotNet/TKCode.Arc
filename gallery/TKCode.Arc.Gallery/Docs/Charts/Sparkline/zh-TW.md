---
title: 迷你趨勢圖
summary: 在數值旁顯示精簡的趨勢。
source-hash: de23d529fe6e
---

<Demo name="KpiCards" />

## 何時使用

`Sparkline` 是一條小型趨勢線，帶有主數值與附註，尺寸適合放在卡片、表格儲存格或 KPI 區塊中。它在數字所佔的空間裡回答「這個數字正往哪裡走？」，讓讀者不必看座標軸與圖例，就能在目前數值旁看出指標的走向與形狀。

- **分析總覽中的營收與用量卡片**：卡片顯示 `$48.2K` 與 `+4.1%`，下方是最近 14 天的營收。形狀能看出數字是穩定上升，還是某一天突然飆升。
- **維運主控台中的服務列**：表格的每一列是一項服務，顯示延遲或錯誤率趨勢。同樣大小的多列讓表格易於掃視：一眼就能找到曲線走向不對的那項服務。
- **CRM 或客服工具中的流失、佇列與積壓卡片**：一個數值加上走向與色調，例如佇列深度上升時以警告色顯示。色調告訴讀者這個趨勢是好是壞。
- **帳務與用量摘要**：每位客戶的席位數或每日 API 呼叫量，旁邊是目前總數。讀者需要的是近期的規律，而不是精確的每個資料點。
- **在詳情面板中回看近期歷史**：讀者沿著線條移動指標或方向鍵，即可讀取較早某個資料點的數值與日期，無需開啟完整圖表。

## 何時不用

- **需要並排比較的離散期間**，例如十二個月的營收或每個工作日的訂單數：用 `BarChart`，它有數值軸，每個期間一根柱子。
- **只有目前數值及其變化、沒有歷史**：用 `MetricCard`。只有一個資料點的迷你趨勢圖會畫成一條平線，無法說明走向。
- **需要座標軸、多個系列或圖例的趨勢**，例如一季內各產品線的營收：用 `LineChart`。
- **讀者必須縮放或平移的超長序列**，例如一年的分鐘級指標：用 `BrushChart`，它在完整範圍上保留一個視窗，讓讀者選取一段時間。
- **相對已知範圍的數值**，例如 500 GB 配額中的磁碟使用量：用 `Gauge`，它顯示數值在刻度上的位置，而不是它的變化路徑。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:Sparkline Label="營收"
                   Value="$48.2K"
                   Change="+4.1%"
                   Tone="Success"
                   Width="220"
                   ItemsSource="{Binding Revenue}" />
</UserControl>
```

`ItemsSource` 依由舊到新的順序保存數值，每個值對應一個資料點。`Value` 與 `Change` 構成靜止時顯示的主數值與附註。`Label` 為指標命名。

`Sparkline` 派生自 `ChartBase`，即 Arc 自繪圖表的基底類別。此基底提供資料管線（物件項目使用 `ItemsSource` 搭配 `YBinding` 或 `YSelector`，也可直接使用一般數字）、色盤、首次顯示動畫、目前資料點讀數（`ActiveIndex`）、數值格式化以及無障礙摘要。迷你趨勢圖在此之上加入主數值與附註、拖曳查看手勢，以及沒有座標軸的繪圖區。由於它與 `LineChart`、`BarChart` 共用基底，同一個畫面中的資料與格式化規則完全一致。

### 結構

1. **標籤**（`Label`）：指標名稱，14 px，次要文字顏色，與主數值的基線對齊。它也是無障礙名稱的開頭。
2. **主數值**（`Value`）：18 px，中等字重，前景色，在其位置中靠右對齊。`Value` 為 null 時隱藏。
3. **附註**（`Change`）：12 px，中等字重。靜止時以色調顏色顯示 `Change`（`Accent` 使用強調色）。拖曳時顯示資料點（見下文「主數值、附註與拖曳查看」）。
4. **標題間距**：標題列與繪圖區之間 12 個單位的間距。
5. **繪圖區**：線條、填色、1 px 的基線（邊框色）以及拖曳游標。線條寬 2.5 個單位，端點為圓角。
6. **面積**（`ShowArea`）：線條下方 9% 的色調文字顏色薄層。它繪製在線條之下，可以關閉。
7. **終點圓點**：一個位於最新資料點上的色調圓點，靜止時直徑 7 個單位，拖曳某個資料點時為 10。首次顯示動畫結束前隱藏。
8. **焦點環**：繪圖區取得鍵盤焦點時，Arc 以焦點色繪製焦點環。視窗預設的焦點裝飾會被隱藏。

### 主數值、附註與拖曳查看

附註區域有兩種狀態。靜止時顯示 `Value` 與 `Change`；拖曳某個資料點時，改為顯示該資料點，主數值與附註捲動為新的文字。

| 狀態 | 主數值 | 附註 |
|---|---|---|
| 靜止，已設定 `Value` 與 `Change` | `Value` | `Change`，使用色調顏色 |
| 拖曳，已設定 `Value` 與 `PointLabels` | 拖曳到的數值，依 `ValueFormat` 格式化 | 資料點標籤，例如 `Sep 14` |
| 拖曳，已設定 `Value`，未設定 `PointLabels` | 拖曳到的數值 | 位置，例如 `4 of 14` |
| 拖曳，`Value` 為 null | 隱藏 | 拖曳到的數值與資料點標籤，以圓點分隔 |
| 靜止，`Value` 為 null | 隱藏 | `Change`；`Change` 為空時不顯示 |

拖曳時，附註改用次要文字顏色，而不是色調顏色。該資料點上並沒有發生變化，日期或位置只是背景資訊，因此保持中性。

### 色調

`Tone` 決定線條、填色、終點圓點，以及靜止時的附註顏色。請依指標的走向與該走向是否理想來選擇，而不是為了裝飾。

| `Tone` | 適用情境 | 附註顏色 |
|---|---|---|
| `Accent`（預設） | 中性趨勢，例如造訪量，或沒有好壞之分的指標 | 強調色 |
| `Success` | 數值上升即為改善的指標：營收、轉換率、可用性 | 成功色 |
| `Warning` | 需要留意但不屬於錯誤的數值：持續增長的佇列、上升的積壓 | 警告色 |
| `Danger` | 正在變差的指標：流失率、錯誤率、超出目標的延遲 | 危險色 |

```xml
<arc:Sparkline Label="流失率" Value="2.4%" Change="+0.3 pts" Tone="Danger" ItemsSource="{Binding Churn}" />
<arc:Sparkline Label="佇列深度" Value="1,204" Change="上升" Tone="Warning" ItemsSource="{Binding Queue}" />
```

同時在 `Change` 中保留正負號。無法分辨顏色的讀者仍能讀到 `+0.3 pts`，黑白列印的讀者也能讀出走向。

### 資料點標籤

`PointLabels` 為每個資料點提供一個標籤，順序與 `ItemsSource` 相同。拖曳到某個資料點時，它的標籤會取代 `Change` 顯示在附註中，也是無障礙數值所朗讀的文字。凡資料點是日期的迷你趨勢圖都應設定它。未設定時，附註顯示位置（例如 `4 of 14`），對讀者毫無意義。

```csharp
var labels = days.Select(d => d.ToString("MMM d", CultureInfo.GetCultureInfo("en-US"))).ToArray();
revenue.PointLabels = labels;
```

標籤的數量應與數值的數量一致。沒有標籤的資料點會退回為顯示位置。

### 尺寸與比例

`DrawingWidth` 與 `DrawingHeight` 以 Arc 的繪圖單位計，預設值為 160 與 52。它們只決定繪圖區的寬高比。繪圖區寬度隨迷你趨勢圖的寬度變化，因此高度等於寬度乘以 `DrawingHeight` 再除以 `DrawingWidth`。在預設比例下，寬 320 像素的迷你趨勢圖高 104 像素。

| 位置 | 寬度 | `DrawingHeight` | 說明 |
|---|---|---|---|
| KPI 卡片 | 200 到 240 | 52（預設） | 主數值與附註位於線條上方的同一列 |
| 表格旁的圖表面板 | 240 到 320 | 52（預設） | 為 `ShowArea` 與拖曳查看留出空間 |
| 表格列 | 120 到 160 | 32 | 設定 `ShowArea="False"` 與 `IsInteractive="False"` |

在元素上設定 `Width` 以固定其尺寸，讓高度依比例跟隨。不要設定 `Height`：卡片縮放時，正是繪圖比例防止線條被拉伸。

### 高密度表格

在列數很多的表格中關閉拖曳查看。`IsInteractive="False"` 會移除每一列的 Tab 停留點與指標處理，使表格保持單一的焦點順序。當二十列各自帶有一層色塊時，`ShowArea="False"` 能讓線條保持安靜。

<Demo name="DenseTable" />

```xml
<arc:Sparkline Label="api-gateway"
               Width="160"
               DrawingHeight="32"
               ShowArea="False"
               IsInteractive="False"
               ItemsSource="{Binding Gateway}" />
```

列內的標籤要簡短。列本身已經標明了服務名稱，因此迷你趨勢圖的標籤可以重複服務名稱，或只寫指標名稱，例如 `Latency`。

### 沒有主數值

未設定 `Value` 時，主數值隱藏，拖曳到的數值及其資料點標籤顯示在附註中。在數值已顯示於別處、而曲線僅供閱讀的詳情面板中，使用這種形式。

<Demo name="WithoutHeadline" />

```xml
<arc:Sparkline Label="造訪量" Width="300" ItemsSource="{Binding Visits}" />
```

資料變化時，線條會從目前形狀變形為新形狀。同一個圖表保持在畫面上的位置，因此造訪量面板的重新整理會被理解為更新，而不是一個新圖表。

### 繫結到檢視模型

在 MVVM 應用程式中，檢視模型擁有序列、主數值、變化量與標籤，`Sparkline` 透過繫結讀取它們。使用 CommunityToolkit.Mvvm 時，檢視模型是帶有可觀察屬性的 `ObservableObject`，每次重新整理都會指派一個新集合，而不是原地修改。

```csharp
public sealed record DailyRevenue(DateOnly Day, double Amount);

public interface IRevenueApi
{
    Task<IReadOnlyList<DailyRevenue>> GetDailyRevenueAsync(int days);
}

public partial class RevenueCardViewModel : ObservableObject
{
    private readonly IRevenueApi _api;

    public RevenueCardViewModel(IRevenueApi api) => _api = api;

    [ObservableProperty]
    private string? _headline = "$48.2K";

    [ObservableProperty]
    private string? _change = "+4.1%";

    [ObservableProperty]
    private IReadOnlyList<double> _amounts = [];

    [ObservableProperty]
    private IReadOnlyList<string> _pointLabels = [];

    [RelayCommand]
    private async Task RefreshAsync()
    {
        // API 依由舊到新的順序回傳各天的資料。新清單是一次資料變化，
        // 因此每次重新整理時線條只變形一次。
        var days = await _api.GetDailyRevenueAsync(14);
        Amounts = days.Select(d => d.Amount).ToArray();
        PointLabels = days.Select(d => d.Day.ToString("MMM d", CultureInfo.GetCultureInfo("en-US"))).ToArray();
        Headline = days[^1].Amount.ToString("C1", CultureInfo.GetCultureInfo("en-US"));
    }
}
```

```xml
<arc:Sparkline Label="營收，14 天"
               Value="{Binding Headline}"
               Change="{Binding Change}"
               Tone="Success"
               Width="220"
               ItemsSource="{Binding Amounts}"
               PointLabels="{Binding PointLabels}" />
```

`Value`、`Change`、`ItemsSource` 與 `PointLabels` 為單向繫結：檢視模型的值流入迷你趨勢圖。唯一的雙向屬性是 `ActiveIndex`，即讀者正在查看的資料點。當畫面需要知道讀者停在哪個資料點時（例如同步更新圖表旁的詳情列），繫結它。

```xml
<arc:Sparkline Label="營收，14 天"
               ItemsSource="{Binding Amounts}"
               ActiveIndex="{Binding SelectedDayIndex, Mode=TwoWay}" />
```

`ActiveIndex` 靜止時為 `null`，讀者拖曳時為所拖曳資料點的索引。`ActiveIndexChanged` 事件回報同樣的變化，供偏好事件的程式碼使用。讀者放手時，它以 `null` 索引觸發。

### 資料結構與效能

`ItemsSource` 接受數字或物件項目。對於物件項目，設定 `YBinding`（或 `YSelector`）讀取數值，設定 `XBinding`（或 `XSelector`）讀取日期，日期可以是 `DateTime`、`DateTimeOffset` 或 `DateOnly`。搭配 `DataType` 使用 `CompiledBinding`，路徑可在編譯期檢查，並相容提前編譯（AOT）。

- **長度**：使用卡片標題所寫的期間，例如「營收，14 天」對應 14 個每日數值。所繪製的曲線取樣點數固定，因此較長的序列每個實例的繪製成本大致相同；卡片中的讀者無法從長序列獲益，應先彙總。
- **平線或空序列**：一個數值畫成平線。空序列在無障礙摘要中顯示 `EmptyLabel`，且不繪製任何內容。當空卡片需要說明原因時（例如「尚無資料」），設定 `EmptyLabel`。
- **更新頻率**：每次變化都會使線條變形，因此每分鐘重新整理一次讀起來像更新。每秒都在變化的序列會使線條持續移動；應在資料進入控制項之前先彙總。
- **大量實例**：每個迷你趨勢圖各自追蹤其尺寸與首次顯示動畫，因此數百列的表格會建立數百個繪圖。應對長表格分頁，並在密集列中關閉拖曳查看。
- **數字格式**：數值由 `ValueFormat` 格式化，它是 .NET 格式字串，例如 `"N0"` 或 `"0.0'%'"`，預設值為 `"#,##0.###"`。主數值 `Value` 請自行以相同規則格式化，使主數值與拖曳到的數值一致。需要依上下文決定文字時，`ValueFormatter` 會接收數值及其索引。

### 範例：營運總覽

平台團隊的總覽在一列中呈現三條趨勢：營收、p95 延遲與支援佇列。每張卡片靜止時都保留主數值，因此目前數字隨時可讀；色調則依對業務的好壞而定：營收上升為 `Success`，延遲上升為 `Danger`，佇列增長為 `Warning`。由於只有三張卡片，它們保持可互動，讀者可能需要查看某次峰值的日期。每張卡片都有各自的資料點標籤，因此拖曳到的資料點會讀作日期。

檢視模型為每張卡片提供主數值、變化量、數值與標籤，XAML 只負責繫結。

```xml
<WrapPanel ItemSpacing="16" LineSpacing="16">
    <arc:Sparkline Label="營收，14 天"
                   Value="{Binding Revenue.Headline}"
                   Change="{Binding Revenue.Change}"
                   Tone="Success"
                   Width="240"
                   ItemsSource="{Binding Revenue.Amounts}"
                   PointLabels="{Binding Revenue.PointLabels}" />
    <arc:Sparkline Label="p95 延遲，14 天"
                   Value="{Binding Latency.Headline}"
                   Change="{Binding Latency.Change}"
                   Tone="Danger"
                   Width="240"
                   ItemsSource="{Binding Latency.Amounts}"
                   PointLabels="{Binding Latency.PointLabels}" />
    <arc:Sparkline Label="支援佇列，14 天"
                   Value="{Binding Queue.Headline}"
                   Change="{Binding Queue.Change}"
                   Tone="Warning"
                   Width="240"
                   ItemsSource="{Binding Queue.Amounts}"
                   PointLabels="{Binding Queue.PointLabels}" />
</WrapPanel>
```

### 文案規範

`Label` 說明指標及其期間，採句首大寫的句子式寫法。無障礙名稱以它開頭，因此「營收，14 天」比單純的指標名稱更好。主數值與變化量應足夠簡短，使卡片能在一列內容納。

| 不要寫 | 改為 |
|---|---|
| `Label="LATENCY"` | `Label="延遲"` |
| `Label="最近兩週的營收趨勢"` | `Label="營收，14 天"` |
| `Value="48200"` | `Value="$48.2K"` |
| `Value="182 milliseconds"` | `Value="182 ms"` |
| `Value="1204"` | `Value="1,204"` |
| `Change="Up"` | `Change="+4.1%"` |
| `PointLabels` 使用 `2026-09-14` | `PointLabels` 使用 `Sep 14` |
| `Label=""` | 為指標命名，例如 `Churn` |

### 最佳做法

**建議**

- 在 `Label` 中寫明指標及其期間，例如「營收，14 天」。它是螢幕閱讀器最先讀到的內容，期間也告訴讀者這條線涵蓋的範圍。
- 依上升對業務是否有利來選擇 `Tone`。同樣的形狀對營收與延遲意味著不同的事，色調告訴讀者是哪一種。
- 凡資料點為日期的迷你趨勢圖都設定 `PointLabels`，使拖曳到的資料點讀作日期而不是位置。
- 在 `Change` 中保留正負號，並以與 `ValueFormat` 相同的規則格式化 `Value`，使主數值與拖曳到的數值一致。
- 在表格列中關閉 `IsInteractive`，否則過多的 Tab 停留點會讓表格難以走訪。

**避免**

- 為單一數值或只有一個資料點的序列使用迷你趨勢圖。它會畫成平線，無法說明走向；這種情況應使用 `MetricCard`。
- 在長表格的每一列都開啟 `ShowArea`。二十個色塊會與旁邊的文字爭奪注意力。
- 以每秒觸發一次的計時器取代 `ItemsSource`。每次變化都會使線條變形，圖表永遠無法穩定；應把資料彙總到卡片所顯示的期間。
- 把 `Tone` 當成裝飾。沒有走向含義的顏色會讓讀者學會忽略顏色。
- 在元素上設定 `Height`。繪圖比例決定高度，固定高度會在卡片縮放時拉伸線條。

### 常見問題

**拖曳時為什麼主數值消失了？** 只有 `Value` 為 null 時主數值才會隱藏，此時拖曳到的數值及其資料點標籤顯示在附註中。設定了 `Value` 時，主數值顯示拖曳到的數值，附註顯示資料點標籤；讀者離開繪圖區或按下 Escape 後，兩者都會回到 `Value` 與 `Change`。

**如何從程式碼讓讀數停在某個資料點上？** 將 `ActiveIndex` 設為該資料點的索引，例如 `revenue.ActiveIndex = 6;`；設為 `null` 即可回到最新數值。此屬性是雙向的，繫結它的檢視模型也會看到讀者的移動。

**一個數值或沒有數值時，迷你趨勢圖顯示什麼？** 一個數值會在繪圖區中央畫出一條平線。空序列不繪製任何內容，無障礙摘要中顯示 `EmptyLabel`。當空卡片應當說明原因時（例如「尚無資料」），設定 `EmptyLabel`。

**為什麼線條比我預期的短？** 高度來自繪圖比例，而不是固定值。寬度為 240、使用預設 `DrawingWidth` 160 與 `DrawingHeight` 52 時，繪圖區約高 78 像素。若要在相同寬度下讓線條更高或更矮，請修改 `DrawingHeight`。

## API 參考

<!-- api: Sparkline -->

## 無障礙

- `Label` 為圖表命名。無障礙名稱合併了標籤、最新數值與變化量，例如「收入：$48.2K。+4.1%」。繪圖本身對輔助技術隱藏，因此螢幕閱讀器讀取的是名稱與文字。
- 主數值與變化量是純文字，依原樣朗讀。
- 圖表可互動時，繪圖區是一個可聚焦的滑桿，名稱為「`Label`，逐點探索」。其數值文字會讀出資料點標籤與數值，例如「週四：22」；沒有資料點標籤時，則讀出位置與數值，例如「第 2 個資料點，共 3 個：1,500」。
- 設定 `PointLabels`，使拖曳到的資料點以日期朗讀，而不是以它在清單中的位置朗讀。
- Arc 以焦點色繪製繪圖區的焦點環。即使不用視窗預設的焦點裝飾，鍵盤焦點也清晰可見。
- 不可互動的迷你趨勢圖沒有 Tab 停留點，也不回應指標。它的名稱與文字仍可供螢幕閱讀器讀取。
- 色調從來不是唯一的訊號。在 `Change` 中寫明正負號（`+4.1%`、`-6 ms`），這樣即使讀者分辨不出顏色，也能讀懂走向。

### 鍵盤

只有當 `IsInteractive` 為 `True` 時，迷你趨勢圖才會取得焦點。其模型與其他單一序列的 Arc 圖表相同：方向鍵逐個移動資料點，Escape 退出讀數。

| 按鍵 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 將焦點移入或移出繪圖區 |
| `Left` 或 `Down` | 移到上一個資料點。靜止時，第一次按下會落在最新資料點的前一個 |
| `Right` 或 `Up` | 移到下一個資料點。靜止時，按下後仍停在最新資料點 |
| `PageUp` / `PageDown` | 向前或向後移動約六分之一的資料點（至少一個） |
| `Home` / `End` | 移到第一個或最後一個資料點 |
| `Escape` | 停止拖曳，附註恢復為 `Value` 與 `Change` |

使用滑鼠時，在繪圖區上按下或移動，會拖曳到最近的資料點；離開繪圖區後回到最新數值。使用觸控或觸控筆時，讀數停留在被觸碰的資料點上，直到接觸結束，然後回到最新數值。

## 動態效果

- 首次顯示時，線條像用筆畫一樣繪製一次，終點圓點隨線條落定而出現。繪製在迷你趨勢圖露出一半時開始，因此位於摺線以下的卡片會在讀者捲動到它時才繪製。
- 拖曳時，游標以靈敏的彈簧在資料點之間移動，游標之後的線段變暗，終點圓點放大，以表示正在讀取某個資料點。
- 主數值與附註捲動為新的文字，主數值的寬度緩和調整以容納新文字。
- 新資料會使線條從目前形狀變形為新形狀，而不是從頭重繪。
- 減少動態效果時整條線立即顯示，游標移動時不使用彈簧。關閉動態效果時直接到達最終狀態。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 首次顯示 | 約 840 ms 內繪完線條，終點圓點隨線條落定而淡入，然後穩定 | 整條線立即出現；終點圓點出現時不使用彈簧 | 最終狀態 |
| 拖曳游標 | 游標以靈敏的彈簧移動 | 游標移動時不使用彈簧 | 立即 |
| 游標之後的線段 | 在短促的標準補間中變暗 | 同樣的短補間中變暗 | 立即 |
| 拖曳時的終點圓點 | 以靈敏的彈簧放大到 10 個單位 | 大小立即改變，不使用彈簧 | 立即 |
| 新資料 | 線條以平滑彈簧變形為新形狀 | 直接跳到新形狀 | 立即 |

## 相關元件

- `BarChart` 用於需要座標軸的離散期間，`LineChart` 用於帶有座標軸、多個系列或圖例的趨勢。
- `MetricCard` 用於單一數值及其變化，沒有趨勢。
- `Gauge` 用於相對已知範圍的數值，`BrushChart` 用於讀者需要縮放的長序列。

| 元件 | 適用情境 |
|---|---|
| `BarChart` | 每個期間是獨立的數值，且人們要比較各期間 |
| `LineChart` | 趨勢需要數值軸、時間軸、多個系列或圖例 |
| `MetricCard` | 只有一個目前數值，且其變化比路徑更重要 |
| `Gauge` | 數值需要對照固定範圍或門檻讀取，例如配額 |
| `BrushChart` | 長序列需要在完整範圍上可縮放的視窗 |
| `Treemap` | 問題是總量如何在各部分之間分配，而不是隨時間如何變化 |
