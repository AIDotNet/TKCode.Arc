---
title: Code suggestion
summary: AI 程式碼修改建議：模型提出的變更以行內差異區塊的形式串流進一個小型編輯器面板，由使用者逐一接受或拒絕，也可以用 Cmd 或 Ctrl + Enter 一次全部接受。
source-hash: 37c31166bfe2
---

<Demo name="Preview" />

## 何時使用

`CodeSuggestion` 在一個小型編輯器面板中，以行內差異的形式呈現 AI 對單一檔案提出的修改。標題列寫明模型與使用者的要求；面板顯示檔案，每處變更是一個差異區塊：刪除的行以紅色底並帶減號，新增的行以綠色底並帶加號。新行逐行串流進來，接著每個差異區塊下方會出現「拒絕」與「接受」，頁尾提供「全部拒絕」與「全部接受」。被接受的差異區塊會以平順的高度變化併入檔案。

- **編輯器中的行內修改**：模型改寫使用者正在開啟的檔案的一部分。
- **核准代理程式的檔案變更**：在寫入之前審閱，並清楚記錄保留了哪些內容。
- **設定或 SQL 變更預覽**：每一行變更都應在生效前被讀到。
- **形象頁面與文件**：用一個逼真、自動播放的修改來展示 AI 程式設計功能。

## 何時不用

- **橫跨多個檔案的大型差異或左右對照**：請使用專門的差異工具；這張卡片只適用於一個檔案和少量差異區塊。
- **沒有變更的程式碼**：請使用 `CodeBlock`。
- **不是檔案修改的代理程式步驟**：請使用 `ToolCall`。
- **一般性的動作核准**（電子郵件、付款、部署）：請使用核准卡片，而不是程式碼審閱。

## 用法

把檔案放進 `Code`，把變更放進 `Hunks`，並設定 `Status`。每個 `CodeSuggestionHunk` 從由零起算的第 `Start` 行開始，用它的 `Insert` 行取代 `Remove` 行（remove 為 0 時在 `Start` 之前插入，insert 為空時表示刪除）。與前面某個差異區塊重疊的差異區塊會被忽略。既沒有程式碼也沒有差異區塊時，卡片顯示一個空白檔案。

```xml
<arc:CodeSuggestion FileName="load.ts"
                    FilePath="src/api"
                    Instruction="Time out slow requests"
                    Code="export async function load(url: string) {&#10;  const res = await fetch(url);&#10;  return res.json();&#10;}">
    <arc:CodeSuggestionHunk Id="timeout" Start="1" Remove="1" Summary="Add a 10s timeout"
                            InsertText="  const res = await fetch(url, { signal: AbortSignal.timeout(10_000) });" />
</arc:CodeSuggestion>
```

```csharp
// 每個差異區塊都有了決定後再寫入檔案；卡片本身從不寫入任何內容。
private async void OnResolved(object? sender, CodeSuggestionResolvedEventArgs e)
{
    await File.WriteAllTextAsync(path, e.Code);
}
```

若想隨著使用者的操作逐步寫入，每接受或拒絕一個差異區塊都會觸發 `DecisionMade`，並附上檔案目前的內容。`GetCode()` 隨時回傳同樣的文字，`Accept`、`Reject`、`AcceptAll` 和 `RejectAll` 可在程式碼中做出決定。

### 結構

1. **標題列**：著色方塊中的模型標記、作為標題的使用者要求，以及其下方的模型名稱。
2. **檔案分頁**：兩個字母的語言標記、檔名、所在資料夾（卡片寬度達 520px 時顯示）以及即時的 +/− 計數。
3. **程式碼**：固定 20px 高的行，附行號；待定差異區塊的刪除行為紅色並帶減號，新增行為綠色並帶加號。待定的新增行還沒有行號。
4. **審閱列**：每個待定差異區塊下方，顯示它的摘要以及「拒絕」與「接受」。
5. **頁尾**：狀態（一個圖示加一段文字）以及動作：停止、全部拒絕與全部接受、重新產生或重試。

### 串流

<Demo name="Streaming" />

模型讀取檔案時把 `Status` 設為 `Thinking`，接著設為 `Streaming`，並隨著詞元抵達擴充 `Hunks`：先加入一個沒有行的差異區塊，再用 `AppendLine`（或取代 `Insert`）附加行。每個新行會展開到自己的行高並淡入。回答完整後把 `Status` 設為 `Ready`，此時每個差異區塊都會出現審閱列。回到 `Thinking` 會清除先前的決定。

`AllowsStop` 會在模型讀取或寫入時顯示「停止」，並引發 `StopRequested`：中止你的請求並設定狀態。`AllowsRegenerate` 會在審閱完成後顯示「重新產生」，出錯後顯示「重試」，並引發 `RegenerateRequested`。

### 狀態

| 狀態 | 程式碼 | 頁尾 |
|---|---|---|
| `Thinking` | 原始檔案 | 旋轉圖示和閃爍的 "Reading fetch-invoice.ts"，停止 |
| `Streaming` | 正在增長的差異區塊 | 旋轉圖示和 "Writing changes"，停止 |
| `Ready` | 附審閱列的差異區塊 | 圓點和 "3 changes to review"，全部拒絕和全部接受 |
| `Ready`，全部已決定 | 檔案目前的樣子 | 勾號和 "Applied 2 of 3 changes"，或叉號和 "Rejected all 3 changes"，重新產生 |
| `Error` | 原始檔案 | 警示圖示和 `ErrorText`，重試 |

### 窄卡片

<Demo name="Narrow" />

卡片會填滿容器，最寬 720px。長行在程式碼區域內水平捲動，而行號固定在左側，每個審閱列固定在可見寬度內，因此「接受」始終觸手可及。卡片窄於 520px 時隱藏資料夾。窄於 440px 時，行號欄變窄，程式碼縮小到 12px，差異區塊摘要隱藏（按鈕仍保留供螢幕閱讀器使用的名稱），頁尾把狀態疊放在滿版動作按鈕上方。最窄可到 320px。

### 長檔案

<Demo name="LongFile" />

`MaxCodeHeight` 限制程式碼區域的高度；更長的檔案會在其中捲動。`AreShortcutsEnabled="False"` 會關閉鍵盤快速鍵（此時「全部接受」不顯示提示），`Model="{x:Null}"` 會隱藏模型那一行。

### 模型與檔案

`Model` 接受一個 `AgentModel`：它的 `Name` 顯示在標題下方，它的 `Mark`（24 × 24 的描邊圖形）顯示在標題列的方塊中，顏色為 `MarkBrush` 或文字顏色。`FileName` 會依副檔名選擇醒目提示語言；函式庫的程式碼醒目提示器認得的語言都會上色，其他語言則以一般文字顏色顯示。`FileMark` 用你自己的內容取代兩個字母的語言標記，`ShowsFileMark="False"` 則會將其隱藏。

### 模擬

用於沒有模型的示範或原型時，給卡片一個 `CodeSuggestionSimulation`，傳入要播放的檔案與差異區塊，檔名、指令與模型照常設定在卡片上。卡片沒有自己的程式碼與差異區塊時就播放這段模擬：讀取檔案 0.8 秒，串流進差異區塊（三個約需 1.6 秒），接著自行接受與拒絕，結束約 2.6 秒後重播。卡片內的任何指標、按鍵或焦點都會把這一輪交給使用者控制；`Restart()` 會開始新的一輪。`AutoPlay="False"` 會保留串流過程，但把審閱留給使用者。設定 `Code` 或 `Hunks` 後模擬即結束。預覽中的模型是真實的模型，搭配中性的標誌，不會繪製任何品牌標誌。

```xml
<arc:CodeSuggestion FileName="greet.ts" Instruction="Greet by name">
    <arc:CodeSuggestion.Simulation>
        <arc:CodeSuggestionSimulation Code="export const greet = () =&gt; &quot;Hello&quot;;" />
    </arc:CodeSuggestion.Simulation>
</arc:CodeSuggestion>
```

```csharp
card.Simulation = new CodeSuggestionSimulation
{
    Code = recordedFile,
    Hunks = [new CodeSuggestionHunk("greet", 0, 1, "export const greet = (name: string) => `Hello, ${name}`;")],
};
```

### 自訂文字

`Labels` 包含卡片顯示或朗讀的每一個字詞。取代整個物件即可在地化或改寫：

```csharp
card.Labels = new CodeSuggestionLabels
{
    Accept = "Übernehmen",
    Reject = "Verwerfen",
    ToReview = count => $"{count} Änderungen offen",
};
```

### 減少動態效果

開啟減少動態效果時，卡片會直接顯示已完成的建議，沒有串流過程與自動播放，摺疊也會立即完成。決定仍會生效，狀態也照常更新。

### 內容準則

- 為每個差異區塊寫一個兩到六個字詞的摘要（"Add retry limits"）；它會成為螢幕閱讀器讀出的按鈕名稱。
- 讓差異區塊保持排序且互不重疊。從統一差異格式對應時，`Start` 是第一行刪除行（或插入位置）由零起算的行號，`Remove` 是刪除的行數，`Insert` 是新增的行。
- 內容是範例時，請在周圍的文字中說明。

### 最佳做法

- 在 `Resolved` 中寫入檔案，或在 `DecisionMade` 中逐步寫入；不要假設建議已經被套用。
- 以附加 `Insert` 的方式串流更新；不要在每個詞元抵達時重建差異區塊清單。
- 把 `StopRequested` 接到中止請求的邏輯上。

### 常見問題

**模型還在寫入時，使用者可以做決定嗎？** 不行：狀態變為 `Ready` 後才會出現審閱列。

**重疊的差異區塊會怎樣？** 觸及前面某個差異區塊已變更之行的差異區塊會被忽略。

## API 參考

<!-- api: CodeSuggestion -->

<!-- api: CodeSuggestionHunk -->

<!-- api: CodeSuggestionSimulation -->

<!-- api: CodeSuggestionLabels -->

<!-- api: CodeSuggestionDecisionEventArgs -->

<!-- api: CodeSuggestionResolvedEventArgs -->

## 無障礙

- 卡片是一個附標籤的群組（"Code suggestion: Add retry with backoff"），程式碼是以檔名命名的可聚焦區域。
- 在程式碼的描述中，每個待定的刪除行或新增行都以 "Removed:" 或 "Added:" 開頭，因此差異從不只依賴紅色與綠色底色；可見的減號與加號也為視力正常的讀者傳達了這項資訊。
- 「接受」與「拒絕」以所屬差異區塊命名（"Accept Retry 429 and 5xx with backoff"）。「全部接受」公開了它的快速鍵。
- 一個禮貌型即時區域會朗讀狀態（"3 changes to review"）、每個決定（"Accepted: Add retry limits. 2 changes to review"）以及最終結果。模擬還會加上 "Simulated, nothing was written to disk."。串流過程只朗讀一次，而不是每行一次。
- 狀態一律是圖示加文字：讀取與寫入時是旋轉圖示，審閱時是圓點，已套用是勾號，已拒絕是叉號，出錯是警示圖示。
- 被按下的控制項隨其差異區塊或狀態離開時，焦點不會落回頁面。指標聚焦不顯示焦點環；鍵盤聚焦使用 Arc 共用的焦點環。

### 鍵盤

| 按鍵 | 動作 |
|---|---|
| Tab | 先移到程式碼區域，再依序移到每個差異區塊的「拒絕」與「接受」，接著是頁尾動作 |
| Enter、Space | 按下取得焦點的接受、拒絕、全部接受、全部拒絕、停止或重新產生；一個差異區塊決定後，焦點移到下一個待定差異區塊的「接受」，沒有剩餘時移到程式碼區域 |
| Cmd 或 Ctrl + Enter | 焦點在卡片內時，接受所有待定差異區塊 |
| Cmd 或 Ctrl + Backspace | 焦點在卡片內時，拒絕所有待定差異區塊 |
| 左方向鍵、右方向鍵 | 程式碼區域取得焦點時水平捲動長行 |

## 動態效果

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 思考 | 約 0.8 秒，標籤上有平緩的閃爍（模擬） | 無閃爍，模擬不串流 | 立即 |
| 差異區塊出現 | 其刪除行在 240ms 內染成紅色 | 立即 | 立即 |
| 新行串流進來 | 在 120ms 內展開到 20px 行高並淡入，每 38ms 一行（模擬） | 立即 | 立即 |
| 審閱列出現 | 同時出現，使用臨界阻尼彈簧（0.24 秒，無回彈） | 立即 | 立即 |
| 接受 | 刪除行與審閱列以 settle 彈簧（0.26 秒）摺疊為零，下方程式碼隨之上移；綠色底色與加號在 240ms 內淡出 | 立即 | 立即 |
| 拒絕 | 新增行與審閱列以同樣方式摺疊；紅色底色淡出 | 立即 | 立即 |
| 頁尾狀態 | 圖示、文字與動作各自原地交叉淡化（淡出 100ms，淡入 160ms，文字上移 2px）；圖示只在種類變化時切換 | 短暫交叉淡化 | 立即 |

行高固定為 20px，摺疊在已知高度之間進行，因此動畫過程中不需要測量任何內容。每一行依文字只排版一次，所以串流進新行不會重新醒目提示其他行。模擬只使用單次計時器，卡片進入可視範圍後才開始，捲出可視範圍時暫停等待。

## 相關

- `CodeBlock`：呈現沒有變更的程式碼。
- `ToolCall`：不是檔案修改的代理程式步驟。
- `AgentPlan`：代理程式在修改任何內容之前寫下的計畫。
