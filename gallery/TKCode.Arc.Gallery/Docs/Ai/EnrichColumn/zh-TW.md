---
title: 補全欄
summary: 資料表中的 AI 補全欄：同一個提示詞逐列執行，每個儲存格在處理時顯示微光，隨後落定為附來源的值，或顯示可重試的失敗。
source-hash: 741e6dfbca5e
---

<Demo name="Preview" />

## 何時使用

`EnrichColumn` 是帶一欄 AI 欄的表格卡片。同一個提示詞逐列執行：每個儲存格先排隊，代理處理時顯示微光，隨後落定為一個簡短的值和它的來源，或者顯示失敗和重試控制項。使用者可以停止執行、重新執行整欄或單獨一列，也可以就地編輯這一欄的名稱和提示詞。

- **補全公司或潛在客戶資訊**：員工人數、募資、技術堆疊等代理查得到的事實。
- **用一個提示詞對各列分類或摘要**，並為每個答案顯示來源。
- **在 CRM、銷售工具或內部後台中呈現逐列 AI 進度**，支援停止、重新執行和重試。

## 何時不用

- **欄位很多且需要編輯的大型表格**：使用資料表格，在其中加一欄 AI 欄。
- **針對整個資料集的單一問題**：問一次、顯示一個答案，而不是一整欄。
- **單一代理的工具呼叫紀錄**：使用 `ToolCall`。

## 用法

把資料列和一個填入儲存格的 `Enrich` 委派交給這一欄。委派收到資料列和一個 `EnrichColumnContext`，傳回值和它的來源。擲出例外即把儲存格標為失敗：例外訊息會作為原因顯示在「重試」旁邊。

```csharp
table.Rows = accounts.Select(a => new EnrichColumnRow(a.Id, a.Name, a.Domain)).ToList();
table.Column = new EnrichColumnDefinition("Headcount and funding", "Find the headcount and latest funding round");
table.Enrich = async (row, context) =>
{
    context.SetActivity($"Searching for {row.Name}");
    var answer = await service.EnrichAsync(row.Domain, context.Prompt, context.CancellationToken);
    if (answer is null)
    {
        throw new InvalidOperationException("No source found");
    }
    return new EnrichColumnResult(answer.Value, new EnrichColumnSource(answer.Host, answer.Url));
};
table.CellChanged += (_, e) =>
{
    if (e.Cell.Status == EnrichColumnCellStatus.Done)
    {
        store.Save(e.Row.Id, e.Cell);
    }
};
```

這一欄自己沒有資料列時，會播放它的 `Simulation`，也就是由你提供的表格；兩者都沒有時顯示空框架：頂列顯示「0 rows」，AI 欄沒有
名稱，沒有資料列，「Run column」無法使用。只有你自己的資料列而沒有 `Enrich` 時，資料列照常顯示，但不會執行任何內容。

### 模擬

`EnrichColumnSimulation` 是 `Rows` 為 null 時這一欄播放的表格：`Title`、`Column`、頁尾中的 `Model`、`Rows`，以及
`Answers`：每個資料列 id 對應一個 `EnrichColumnSimulationAnswer`，包含儲存格最後顯示的值和來源。它的 `Steps` 是儲存格在此之前
顯示的活動，每個持續各自的 `Duration`；帶有 `Error` 的答案在第一次嘗試時失敗，因而顯示失敗狀態和重試，重試後得到該值。這一欄
第一次進入檢視區時會自動執行它（`AutoRun`），在頂列（「sample data」）和螢幕報讀程式中說明這一點，並且不會傳送任何內容。你自己
設定的 `Title`、`Column` 和 `Model` 優先於模擬中的值；把 `Simulation` 設為 null 會停止執行並顯示空框架。

```csharp
table.Simulation = new EnrichColumnSimulation
{
    Title = "Target accounts",
    Column = new EnrichColumnDefinition("Headcount and funding", "Find the headcount and latest funding round"),
    Model = new EnrichColumnModel("Your model", "web search"),
    Rows = [new EnrichColumnRow("acme", "Acme Robotics", "acme.example")],
    Answers =
    [
        new EnrichColumnSimulationAnswer("acme", "410 people · Series C", new EnrichColumnSource("acme.example/press", "https://acme.example/press"))
        {
            Steps =
            [
                new EnrichColumnSimulationStep("Searching for Acme Robotics", TimeSpan.FromMilliseconds(700)),
                new EnrichColumnSimulationStep("Reading acme.example", TimeSpan.FromMilliseconds(900)),
            ],
            Error = "No source found", // the first attempt fails, so Retry shows
        },
    ],
};
```

本頁的示範從 `EnrichColumnDemoData` 取得表格：公司和數字都是虛構的，模型是真實的（核對於 2026-10-29）。

### 你的資料列與服務

<Demo name="YourData" />

內容物件包含儲存格開始時的提示詞和欄位定義、一個在使用者停止執行或重新執行整欄時觸發的 `CancellationToken`、嘗試次數（首次執行為 1，第一次重試為 2），以及 `SetActivity`：它設定執行中儲存格的微光標籤，可以在任何執行緒呼叫。把權杖傳給你的請求：被取消的儲存格會恢復為執行前的顯示，停止後才抵達的答案會被忽略。

### 結構

1. **頂列**：表格名稱（`Title`）、列數和 `Note`，以及欄位按鈕：未執行時為「Run column」，執行中為「Stop」，有值後為「Re-run」。
2. **表頭**：資料列欄的表頭，接著是 AI 欄：一個閃光圖示、欄名及其下方的提示詞，以及編輯控制項。
3. **編輯器**：在表頭下方展開，包含欄名和提示詞。「Save and run」套用修改並重新執行每一列；「Cancel」或 Escape 關閉它。
4. **資料列**：標誌（16px；資料列沒有 `Logo` 時顯示首字母）、名稱和網域，接著是儲存格：狀態圖示、內容和這一列的執行或重試控制項；控制項在指標停留、取得焦點和觸控螢幕上顯示。
5. **頁尾**：模型（`Model` 或模擬中的模型；為 null 時隱藏）和進度，以及失敗數。

### 狀態

| 狀態 | 圖示 | 內容 |
|---|---|---|
| `Idle` | 淺色虛線圓環 | 一條短橫線 |
| `Queued` | 時鐘 | 「Queued」 |
| `Running` | 載入圈 | 顯示微光的活動標籤 |
| `Done` | 自行描出的勾號 | 值（單行，停留時顯示全文）和來源連結 |
| `Failed` | 紅色警示圖示 | 「Failed」和原因 |

### 執行

`Concurrency`（預設 2）限制同時執行的儲存格數。各個工作者相隔 140ms 啟動，讓這一欄由上到下依序填入；每個工作者完成後從佇列取下一列。工作者在取列時讀取最新的資料列和欄位定義，因此執行中修改資料列不會作用於過時的資料：排隊期間被移除的資料列會被略過。

`Run()`、`Stop()` 和 `RunRow(id)` 與對應按鈕的作用相同。只要有儲存格在排隊或執行，`IsRunning` 就為 true。一次執行落定（完成或停止）時觸發 `RunCompleted`，並附上所有儲存格。

### 已儲存的值

<Demo name="SavedValues" />

把已有的值依資料列 id 透過 `InitialCells` 傳回。`AutoRun` 會在這一欄第一次捲入檢視區時執行一次，但僅限尚未有任何儲存格有值時。播放模擬時預設開啟，使用你自己的委派時預設關閉，除非你明確設定。`IsEditable="False"` 會隱藏編輯控制項。

### 窄版與長表格

<Demo name="Narrow" />

卡片填滿容器，設計寬度最大為 760px。寬度小於 560px 時，網域、來源文字和模型說明會收起；來源保留箭頭連結。小於 440px 時，左右留白收緊到 16px，值在 60px 高的資料列中換成兩行，失敗原因隱藏在標籤後面（停留或聚焦即可查看）。在觸控螢幕上，資料列控制項一律可見。

資料列是虛擬化清單。為卡片設定高度（或 `MaxHeight`）後，只會建立畫面上的資料列；未設定時顯示全部資料列。

### 自訂文案

`Labels` 包含這一欄顯示或朗讀的所有文字。替換整個物件即可在地化或改寫：

```csharp
table.Labels = new EnrichColumnLabels
{
    RowHeader = "Account",
    RunColumn = "Enrich",
    Rows = n => $"{n} accounts",
};
```

### 內容指引

- 值保持為簡短的一行（「240 people · Series B」）；儲存格會以刪節號截斷，停留時顯示完整的值。
- 為每個值提供使用者可以開啟的來源；可查證的連結才讓 AI 給出的值真正可用。
- 失敗原因要簡短具體（「No source found」、「Rate limited」）。

### 最佳實務

- 遵守取消權杖；對有速率限制的服務，把 `Concurrency` 設低一些。
- 在 `CellChanged` 中儲存值，下次透過 `InitialCells` 傳回。
- 資料列達到數百筆時，為卡片設定高度以啟用虛擬化，並維持較低的並行數。

### 常見問題

**使用者停止後儲存格會怎樣？** 它的請求被取消，並恢復為執行前的顯示：先前的值，或一條短橫線。

**儲存欄位定義會保留舊值嗎？** 每一列都會用新的提示詞重新執行；輪到之前顯示「Queued」。

**如何自行開啟來源？** 處理 `SourceRequested` 並設定 `Handled`；否則由系統開啟該網址。

## API 參考

<!-- api: EnrichColumn -->

<!-- api: EnrichColumnRow -->

<!-- api: EnrichColumnDefinition -->

<!-- api: EnrichColumnCell -->

<!-- api: EnrichColumnContext -->

<!-- api: EnrichColumnResult -->

<!-- api: EnrichColumnSource -->

<!-- api: EnrichColumnModel -->

<!-- api: EnrichColumnSimulation -->

<!-- api: EnrichColumnSimulationAnswer -->

<!-- api: EnrichColumnSimulationStep -->

<!-- api: EnrichColumnLabels -->

## 無障礙

- 各資料列構成一個表格：公司是每一列的列標題，AI 欄的名稱和提示詞是它的欄標題。
- 每種儲存格狀態都由圖示和文字搭配表示（Queued、微光活動、值、附原因的 Failed），從不只靠顏色。
- 禮貌模式的即時區域會播報開始執行、停止和最終數量，並在值是模擬資料、沒有傳送任何內容時說明。
- 圖示按鈕的標籤包含列名（「Run Lumen Freight」、「Retry Quarry Labs」）；來源連結會說明在新分頁中開啟。
- 所有文字都在 `EnrichColumnLabels` 中。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| Tab | 依序經過欄位按鈕、編輯控制項、每列的執行或重試控制項和來源連結。資料列控制項在取得焦點時出現 |
| Enter、Space | 執行、停止或重新執行整欄，執行一列，重試失敗的儲存格，或開啟來源 |
| Ctrl + Enter（⌘ + Enter） | 儲存編輯的欄位並重新執行 |
| Escape | 不儲存並關閉欄位編輯器，焦點回到編輯控制項 |

## 動效

| 變化 | 完整 | 減弱 | 關閉 |
|---|---|---|---|
| 儲存格狀態變化 | 在同一格線儲存格內交叉淡化一次：舊內容 100ms 淡出，新內容 160ms 淡入並上升 2px | 120ms 交叉淡化，不上升 | 立即 |
| 狀態圖示 | 120ms 交叉淡化，搭配無回彈的縮放（0.6 到 1） | 僅交叉淡化 | 立即 |
| 完成勾號 | 240ms 描出 | 直接完整顯示 | 直接完整顯示 |
| 執行標籤 | 平緩的微光，每 1.8s 掃過一次；新的活動文字在其中替換 | 靜止 | 靜止 |
| 載入圈 | 每 800ms 轉一圈 | 每 1.6s 轉一圈 | 靜止 |
| 編輯器展開或收合 | 臨界阻尼的高度彈簧（0.25s，無回彈） | 立即 | 立即 |
| 資料列控制項出現 | 160ms 淡入 | 120ms 淡入 | 立即 |

儲存格處理期間尺寸不會變化：列高固定，儲存格內容在同一位置替換。

## 相關

- `ToolCall`：代理發出的工具呼叫。
- `AgentStatus`：整個執行的即時狀態。
- `Table`：不含 AI、可排序、可選取的資料表格。
