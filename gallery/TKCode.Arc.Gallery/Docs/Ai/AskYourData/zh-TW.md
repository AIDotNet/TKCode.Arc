---
title: Ask your data
summary: 以自然語言產生圖表的卡片：輸入問題，SQL 串流寫入一個可收合、帶語法著色的面板，查詢執行時列數滾動計數，答案以堆疊長條、折線或表格逐步呈現，下方附一行洞察。
source-hash: 8e9aa8eaacfc
---

<Demo name="Preview" />

## 何時使用

`AskYourData` 在一張卡片裡把一句白話問題變成圖表。模型寫出的 SQL 帶著語法著色串流寫入「Generated SQL」面板，查詢執行時已掃描列數滾動計數，答案以堆疊長條、折線或表格逐步呈現，附帶圖例、每個類別的讀數，以及在下方串流出現的一行洞察。SQL 隨時只需點一下即可檢視，方便核對或複製實際執行的內容。

- **產品內的自助分析**：儀表板、指標頁面或管理後台裡的提問框。
- **AI 助理的結果檢視**：當答案是一個最適合用圖表閱讀的數字時。
- **講解或稽核文字轉 SQL**：呈現產生的查詢能建立信任。

## 何時不用

- **查詢是固定的**，沒有問題可問：使用 `BarChart` 或 `LineChart`。
- **資料庫查詢只是較長代理程式紀錄中的一步**：使用 `ToolCall`。
- **由人手寫並調校查詢**：使用 SQL 編輯器。

## 用法

透過三個委派把卡片接上你的模型與唯讀資料庫角色。一個委派都沒有時，它會播放它的 `Simulation`，也就是由你提供的答案；兩者都沒有時，它會停在空狀態（「Ask a question about your data」），提問按鈕無法使用。

```xml
<arc:AskYourData Placeholder="Ask about your orders" />
```

你的模型與資料庫：

```csharp
card.Source = new AskYourDataSource { Name = "analytics", Dialect = "PostgreSQL" };

// The whole SQL at once: the card reveals it four tokens a tick at its final size.
card.GenerateSql = request => model.WriteSqlAsync(request.Question, request.CancellationToken);

// Run it read only and shape the rows for the chart. Throw to show the database's message.
card.RunQuery = async request =>
{
    var rows = await db.QueryAsync(request.Sql, request.CancellationToken);
    request.ReportProgress(rows.Count, rows.Count);
    return new AskYourDataResult
    {
        Title = "Weekly signups by plan",
        Categories = WeeksOf(rows),
        Series = SeriesByPlan(rows),
        RowCount = rows.Count,
        DurationMs = rows.Milliseconds,
    };
};

card.ResultReady += (_, e) => history.Save(e.Result.Title, e.Sql);
```

`AutoRun` 只在由模擬撰寫 SQL 時預設開啟；把它設為 `True`，卡片一出現就會提出初始的 `Question`。`Ask()` 提問輸入框裡的問題，`Ask(question)` 先把新問題放進輸入框再提問，`Stop()` 取消正在進行的執行。

### 模擬

`AskYourDataSimulation` 播放一個由你描述的模型與資料庫：卡片有 35% 出現在螢幕上時自行提出的 `Question`、`Suggestions`、`Source`，以及它的 `Answers`。問題中包含某個答案的 `Keywords` 之一時，會得到該答案的 `Sql`、`RowsScanned` 與 `Result`；其他問題得到 `Fallback`（SQL 中的 `{keyword}` 會換成問題中最長的字詞），或者沒有任何資料列。SQL 在短暫停頓後逐步顯示，計數分十步、每步 110ms 滾動上升，接著圖表建立、洞察逐字出現；即時區域會補充說明沒有查詢任何內容。它只替代你沒有接上的委派，你自己設定的 `Question`、`Suggestions` 與 `Source` 優先於它。把 `Simulation` 設為 null 會停止它的執行並回到空狀態。

```csharp
card.Simulation = new AskYourDataSimulation
{
    Question = "Weekly signups by plan since August",
    Suggestions = ["Weekly signups by plan since August", "Churn rate by plan per month"],
    Source = new AskYourDataSource { Name = "analytics", Dialect = "PostgreSQL" },
    Answers =
    [
        new AskYourDataSimulationAnswer
        {
            Keywords = ["signup", "sign up"],
            Sql = "SELECT week, plan, count(*) AS signups FROM signups GROUP BY week, plan;",
            RowsScanned = 18_342,
            Result = weeklySignups, // an AskYourDataResult you build
        },
    ],
};
```

本頁的示範從 `AskYourDataDemoData` 取得答案：一個虛構產品的註冊數、MRR 與流失率。

### 串流輸出 SQL

<Demo name="Streaming" />

用 `StreamSql` 取代 `GenerateSql`，傳入來自模型的非同步分段串流，面板會隨分段抵達而增高，每個分段淡入出現。在 `RunQuery` 中呼叫 `ReportProgress(rowsScanned, totalRows)` 可讓執行狀態中的計數滾動；傳入總數會預留寬度，這一行就不會跳動。兩個委派都會收到取消權杖：請遵守它，這樣「停止」才能真正取消。

```csharp
card.StreamSql = request => model.StreamSqlAsync(request.Question, request.CancellationToken);
```

### 結果

`AskYourDataResult` 的結構為圖表而設計：`Categories` 是依序排列的 X 軸標籤，每個 `AskYourDataSeries` 為每個類別提供一個值。各數列預設可以相加（`Stacked`）：長條堆疊，圖例、讀數與表格顯示合計。比率與平均值請設 `Stacked = false`；長條改為並排分組，也不顯示合計。

| 屬性 | 作用 |
|---|---|
| `Title`、`Subtitle` | 圖表建立後的標題（"Aug 3 to Oct 25 · 1,430 signups"） |
| `LegendValue` | 註冊數等流量用 `Sum`，MRR 等存量用 `Last`，不顯示用 `None` |
| `Chart` | 這個結果開啟時的檢視；否則使用 `DefaultChart` |
| `FormatValue` | 格式化刻度、圖例、讀數與表格儲存格（$258k、3.9%） |
| `Insight` | 解讀圖表的一兩句話，在圖表下方串流出現，並預留兩行高度 |
| `RowCount`、`DurationMs` | SQL 中繼資訊："PostgreSQL · 48 rows · 214ms" |

沒有類別或數列的結果會顯示 "No rows matched" 並提供建議問題。

### 錯誤與停止

<Demo name="Errors" />

在 `GenerateSql`、`StreamSql` 或 `RunQuery` 中擲回例外，卡片會原樣顯示訊息並提供「重試」，同時引發 `Failed`，並帶出失敗的階段（`Sql` 或 `Query`）。卡片運作期間，「提問」按鈕會停止執行，在輸入框中按 Escape 也會停止；停止的執行顯示 "Stopped"，不會引發任何事件。

### SQL 面板

撰寫 SQL 時面板展開，查詢開始後、圖表建立之前收合，這樣同一時間只有一個區域在動。`KeepSqlOpen` 讓它保持展開。一旦使用者在執行中自行展開或收合了面板，就以使用者的選擇為準。SQL 由函式庫內建的程式碼醒目提示著色，並在自己的區域內橫向捲動；「複製 SQL」會複製整段查詢。

### 圖表檢視

切換器在長條、折線與表格之間切換。`Chart` 保持為 null 時，每個結果依自己的 `Chart` 或 `DefaultChart` 開啟；繫結 `Chart` 即可自行控制檢視。使用者切換時會引發 `ChartChanged`。

```xml
<arc:AskYourData DefaultChart="Table" KeepSqlOpen="True" />
```

### 窄卡片

<Demo name="Narrow" />

寬度小於 520px 時，資料來源只保留圖示，圖表切換器移到結果中、圖例上方並佔滿整列，舞台區域會增高以容納換行後的圖例與洞察。類別標籤會自動疏化，保持 46px 的間距；過寬的 SQL 行與表格在各自的區域內捲動。

### 你自己的文字

`Labels` 包含卡片顯示或朗讀的所有文字；`Placeholder` 是輸入框的提示，`Suggestions` 是空狀態中提供的問題（空清單會隱藏它們）。

```csharp
card.Labels = new AskYourDataLabels
{
    WritingSql = "SQL wird geschrieben",
    RunningQuery = "Abfrage läuft",
    Rows = rows => $"{rows} Zeilen",
};
```

### 最佳實務

- 把資料庫結構寫進模型的提示詞，並用唯讀角色與陳述式逾時來執行 SQL。
- 使用預設配色時，數列最多保持四個。
- 讓模型根據傳回的資料列撰寫洞察，使用結果中確實存在的具體數字。
- 極快的進度回報請合併為每秒幾次。

## API 參考

<!-- api: AskYourData -->

<!-- api: AskYourDataResult -->

<!-- api: AskYourDataSeries -->

<!-- api: AskYourDataSource -->

<!-- api: AskYourDataSimulation -->

<!-- api: AskYourDataSimulationAnswer -->

<!-- api: AskYourDataLabels -->

## 無障礙

- 一個禮貌型即時區域會播報執行過程：正在撰寫 SQL、正在執行查詢，接著是圖表標題、列數與洞察。沒有 `GenerateSql`、`StreamSql` 與 `RunQuery` 時，還會加上 "Simulated with sample data, nothing was queried."
- 狀態從不只靠顏色表達：SQL 列與舞台標題中，文字標籤旁總有旋轉指示器、繪製出的勾號、警示或停止圖示。
- SQL 列是一個會回報展開或收合狀態的按鈕，名稱由標題與中繼資訊組成，例如 "Generated SQL, PostgreSQL · 48 rows · 214ms"。
- 圖表可取得焦點，並以結果命名；方向鍵移動讀數並播報，例如 "Aug 10: Free 61, Pro 26, Team 10, Enterprise 2, total 99"。
- 輸入框取得焦點時邊框變為強調色；其他控制項顯示 Arc 的鍵盤焦點環。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| Enter | 在問題輸入框中提問 |
| Escape | 在問題輸入框中停止正在進行的執行 |
| Tab | 依序經過輸入框、提問按鈕、Generated SQL 列、複製 SQL、SQL 區域、圖表切換器、圖表，以及建議問題或「重試」 |
| Enter、Space | 在 Generated SQL 列上展開或收合 SQL |
| Left、Right、Home、End | 在圖表上於類別之間移動讀數；在圖表切換器上切換檢視 |
| Escape（圖表） | 清除讀數 |

## 動效

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| SQL 串流出現 | 每 34ms 四個語彙單元，每個分段以 140ms 淡入；先完成排版，面板維持最終尺寸 | 一次顯示全部 | 一次顯示全部 |
| SQL 面板展開或收合 | 300ms 無回彈的高度彈簧，內容淡入淡出 | 立即 | 立即 |
| 長條 | 從基線升起，耗時 320ms，錯開最多 180ms | 直接畫出 | 直接畫出 |
| 折線 | 由左向右繪製，耗時 460ms | 直接畫出 | 直接畫出 |
| 切換檢視 | 在固定區域內交叉淡化，淡出 100ms、淡入 160ms | 立即 | 立即 |
| 標題與 SQL 中繼資訊 | 以單行交叉淡化，並有 2px 的落定 | 短暫淡化 | 立即 |
| 已掃描列數 | 等寬數字逐位滾動，寬度預留 | 原地更新 | 原地更新 |
| 洞察 | 每個節拍四個字詞，預留兩行高度 | 一次顯示全部 | 一次顯示全部 |
| 旋轉指示器與骨架 | 旋轉與呼吸；離開螢幕時暫停 | 靜止 | 靜止 |

## 相關

- `BarChart` 與 `LineChart`：查詢固定、沒有問題可問時使用。
- `ToolCall`：資料庫查詢作為代理程式紀錄中的一步時使用。
- `ReasoningBlock`：在答案上方呈現模型的思考過程。
