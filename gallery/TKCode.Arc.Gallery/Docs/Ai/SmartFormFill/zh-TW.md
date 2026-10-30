---
title: Smart form fill
summary: 貼上即填的表單卡片：AI 讀取電子郵件簽名或零散筆記，逐一填寫聯絡人表單的欄位，每個值附上信心度圓點，來源在文字中醒目標示，猜測的值需要複核，並可復原。
source-hash: 1f6fca89440b
---

<Demo name="Preview" />

## 何時使用

`SmartFormFill` 把貼上的文字變成一筆紀錄。使用者把電子郵件簽名、轉寄的郵件或筆記貼到左側區域，AI 讀取後逐一填寫表單欄位。每個值淡入時附上一個信心度圓點，它所來自的文字在貼上的內容中醒目標示；猜測的值在儲存前需要複核，「復原」會還原先前的值。

- **從電子郵件簽名或通話筆記建立聯絡人或潛在客戶**。
- **新手引導與 CRM 流程**，手動輸入六個欄位是主要負擔。
- **以非結構化文字形式送來的供應商、公司或收件紀錄**；傳入你自己的欄位。
- **任何 AI 填寫的資料需要先核對再儲存的表單**，信心度一目了然，複核路徑簡單。

## 何時不用

- **沒有非結構化來源的一般輸入**：直接使用 `Input` 等欄位。
- **很長的多頁申請**：使用分步驟表單，並在其中一頁加入貼上步驟。
- **大量匯入多筆紀錄**：使用表格匯入流程，而不是一張卡片。

## 用法

透過 `Extract` 把你的模型交給卡片。它接收貼上的文字和一個 `CancellationToken`，回傳 `SmartFormFillResult`：依欄位 id 提供一個 `SmartFormFillValue`，包含值、0 到 1 的信心度、該值在文字中的原文子字串，以及猜測時說明原因的備註。結果中沒有的欄位維持原值。`Submit` 儲存表單；工作執行期間按鈕顯示載入狀態。

```csharp
card.Extract = async (text, token) =>
{
    // Ask your model for structured output: { field: { value, confidence, source, note } }.
    var contact = await contacts.ExtractAsync(text, token);
    var result = new SmartFormFillResult();
    foreach (var field in contact.Fields)
    {
        result[field.Id] = new SmartFormFillValue(field.Value, field.Confidence, field.Source, field.Note);
    }
    return result;
};
card.Submit = values => crm.SaveContactAsync(values);
```

沒有擷取器時，卡片使用模擬讀取器：貼上的文字如果是某個範例的文字，就傳回該範例的 `Result`，其他文字由一個以規則為基礎的小型剖析器處理（不會傳送任何內容）。既沒有 `Extract` 也沒有 `Simulation` 時，表單一開始是空的，沒有 Try sample 按鈕，也不顯示模型。

### 模擬

想在沒有模型的情況下展示卡片（示範、設計審查、新手導覽），就給它一個 `SmartFormFillSimulation`：Try sample 背後的範例，每個都附有讀取時傳回的結果（`SmartFormFillSample.Result`），以及要顯示的模型。動態效果為完整時，卡片會自動循環示範（貼上、填寫、核對、復原），直到有人在卡片內點按或輸入。你自己的 `Extract` 優先於模擬；把 `Simulation` 設為 null 會停止模擬並清除它填寫的內容。產品畫面中請設定 `AutoPlay="False"`。

```csharp
card.Simulation = new SmartFormFillSimulation
{
    ModelName = "DeepSeek-V4.1-Flash-1017",
    Samples =
    [
        new SmartFormFillSample("Email signature", signature)
        {
            Result = new SmartFormFillResult
            {
                ["name"] = new SmartFormFillValue("Maya Okafor", 0.98, "Maya Okafor"),
                ["email"] = new SmartFormFillValue("maya.okafor@example.com", 0.99, "maya.okafor@example.com"),
            },
        },
    ],
};
```

上方的預覽播放的就是一個模擬，其文字和結果來自圖庫自己的示範資料。

### 結構

1. **標題區**：標題及其下方的一行說明（`Title`、`Description`；說明為空時隱藏這一行）。
2. **貼上區**：標題列顯示負責讀取的模型（"Read by DeepSeek-V4.1-Flash-1017"），接著是貼上框，以及 Try sample、Edit text 和 Fill。開始讀取後，貼上框會變成貼上的文字，每個欄位的來源都在其中醒目標示。
3. **表單**：依序排列的欄位，兩個半寬欄位佔一列。每個欄位有標籤、信心度標記（圓點加百分比，或 Review、Checked、Edited）、一個 Arc `Input`；猜測的值還有警告色外框、核對按鈕和下方的原因。公司欄位在開頭顯示公司的標誌或首字母。
4. **底列**：附圖示的狀態，接著是「復原」和送出按鈕。

### 讀取與填寫

在貼上框中貼上（Cmd/Ctrl+V）會立即開始讀取；Cmd/Ctrl+Enter 或 Fill 讀取手動輸入的文字；Try sample 貼上下一個範例。讀取期間，狀態以微光顯示 "Reading with DeepSeek-V4.1-Flash-1017"，Fill 變成 Stop。Stop 或 Escape 會取消擷取器的權杖，什麼都不會改變。

結果送達後，欄位依表單順序每隔 140ms 填寫一個。正在填寫的欄位，其來源在貼上的文字中顯示較深的底色；之後游標停留或聚焦某個欄位也會如此，讓因果關係在卡片上一目了然。一次讀取的所有欄位都填好後觸發 `Filled`；每次值改變（無論輸入或填寫）都觸發 `ValuesChanged`。

```csharp
card.Filled += (_, e) => analytics.Track("contact-filled", e.FilledCount, e.ReviewCount);
card.ValuesChanged += (_, e) => draft.Save(e.Values);
```

`ReadAsync(text)`、`Stop()`、`TrySampleAsync()`、`Check(id)`、`Undo()` 和 `SubmitAsync()` 在程式碼中完成相同的操作；`GetFieldValue` 和 `SetFieldValue` 讀寫單一欄位。

<Demo name="Extractor" />

### 複核與復原

信心度低於 `ReviewThreshold`（預設 0.8）的值需要複核：標記顯示 Review，輸入框外有一圈警告色外框，下方顯示原因，末端有一個核對按鈕。核對它或修改其值即可結束複核。仍有欄位需要複核時無法儲存：卡片會說明還剩幾個並聚焦第一個。「復原」還原上一次填寫之前的值，並以這些值觸發 `Undone`。

### 自訂欄位

<Demo name="CustomFields" />

`Fields` 取代預設的聯絡人表單。每個欄位有 `Id`（值與結果中的鍵）、`Label`、`Kind`（`Text`、`Email`、`Phone`、`Url` 或 `Company`，後者附標誌位置）、`Placeholder` 和 `Span`（`Half` 或 `Full`）。`DefaultValues` 在讀取之前預先填寫欄位。`Samples` 設定 Try sample 背後的文字（為每個範例提供 `Result`，供模擬讀取器傳回）；沒有範例時會隱藏該按鈕並關閉自動示範。

```xml
<arc:SmartFormFill Title="New vendor" SubmitLabel="Add vendor" AutoPlay="False">
    <arc:SmartFormFillField Id="company" Label="Vendor" Kind="Company" Span="Full" />
    <arc:SmartFormFillField Id="email" Label="Billing email" Kind="Email" />
</arc:SmartFormFill>
```

`ResolveLogo` 為公司欄位提供你自己的標誌：依公司名稱回傳一個控制項，回傳 null 則顯示首字母。`ModelName` 和 `ModelMark` 指定貼上區旁顯示的模型。

### 窄欄

<Demo name="Narrow" />

卡片依自身寬度調整，而不是視窗寬度。內容寬度達 720px 時貼上區位於表單左側；更窄時貼上區以固定高度疊在表單上方。低於 440px 時每個欄位獨佔一列，底列把狀態放在操作按鈕上方。寬度為 360px 時也不會出現水平捲動。

### 自訂文字

`Labels` 包含卡片顯示或朗讀的其他所有文字；取代整個物件即可在地化或改寫：

```csharp
card.Labels = new SmartFormFillLabels
{
    TrySample = "Beispiel",
    Fill = "Ausfüllen",
    Reading = model => $"{model} liest",
    Filled = (filled, review) => $"{filled} Felder ausgefüllt",
};
```

### 內容指引

- `Description` 用一句平實的話說明要貼上什麼。
- 備註簡短具體："Two numbers in the text; picked the mobile."，而不是「信心度低」。
- 回傳原文中確切的子字串；改寫過的來源無法醒目標示。

### 最佳實務

- 把 `ReviewThreshold` 維持在 0.8 左右，讓推斷出的州名、改寫過的職稱、多組電話號碼中選出的一組被標示出來，而不是被悄悄儲存。
- 把權杖傳給你的 HTTP 呼叫，這樣 Stop、Escape 和新的貼上才能真正取消請求。
- 在產品畫面中關閉 `AutoPlay`；循環示範是為文件與行銷頁面準備的。

### 常見問題

**使用者可以手動輸入而不是貼上嗎？** 可以。每個欄位都是一般的輸入框；在貼上框中輸入的文字可以用 Fill 或 Cmd/Ctrl+Enter 讀取。

**讀取之前使用者已輸入的值會怎樣？** 一次讀取只修改它回傳的欄位；「復原」會還原讀取之前的內容。

## API 參考

<!-- api: SmartFormFill -->

<!-- api: SmartFormFillField -->

<!-- api: SmartFormFillValue -->

<!-- api: SmartFormFillSample -->

<!-- api: SmartFormFillSimulation -->

<!-- api: SmartFormFillLabels -->

## 無障礙

- 底列狀態是一個禮貌的即時區域：會播報讀取中、填寫了多少欄位及其中多少需要複核、核對、復原和儲存。使用內建讀取器時會附加 "Simulated"。
- 每個已填寫的輸入框都描述其信心度（"Filled by AI, 94% confident"）；猜測的值還會提供原因，並在核對或修改之前回報 Review 狀態。
- 狀態從不只靠顏色表達：信心度顯示為圓點加百分比或文字，底列的圖示一律搭配文字。
- 需要複核的欄位內的核對按鈕以欄位命名（"Mark Phone as checked"）。儲存會聚焦第一個仍需複核的欄位。
- 貼上文字的檢視是一個可聚焦、附標籤的區域（"Pasted text"），鍵盤使用者可以捲動它；鍵盤聚焦時會在周圍顯示焦點環。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| Cmd/Ctrl + V | 貼到文字框；立即開始讀取 |
| Cmd/Ctrl + Enter | 讀取文字框中輸入的文字 |
| Escape | 停止進行中的讀取；什麼都不會改變 |
| Tab | 依序經過文字框、Try sample、Fill、每個欄位、核對按鈕、復原和儲存 |
| Enter、Space | 按下取得焦點的按鈕 |

## 動效

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 值送達 | 依表單順序，每隔 140ms 一個 | 全部同時 | 全部同時 |
| 一個值就位 | 200ms 淡入；輸入框泛起 900ms 的強調色底色；沒有位移 | 立即，沒有底色 | 立即 |
| 文字中目前的來源 | 標示在 160ms 內變為完整底色 | 相同 | 立即 |
| 標記、標誌與狀態 | 舊內容 100ms 淡出，新內容 160ms 淡入並上升 2px | 短暫交叉淡化 | 立即 |
| 讀取標籤 | 平緩的微光 | 靜止 | 靜止 |
| 複核外框 | 160ms 淡入 | 立即 | 立即 |
| 自動示範循環 | 卡片在畫面上時播放 | 從不播放 | 從不播放 |

沒有任何版面動畫：值、標記和醒目標示都在原處改變，所以卡片填寫時不會跳動。卡片離開畫面時循環暫停，卡片被移除時進行中的讀取會被取消。

## 相關

- `Input` 和 `Textarea`：卡片使用的欄位。
- `ToolCall`：呈現代理對工具做了什麼。
- `ContextWindow`：呈現一段對話佔用了模型上下文的多少。
