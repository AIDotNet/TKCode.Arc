---
title: 客戶星系
summary: 每位客戶都是一個點，大小依營收、顏色依健康度，像星系一樣緩緩轉動；將它們分成叢集、飛入其中，並查看總計。
source-hash: 2005b60c7240
---

<Demo name="Preview" />

## 何時使用

`CustomerGalaxy` 把每個客戶帳戶畫成一個點：點的面積隨帳戶的每月經常性營收（MRR）增長，顏色則表示帳戶的健康度。在總覽中，這些點排成一道傾斜、緩緩轉動的螺旋，最大的帳戶擠在核心，眾多小帳戶則拖成兩條尾臂。選擇一種分組後，每個點都會彈到各自的位置，聚成一個緊密的叢集，每個值一個，並標示其 MRR 與帳戶數。緊鄰舞台旁邊的總計面板（在窄版容器中則位於舞台下方）會加總所有內容，或加總你飛入的那個叢集，並列出各叢集；每一列的長條依健康度分段。當問題是「數千個帳戶中，營收與風險各落在哪裡」時，請使用它。

- **客戶成功首頁**：團隊依健康度分組，每週一開始就飛入有風險的叢集。紅色大點是優先要聯絡的續約客戶；滑過其中一個點，會顯示它的負責人與一行備註。
- **營收檢視**：財務依方案分組，讀出最高階層承載了多少 MRR，再把清單切換為帳戶，看每個層級有多少客戶。
- **區域銷售儀表板**：依地區分組，每位經理飛入自己的叢集，並用方向鍵由最大的帳戶開始逐一查看。
- **產品分析頁面**：依產業分組客戶，看哪些市場帶來大型帳戶、哪些市場帶來大量小型帳戶，以及哪些正在成長。
- **董事會簡報螢幕**：停留在總覽上。會轉動的星系搭配總計，一眼就能讀懂，也會讓人想點進去看看。

## 何時不用

- **比較少數幾個數字。** 請使用 `BarChart` 或 `DonutChart`；星系要在有數百或數千個帳戶時才有價值，五個帳戶不適合。
- **並排讀取許多帳戶的精確數值。** 請使用 `Table` 或 `DataGrid`；星系一次只在卡片中顯示一個帳戶。
- **隨時間的趨勢。** 請使用 `LineChart` 或 `Streamgraph`。星系是一個快照：它的叢集不會隨日期移動。
- **側邊欄或卡片中的小型小工具。** 此元件的舞台至少需要 360 px 高才看得清楚；請改用搭配 `Sparkline` 的 `MetricCard`。
- **只依單一維度呈現整體的部分。** 請使用 `Treemap` 或 `WaffleChart`，它們能保留精確的面積，不會有封裝的空隙。

## 用法

把你的帳戶與想要的分組以分頁的形式交給控制項。每個 `GalaxyAccount` 帶有名稱、MRR、健康度鍵，以及在 `Groups` 中每個分組的值。控制項會自行配置螺旋與叢集、加總總計並建立清單。

```xml
<arc:CustomerGalaxy Title="Where the revenue sits"
                    ItemsSource="{Binding Accounts}">
    <arc:GalaxyGrouping Key="plan" Header="Plan">
        <arc:GalaxyGroupDefinition Key="solo" Label="Solo" />
        <arc:GalaxyGroupDefinition Key="team" Label="Team" />
        <arc:GalaxyGroupDefinition Key="business" Label="Business" />
        <arc:GalaxyGroupDefinition Key="corporate" Label="Corporate" />
    </arc:GalaxyGrouping>
    <arc:GalaxyGrouping Key="region" Header="Region" />
    <arc:GalaxyGrouping Key="health" Header="Health" />
</arc:CustomerGalaxy>
```

`CustomerGalaxy` 衍生自 Avalonia 的 `TemplatedControl`。舞台由控制項自己繪製（每個帳戶一個點，依顏色與淡化程度批次處理），因此即使有數千個帳戶也能保持流暢；標籤、卡片、按鈕與面板都是一般控制項。控制項會設定 `:grouped`、`:cluster-focused`、`:row-hover`、`:side` 與 `:empty` 這幾個偽類，供樣式使用。

### 結構

1. **標題與副標題**（`Title`、`Subtitle`、`SubtitleFormat`）：16 px 中等字重的標題，加上 14 px 淡化色的一行字。若沒有 `Subtitle`，這一行會顯示「2,400 accounts. Dot size shows MRR, color shows health.」；設為空字串則隱藏。
2. **分組分頁**（`Groupings`、`AllText`）：「All」與每個分組各一個分頁，位於淡化色、34 px 高的軌道上；一個表面色的膠囊會滑到所選的分頁。當容器比分頁窄時，這一列可以左右捲動。
3. **圖例**（`ShowLegend`、`SizeLegendText`）：每種健康狀態一個有色的點，中間是一條分隔線，然後是一個小與一個大的淡化色點，用來表示大小。兩者都放得下時，圖例位於分頁右側；否則會移到分頁下方。
4. **舞台**：表面色上的點。在總覽中，圓盤會被壓扁為高度的 68%，靠前的點則放大至多 12%，使它看起來像一個朝向你傾斜的圓盤。
5. **叢集標籤**：每個叢集的名稱，其下是 MRR（滾動顯示）與帳戶數，位於叢集上方 10 px。
6. **卡片**（`AccountTooltipTemplate`）：帳戶的名稱、各分組的值、MRR、負責人、健康度與備註，位於指標旁，或在選取的點旁釘住。
7. **手勢提示**（`ShowHint`、`HintText`）：舞台左下角一行淡化色的文字。叢集被聚焦或清單列被懸停時，它會淡出。
8. **縮放列**（`ShowZoomControls`）：縮小、放大與符合畫面，位於右下角 12 px 處。當視圖已顯示全部內容時，符合畫面按鈕會變淡。
9. **「Show all」**（`ShowAllText`）：叢集被聚焦時，位於左上角的一個膠囊。
10. **總計面板**（`ShowTotals`、`TotalsPlacement`）：範圍名稱、大字體的 MRR、帳戶數、成長與有風險的小計，以及叢集列表與 MRR / Accounts 切換。

### 帳戶

在 `Items` 中直接加入 `GalaxyAccount` 物件，或把 `ItemsSource` 設為一個清單；當清單是可觀察集合時，星系會跟隨它的變化。當選取的帳戶與聚焦的叢集仍然存在時，它們在變化後會被保留。若你就地修改某個帳戶，請呼叫 `Refresh()`。

- `Id` 是帳戶的穩定鍵，選取依此比對。`Name` 會顯示在卡片上，並會被朗讀。
- `Mrr` 設定點的面積：在適合的縮放下，其半徑為 `0.30 + 0.028 × √MRR` px，最小為 0.74 px。
- `Health` 是其中一個 `HealthStates` 的鍵；帶有未知鍵的帳戶會以中性狀態繪製。
- `Groups` 依分組的鍵保存帳戶在每個分組中的值（`["plan"] = "business"`）。
- `Owner` 與 `Note` 填入卡片；`Tag` 由你自行使用。

```csharp
var account = new GalaxyAccount
{
    Id = "acct-0042",
    Name = "Juniper Labs",
    Mrr = 18_400,
    Health = "growing",
    Owner = "Rhea Ingram",
    Note = "Upgrade call booked"
};
account.Groups["plan"] = "business";
account.Groups["region"] = "harbor";
accounts.Add(account);
```

### 分組

每個 `GalaxyGrouping` 會加入一個分頁。它的 `Key` 指定所讀取的 `Groups` 項目；鍵 `health` 則依 `Health` 分組，叢集、標籤與顏色取自 `HealthStates`。列出 `GalaxyGroupDefinition` 可以為各值提供標籤，並為並列時定下固定順序；帳戶攜帶但未列出的值會排在其後；沒有值的帳戶則組成一個「Other」叢集（`OtherText`）。`Header` 是分頁的文字，`ListTitle` 是清單的標題（未設定時，由 `ListTitleFormat` 組成「By plan」）。

`SelectedGroupingKey` 是雙向的：為 null 時顯示總覽。它改變時，每個點都會從目前位置彈到新配置中的位置，相機也會對準新配置；標籤會在 120 ms 後淡入。叢集依帳戶數由大到小封裝，最大的叢集置於中央，其他叢集依 MRR 圍繞在它的左、右、左下、右上、左上……並保留標籤的空間。總覽的清單依健康度排列；選取其中一列會切換到健康度分組，並飛入該叢集。

<Demo name="Clusters" />

### 健康狀態

`HealthStates` 預設有四個狀態，依圖例順序為：成長中（`Success`）、穩定（`TextMuted`，72%）、降溫（`Warning`）與有風險（`Danger`）。每個狀態在 `Tone` 中指定其所用的 ArcDesign 顏色（或以固定的 `Color` 指定），其點與長條段的繪製強度（`DotOpacity`、`BarOpacity`），以及繪製順序（`Layer`：穩定的點先畫，有風險的點畫在最上層）。設定 `IsGrowth` 的狀態會提供「成長」小計，設定 `IsRisk` 的狀態則提供「有風險」小計與每個叢集的風險比例。顏色會在每次主題或強調色變更時重新從主題讀取。

```csharp
galaxy.HealthStates.Clear();
galaxy.HealthStates.Add(new GalaxyHealthState { Key = "expanding", Label = "Expanding", Tone = GalaxyTone.Success, Layer = 1, IsGrowth = true });
galaxy.HealthStates.Add(new GalaxyHealthState { Key = "stable", Label = "Stable", Tone = GalaxyTone.Neutral, DotOpacity = 0.72, BarOpacity = 0.5 });
galaxy.HealthStates.Add(new GalaxyHealthState { Key = "churning", Label = "Churning", Tone = GalaxyTone.Danger, Layer = 2, IsRisk = true });
```

### 聚焦叢集與選取帳戶

雙擊叢集（或其標籤）、在清單中選取它的列、按 ← 或 →，或設定 `FocusedGroupKey`，即可飛入該叢集：叢集、其 10 px 間距與標籤會佔滿舞台高度的四分之三，其他叢集淡化至原本不透明度的七分之一，其標籤淡化至 30%，「Show all」隨之出現，面板也會切換為該叢集的數字。`FlyToGroup(key)`、`FocusNextGroup()` 與 `FocusPreviousGroup()` 可以從程式碼做到同樣的事，而 `GroupFocused` 會回報每一次變更。`ResetView()`、「Show all」、符合畫面、0 與 Home 會回到所有叢集。

把指標移到點上會在指標旁顯示其卡片；點擊它即可選取（`SelectedAccount`，雙向，並有 `AccountSelected`）。被選取的點會保留一個光環，其卡片也保持開啟：指標在舞台上時位於指標旁，指標離開時則釘在點右側 12 px 處。點擊空白處或按 Escape 會清除選取。↓ 與 ↑（或 `SelectNextAccount()` 與 `SelectPreviousAccount()`）會依 MRR 由大到小，逐一走過聚焦叢集中的帳戶；`FlyToAccount(account)` 會選取一個帳戶並縮放至它。

<Demo name="FocusedCluster" />

```csharp
galaxy.GroupFocused += (_, e) =>
{
    // e.Group is null when the view returned to every cluster.
    status.Text = e.Group is { } group ? $"{group.Label}: {group.AccountCount} accounts" : "All clusters";
};
```

### 相機

拖曳即可平移：舞台以 1:1 跟隨指標，超出內容邊緣時會以橡皮筋效果拉伸，然後彈回；沒有滑行。滑鼠滾輪會以指標為中心，每一格縮放 1.246 倍；觸控板捲動仍會捲動頁面，而按住 Ctrl 或 ⌘ 搭配滾輪或捏合手勢也會縮放。縮放按鈕與 + 和 − 鍵會以中心為基準，依 `ZoomStep`（1.6）縮放。視圖不會縮小到符合畫面之外，也不會放大超過 `MaxZoom`（9）倍。使用滾輪縮放後，視圖會等待 1.1 s 才滑回內容範圍內。`ZoomBy(factor, anchor)` 可從程式碼縮放，`Zoom` 會回報目前的縮放倍率（1 為符合畫面）。

### 總計與清單

面板會標示範圍名稱（「All accounts」，或被聚焦的叢集），以大字體滾動顯示 MRR，並列出三個小計：帳戶數、成長的 MRR 與有風險的 MRR。清單每個叢集一列（總覽中則為每個健康狀態一列）：名稱、數值、4 px 的長條與說明文字。將 `GroupSort` 設為 `Mrr` 時，各列依 MRR 排序，長條也依 MRR 量度，說明則計算帳戶數；設為 `Accounts` 時，則依帳戶數排序與量度，說明則顯示 MRR。在健康度分組以外，說明會加上該叢集有風險的 MRR 比例，長條則依健康狀態分成數段。懸停某一列會淡化其他叢集，但不會移動相機；選取它則會飛入該叢集。`Summary` 會提供相同的數字，供你自己的文字使用。

### 版面與容器寬度

版面依控制項自己的寬度變化，而不是視窗的寬度：

| 控制項寬度 | 總計 | 舞台高度 |
|---|---|---|
| 880 px 以上 | 位於舞台旁，寬 288 px | 舞台寬度的 16:9，最多 560，且至少與面板同高 |
| 620 至 880 px | 位於舞台下方，分為兩欄 | `max(360, width × 0.52)` |
| 低於 620 px | 位於舞台下方，單欄 | 380 |

將 `TotalsPlacement` 設為 `End` 時，無論寬度多少，面板都會留在舞台旁；設為 `Bottom` 則會留在舞台下方。符合畫面的縮放會隨舞台大小變化，因此較寬的舞台會畫出較大的星系；標籤則維持原有大小。

<Demo name="SidePanel" />

<Demo name="Narrow" />

### 你自己的資料

任何客戶清單都可以：工作區、門市、診所。自備分組、健康狀態與文案，並以 `CurrencySymbol` 與 `CultureName` 格式化金額，或改用你自己的 `MrrFormatter`。

<Demo name="YourData" />

```xml
<arc:CustomerGalaxy CurrencySymbol="€"
                    AllAccountsText="All workspaces"
                    AccountCountFormat="{}{0} workspaces"
                    OneAccountFormat="{}{0} workspace"
                    SizeLegendText="Size is revenue"
                    ShowHint="False" />
```

### 文字

每個文字都有英文預設值與一個可更改的屬性。格式字串以 `{0}` 代表它所指稱的值。

| 屬性 | 預設值 | 用途 |
|---|---|---|
| `SubtitleFormat` | "{0} accounts. Dot size shows MRR, color shows health." | `Subtitle` 為 null 時的副標題 |
| `AllText` | "All" | 總覽分頁 |
| `AllAccountsText` | "All accounts" | 未聚焦叢集時面板的範圍 |
| `MrrCaptionText` | "Monthly recurring revenue" | 大字數字上方的說明 |
| `MrrText` | "MRR" | 排序按鈕與卡片的第一列 |
| `AccountsText` | "Accounts" | 小計與排序按鈕 |
| `OwnerText` | "Owner" | 卡片的負責人列 |
| `HealthText` | "Health" | 卡片的健康度列 |
| `ListTitleFormat` | "By {0}" | 清單標題；`{0}` 是小寫的分組名稱 |
| `HealthGroupingText` | "Health" | 健康度分組的名稱（總覽清單與播報用） |
| `AccountCountFormat` | "{0} accounts" | 帳戶數 |
| `OneAccountFormat` | "{0} account" | 一個帳戶 |
| `AtRiskShareFormat` | "{0} at risk" | 列說明中的風險比例 |
| `HintText` | "Drag to move · scroll to zoom · double-click a cluster to open it" | 手勢提示 |
| `ShowAllText` | "Show all" | 從叢集返回的膠囊 |
| `ZoomInText`, `ZoomOutText`, `FitText` | "Zoom in", "Zoom out", "Show every account" | 縮放按鈕的名稱 |
| `SizeLegendText` | "Size shows MRR" | 圖例中的大小項目 |
| `GroupByText` | "Group accounts by" | 分頁的名稱 |
| `SortByText` | "Order the list by" | MRR / Accounts 切換的名稱 |
| `OtherText` | "Other" | 沒有值之帳戶的叢集 |
| `EmptyText` | "No accounts to show" | 沒有帳戶時的舞台 |
| `FlyToClusterText`, `GroupAndFlyFormat` | "Fly to this cluster", "Group by {0} and fly to this cluster" | 供螢幕閱讀器使用的列動作 |
| `KeyboardHelpText`, `StageSummaryFormat`, `OverviewLayoutText`, `GroupedLayoutFormat` | （句子） | 舞台的螢幕閱讀器名稱與說明 |
| `GroupAnnouncementFormat`, `AccountAnnouncementFormat`, `GroupingAnnouncementFormat`, `OverviewAnnouncementText` | （句子） | 即時播報 |

### 資料結構與效能

- 資料變更時，控制項會讀取每個帳戶的 `Id`、`Name`、`Mrr`、`Health`、`Owner`、`Note` 與 `Groups`，並在自己的平面陣列副本上運作；轉動中的星系每一影格都不配置記憶體。
- 在無頭 Skia 測試主機上，於開發用筆電中，2,400 個點每影格約需 3 ms；改變分組或相機的成本只多出一小部分毫秒。在瀏覽器（WebAssembly）中預期會慢上數倍；請將那裡的資料控制在數千個帳戶以內。
- 只有位於舞台內的點會被繪製，因此放大的視圖繪製的點很少。
- 舞台只在有東西移動時才請求影格。當指標停在舞台上、離開螢幕時，以及在減少動態效果時（星系會成為一張靜態圖片），它都會停止。

## API 參考

<!-- api: CustomerGalaxy -->

<!-- api: GalaxyAccount -->

<!-- api: GalaxyGrouping -->

<!-- api: GalaxyGroupDefinition -->

<!-- api: GalaxyHealthState -->

## 無障礙

- 舞台是一個可取得焦點的自訂控制項，被描述為「互動式圖表」。其名稱概述視圖（「Customer galaxy of 2,400 accounts and $6.05M in monthly recurring revenue, grouped by plan …」），其說明文字則列出按鍵（`KeyboardHelpText`）。焦點環位於舞台外側 2 px，且只在鍵盤焦點時顯示。
- 一個隱藏的表格取代畫布：每個目前分組的叢集各佔一列，列出其帳戶、MRR，以及各健康狀態的帳戶數。
- 一個禮貌模式的即時區域會播報聚焦的叢集（「Corporate, 70 accounts, $2.80M MRR, $60K at risk.」，並帶入你的數字）、以方向鍵選取的帳戶，以及新的分組或重設（「4 clusters by plan. Corporate is the largest …」）。
- 分組分頁是一個分頁列：只有被選取的分頁是 Tab 停駐點，方向鍵、Home 與 End 會移動選擇。MRR / Accounts 按鈕會回報是否已按下，每個清單列是一個按鈕，會回報其叢集是否被聚焦；其名稱為叢集名稱、MRR、帳戶數與風險比例，其動作則以說明文字提供。
- 縮放按鈕都有名稱；當視圖已顯示全部內容時，符合畫面按鈕會停用，且 Tab 會略過它。
- 滾動的數字只朗讀一次，並讀出其最終值。
- 顏色從來不是唯一的訊號：卡片與清單會標示每個健康狀態，圖例也會為顏色加上標籤。

### 鍵盤

| 按鍵 | 位置 | 操作 |
|---|---|---|
| `Tab` | 任何地方 | 分頁、舞台、「Show all」、縮放按鈕、MRR / Accounts、清單列 |
| `ArrowLeft` / `ArrowRight` | 分頁 | 選擇上一個或下一個分組（循環）；`Home` / `End` 為第一個或最後一個 |
| `ArrowRight` / `ArrowLeft` | 舞台，已分組 | 依 MRR 飛入下一個或上一個叢集（循環；從最大或最小的開始） |
| `ArrowDown` / `ArrowUp` | 舞台 | 在聚焦叢集中依 MRR 選取下一個或上一個帳戶（否則在所有帳戶中）；卡片會釘在它旁邊 |
| `+`, `=` / `−` | 舞台 | 以中心為基準，依 1.6 倍放大或縮小 |
| `0`, `Home` | 舞台 | 回到所有叢集 |
| `Escape` | 舞台 | 清除選取；再按一次則回到所有叢集 |
| `Enter`, `Space` | 列、按鈕 | 選擇列（飛入其叢集）或按下按鈕 |

## 動態效果

- 總覽與每個叢集都以每秒 1.6° 順時針轉動（約 224 s 轉完一圈），總覽中的每個點也會在數分鐘內緩慢漂移數個像素。當指標停在舞台上時，轉動會在約 0.4 s 內緩和停止，舞台也停止繪製；指標離開後會再次開始。
- 分組變更會讓每個點彈到其新位置：約 70% 立即出發，其餘最多延遲三分之一秒；每個點先緩慢游移，然後螺旋地靠攏，飛行時會略為膨脹。大多數點會在一秒內抵達。相機以相同的時序對準新配置，新的標籤則在 120 ms 後升起就位。
- 飛入叢集、「Show all」與符合畫面會以緩慢的臨界彈簧（約 1 s）移動相機；滾輪、按鈕與按鍵的縮放則使用較快的彈簧（約 0.55 s），並讓指標下的點保持不動。拖曳會精確地跟隨指標。
- 其他叢集以約四分之一秒淡化至七分之一（7 階）；被懸停的點會放大至 1.84 倍，並帶有 3 px 的光環。
- 卡片以 96% 的大小淡入並放大；數字逐位滾動，從左邊開始，每位相隔 26 ms；長條與其段落緩和地變為新寬度；重新排序的列滑入其位置；分頁的膠囊滑動。
- 減少動態效果時，星系會是一張靜止的圖：沒有轉動，沒有漂移，每次分組變更、飛行、縮放、淡化與懸停都立即完成；卡片與標籤只淡入淡出。關閉時，所有變更都立即套用。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 總覽與叢集轉動、點漂移 | 每秒 1.6°，指標停留時緩和停止 | 靜止 | 靜止 |
| 分組變更 | 每個點以錯開的起點彈到其位置 | 立即完成 | 立即 |
| 飛入叢集、「Show all」、符合畫面 | 臨界彈簧，約 1 s | 立即完成 | 立即 |
| 滾輪、按鈕或按鍵縮放 | 臨界彈簧，約 0.55 s，以錨點為中心 | 立即完成 | 立即 |
| 拖曳超出邊緣後放開 | 橡皮筋效果，彈回 | 橡皮筋效果，落在內部 | 橡皮筋效果，落在內部 |
| 淡化其他叢集 | 約 0.26 s 淡入，7 階 | 立即 | 立即 |
| 被懸停的點 | 約 0.4 s 放大至 1.84× | 立即 | 立即 |
| 叢集標籤 | 上升 4 px，120 ms 後淡入；240 ms 內淡化至 30% | 淡入淡出 | 立即 |
| 卡片 | 以 96% 淡入並放大 | 淡入淡出 | 立即 |
| 數字 | 數字滾動，相隔 26 ms | 立即變更 | 立即 |
| 長條與其段落 | 緩和至新寬度（580 ms） | 立即變更 | 立即 |
| 因排序切換而重新排列的列 | 滑入其位置（約 0.55 s） | 跳入 | 立即 |
| 分頁膠囊 | 以靈敏的彈簧滑動 | 跳動 | 立即 |
| 手勢提示 | 480 ms 內淡出 | 快速淡出 | 立即 |

## 相關元件

- `BarChart`、`DonutChart` 與 `Treemap`，用於少數精確數值或整體的部分。
- `DataGrid` 與 `Table`，用於逐列閱讀與比較帳戶。
- `MetricCard` 與 `AnimatedCounter`，用於單一的主要數字。
- `SegmentedControl`，分組分頁與它很相似。

| 元件 | 適用情境 |
|---|---|
| `Treemap` | 需要整體部分的精確面積，且不想要封裝的空隙 |
| `BarChart` | 以單一數值比較少數幾個類別 |
| `DataGrid` | 使用者需要自行閱讀、排序與編輯帳戶 |
| `MetricCard` | 儀表板磚塊只顯示一個數字及其變化 |
| `ActivityHeatmap` | 比起客戶是誰，按天與小時的活動更重要 |
