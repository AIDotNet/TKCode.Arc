---
title: Ghost text
summary: 行內 AI 自動完成：淡色的幽靈文字在游標處接著把句子寫下去，Tab 原地接受，Cmd+Right 接受一個字詞，Escape 將其關閉，一個安靜的標籤顯示背後的模型。
source-hash: 90d90ae2b1e5
---

<Demo name="Preview" />

## 何時使用

`GhostText` 是一個帶有 AI 輔助、卻不打擾人的文字欄位。使用者停下來時，淡色的幽靈文字會剛好在游標處接著他們的句子寫下去。Tab 讓它原地變成正文，Cmd+Right 一次接受一個字詞，Escape 讓它淡出；沒有明確的按鍵，文字永遠不會被更動。末端的小標籤顯示背後的模型。

- **產品中需要長篇撰寫的欄位**：拉取請求說明、電子郵件、客服回覆、問題內文、版本說明、CRM 備註。
- **簡短的單行欄位**，可能的完成內容能省下打字，例如郵件主旨或提交訊息。
- **任何希望 AI 輔助等到停頓才出現**、且只在按鍵後才生效的地方。

## 何時不用

- **向助理傳送訊息的聊天輸入框**：請使用撰寫器（composer）。
- **逐段接受或拒絕的程式碼修改**：請使用程式碼建議檢視。
- **從已知選項中挑選**：請使用下拉式方塊或命令選單。
- **敏感欄位**（密碼、卡號、健康資料），除非你的模型供應商與資料政策允許。

## 用法

給欄位一個 `Suggest` 函式。它會收到完整文字（呼叫時游標一定在末端）與一個取消權杖，並回傳一個 `GhostTextResponse`：字串、`Task<string?>`，或以 `GhostTextResponse.FromStream` 包裝的分塊串流。回傳 `null` 表示略過這段文字，而且不顯示 Thinking。

```csharp
field.Suggest = (text, cancellationToken) =>
    GhostTextResponse.FromStream(StreamCompletion(text, cancellationToken));

async IAsyncEnumerable<string> StreamCompletion(string text,
    [EnumeratorCancellation] CancellationToken cancellationToken)
{
    using var response = await http.PostAsJsonAsync("/api/autocomplete", new { text }, cancellationToken);
    response.EnsureSuccessStatusCode();
    using var reader = new StreamReader(await response.Content.ReadAsStreamAsync(cancellationToken));
    var buffer = new char[256];
    int read;
    while ((read = await reader.ReadAsync(buffer, cancellationToken)) > 0)
    {
        yield return new string(buffer, 0, read);
    }
}
```

`Text` 是雙向的。去除前後空白的文字達到 `MinLength`（3）個字元，並安靜 `Delay`（450ms）之後，欄位才會發出請求。每次按鍵都會重新計時並取消進行中的請求，所以打字很快的人在停下之前不會觸發任何請求。沒有 `Suggest` 也沒有文字時，欄位就是一般的文字欄位，或是播放你傳入的模擬。

### 串流建議

<Demo name="Streaming" />

每個串流進來的分塊都會接在幽靈文字後面，並各自淡入。`Label` 在文字上方加一列安靜的標籤，`Footer` 是頁尾列開頭的內容。`Rows`（3）是以行計的最小高度，`MaxRows`（10）是欄位停止長高、開始捲動的高度。`Model` 提供模型名稱與 12px 的標誌；用 `AgentModel` 傳入你自己的圖形與顏色。

用於沒有模型的示範或原型時，給欄位一個 `GhostTextSimulation`，在 `Script` 中寫好各行：它會逐行輸入，短暫思考後以 3 到 6 個字詞的分塊串流出該行的 `Reply` 並接受，接受方式在 Tab 與逐字詞之間交替，然後從頭開始。點一下欄位即可把它交給使用者，使用者會得到劇本中的回覆，其他內容則由模擬自己的 `Suggest` 回答（預覽傳入了一個以規則運作的小替身，會補完字詞，遇到 "timeout" 時失敗）。標籤、預留位置文字、頁尾與模型仍設定在欄位上。設定 `Suggest` 或 `Text` 後模擬即結束。

```csharp
field.Label = "Commit message";
field.Simulation = new GhostTextSimulation
{
    Script = [new GhostTextSimulationLine("Bump the parser", " to fix the date bug.")],
};
```

### 單行

<Demo name="SingleLine" />

`IsMultiline="False"` 產生單行欄位：它會隨游標水平捲動，標籤保持在行尾。輸入或貼上的換行會變成空格。

### 精簡樣式

<Demo name="Plain" />

`Variant="Plain"` 拿掉邊框、背景與焦點環，讓文字平貼在你既有的卡片裡。請給那張卡片它自己的焦點樣式。

### 事件與方法

<Demo name="Events" />

`Accepted` 回報被接受的文字，以及接受的是整段建議還是一個字詞；`Dismissed` 回報以 Escape 關閉的內容。兩者都記錄下來，就能依模型與提示詞衡量接受率。`Status`、`Suggestion` 與 `IsStreaming` 隨時描述建議的狀況，`StatusChanged` 回報每一次變化。

`AcceptAll()`、`AcceptWord()`、`Dismiss()`、`Request()`（立即請求，不等停頓）與 `FocusField()` 可以從你自己的按鈕或測試中驅動欄位。

### 狀態

| 狀態 | 標籤顯示 | 文字 |
|---|---|---|
| `Idle` | 模型的標誌與名稱 | 只有你的文字 |
| `Loading` | 閃爍的 "Thinking" | 只有你的文字 |
| `Ready` | Tab 鍵帽與 "to accept"（觸控螢幕上為 "Tap to accept"） | 你的文字，後接幽靈文字 |
| `Error` | 警示圖示、"No suggestion" 與 "Retry" | 只有你的文字 |

按下 Tab 後，標籤會短暫顯示帶勾號的 "Accepted"，接著回到模型名稱。

### 撰寫建議

- 只請求接續的內容：不要開場白，也不要重複輸入。
- 文字在句子中間結束且末端沒有空格時，以空格開頭；文字末端已有空白時，欄位會拿掉開頭的空格。
- 控制在一句話或一短行以內，並使用快速的模型：大家期望停頓後約半秒內就看到幽靈文字。
- 對永遠不想補完的情境（程式碼區塊內、網址之後）回傳 `null`，Thinking 就不會一閃而過。

### 你自己的文字

`Labels` 存放欄位顯示或朗讀的每一個字。替換整個物件即可在地化或改寫：

```csharp
field.Labels = new GhostTextLabels
{
    Thinking = "Réflexion",
    ToAccept = "pour accepter",
    SuggestionAnnouncement = suggestion => $"Suggestion : {suggestion}. Tab pour accepter.",
};
```

### 常見問題

**在幽靈文字上繼續打字會重新請求嗎？** 不會。與幽靈文字開頭相同的字元會原地消耗它；其他任何輸入都會清除它，並在下一次停頓後請求新的建議。

**為什麼點一下幽靈文字就不見了？** 建議只從文字末端接續。把游標移離末端會清除建議；在末端再次停頓後它就會回來。

## API 參考

<!-- api: GhostText -->

<!-- api: GhostTextResponse -->

<!-- api: GhostTextLabels -->

<!-- api: GhostTextSimulation -->

<!-- api: GhostTextSimulationLine -->

## 無障礙

- 欄位是原生文字方塊，由 `AutomationProperties.Name` 或字串形式的 `Label` 命名，並以一句話描述 Tab、Command 或 Control 加向右鍵以及 Escape 的作用（`IsHintVisible` 會連同標籤一起關閉它）。
- 幽靈文字只是文字的繪製方式，從不屬於文字本身，因此不會被當成內容朗讀。一個禮貌的即時區域會朗讀完成的建議（"Suggestion: date the plan changed. Press Tab to accept."），之後是 Suggestion accepted、Suggestion dismissed 或 Couldn't get a suggestion。`Notice` 會在同一個區域加入一次性的訊息。
- 標籤是一個按鈕，每種狀態都有清楚的名稱（Accept suggestion、Retry suggestion）。只有在錯誤狀態下 Tab 才會到達它，這時 Retry 就是它的動作；就緒狀態下 Tab 本身就是接受。按下它永遠不會搶走欄位的焦點。
- 狀態從不只靠顏色表達：Thinking、帶 "to accept" 的 Tab 鍵帽、帶勾號的 Accepted 與帶警示圖示的 No suggestion 都有文字。
- 鍵盤焦點會在整個欄位外圍畫出 Arc 的焦點環；指標焦點不會。

### 鍵盤

| 按鍵 | 動作 |
|---|---|
| Tab | 接受整段建議，游標留在末端。沒有建議時，Tab 照常移動焦點 |
| Cmd+Right、Ctrl+Right | 接受下一個字詞，其餘保留。Option+Right 與 Alt+Right 也可以。沒有建議時，這些鍵照常移動游標 |
| Escape | 關閉建議。沒有建議時不處理 Escape，外層對話方塊仍可關閉 |
| 打字 | 與幽靈文字開頭相同的字元會原地消耗它；其他輸入會清除它 |
| 方向鍵、點選 | 把游標移離末端會清除建議 |

## 動效

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 分塊串流進來 | 在 140ms 內淡入，只改透明度 | 立即 | 立即 |
| Escape、離開欄位 | 幽靈文字在 110ms 內淡出 | 立即 | 立即 |
| 輸入不同的字元 | 幽靈文字立即消失 | 立即 | 立即 |
| Tab、Cmd+Right | 被接受的文字在 200ms 內原地從幽靈墨色過渡到正文墨色；其餘幽靈文字不會再次淡入 | 立即 | 立即 |
| 文字變多或變少 | 高度以 180ms 補間跟隨，移動時裁切，從不回彈 | 立即 | 立即 |
| 標籤狀態 | 舊內容 100ms 淡出、新內容 160ms 淡入，寬度以錨定右緣的 200ms 補間跟隨 | 短暫交叉淡化 | 立即 |
| 鍵帽 | 對應按鍵或方法觸發時按下 160ms | 立即 | 立即 |
| Thinking | 安靜的閃光 | 靜止 | 靜止 |

完整動效下模擬會循環播放；減少或關閉動效時只播放一次就停止。它在螢幕外時會暫停。

寬度小於 520px 時，字詞與 Escape 提示會讓開，只保留標籤，頁尾備註以刪節號截斷。

## 相關

- `Textarea` 與 `Input`：一般的文字欄位。
- `MentionInput`：訊息中的 @ 提及。
- `TextShimmer`：Thinking 狀態的安靜閃光。
- `ToolCall` 與 `AgentStatus`：AI 回覆的其餘部分。
