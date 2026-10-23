---
title: AI 輸入框
summary: AI 應用程式的提示輸入框：會隨內容增高的文字區，斜線命令與 @ 提及會變成權杖，附件在原地上傳，工具開關、模型與推理強度選單，字數計數器，以及會變成停止鈕的傳送鈕。
source-hash: 922e5fc99c9d
---

<Demo name="Preview" />

## 何時使用

AI 輸入框是人們撰寫提示的方框。它是一個圓角的單一表面，上方是文字區，下方是工具列，而從輸入到傳送之間的一切都由它處理：命令、檔案、工具、模型與字數上限。

- **聊天或助理應用程式**：對話底部的方框，在回覆生成期間會顯示停止鈕。
- **工具中的提示欄位**："/" 命令用於你的產品動作，"@" 提及用於它可以讀取的檔案或文件。
- **隨提示一起接收檔案的表單**：挑選、貼上或拖放檔案；它們會在原地上傳，訊息只帶著已備妥的檔案。
- **提示旁的模型選擇**：工具列有一個模型膠囊，附帶推理強度設定，讓選擇留在工作發生的地方。

## 何時不用

- **簡短的單行欄位**：使用 `Input`，搜尋則使用 `SearchField`。
- **含 @人員 的留言方塊**：使用 `MentionInput`，它提供人員、頻道與序列化功能。
- **有自己權杖與自己表面的文字編輯器**：以 `TokenTextEditor` 為基礎，它是輸入框內部的編輯器。
- **在設定頁面選擇模型**：使用 `ModelPicker`；輸入框的膠囊是用於寫作時的選擇。
- **執行模型**：輸入框只回報訊息。由你的程式碼呼叫模型，並告知何時正在生成回覆。

## 用法

`AiComposer` 衍生自 Avalonia 的 `TemplatedControl`。把使用者可以使用的內容（命令、上下文項目、工具、模型）交給它，並處理 `Sent`。傳送後它會自行清空；訊息包含文字、權杖、已備妥的附件、模型、推理強度，以及每個工具的狀態。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:AiComposer Placeholder="Ask a question, type / for actions or @ for a file">
        <arc:AiComposer.Commands>
            <arc:AiCommand Id="recap" Name="recap" Description="Boil a doc or file down to its main points" />
            <arc:AiCommand Id="rewrite" Name="rewrite" Description="Rewrite in a clearer or friendlier tone" />
        </arc:AiComposer.Commands>
        <arc:AiComposer.ContextItems>
            <arc:AiContextItem Id="style-guide" Label="style-guide.pdf" Hint="Brand, 3.1 MB" Group="Files" />
        </arc:AiComposer.ContextItems>
    </arc:AiComposer>
</UserControl>
```

```csharp
composer.Sent += (_, e) =>
{
    var message = e.Message;               // Text, Tokens, Attachments, Model, Effort, Tools
    StartReply(message);
    composer.IsGenerating = true;          // the send button becomes a stop button
};
composer.StopRequested += (_, _) => CancelReply();
```

### 命令與提及

在行首輸入 "/" 會開啟 `Commands` 清單（依名稱開頭篩選）；在行首或空白之後輸入 "@" 會開啟 `ContextItems` 清單，依 `MentionGroups` 的順序分組（預設為 "Files" 然後 "Docs"），並依標籤或提示篩選。"@" 前面若是 "(" 則不會開啟清單。上下鍵移動（並循環），Enter 或 Tab 插入，Escape 關閉，而清單絕不會取得焦點。插入的內容會成為一個權杖，後面接一個空白；`Value.Tokens` 會列出它們及其位移量。若沒有命令或沒有項目，該符號就只是一般文字。

`SetPrompt(text)` 為建議填入方框：它認得的命令與提及會成為權杖，插入點移到結尾，焦點移到文字。`InsertToken(kind, id, label)` 會在插入點加入一個權杖。

<Demo name="Suggestions" />

### 附件

加號按鈕會開啟一個選單，內含 "Upload file"（上傳檔案）、你的 `AttachActions`（雲端硬碟、儲存庫、螢幕擷取，若你要求則在它們之間加入分隔線），以及在有上下文項目時的 "Mention a file or doc"（提及檔案或文件）。檔案也可以貼到或拖放到輸入框上。貼上的內容至少達到 `LongPasteCharacters` 個字元或 `LongPasteLines` 行時，會變成一個 "Pasted text"（貼上的文字）附件，而不是文字。

每個檔案會成為一個晶片，並在原地上傳：上傳進行時顯示帶百分比的圓環，完成後變成勾號，然後是移除鈕。超過 `MaxFileSize`（預設 30 MB）或不符合 `AcceptedFileTypes`（`FileDropzone.Accept` 的語法）的檔案，會得到一個附有原因的錯誤晶片；它可以被移除，且不會阻止傳送。`UploadHandler` 負責上傳：回報進度、遵守其取消權杖、拋出例外即表示拒絕。沒有處理常式時，檔案立即就緒。`IsAttachmentEnabled` 會關閉加號、貼上與拖放。

<Demo name="Attachments" />

### 工具、模型與推理強度

`Tools` 中的每個 `AiComposerTool` 都是工具列中的一個開關（一個圖示與一個標籤，在較窄的輸入框中標籤會隱藏）；其 `IsChecked` 是雙向的，`ToolToggled` 會回報變更。當 `Models` 不是空的時，一個膠囊會顯示所選模型及其推理強度；它的選單包含模型、一條分隔線，以及一列推理強度，選擇後立即套用。`SelectedModel` 與 `Effort` 是雙向的，`ModelChanged` 與 `EffortChanged` 會回報變更；模型不提供的推理強度會移到該模型提供的最接近層級。

### 字數上限

`MaxLength`（預設 4000；null 表示沒有上限）會加上一個計數器：從 `CounterThreshold`（80%）開始顯示剩餘字數，從 `WarningThreshold`（95%）開始轉為警告色，超過上限時顯示超出的字數並阻止傳送。文字絕不會被截斷。

<Demo name="Limit" />

### 傳送與停止

傳送鈕有四種狀態：空白（沒有可傳送的內容）、就緒、受阻（仍有上傳進行中，或文字超過上限；其名稱會說明是哪一種），或生成中。Enter 傳送；Shift+Enter 加入一行。在輸入法組字中、清單開啟中，或回覆生成中，Enter 不會有任何作用（方框仍可編輯，不會排入任何佇列）。當 `IsGenerating` 為 true 時，按鈕是位於圓環中的停止方塊，圓環跟隨 `GenerationProgress` 並且絕不倒退；按下它，或按 Escape，即會引發 `StopRequested`。你的程式碼會將 `GenerationProgress` 設為 1，然後將 `IsGenerating` 設為 false，以結束一次回覆。

`SendCommand` 與 `StopCommand` 對 MVVM 做同樣的事。`SendState` 會指出按鈕處於四種狀態中的哪一種。

### 最簡輸入框

<Demo name="Minimal" />

### 文案

所有文字都來自 `Labels`（`AiComposerLabels`）：預留位置、按鈕名稱、計數器、晶片文字、拖放覆蓋層與朗讀通知。替換這個實例即可翻譯或改寫；格式化的文字是函式。

### 文案規範

- **預留位置**：用一行說明輸入框接受什麼："Ask a question, type / for actions or @ for a file"。
- **命令**：簡短的動詞（"recap"、"rewrite"），以及一行說明它做什麼。
- **上下文項目**：人們認得的名稱，以及能區分同名兩項的提示（路徑或來源）。
- **晶片上的錯誤**：幾個字，例如 "Over 30 MB"（超過 30 MB）、"Type not accepted"（不接受此類型）。

### 最佳做法

- 把輸入框放在對話底部，並給它與對話串相同的寬度；它會依自己的寬度調整（寬度低於 680 px 時為緊湊，低於 560 與 400 時更窄）。
- 一開始回覆就設定 `IsGenerating`，並務必處理 `StopRequested`。
- 也要在伺服器上驗證：輸入框會限制它傳送的內容，但它不是安全邊界。
- 不要在回覆執行時默默地排入訊息；輸入框不會這樣做。

### 常見問題

**為什麼傳送後方框會清空？** 它一律會清空文字、權杖與附件；工具、模型與推理強度會保留。若你需要還原訊息，請自行保存。

**語音輸入在哪裡？** 不屬於此版本。

## API 參考

<!-- api: AiComposer -->

<!-- api: AiCommand -->

<!-- api: AiContextItem -->

<!-- api: AiAttachment -->

<!-- api: AiComposerTool -->

<!-- api: AiComposerAction -->

### AiComposerMessage

| 屬性 | 說明 |
|---|---|
| `Text` / `Tokens` / `Value` | 提示、其中的權杖，以及兩者合併 |
| `Attachments` | 已備妥的附件；失敗或仍在上傳的會被排除 |
| `Model` / `Effort` | 所選的模型與推理強度 |
| `Tools` | 每個工具的 id，以及它是否開啟 |

### 事件

| 事件 | 引發時機 | 引數 |
|---|---|---|
| `Sent` | 人們傳送提示 | `Message` |
| `StopRequested` | 人們停止正在生成的回覆 | |
| `AttachmentAdded` / `AttachmentRemoved` | 附件被加入、移除，或其上傳被取消 | `Attachment` |
| `ToolToggled` | 工具被開啟或關閉 | `Tool` |
| `ModelChanged` / `EffortChanged` | 人們選擇模型或推理強度 | 舊值與新值 |

## 無障礙

- 文字區有無障礙名稱（"Message"）；命令與提及清單是清單，其列以標題與提示命名，醒目提示的列透過文字區回報，因此焦點會停留在人們輸入的地方。
- 傳送鈕的名稱會說明它做什麼，或為何不能按："Send message"（傳送訊息）、"Type a message to send"（輸入訊息以傳送）、"Wait for uploads to finish"（等待上傳完成）、"Shorten the message by N characters"（將訊息縮短 N 個字元）、"Stop generating"（停止生成）。
- 工具列按鈕有名稱；工具開關會回報是否按下；加號按鈕會回報它會開啟一個選單。
- 一則禮貌的即時區域會朗讀建議的數量、工具的開啟或關閉、模型與推理強度的變更、完成與失敗的上傳、移除、長段貼上、已傳送的訊息與停止。
- 晶片上的按鈕有名稱（"Cancel upload of report.pdf"（取消 report.pdf 的上傳）、"Remove report.pdf"（移除 report.pdf））；移除最後一個晶片時，焦點移到文字。
- 焦點會移到人們接下來會看的地方：在選單中按 Escape 後回到加號按鈕，選擇一個項目後回到文字，做出選擇後回到模型膠囊。
- 狀態絕不只靠顏色傳達：錯誤會加上圖示與原因，計數器會加上數字。
- 減少動態效果會保留每一種狀態與每一則朗讀。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| `Enter` | 傳送（在組字中、清單開啟中，或回覆生成中時不會） |
| `Shift+Enter` | 加入一行 |
| 行首的 `/`、空白之後的 `@` | 開啟命令或提及清單 |
| 清單中的 `ArrowUp` / `ArrowDown` | 在列之間移動；會循環 |
| 清單中的 `Enter` 或 `Tab` | 將該列插入為權杖 |
| `Escape` | 關閉清單或選單；若沒有開啟的項目，則停止正在生成的回覆 |
| 加號或模型膠囊上的 `Enter`、`Space` 或 `ArrowDown` | 開啟其選單 |
| 推理強度列上的 `ArrowLeft` / `ArrowRight` | 變更推理強度 |
| `Tab` | 依序移過加號、工具、計數器、模型、傳送鈕與晶片 |

## 動態效果

- 文字區從 `MinLines` 到 `MaxLines`（1 到 8 行）逐行增高，時長 160 ms 的 ease，之後才開始捲動。
- 輸入框取得焦點時，邊框加深（160 ms）；檔案被拖曳經過時，拖放覆蓋層淡入。
- 晶片以 0.92 的縮放同時淡入；圓環在彈簧動畫上變成勾號，然後變成移除鈕。
- 傳送鈕的箭頭在彈簧動畫上變成停止方塊，同時其周圍的圓環淡入；圓環跟隨進度，且絕不倒退。
- 選單與建議清單從其按鈕或插入點長出；清單的高度在彈簧動畫上跟隨其列。
- 計數器與模型膠囊的文字以彈簧動畫取得所需的寬度。
- 減少動態效果保留 120 ms 的淡入淡出，並移除移動與彈簧。關閉則立即套用每項變更。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 方框的高度 | 160 ms ease | 立即 | 立即 |
| 邊框與懸停顏色 | 160 ms | 120 ms | 立即 |
| 傳送變停止 | 形狀在彈簧上改變，圓環淡入 | 以 120 ms 淡入交換 | 立即 |
| 晶片進入或離開 | 縮放與淡入淡出 | 120 ms 淡入淡出 | 立即 |
| 選單與建議清單 | 從錨點長出 | 120 ms 淡入淡出 | 立即 |

## 相關元件

- `ModelPicker`：用於在設定頁面選擇模型。
- `TokenTextEditor`：只需要帶權杖的文字區時使用。
- `MentionInput`：用於含人員與頻道的留言。
- `AgentMemory`：用於查看助理記住了什麼。

| 元件 | 適用情境 |
|---|---|
| `ModelPicker` | 模型的選擇與撰寫提示無關 |
| `TokenTextEditor` | 你需要的是編輯器，而不是整個方框 |
| `MentionInput` | 提及的是留言中的人員與頻道 |
| `Textarea` | 文字沒有命令、檔案或工具 |
