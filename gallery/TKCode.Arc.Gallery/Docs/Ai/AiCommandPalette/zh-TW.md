---
title: AI command
summary: 具備 AI 模式的 Cmd+K 命令面板：搜尋頁面與動作，或按 Tab 提問，回答就地串流呈現，附有編號來源，以及可直接在清單中執行的後續步驟。
source-hash: 5fdd634d1cb1
---

<Demo name="Preview" />

## 何時使用

`AiCommandPalette` 是一個也能回答問題的命令面板。輸入內容會搜尋你的頁面和動作；按 Tab 會把查詢變成向模型提出的問題，
回答在面板內串流呈現，並附有編號來源。模型建議的後續步驟（例如建立議題或開啟文件）會以清單列的形式出現，按 Enter 就地執行。

- **全域 Cmd+K**：在一個使用者既要跳到頁面、又要詢問工作區狀況的產品中。
- **找不到結果時改為回答的搜尋**：沒有符合項目時，最後一列會針對該查詢詢問模型。
- **引導到其他工具中動作的回答**，例如建立議題或把摘要發到頻道。

## 何時不用

- **帶有追問的長篇對話**：請使用聊天串；面板一次只回答一個問題。
- **沒有 AI 的一般導覽**：請使用 `CommandPalette` 區塊。
- **在表單欄位輸入時的建議**：請使用自動完成輸入框。

## 用法

把你的頁面和動作以 `AiCommandPaletteItem` 交給面板，並用 `Responder` 回答問題。兩者都沒有提供時，它是一個空的面板，提問也不會得到回答。`ModelName` 預設為 null。

```csharp
palette.ItemsSource = commands;
palette.ItemSelected += (_, e) => Navigate(e.Item.Id);
palette.Responder = async context =>
{
    var reply = await api.AskAsync(context.Question, context.CancellationToken);
    context.SetSources(reply.Sources);          // the chips land first
    await foreach (var delta in reply.Stream)
    {
        context.Append(delta);                   // cite sources as [1], [2]
    }
    return new AiCommandPaletteAnswer
    {
        Actions = [new AiCommandPaletteAction("issue", "Create issue in Linear", "ENG", ArcIcons.CircleDot,
            token => CreateIssueAsync(context.Question, token))],
    };
};
```

### 你自己的頁面和動作

<Demo name="Wired" />

每個項目都有一個標籤、一個用來替結果分區的選用 `Group`、一個 16px 的 `Icon`、低調的 `Meta` 文字、以鍵帽顯示的 `Shortcut`，
以及額外的 `Keywords`。標記為 `IsSuggested` 的項目會在查詢為空時列出。搜尋是同步的逐字比對：查詢中的每個字詞都必須是標籤、
分組、附註或關鍵字中某個字詞的開頭，因此適合幾百列的規模；超過時請在伺服器端篩選。選取一列會引發 `ItemSelected`，並執行該項目的 `Command`。

### 提問

`Responder` 會收到一個 `AiCommandPaletteAskContext`，其中包含問題和一個取消權杖。用 `Append` 以任意大小的片段串流輸出文字，
並在引用來源的文字之前呼叫 `SetSources`。最後傳回一個 `AiCommandPaletteAnswer`，其中包含建議的動作（如果沒有串流輸出，
還要包含完整的 `Text`）。擲回例外狀況會顯示失敗狀態和一列「Try again」。當使用者再次提問、返回搜尋或面板離開視窗時，權杖會被取消。

引用在文字中寫成 `[1]`、`[2]`，並會變成小按鈕；滑鼠停留或聚焦其中一個會點亮對應的來源標籤，反之亦然。選取任一個都會引發
`SourceSelected`；帶有 `Href` 的來源會用系統啟動器開啟，除非你處理了該事件。

### 建議的動作

每個 `AiCommandPaletteAction` 都就地執行：該列先顯示載入圈和執行中的標籤，接著顯示只繪製一次的勾號和 `Run` 傳回的標籤
（「Created ENG-2481」），或顯示警示圖示和錯誤標籤，這時按 Enter 會重試。`ActionCompleted` 會回報結果。設定 `DoneMeta`
說明發生了什麼，例如模擬的動作沒有送出任何內容。

### 作為對話方塊

<Demo name="Dialog" />

`AiCommandPaletteDialog` 會把面板顯示在頁面上方、靠近視窗頂端，並加上調暗的遮罩。Cmd+K（其他平台為 Ctrl+K）可在視窗任何位置
切換它，`IsOpen` 是雙向的，按鈕上的 `arc:AiCommandPaletteDialog.Opens` 可以開啟它。在空白搜尋中按 Escape、點按遮罩或選取頁面
都會關閉它。把你自己的面板放進去：

```xml
<arc:AiCommandPaletteDialog IsOpen="{Binding IsPaletteOpen}">
    <arc:AiCommandPalette ItemsSource="{Binding Commands}" />
</arc:AiCommandPaletteDialog>
```

內嵌使用時，`IsElevated` 會加上浮層陰影，`IsHotkeyEnabled` 會讓 Cmd+K 或 Ctrl+K 聚焦輸入框。在空白搜尋中按 Escape 會引發
`CloseRequested`。

### 窄容器

<Demo name="Narrow" />

面板會填滿容器，最寬 680px，窄到 360px 也能正常使用。過長的標籤和附註會以刪節號截斷，附註先截斷；寬度低於 540px 時，
頁尾的導覽提示會隱藏，來源標籤會換行。`ResultsHeight` 設定結果區域的高度（預設 392），在輸入、切換模式或串流輸出時都不會改變。

### 你自己的用語

`Labels` 包含面板顯示或朗讀的所有文字，包括預留位置文字、「Searching your workspace」狀態和頁尾提示。`ModelName` 和
`ModelMark` 會在頁尾、輸入框和每則回答旁標示所用的模型。

### 模擬工作區

用於沒有後端的示範或原型時，給面板一個 `AiCommandPaletteSimulation`：它的 `Items`、`Questions` 和預設的 `Answers` 會補上面板缺少的部分。模擬模型會選出問題中提到其 `Keywords` 最多的回答（沒有關鍵字的回答接住其餘問題），「搜尋」700ms，放出來源並串流輸出文字；它的動作只是假裝執行，並顯示「Simulated, nothing was sent」。面板本身既沒有項目也沒有 responder 時，還會在畫面上可見時播放示範：搜尋 `SearchQuery`，開啟 Ask AI，提出第一個問題並執行第一個動作；在面板內第一次真正的點按或按鍵會永久停止示範。

```csharp
palette.ModelName = "Your model";
palette.Simulation = new AiCommandPaletteSimulation
{
    Items = [new AiCommandPaletteItem("inbox", "Inbox", "Pages", ArcIcons.Inbox) { IsSuggested = true }],
    Questions = ["What changed this week?"],
    SearchQuery = "in",
    Answers =
    [
        new AiCommandPaletteSimulationAnswer
        {
            Sources = [new AiCommandPaletteSource("notes", "Release notes")],
            Text = "Saved views shipped on Tuesday [1].",
            Actions = [new AiCommandPaletteSimulationAction("open", "Open release notes", "Docs", ArcIcons.FileText, "Opened")],
        },
    ],
};
```

```csharp
palette.Labels = new AiCommandPaletteLabels
{
    SearchPlaceholder = "Rechercher des pages et des actions",
    AskMode = "Demander à l’IA",
    Thinking = "Recherche dans votre espace",
};
```

### 最佳做法

- 項目標籤保持簡短，把脈絡放進 `Meta`；用 `Group` 替結果分區。
- 先傳回來源再傳回文字，讓來源標籤先出現，並在文字中引用它們。
- 為每個動作提供一個遵守權杖的 `Run`，並把所建立物件的名稱作為完成標籤傳回。

## API 參考

<!-- api: AiCommandPalette -->

<!-- api: AiCommandPaletteDialog -->

<!-- api: AiCommandPaletteItem -->

<!-- api: AiCommandPaletteSource -->

<!-- api: AiCommandPaletteAction -->

<!-- api: AiCommandPaletteAnswer -->

<!-- api: AiCommandPaletteAskContext -->

<!-- api: AiCommandPaletteLabels -->

<!-- api: AiCommandPaletteSimulation -->

<!-- api: AiCommandPaletteSimulationAnswer -->

<!-- api: AiCommandPaletteSimulationAction -->

## 無障礙

- 焦點始終留在輸入框中，輸入框會被讀成下拉式方塊；各列是清單項目，反白的那一列會回報自己處於選取狀態，因此方向鍵永遠不會把焦點移出輸入框。
- 一個禮貌型即時區域會播報問題、完成的回答、動作結果和模擬執行（「Simulated, nothing was sent」）。
- 狀態從不只靠顏色表達：執行中、已完成或失敗的動作會同時改變圖示和標籤。
- 來源標籤和引用都是按鈕，以來源的完整說明命名；滑鼠停留或聚焦其中一個會點亮與它對應的另一個。
- 模式切換是一對帶有按下狀態的按鈕，不會從輸入框搶走焦點。

### 鍵盤

| 按鍵 | 動作 |
|---|---|
| Up、Down | 在結果、問題或建議的動作之間移動（到底時循環） |
| Enter | 開啟頁面、執行動作、提出問題，或重試失敗的回答或動作；在 Ask AI 中有文字時提出該問題 |
| Tab | 針對目前的查詢詢問 AI；查詢為空時開啟附有建議問題的 Ask AI，在那裡按 Tab 會返回搜尋 |
| Escape | 從 Ask AI 返回搜尋（取消串流中的回答）；在搜尋中先清除查詢，再引發 `CloseRequested` |
| Cmd+K、Ctrl+K | 開啟 `IsHotkeyEnabled` 時聚焦輸入框；切換 `AiCommandPaletteDialog` |

## 動態效果

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 檢視（結果、問題、回答） | 舊檢視在 100ms 內淡出，新檢視在 160ms 內淡入並上升 2px | 120ms 淡入淡出 | 立即 |
| 列與列之間的反白 | 130ms 滑動；清單本身改變時直接跳到定位、不移動 | 立即 | 立即 |
| 回答文字 | 每 34ms 出現 3 到 7 個字詞，每批在 140ms 內淡入；檢視會跟著文字往下，除非你往上捲動 | 每 240ms 最多 24 個字詞，不淡入 | 與減少相同 |
| 來源標籤、建議的動作 | 160ms 淡入 | 立即 | 立即 |
| 動作標籤、狀態圖示、頁尾提示 | 一次交叉淡化；完成的勾號只繪製一次 | 短暫淡入淡出 | 立即 |
| 示範 | 可見時播放，不可見時暫停，回來後從乾淨的狀態重新開始 | 從不播放 | 從不播放 |

面板的尺寸從不改變：結果區域在每個檢視中都維持同樣的高度。

## 相關元件

- `CommandPalette`（區塊）：不含 AI 的命令選單。
- `ToolCall`：代理程式發出的工具呼叫。
- `AgentStatus`：執行中代理程式的即時狀態。
