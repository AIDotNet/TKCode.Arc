---
title: AI 側邊面板
summary: 與人們閱讀的頁面並排的助手：它會讀取整個頁面，回答關於頁面或選取範圍的問題，並以文字差異提出修改，由人們套用、捨棄或復原；頁面上會有掃過的動畫，並出現一個 "Undo" 膠囊。
source-hash: fd63b95acc6a
---

<Demo name="Preview" />

## 何時使用

AI 側邊面板適用於人們閱讀、有時也需要修改的頁面：規格、紀錄、知識庫文章。助手坐在頁面旁邊，讀取整個頁面，
而當頁面應該改變時，它不會在人們背後改寫，而是把修改顯示成一張卡片，由人們決定。

- **設計說明或規格**："Shorten the summary"（縮短摘要）會回傳一張卡片，內含以文字差異呈現的新文字；按下 "Apply"（套用）即寫入頁面。
- **含表格的紀錄或設定頁面**：要求新增一欄，會先以一個小表格呈現，並將新的儲存格標上色彩，之後才會有任何變更。
- **人們會詢問段落的說明頁面**：選取文字，按下 "Ask about selection"（詢問選取範圍），問題就會把該段落當作上下文。
- **必須由人們掌控的頁面**：每項修改都可以復原，沒有經過 "Apply"（套用）就不會套用任何東西。

## 何時不用

- **沒有頁面的對話**：使用 `AiChat`。
- **在原地改寫一小段文字**：行內編輯控制項比較合適；此面板是以區塊組成的頁面為對象。
- **不是由標題、段落與表格組成的頁面**：面板只呈現這三種區塊。較豐富的頁面請自行呈現，並在旁邊使用面板的對話串。
- **執行模型**：面板會詢問你的 `IAiPanelResponder`；它本身不會呼叫任何東西。

## 用法

`AiSidePanel` 衍生自 Avalonia 的 `TemplatedControl`，會填滿其容器，因此需要給它一個高度。設定 `Page`、`Responder`，以及用於空白對話串的 `Suggestions`。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:AiSidePanel Height="640" AssistantName="Helper" ModelName="Model X1" PanelWidth="360" />
</UserControl>
```

```csharp
var page = new AiPanelPage { Title = "Offline sync", Meta = "Design note, updated Mar 3" };
page.Blocks.Add(new AiPanelParagraph { Id = "summary", Label = "Summary", Text = "Offline sync is a planned change..." });
panel.Page = page;
panel.Responder = new MyResponder();
panel.ChangeApplied += (_, e) => Save(e.Page);
```

### 頁面

`AiPanelPage` 有標題、一行中繼文字，以及 `Blocks`：`AiPanelHeading`、`AiPanelParagraph` 或 `AiPanelTable`（欄與列）。
每個區塊都有 `Id` 與 `Label`（"Summary"、"Storage table"）；標籤會在選取範圍膠囊、修改卡片與預留位置上為區塊命名，並提供給螢幕閱讀器。
面板操作的是你給它的頁面物件：套用修改時，頁面中的區塊會被取代，因此你手上的頁面永遠是最新的。替換 `Page` 會重設對話串與復原紀錄。

### 回答

`IAiPanelResponder` 會接收問題、目前的頁面（包含所有已套用的修改）與選取範圍，並回傳一個 `AiPanelReply`：文字，以及選擇性的修改。

```csharp
public async ValueTask<AiPanelReply> RespondAsync(AiPanelRequest request, CancellationToken cancellationToken)
{
    var summary = (AiPanelParagraph)request.Page.Find("summary")!;
    return AiPanelReply.FromText(
        "Here is a shorter summary.",
        new AiPanelChangeProposal("summary", new AiPanelParagraph { Id = "summary", Label = "Summary", Text = shorter }));
}
```

在你的方法執行期間，面板會顯示 "Reading the page"（正在讀取頁面）；接著文字會以完整單字的方式出現，約每 50 毫秒釋出一步（每步四到七個詞，積壓很長時會加快追上）。
`AiPanelReply.FromText` 接受整段文字，面板會自行切成單字；若要真正的串流，請傳入 `IAsyncEnumerable<string>`。停止會取消權杖並保留已顯示的內容，附上 "Stopped"（已停止）的說明，且不出現卡片。
例外會顯示一張附有 "Retry"（重試）按鈕的錯誤卡片，重試會再次詢問同一個問題。

### 修改

回覆可以提議修改**一個區塊**：它的 id 與整個新區塊。面板會顯示一張卡片，包含標題（區塊的標籤與摘要，例如 "21 words, was 45"（21 個詞，原為 45 個）或 "Adds Sync column"（新增 Sync 欄））、以文字差異呈現的修改（被刪除的詞加上刪除線，新增的詞著色；表格會顯示新表格，且新的儲存格著色），以及 "Discard"（捨棄）與 "Apply"（套用）按鈕。

- **Apply**（套用）會把修改寫入頁面。頁面上的區塊會由左至右掃過一層色彩，舊文字淡出、新文字淡入，區塊高度以彈簧變化至新尺寸；區塊上方會出現一個 "Summary updated, Undo" 膠囊，焦點移至它。`ChangeApplied` 會附上已更新的頁面引發；請在那裡儲存。
- **Undo**（復原）會以同樣的掃過效果還原區塊，卡片顯示 "Undone"（已復原）；之後仍可再次套用。只有**最後一次**套用的修改可以復原，膠囊會一直保留到復原為止。
- **Discard**（捨棄）會讓卡片變淡並結束它；之後無法再套用。

數張卡片可以同時等待，並以任何順序套用。

<Demo name="Programmatic" />

### 選取範圍

在頁面上拖曳選取文字，選取範圍下方會出現一個 "Ask about selection"（詢問選取範圍）按鈕。按下它會清除選取範圍，在標題中加入一個 "Selection: Summary"（選取範圍：摘要）膠囊，並在該區塊左側加上一條線標示；輸入框會取得焦點，並顯示與該區塊相關的預留位置。
膠囊會保留給後續的問題，直到按下其 × 將它移除。選取範圍屬於開始選取的那個區塊；表格的選取範圍則以該表格命名。`SelectionAsked` 會回報它，`ClearSelection()` 則可從程式碼移除它。

### 開啟、關閉與窄容器

`IsOpen`（雙向）、`Open()` 與 `Close()` 可顯示與隱藏助手。停靠時，其寬度會在 `PanelWidth`（360）與零之間以彈簧變化，頁面欄位重新置中；關閉時，頁面右上角會出現一個附有助手名稱的膠囊，用以重新開啟。
在面板內按下 Escape 會關閉它，並將焦點返回該膠囊；開啟時焦點會進入輸入框。在比 `SheetBreakpoint`（640）更窄的容器中，助手會成為覆蓋在頁面上的底部面板（`SheetHeightRatio`，74%）：開啟期間頁面會變暗且無法互動；按下變暗的頁面或關閉鈕即可關閉。

<Demo name="Narrow" />

### 文案

所有文字都來自 `Labels`（`AiSidePanelLabels`）：要翻譯或改寫，替換這個實例即可；格式化的文字是函式。`AssistantName` 與 `ModelName` 填入標題；`AssistantMark` 會取代閃光圖示（可以是幾何圖形或影像）。

### 內容指引

- **區塊標籤**：人們會說出口的短名詞（"Summary"、"Pricing table"）。
- **建議**：最多三個，每個都是頁面允許助手執行的事（"Shorten the summary"）。
- **修改摘要**：讓面板自動產生；它們是數量，不是散文。
- **回答**：用一句話說明改了什麼；卡片會顯示如何改。

### 最佳做法

- 在 `ChangeApplied` 與 `ChangeUndone` 中儲存頁面，而不是每次回答都儲存。
- 給區塊穩定的 id；修改是以 id 指向其區塊的。
- 每次回答只提議一個區塊；人們可以再要求下一個。
- 讓回應器能快速開始：第一個回饋就是 "Reading the page"。

### 常見問題

**為什麼套用後我的資料沒有改變？** 它改變的是你給面板的 `Page` 物件；請在 `ChangeApplied` 中儲存它。

**為什麼只能復原最後一次的修改？** "Undo" 膠囊屬於最近一次套用的修改；套用另一項修改會取代它，較早的卡片仍會顯示 "Applied"（已套用）。

**助手能一次編輯數個區塊嗎？** 每張卡片一個區塊；請再要求下一個。

## API 參考

<!-- api: AiSidePanel -->

<!-- api: AiPanelPage -->

<!-- api: AiPanelHeading -->

<!-- api: AiPanelParagraph -->

<!-- api: AiPanelTable -->

<!-- api: AiPanelTurn -->

<!-- api: AiPanelChange -->

### 事件

| 事件 | 引發時機 | 引數 |
|---|---|---|
| `ChangeApplied` | 修改已套用，且頁面資料已更新 | `Change`, `Page` |
| `ChangeUndone` | 修改已復原 | `Change`, `Page` |
| `ChangeDiscarded` | 修改已捨棄 | `Change`, `Page` |
| `SelectionAsked` | 一個選取範圍成為下一個問題的上下文 | `Selection` |

### 回應器

| 類型 | 說明 |
|---|---|
| `IAiPanelResponder` | `RespondAsync(request, cancellationToken)` 回傳一個 `AiPanelReply` |
| `AiPanelRequest` | 提示、目前的頁面，以及選取範圍（若有） |
| `AiPanelReply` | 回答文字（以 `FromText` 整段提供，或以串流提供）與選擇性的修改 |
| `AiPanelChangeProposal` | 區塊的 id 與整個新區塊 |

## 無障礙

- 面板是以助手命名的區域；頁面與對話串是分開的區域。每個頁面區塊都以其標籤命名。
- 標題的上下文膠囊說明助手會讀取什麼；選取範圍膠囊是名為 "Remove selection"（移除選取範圍）的按鈕，以所選文字作為其描述。
- 一則禮貌的即時區域會宣告：助手正在讀取頁面、回答已就緒、已停止或失敗、已提出修改、已套用、已復原或已捨棄、已新增選取範圍，以及面板已開啟或關閉。串流的詞不會逐一宣告。
- 修改卡片的按鈕有名稱並回報其狀態；已套用的卡片不是按鈕。
- 焦點會移到人們接下來會看的地方：套用後移到 "Undo" 膠囊，復原後回到該卡片的 "Apply"，捨棄或開啟面板後進入輸入框，關閉後移到重新開啟的膠囊。
- 在底部面板模式中，面板開啟時其下方的頁面無法互動。
- 減少動態效果會保留每一種狀態與每一則宣告。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| `Tab` | 頁面控制項、標題、對話串（卡片、"Retry"）、"Undo" 膠囊、輸入框 |
| `Enter` 或 `Space` | 按下已聚焦的按鈕 |
| 輸入框中的 `Enter` | 送出；回答撰寫中按 `Escape` 則停止 |
| 面板其他地方的 `Escape` | 關閉助手，並將焦點移到重新開啟的膠囊 |

## 動態效果

- 停靠面板的寬度在開啟與關閉之間以彈簧變化；面板本體保持其寬度，隨邊緣滑動，頁面則重新置中。
- 串流的詞以每 50 毫秒一步、每步四到七個詞的節奏出現，每一步在 150 毫秒內淡入。
- 套用修改時，一層色彩會掃過區塊（約 420 毫秒）、停留，然後淡出（約 630 毫秒）；舊文字在 100 毫秒內淡出，新文字在 80 毫秒後以 180 毫秒淡入，高度以彈簧變化。
- 詢問按鈕、"Undo" 膠囊與重新開啟的膠囊會淡入與淡出。
- 底部面板會滑入並淡入。
- 減少動態效果會移除彈簧與位移：掃過效果變成整個區塊的色彩淡入淡出，面板以原地淡入呈現，詞保持其節奏但不淡入。關閉則立即套用每項變更。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 開啟與關閉 | 寬度彈簧 | 120 毫秒淡入 | 立即 |
| 套用或復原 | 掃過、交叉淡化、高度彈簧 | 整個區塊色彩淡入淡出，無位移 | 立即 |
| 新的詞 | 以整個詞為一步，150 毫秒淡入 | 相同的步進，無淡入 | 相同的步進，無淡入 |
| 底部面板 | 滑入並淡入 | 原地淡入 | 立即 |

## 相關元件

- `AiChat`：用於沒有頁面的對話。
- `AiComposer`：用於單獨的提示框。
- `PromptStudio`：用於編輯提示，而非頁面。

| 元件 | 適用情境 |
|---|---|
| `AiChat` | 沒有可讀取或修改的頁面 |
| `AiComposer` | 你只需要方框 |
| `PromptStudio` | 被編輯的是提示範本 |
