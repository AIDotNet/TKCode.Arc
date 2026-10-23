---
title: 提示詞工作室
summary: 用彩色變數膠囊編輯提示詞範本的卡片，可用已儲存的測試案例試跑，並比較與上線版本：包含版本與模型選單、變數欄位、執行按鈕，以及顯示回覆的輸出面板。
source-hash: da3db15ce44c
---

<Demo name="Preview" />

## 何時使用

提示詞工作室給維護 AI 功能背後提示詞的人使用，而不是給該功能的終端使用者。它把整個循環放在同一張卡片上：編輯範本、用已儲存的測試案例試跑、查看版本之間改了什麼，並將其中一個版本上線。

- **AI 功能的管理頁面或設定頁面**：客服工單系統的回覆提示詞，或文件工具的摘要提示詞。
- **提示詞變更的審查**：上線前，把開啟的版本與另一個版本以文字差異比較。
- **用真實範例試用提示詞**：已儲存的測試案例會填入變數，「Run」會顯示回覆及其時間、權杖與費用。
- **在提示詞旁選擇模型與推理強度**：附價格，讓變更的成本一目了然。

## 何時不用

- **與助理的聊天**：改用 `AiChat`。
- **程式碼或自由文字編輯器**：範本編輯器只認得一種標記，即兩對大括號中的變數。
- **用數百個案例評估提示詞**：卡片一次只執行一個測試案例。
- **儲存提示詞**：卡片編輯草稿並告訴你；儲存、版本與上線由你的程式碼負責。

## 用法

`PromptStudio` 衍生自 Avalonia 的 `TemplatedControl`。提供 `Versions`、`Variables`、`TestCases` 與 `Models`，以及一個 `RunHandler`；沒有處理常式時，內建的模擬器會回答，卡片並會說明這是模擬。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:PromptStudio Title="Refund reply" MaxWidth="660" />
</UserControl>
```

```csharp
studio.Versions.AddRange(versions);                 // newest first
studio.Variables.AddRange(["buyer_name", "ticket_id"]);
studio.TestCases.AddRange(testCases);
studio.Models.AddRange(models);
studio.OpenVersion = studio.Versions[0];
studio.ProductionVersion = studio.Versions[0];
studio.RunHandler = async IAsyncEnumerable<string> (request, cancellationToken) => { /* yield text */ };
```

### 範本

範本有一則系統訊息與一則使用者訊息（「System」與「User」分頁）。變數以兩對大括號書寫，例如 `{{buyer_name}}`，在編輯器中顯示為只帶名稱的彩色膠囊。
每個變數依 `Variables` 的順序取四種系列色之一；文字中找到但未列出的名稱，會接在後面加入。

- 輸入兩個左大括號會開啟目前已知變數的清單，它會顯示在插入點處：上下鍵移動、Enter 或 Tab 插入、Escape 關閉，輸入時會篩選；最後一列會把你輸入的名稱加為新變數。輸入名稱加上兩個右大括號也會建立膠囊，貼上時大括號內含空白的文字會被正規化。
- 膠囊是一個整體：插入點會跳過它，Backspace 或 Delete 會整個刪除，複製時會寫出大括號與名稱。
- 復原與重做會還原膠囊；輸入會依約一秒的停頓與空白分組為步驟。
- 按下膠囊會聚焦該變數的欄位。

`Draft` 是正在編輯的範本（雙向繫結），`TemplateChanged` 會在每次編輯後觸發；請在那裡儲存。編輯器的右上角會顯示所見部分的權杖估計；設定 `TokenEstimator` 即可使用你自己的分詞器。

### 變數與測試案例

每個變數都有一個欄位，前面是一個與其顏色相同的圓點，以及標籤。聚焦欄位會醒目提示編輯器中的膠囊，每次按鍵都會閃爍，而空值的膠囊會以虛線外框呈現。選擇測試案例會把值重設為該案例自己的值；你輸入的值會被捨棄。`Values` 保存目前的值。

<Demo name="Minimal" />

### 執行

Run（或在卡片中任何位置按 Ctrl+Enter；在 Mac 上為 Cmd+Enter）會以值渲染範本並呼叫 `RunHandler`，它以文字串流的形式回傳回覆。輸出面板先顯示 "Thinking"，接著是 "Writing"，回覆會以整個字逐步出現，最後是 "Reply"，其下方的註腳例如 "1.57 s, 245 tokens, $0.0014"；費用來自模型每百萬權杖的 `InputPrice` 與 `OutputPrice`，若沒有價格則省略。執行中按鈕會變成 "Stop"；停止會保留已寫出的內容。
處理常式擲出例外時，會顯示一個帶有 "Retry" 的錯誤方塊（`RunFailed` 可讓你改寫訊息）。同一時間只會進行一次執行：新的執行或 Stop 會取消前一次執行的取消權杖。`RunState`、`LastResult`、`RunStarted` 與 `RunCompleted` 提供其餘資訊。

### 版本

版本膠囊（"v4, Production"）會開啟版本選單：每個版本都附其作者、提交訊息與距今時間，並提供兩個作用於所選版本的動作。

- **Compare**（比較）會把編輯器變成所開啟版本與所選版本之間的文字差異：新增的字會上色，刪除的字會加上刪除線，並顯示例如 +17 −1 的計數，以及一個 "Back to editing" 按鈕；分頁上的圓點會顯示哪個部分有變動。`IsCompareMode` 與 `CompareVersion` 為雙向繫結，Escape 會離開。
- **Promote to production**（上線）會把 Production 標記移到所選版本。`ProductionVersion` 立即變更，並觸發 `Promoted`，其中帶有該版本與前一個版本；設定 `Cancel` 即可拒絕，此時工作室會回復。選單中的 Undo 會把兩者換回去，並以 `IsUndo` 再次觸發 `Promoted`。

<Demo name="Compare" />

### 模型與推理強度

模型膠囊會開啟 `Models` 的選單，列出每百萬權杖的價格（輸入與輸出），以及一列推理強度。選擇模型會把推理強度重設為該模型的預設值，並讓選單保持開啟；標示為範例的價格會在底部註明。

### 文案

所有字都來自 `Labels`（`PromptStudioLabels`）：替換整個實例即可翻譯或改寫；帶格式的文字是函式。`Now` 設定相對時間（例如 "last week"）的計算基準時刻。

### 文案規範

- **變數名稱**：小寫，以底線分隔，名稱要能說明應填入的內容（`buyer_name`）。
- **測試案例**：簡短的標籤（最多三到四個字）；它們共用同一個分段控制項。
- **提交訊息**：用幾個字說明改了什麼。
- **價格**：註明它是範例。

### 最佳做法

- 在 `TemplateChanged` 中儲存，並在你的伺服器拒絕上線時以 `Cancel` 回復上線。
- 把取消權杖傳入你的執行處理常式，讓 Stop 真的會停止。
- 保留涵蓋提示詞邊界情況的測試案例，例如缺少某個值的情況。
- 上線前先比較：差異就是審查。

### 常見問題

**為什麼 Run 會顯示 "simulated"？** 沒有設定 `RunHandler`，所以是內建的模擬器在回答。

**為什麼膠囊是虛線？** 它的變數在目前的測試案例中沒有值。

**我可以使用自己的變數語法嗎？** 卡片使用兩對大括號。

## API 參考

<!-- api: PromptStudio -->

<!-- api: PromptVersion -->

<!-- api: PromptAuthor -->

<!-- api: PromptTestCase -->

### 事件

| 事件 | 觸發時機 | 引數 |
|---|---|---|
| `TemplateChanged` | 範本被編輯 | `Template` |
| `RunStarted` | 執行開始 | `Request` |
| `RunCompleted` | 執行結束：完成、停止或失敗 | `State`, `Result` |
| `RunFailed` | 處理常式擲出例外；變更 `Message` 可改寫錯誤訊息 | `Exception`, `Message` |
| `Promoted` | 版本已上線或上線已撤銷；設定 `Cancel` 即可回復 | `Version`, `Previous`, `IsUndo` |
| `VariableAdded` | 從編輯器新增了變數 | |

### 執行

| 型別 | 說明 |
|---|---|
| `PromptRunHandler` | `(request, cancellationToken)` 以 `IAsyncEnumerable<string>` 回傳回覆 |
| `PromptRunRequest` | 原始與渲染後的範本、值、模型、推理強度、測試案例 id 與版本 id |
| `PromptRunResult` | 文字、經過時間、估計的權杖、費用，以及是否為模擬 |
| `PromptTemplate` | 系統訊息與使用者訊息 |

## 無障礙

- 卡片是一個以提示詞命名的區域。分頁是分頁列表；變數欄位、測試案例與輸出面板是具名群組。
- 編輯器對輔助技術而言是一個文字方塊：膠囊會以名稱加上大括號被朗讀，變數清單會宣布符合的名稱數量。
- 版本與模型膠囊是按鈕，會回報它們會開啟對話框；版本是單選項；Production 版本會被明確標示為 Production。
- 一個禮貌的即時區域會宣布：執行已開始、回覆已就緒、已停止或失敗（並附原因）、已新增變數、版本已上線或上線已撤銷、比較已開啟，以及回覆已複製。串流中的字不會逐一宣布。
- 狀態從不只靠顏色表達：虛線膠囊也列在缺少值的清單中，錯誤有圖示與一句說明，Production 則以文字標示。
- 減少動態效果會保留每一種狀態與每一則宣布。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| `Ctrl+Enter` / `Cmd+Enter` | 從卡片任何位置執行提示詞 |
| `Tab` | 膠囊、分頁、編輯器、變數欄位、測試案例、Run、Copy |
| 分頁與測試案例上的 `ArrowLeft` / `ArrowRight` | 移動並選取 |
| 編輯器中輸入兩個左大括號 | 開啟變數清單 |
| 清單中的 `ArrowUp` / `ArrowDown` | 移動；會循環 |
| 清單中的 `Enter` 或 `Tab` | 插入變數 |
| 膠囊旁的 `Backspace` / `Delete` | 整個刪除 |
| `Ctrl+Z`、`Ctrl+Shift+Z` | 復原與重做，包含膠囊 |
| `Escape` | 關閉清單或選單；在比較檢視中，回到編輯 |

## 動態效果

- 版本與模型膠囊以彈簧動畫取得所需的寬度，並交叉淡入淡出其標籤；分頁的醒目提示與測試案例的醒目提示以快速彈簧滑動。
- 上線標記在版本選單的列之間滑動；選單從其膠囊長出來。
- 輸出面板會以彈簧跟隨其內容，並在重新執行時保留舊的高度；回覆以整個字的方式出現，每個步驟在 150 ms 內淡入。
- 編輯欄位時膠囊會閃爍；權杖計數的數字會滾動。
- 減少動態效果會移除彈簧與位移，並保留文字的出現節奏但不淡入；關閉動態效果時，所有變化立即套用。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 膠囊標籤與寬度 | 交叉淡入淡出，寬度以彈簧變化 | 120 ms 交叉淡入淡出 | 立即 |
| 回覆的字 | 整個字，150 ms 淡入 | 相同步驟，不淡入 | 相同步驟，不淡入 |
| 輸出面板高度 | 彈簧，重新執行時保留 | 立即 | 立即 |
| 膠囊閃爍 | 短暫脈衝 | 無 | 無 |

## 相關元件

- `AiSidePanel`：用於頁面旁的助理。
- `ModelPicker`：用於只需要選擇模型的情況。
- `TokenTextEditor`：範本編輯器所建構於其上的標記編輯器。

| 元件 | 適用情境 |
|---|---|
| `AiSidePanel` | 被編輯的是一頁內容 |
| `ModelPicker` | 只需要選擇模型 |
| `TokenTextEditor` | 你需要在文字編輯器中使用自己的標記 |
