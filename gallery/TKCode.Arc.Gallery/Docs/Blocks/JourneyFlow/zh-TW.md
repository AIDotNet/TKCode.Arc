---
title: Journey flow
summary: "以桑基圖呈現旅程：指向一個步驟或一條流向，即可追蹤該群體經過的每個階段；選取一個步驟，檢視這些人從哪裡來、接下來去了哪裡；切換區隔群體，觀看每條流向變形。"
source-hash: 340dafb201d4
---

<Demo name="Preview" />

## 何時使用

`JourneyFlow` 展示一群人如何穿過一系列階段，以及他們在哪裡離開。階段從左到右排列；每個階段包含主路徑上的一個步驟，以及在它下方停在這裡的人；相鄰階段之間的絲帶有多粗，就表示走這條路的人有多少。最後一個階段中的目標為綠色。

- **帶流失的漏斗。** 造訪、註冊、引導、啟用、方案：每一步的流失都是一條看得見的絲帶，而不只是一個百分比。
- **比較區隔群體。** 共用同一結構的管道、群組或月份。圖表上方的開關會把每根柱、每條絲帶和每個計數變形為另一個區隔群體，因此人們看到的是變化本身。
- **追問「這些人是誰」。** 指向一個步驟，會在整個旅程中追蹤所有經過它的人：他們從哪裡來，最後去了哪裡。指向單條絲帶，則對走這條路的人做同樣的事。

## 何時不用

- **迴圈、跳轉或眾多路徑。** 連線只連接相鄰階段，節點按你給出的順序堆疊。對於人們會回退或跳到後面的旅程，請使用圖檢視。
- **每個階段超過三個節點。** 每個節點需要在上一個節點下方留出 44 像素放置它的標籤；一欄中有四五個節點時，留給柱的空間所剩無幾。
- **單一轉換率。** KPI 磚塊或一根小柱用更小的空間就能說明「6% 已訂閱」。
- **先要精確數字。** 圖表用於呈現形狀和比例；其下的 "Flow table" 提供精確計數，當數字才是重點時，一般表格更合適。

## 用法

區塊衍生自 Avalonia 的 `TemplatedControl`，圖表由它自己繪製。為它提供階段、節點（每個節點帶有其階段的鍵）以及一個或多個區隔群體，每個區隔群體是從一個節點到下一個階段中某個節點的連線清單。

```xml
<arc:JourneyFlow Title="Trial to paid journeys"
                 SubtitleFormat="{}{0} visits in October, from first page to plan"
                 GoalCaption="of visitors picked a paid plan">
    <arc:JourneyFlow.Stages>
        <arc:JourneyStage Key="landing" Header="Landing" ShortHeader="Land" />
        <arc:JourneyStage Key="trial" Header="Trial" />
        <arc:JourneyStage Key="plan" Header="Plan" />
    </arc:JourneyFlow.Stages>
    <arc:JourneyFlow.Nodes>
        <arc:JourneyNode Key="arrived" StageKey="landing" Header="Arrived" Description="Opened a course page" />
        <arc:JourneyNode Key="started" StageKey="trial" Header="Started trial" />
        <arc:JourneyNode Key="left" StageKey="trial" Header="Left" Kind="Exit" DropOffLabel="Left before the trial" />
        <arc:JourneyNode Key="subscribed" StageKey="plan" Header="Subscribed" Kind="Goal" />
        <arc:JourneyNode Key="lapsed" StageKey="plan" Header="Lapsed" Kind="Exit" />
    </arc:JourneyFlow.Nodes>
    <arc:JourneySegment Key="all" Header="All learners">
        <arc:JourneyLink SourceKey="arrived" TargetKey="started" Value="5320" />
        <arc:JourneyLink SourceKey="arrived" TargetKey="left" Value="11470" />
        <arc:JourneyLink SourceKey="started" TargetKey="subscribed" Value="1025" />
        <arc:JourneyLink SourceKey="started" TargetKey="lapsed" Value="4295" />
    </arc:JourneySegment>
</arc:JourneyFlow>
```

### 結構

- **頁首**：`Title`、由 `SubtitleFormat` 得到的副標題（訪客數在 `{0}` 的位置滾動顯示），以及區隔群體開關（有兩個或更多區隔群體時顯示）。
- **圖表**：每個階段一欄，帶有一條參考線，階段名稱位於其下；節點柱寬 8，從頂部起每隔 44 堆疊；每根柱上方有兩行標籤（名稱，然後是訪客的 "count · share"）；階段之間是絲帶。節點的計數取流入和流出中較大的一個，因此第一個階段的總數就是訪客數。所有階段使用同一比例，所以最滿的階段會占滿整個高度。
- **側邊面板**：從 860 像素起位於圖表旁邊，更窄時位於圖表下方。沒有選取項時它是概覽：目標數量及其占訪客的比例，最大的流失（最後一個階段之前、無人繼續前行的退出，以及它們占上一步的比例），以及最後一個階段的結果。選取某個步驟時它顯示該步驟：它的計數、占訪客的比例、其人員從哪裡抵達以及接下來去了哪裡。
- **Flow table**：圖表下方的摺疊面板，列出每條流向及其占所離開步驟的比例。

### 節點類型

`Kind` 決定顏色：`Step` 以強調色繪製，流入它的流向為 10% 的 Accent；`Exit` 為柔和的灰色，流向很淡（4%）；`Goal` 為成功色，流向為綠色（16%）。退出是概覽中列為流失的內容，目標則是概覽所統計的內容。

### 追蹤與選取

指向一個步驟或它的標籤，或指向一條絲帶，會追蹤該群體：圖表的其餘部分淡出，該群體走過的每條流向都被一條同等大小的強調色帶覆蓋，群體在每根柱中所占的部分變為實心；該群體從未抵達的步驟的標籤也會淡出。提示框跟隨指標，顯示計數和兩個比例。點擊一個步驟會選取它：當你指向別處時它仍保持被追蹤狀態，側邊面板切換到它，並且選取在切換區隔群體後依然保留。再次點擊它、點擊圖表空白處、按 Escape 或使用面板的 Clear 按鈕即可清除選取。點擊絲帶不會選取。

`SelectedNode` 和 `SelectedSegment` 是雙向的。`SelectedNodeChanged`、`SelectedSegmentChanged` 和 `TraceChanged` 回報這些變化（最後一個針對被預覽的節點或連線，指標或鍵盤均可），`NodeSelectedCommand` 和 `SegmentSelectedCommand` 在使用者操作時執行，`GetCohort` 傳回每條連線上被追蹤的計數，供你自己的彙總使用。

<Demo name="Selection" />

```csharp
journey.SelectNode("hooked");
journey.SelectedSegment = journey.Segments.First(s => s.Key == "social");
journey.TraceChanged += (_, e) =>
{
    if (e.Node is { } node)
    {
        var cohort = journey.GetCohort(node);
        status.Text = $"{node.Header}: {cohort.Values.Sum():N0} people across {cohort.Count} flows";
    }
};
```

### 窄容器

區塊讀取自己的寬度，而不是視窗的寬度。小於 860 時，側邊面板移到圖表下方，其兩個群組並排；559 或更小時，開關占滿整個寬度，內距收緊到 16，群組改為堆疊；479 或更小時，標籤變小。圖表從不捲動或旋轉：它在每個寬度下都會重新配置，720 像素寬的圖表高 404，480 起為 372，更窄則為 340。小於 600 時標籤去掉比例，小於 440 時每個階段最上方的節點只顯示計數，階段名稱切換為 `ShortHeader`。

<Demo name="Narrow" />

### 數字和文字

`ValueFormatter` 格式化每一個計數（標籤、提示框、面板、表格、朗讀的名稱），`ShareFormatter` 格式化每個以分數給出的比例，`LabelShareFormatter` 格式化圖表標籤上四捨五入後的比例。即使使用你自己的格式化器，面板中的數字也會逐位滾動：數字前後的文字保持不動。每個片語都是帶有英文預設值的屬性：`CountFormat`（"{0} users"）、`ShareOfFormat`、`VisitorsShareText`、`VisitorsCaption`、`GoalCaption`、`OverviewHint`、`DropOffsHeader`、`OutcomesHeader`、`InboundHeader`、`OutboundHeader`、`JourneyStartText`、`JourneyEndText`、`ClearText`、`TableHeader`、`TableCaptionFormat`、`TableColumnHeaders` 和 `AnnouncementFormat`。提示框中的 "Share of …" 一行會指明該階段所有人所經過的步驟；設定 `JourneyStage.ShareBaseLabel` 或 `JourneyNode.ShareLabel` 即可自己定義這行的措辭。

只有一個區隔群體時開關隱藏；`IsTableVisible` 和 `IsSidePanelVisible` 可移除表格和面板。

<Demo name="Tickets" />

### 即時計數

修改所顯示區隔群體中某條連線的 `Value`，圖表會就地變形，就像切換區隔群體一樣。`StagesSource`、`NodesSource` 和 `SegmentsSource` 接受由你自己持有的集合（會跟隨可觀察集合的變化），取代 `Stages`、`Nodes` 和 `Segments` 清單。

<Demo name="Live" />

## API 參考

<!-- api: JourneyFlow -->

<!-- api: JourneyStage -->

<!-- api: JourneyNode -->

<!-- api: JourneyLink -->

<!-- api: JourneySegment -->

<!-- api: JourneyNodeKind -->

<!-- api: JourneyNodeChangedEventArgs -->

<!-- api: JourneySegmentChangedEventArgs -->

<!-- api: JourneyTraceChangedEventArgs -->

## 無障礙

### 鍵盤

- **Tab** 停在區隔群體開關上，然後在圖表上停一次（上次取得焦點的步驟，起初是第一個），選取某個步驟時再停在面板的 Clear 按鈕上，然後是 "Flow table"。
- 在開關上，**←/↑** 和 **→/↓** 移到上一個和下一個區隔群體並循環；圖表立即跟隨。**Home** 和 **End** 跳到第一個和最後一個。
- 在圖表上，**↑/↓** 在階段內移動（到兩端停止），**←/→** 移到相鄰階段中中心最近的步驟，**Home** 和 **End** 跳到第一個和最後一個步驟。取得焦點的步驟會被追蹤，其提示框位於它旁邊。
- **Enter** 或 **Space** 選取取得焦點的步驟，若已選取則清除。**Escape** 清除選取並讓焦點保持原處。
- 點擊 Clear 之後，焦點回到原來被選取的步驟。

### 螢幕閱讀器

- 每個步驟是一個切換按鈕，名稱如 "Started trial: 5,320 users, 31.7% of visitors. Began the free trial."，其按下狀態就是選取狀態。這些步驟構成一個由 `ChartLabel` 命名的群組，它的說明文字逐階段朗讀整張圖表。
- 區隔群體開關是一個由 `SegmentsLabel` 命名的群組；其中的各個區隔群體會回報哪一個被按下。
- 切換區隔群體時會以禮貌的方式播報（"Search: 8,050 visits and 510 paid plans."）。追蹤和選取不會播報；取得焦點的步驟的名稱已說明了一切。
- 提示框只是繪製出來的，並對螢幕閱讀器隱藏。滾動的數字讀出的是最終值，而不是滾動中的值。
- 側邊面板是一個由 `PanelLabel` 命名的區域；空的列槽位會被隱藏。Flow table 按鈕會回報是否已展開，表格以帶標題的文字形式讀出。
- 焦點環寬 2 像素，位於步驟命中框外 2 像素處。

## 動態效果

- **區隔群體開關**：每個計數都跟隨一個彈簧（剛度 70，阻尼 16.4，無過衝，大約 0.8 秒穩定），整張圖表每一幀都根據移動中的計數重新配置，因此柱、絲帶和標籤作為一個整體移動。標籤上的計數以同樣的彈簧晚 65 ms 跟隨。副標題中的訪客數和面板中的數字在平滑彈簧上逐位滾動，比例條滑動，面板的標題在 120 ms 內淡出，而新標題在 240 ms 內上升 4 像素進入。開關的滑塊在快速彈簧上滑動。
- **追蹤**：圖表的其餘部分在 160 ms 內淡出；該群體的色帶和柱上的疊層在快速彈簧上增長，當你移到另一個步驟時，它們從目前位置變化，而不是重新開始。提示框在 120 ms 內淡入，毫無延遲地跟隨指標，並在被追蹤的對象變化時交叉淡入淡出其內容。
- **選取**：步驟的環在 160 ms 內淡入；面板的文字如上所述切換。
- **Flow table**：高度在平滑彈簧上展開，箭頭在快速彈簧上翻轉。
- 減少動態效果會讓圖表和數字立即就位，只保留短暫的淡入淡出（提示框、面板標題）。關閉則立即套用每個變化。區塊首次出現時沒有任何動畫。

## 相關

- `Cohort retention` 和 `Metrics dashboard`，即其他 Analytics 區塊。
- `SegmentedControl`，用於獨立的開關。
- `Sortable data table`，適用於數字比形狀更重要的場合。
