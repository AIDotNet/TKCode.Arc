---
title: Reply draft
summary: 收到的郵件下方的 AI 回覆起草器：快速回覆引導一份新的串流草稿，語氣與長度以一次交叉淡化改寫它，讀者在插入前可以編輯。
source-hash: 19e28ee0c566
---

<Demo name="Preview" />

## 何時使用

`ReplyDraft` 顯示一封收到的郵件，並在其下方提供 AI 回覆。模型以快速、柔和淡入的片段串流寫出草稿；快速回覆標籤引導一份新的草稿，語氣與長度兩組選項以一次交叉淡化改寫已完成的草稿，讀者可以在按下 Insert 之前就地修改某個字詞。Insert 把文字（包含讀者的修改）交給你的編輯器：卡片本身從不寄出任何內容。

- **用模型起草回覆**：回覆郵件、工單或訊息，由人審閱並編輯後再寄出。
- **提供意圖以及語氣與長度選項**：用於收件匣、客服台或 CRM 中產生的文字。
- **在登陸頁展示 AI 寫作功能**：一個會自動播放的逼真範例。

## 何時不用

- **與模型自由對話**：使用聊天串與輸入框；這裡的草稿只回覆一則訊息。
- **輸入時的行內自動完成**：在游標處使用幽靈文字，而不是獨立的草稿框。
- **寄出郵件**：此卡片只插入草稿，寄出由你的編輯器負責。

## 用法

把訊息、你的快速回覆以及一個串流輸出模型回覆的產生器交給卡片。每個新操作（標籤、語氣或長度的變更、Regenerate）都會取消上一個請求的權杖，所以請把它傳給你的用戶端。

```csharp
draft.Message = new ReplyDraftMessage
{
    FromName = email.From.Name,
    FromEmail = email.From.Address,
    Subject = email.Subject,
    Body = email.PlainText,          // blank lines split paragraphs
    ReceivedAt = email.Received,
};
draft.Suggestions = [new ReplyDraftSuggestion("yes", "Sounds good"), new ReplyDraftSuggestion("later", "Next week")];
draft.Model = new AgentModel("Mistral Medium 3.5");
draft.Generator = request => replies.StreamAsync(email.Id, request.Tone, request.Length,
    request.Suggestion?.Id, request.Mode, request.Previous, request.CancellationToken);
draft.InsertHandler = async args => await composer.InsertAsync(args.Draft);
```

你自己的郵件如果沒有 `Generator`，就無法起草：草稿框顯示提示行，「Draft reply」按鈕處於關閉狀態。既沒有 `Message` 也沒有 `Simulation` 時，卡片是空的：一封空白郵件，沒有快速回覆，顯示「No reply drafted yet.」，「Draft reply」按鈕處於關閉狀態。

### 模擬

想在沒有模型的情況下展示卡片（示範、設計審查、新手導覽），就給它一個 `ReplyDraftSimulation`：郵件、快速回覆、要標示的模型，以及你為每個快速回覆、語氣和長度寫好的回覆（`ReplyDraftVersion`）。卡片會在短暫的閱讀之後串流輸出相符的回覆，說明這是模擬、沒有寄出任何內容，並循環示範語氣、長度、第二個快速回覆和插入，直到有人在卡片內按下或輸入。你自己的 `Message` 優先於模擬；把 `Simulation` 設為 null 會停止模擬並顯示空卡片。在產品中請關閉 `AutoPlay`。

```csharp
draft.Simulation = new ReplyDraftSimulation
{
    Message = email,
    Suggestions = [new ReplyDraftSuggestion("yes", "Sounds good")],
    Model = new AgentModel("Mistral Medium 3.5"),
    Replies =
    [
        new ReplyDraftVersion("yes", ReplyDraftTone.Friendly, ReplyDraftLength.Medium, "Hi Maya,\n\nSounds good!\n\nSam"),
        new ReplyDraftVersion("yes", ReplyDraftTone.Formal, ReplyDraftLength.Medium, "Dear Maya,\n\nThat suits me.\n\nSam"),
    ],
};
```

`ReplyDraftSimulation.Generate` 以 `Generator` 的形式串流輸出同樣的回覆，供你自行包裝模擬時使用。上方的預覽播放的就是一個模擬，其郵件和回覆來自圖庫自己的示範資料。

### 結構

1. **郵件**：郵件圖示、主旨與時間；寄件人的頭像、姓名、地址與收件人；內文縮排在頭像下方。
2. **回覆標題**：模型的標記與 "Reply with ..."，右側是狀態：一個圖示加一個詞，模型運作時會閃爍。
3. **快速回覆**：可切換的標籤；正在引導草稿的那個以強調色填滿。
4. **草稿框**：草稿在這裡串流出現，完成後變為可編輯的文字。
5. **語氣與長度**：兩個帶滑動膠囊的小型單選群組，中間以細線分隔。
6. **操作**：鍵盤提示、Regenerate 與 Insert。

### 狀態

| Status | 草稿框顯示 | 狀態列 |
|---|---|---|
| `Idle` | 一行閃爍的 "Reading Maya's email"，或在 `AutoStart` 關閉時顯示 Draft reply | "No draft yet" |
| `Drafting` | 正在串流寫出的草稿 | 閃爍的 "Drafting" |
| `Rewriting` | 變暗且唯讀的舊草稿 | 閃爍的 "Rewriting" |
| `Ready` | 可編輯的草稿 | 勾號與 "Ready to edit" |
| `Inserting` | 草稿；Insert 顯示載入圈 | 閃爍的 "Inserting" |
| `Inserted` | 草稿 | 雙勾號與 "Inserted" |
| `Failed` | 錯誤訊息與 Try again | 警示圖示與 "Couldn't draft" |

### 使用你自己的模型

<Demo name="OwnData" />

`Generator` 傳回由文字增量組成的 `IAsyncEnumerable<string>`。當你的服務一次傳回整段回覆時，用 `ReplyDraftStreams.FromText` 包裝它，卡片會在本機以每塊 3 到 8 個詞的片段串流顯示。請求的 `Mode` 為 `Draft` 表示新的回覆，為 `Rewrite` 表示已完成的草稿變更了語氣或長度：此時請改寫 `Previous`，它保存螢幕上包含讀者修改的文字。擲回例外即可顯示錯誤狀態。

每當一份草稿或改寫完成，`DraftCompleted` 就會觸發，並帶有文字、語氣、長度與快速回覆。

### 插入

<Demo name="Insert" />

Insert，或在卡片內任意位置按 Cmd+Enter（Ctrl+Enter），會先引發 `InsertRequested`，再等待 `InsertHandler`，其間按鈕上顯示載入圈。處理常式擲回例外時保留草稿，並顯示 "Couldn't insert"。既沒有處理常式，也沒有把 `InsertRequested` 標記為已處理的事件處理常式時，插入是模擬的，播報會說明沒有寄出任何內容。

### 依需求開始

`AutoStart` 會在卡片捲動進入檢視時立即寫出第一份草稿。將它設為 false 則改為顯示一行簡短說明和 Draft reply 按鈕，只在讀者要求時才執行模型。`StartDraft()` 與 `Regenerate()` 在程式碼中做同樣的事。

### 窄容器

<Demo name="Narrow" />

卡片填滿容器，最寬 600px。低於 480px 時內距降為 16px，內文不再縮排到頭像下方；低於 460px 時兩組之間的細線隱藏，Length 移到單獨一行。標籤會換行，過長的姓名與地址會截斷，不會出現水平捲動。觸控按下之後，選項增大到 32px，標籤增大到 36px。

### 自訂文案

`Labels` 包含卡片顯示或播報的每個詞；替換整個物件即可在地化或改寫：

```csharp
draft.Labels = new ReplyDraftLabels
{
    Ready = "Prêt à modifier",
    Insert = "Insérer",
    ReplyWith = model => "Répondre avec " + model,
};
```

`SourceMark` 用你自己的 24 × 24 描邊標記取代主旨前的郵件圖示，`AgentModel.Mark` 取代模型的星芒。

### 內容準則

- 快速回覆的標籤用兩三個詞（"Sounds good"、"Next week"）；它們的 id 是你的提示詞能理解的內容。
- 草稿保持純文字：草稿框是純文字編輯器。
- 在你自己的編輯器中說明插入會做什麼；卡片只負責交出文字。

### 最佳做法

- 把請求的取消權杖傳給模型用戶端：每個新操作都會中止上一個請求。
- 把 `Rewrite` 當作對 `Previous` 的改寫，這樣讀者的修改在語氣變更後仍然保留。
- 在產品中關閉 `AutoPlay`；它用於行銷頁與文件頁。

## API 參考

<!-- api: ReplyDraft -->

<!-- api: ReplyDraftMessage -->

<!-- api: ReplyDraftSuggestion -->

<!-- api: ReplyDraftRequest -->

<!-- api: ReplyDraftGenerator -->

<!-- api: ReplyDraftInsertHandler -->

<!-- api: ReplyDraftStreams -->

<!-- api: ReplyDraftSimulation -->

<!-- api: ReplyDraftVersion -->

<!-- api: ReplyDraftLabels -->

<!-- api: ReplyDraftEventArgs -->

<!-- api: ReplyDraftSuggestionEventArgs -->

<!-- api: ReplyDraftToneChangedEventArgs -->

<!-- api: ReplyDraftLengthChangedEventArgs -->

<!-- api: ReplyDraftTone -->

<!-- api: ReplyDraftLength -->

<!-- api: ReplyDraftMode -->

<!-- api: ReplyDraftStatus -->

## 無障礙

- 卡片以寄件人命名（"Email from Maya Chen"）；回覆區域是一個有名稱的區塊。
- 快速回覆是會回報是否按下的切換按鈕；語氣與長度是單選群組，只有選取的選項是 Tab 停駐點。
- 完成的草稿是名為 "Draft reply" 的純文字可編輯文字方塊，讀者可以在 Insert 之前修改某個字詞；Insert 交出的就是修改後的內容。
- 禮貌型即時區域會播報起草、改寫、就緒、已插入與錯誤；模擬的操作會說明沒有寄出任何內容。示範自動播放時它保持安靜。
- 狀態使用圖示加一個短詞，從不只靠顏色。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| Tab | 依序經過快速回覆、草稿文字、語氣、長度、Regenerate 與 Insert |
| 方向鍵 | 在 Tone 或 Length 中移動選取並改寫草稿 |
| Home、End | 在 Tone 或 Length 中跳到第一個或最後一個選項 |
| Enter、Space | 啟用快速回覆、選項或按鈕 |
| Cmd+Enter、Ctrl+Enter | 在卡片內任意位置插入已完成的草稿 |

## 動態效果

| 變化 | 完整 | 減弱 | 關閉 |
|---|---|---|---|
| 草稿串流出現 | 每 34ms 一塊 3 到 8 個詞；每塊在 140ms 內淡入 | 草稿整體出現 | 草稿整體出現 |
| 語氣或長度改寫 | 舊草稿變暗，在 100ms 內淡出；新草稿在 160ms 內淡入並上升 2px | 立即 | 立即 |
| 草稿框高度 | 彈簧過渡到新高度（0.26s，無回彈）；重新產生期間保持高度直到新草稿完成 | 立即 | 立即 |
| 語氣與長度的膠囊 | 以同樣的無回彈彈簧滑動 | 直接跳到位 | 直接跳到位 |
| 狀態 | 交叉淡化（淡出 100ms，淡入 160ms）；運作中的詞會閃爍 | 交叉淡化，不閃爍 | 立即 |
| 按下標籤或選項 | 縮小到 0.97 | 不縮放 | 不縮放 |

## 相關元件

- `Textarea`：由人自己書寫的一般多行輸入框。
- `ToolCall` 與 `AgentStatus`：呈現智慧代理正在做什麼。
- `SegmentedControl`：在少量檢視之間切換。
