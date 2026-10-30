---
title: PII redaction
summary: 一張防護卡片：提示詞送到模型之前，其中的個人資料會先被遮蔽；回覆傳回時，原始值再被放回。
source-hash: f3818a4777ad
---

<Demo name="Preview" />

## 何時使用

`PiiRedaction` 展示隱私防護對提示詞做了什麼。掃描會在訊息中標出每個姓名、電子郵件、電話號碼與卡號及其類型，接著在一次交叉淡化中把它們各自換成帶編號的預留位置，例如 `[EMAIL_1]`。傳送會讓安全提示詞沿著一條小流程（你、PII 防護、模型）傳遞，回覆帶著預留位置以串流方式傳回，防護再為使用者放回原始值。「顯示原文」會在安全提示詞與使用者輸入的內容之間切換。

- **說明或證明一項防護機制**：客服助理、醫療或金融副駕駛、企業聊天等 AI 產品。
- **設定或導覽畫面**：管理員在啟用 PII 篩選之前先看到它會做什麼。
- **實驗場**：使用者貼上文字，確切看到哪些內容會送到模型。

## 何時不用

- **對話本身**：使用聊天或輸入框元件，在背景遮蔽；只在需要說明防護的地方顯示這張卡片。
- **法規遵循**：不要依賴內建偵測器。透過 `Detector` 接上真正的 DLP 或 NER 服務。
- **對話紀錄中任意的工具步驟**：使用 `ToolCall`。

## 用法

```xml
<arc:PiiRedaction AutoPlay="False" Message="" />
```

```csharp
card.Names = [user.Name];
card.Detector = async (text, token) => await dlp.ScanAsync(text, token);   // spans with an exclusive end
card.Sender = async (prompt, token) => await chat.CompleteAsync(prompt, token); // may contain [NAME_1], [EMAIL_1]
card.Redacted += (_, e) => audit.Log("pii.redacted", e.Entities.Select(x => x.Type));
```

`PiiRedaction` 是 Special 套件（`UseArcSpecial()`）中的 `TemplatedControl`，寬度填滿容器，最多 640px。既沒有 `Message` 也沒有 `Simulation` 時，卡片顯示一個空的訊息框（"Paste a message with names, emails, phone numbers or cards"），在輸入內容之前 Scan 處於關閉狀態；在設定 `ModelName` 之前，流程列把模型顯示為 "Model"。

### 模擬

想在沒有模型的情況下展示卡片（示範、設計審查、新手導覽），就給它一個 `PiiRedactionSimulation`：訊息、要標記的姓名、安全提示要送往的模型，以及附占位符的模型回覆。卡片沒有 `Message` 時會顯示模擬的訊息，並循環播放掃描、傳送和還原，直到有人接手；回覆是模擬的，卡片會說明這一點。編輯過的訊息會得到一則簡短的替身回覆。你自己的值優先於模擬的值；把 `Simulation` 設為 null 會停止模擬並顯示空的訊息框。

```csharp
card.Simulation = new PiiRedactionSimulation
{
    Message = "Hi, I'm Juniper Vale. Send the receipt to juniper.vale@example.com.",
    Names = ["Juniper Vale"],
    ModelName = "Grok 4.7",
    Reply = "Done, [NAME_1]. The receipt is on its way to [EMAIL_1].",
};
```

上方的預覽播放的是一段虛構客服對話的模擬，來自圖庫自己的示範資料：Juniper Vale 關於重複扣款的請求，其中有一組測試卡號（4242 4242 4242 4242）、一個 example.com 電子郵件，以及一個虛構 555 號段的電話號碼。

### 結構

1. **標頭**：一個盾牌、「PII guard」，以及回覆為模擬時顯示的 Simulated 標籤。
2. **訊息**：掃描前是可編輯的輸入框；掃描時是帶標記的原文；之後是安全提示詞，每個預留位置以其類型的顏色顯示在圓角標記上。上方是標籤（「Message」、「Safe prompt」或「Original message」）與「顯示原文」開關。
3. **類型格**：Name、Email、Phone 與 Card，每格顯示「Not checked」、閃爍的「Checking」、它的權杖（「EMAIL_1」，更多時為「EMAIL_1 +1」）或「None found」。找到內容的格子會帶上淺色底色。
4. **流程列**：你、防護與模型，每一跳都有一個圓點經過。正在運作的節點標籤會閃爍。
5. **回覆**：模型的標記與名稱位於回覆上方；回覆帶著預留位置以串流方式出現，接著顯示還原後的文字，每個放回的值下方都有標記。
6. **頁尾**：帶圖示的狀態、有內容可重設時出現的 Reset，以及唯一的主要動作。

### 階段

主要動作是同一個按鈕，標籤隨階段變化，因此鍵盤焦點在整個流程中都不會移動。

| 階段 | 狀態 | 主要動作 |
|---|---|---|
| `Idle` | Ready to scan | Scan |
| `Scanning` | Scanning for personal data | Stop |
| `Redacted` | 4 items redacted，或 No personal data found | Send |
| `Sending` | Sending the safe prompt | Stop |
| `Thinking` | Waiting for Grok 4.7 | Stop |
| `Streaming` | Grok 4.7 is replying | Stop |
| `Restoring` | Restoring original values | Stop |
| `Done` | Reply restored | Run again |
| `Error` | 例外的訊息 | Retry |

掃描中按 Stop 會回到可編輯的訊息；請求中按 Stop 會取消傳給 `Sender` 的權杖，並回到安全提示詞。Retry 會重新執行失敗的步驟。Reset 會取消所有進行中的作業並回到輸入框。`Scan()`、`Send()`、`Stop()` 與 `Reset()` 在程式碼中做同樣的事。

### 實驗場

<Demo name="Playground" />

`AutoPlay="False"` 搭配 `Message=""` 會得到空白的輸入框。使用者貼上文字後按 Scan，或在輸入框中按 Cmd 或 Ctrl 加 Enter。`Message` 是雙向的，應用程式可以讀取輸入的內容。`IsEditable="False"` 讓輸入框維持唯讀。

### 你的偵測器與你的模型

<Demo name="OwnModel" />

`Detector` 以你的 DLP 或 NER 服務取代內建偵測器。它接收文字與一個取消權杖，傳回 `PiiMatch` 區間（類型、起點與不含在內的終點）。`Sender` 把安全提示詞傳給你的模型，並傳回帶預留位置的回覆；卡片會將它們還原。沒有 `Sender` 時回覆是模擬的：標頭顯示 Simulated 標籤，狀態會補上「Simulated, nothing was sent」。兩者任一擲出例外時，都會顯示例外訊息並提供 Retry。

`Redacted` 回報安全提示詞與每個 `PiiEntity`（類型、位置、值、權杖與預留位置）。`ReplyRestored` 回報原始回覆、還原後的文字，以及是否為模擬。`PhaseChanged` 跟隨每一次階段變化。`Phase`、`SafePrompt` 與 `Reply` 為唯讀且可繫結；`ShowOriginal` 是雙向的。`Entities` 保存上一次遮蔽的實體，`IsSimulated` 表示回覆是否為模擬。

`ModelName` 與 `ModelMark`（24 × 24 的描邊圖形，預設為火花）在流程列與回覆上方標示模型；`ModelMarkBrush` 為標記上色。`NameBrush`、`EmailBrush`、`PhoneBrush` 與 `CardBrush` 設定各類型的顏色（預設為 Series1 到 Series4）。

### 不帶卡片的輔助方法

```csharp
// Server side, before the prompt leaves your backend.
var result = PiiRedactor.Redact(message, PiiRedactor.Detect(message, [user.Name]));
var reply = PiiRedactor.Restore(await model.CompleteAsync(result.Prompt), result.Entities);
```

| 成員 | 作用 |
|---|---|
| `PiiRedactor.Detect(text, names)` | 找出電子郵件、卡號（13 到 19 位且通過 Luhn 檢查）、電話號碼（7 到 15 位）與姓名（名單中的姓名，以及「I'm」、「I am」、「this is」與「my name is」後面首字母大寫的字），依位置排序且不重疊 |
| `PiiRedactor.Redact(text, matches)` | 傳回包含安全提示詞與實體的 `PiiRedactionResult`；第一個之後與之重疊的區間會被略過，重複的值保留同一個權杖 |
| `PiiRedactor.Restore(reply, entities)` | 放回原始值，未知的權杖維持原樣 |
| `PiiRedactor.PassesLuhn(value)` | 對一個值中的數字做 Luhn 檢查 |

| 類型 | 成員 |
|---|---|
| `PiiType` | `Name`、`Email`、`Phone`、`Card` |
| `PiiRedactionPhase` | `Idle`、`Scanning`、`Redacted`、`Sending`、`Thinking`、`Streaming`、`Restoring`、`Done`、`Error` |
| `PiiMatch` | `Type`、`Start`、`End`（不含在內）、`Length` |
| `PiiEntity` | `Type`、`Start`、`End`、`Value`、`Token`（`EMAIL_1`）、`Placeholder`（`[EMAIL_1]`）、`PromptStart`、`PromptEnd` |
| `PiiRedactionResult` | `Prompt`、`Entities` |

### 窄容器

<Demo name="Narrow" />

版面跟隨卡片本身的寬度。窄於 520px 時，四個類型格會排成兩列，每列兩個。窄於 400px 時，卡片收緊內距與間距並隱藏 Simulated 標籤（狀態仍會朗讀），過長的模型名稱在流程列中以刪節號截斷。文字圖層疊放在同一個儲存格中，回覆區預留模擬回覆還原後的高度，因此在 360px、390px 或桌面寬度下都不會跳動。

### 內容指南

- 在系統提示詞中要求模型原樣保留預留位置（「Refer to [EMAIL_1] exactly as written」），讓防護能將它們還原。
- 如果模型會回頭參照某個值，請在整段對話中為它保留同一個權杖：`Redact` 每次呼叫各自編號，所以要在輪次之間保存實體對應。
- 範例中的樣本資料必須一看就是假的：虛構的姓名、example.com 電子郵件、555 號碼與測試卡號。

### 最佳做法

**建議**

- 在正式環境中於伺服器端遮蔽，並把實體保留在請求範圍內。
- 在產品介面中設定 `AutoPlay="False"`。
- 只記錄被遮蔽內容的類型，絕不要把值和提示詞記錄在一起。

**避免**

- 把內建偵測器當作完整方案：名單以外的姓名與簡單自我介紹以外的情況需要 NER 模型。
- 把原始訊息傳送到卡片標示為安全的任何地方。

### 常見問題

**自動播放什麼時候停止？** 只要有人編輯訊息、切換「顯示原文」或按下任何按鈕，就會永久停止。它只在卡片至少四分之一位於畫面內時執行，減少動態效果時從不執行。

**卡片不認得的預留位置會怎樣？** 維持原樣，所以模型編造的 `[CARD_7]` 會原樣顯示。

**能只保留安全提示詞而不顯示回覆嗎？** 讓卡片停在 `Redacted` 並讀取 `SafePrompt`；是否傳送由使用者決定。

## API 參考

<!-- api: PiiRedaction -->

<!-- api: PiiRedactionLabels -->

<!-- api: PiiRedactionSimulation -->

<!-- api: PiiRedactedEventArgs -->

<!-- api: PiiReplyRestoredEventArgs -->

<!-- api: PiiRedactionPhaseChangedEventArgs -->

## 無障礙

- 一個禮貌的狀態區域會朗讀每個階段（「Scanning for personal data」、「4 items redacted」、「Reply restored」），回覆為模擬時還會補上「Simulated, nothing was sent」。
- 狀態從不只依賴顏色：每個階段都把圖示和簡短標籤配對，每個類型格讀出名稱及其權杖或「None found」，原文會列出它的標記（「juniper.vale@example.com: Email, redacted as EMAIL_1」）。
- Scan、Stop、Send、Run again 與 Retry 是同一個按鈕，只是標籤改變，所以焦點維持不動。Reset 會把焦點送回訊息輸入框。
- 只有可見的文字圖層會提供給輔助技術；用來保持高度穩定的隱藏圖層不會。
- 流程列是裝飾性的，對螢幕閱讀器隱藏，因為狀態區域已經朗讀了每一跳。
- 「顯示原文」是開關本身的標籤，在有安全提示詞之前開關會停用並變暗。

### 鍵盤

| 按鍵 | 動作 |
|---|---|
| Tab | 依序經過訊息輸入框（掃描前）、有安全提示詞後的「顯示原文」、顯示時的 Reset，以及主要動作 |
| Cmd + Enter、Ctrl + Enter | 從輸入框開始掃描；焦點移到主要動作，此時它顯示 Stop |
| Space、Enter | 切換「顯示原文」，或按下 Scan、Stop、Send、Run again、Retry 或 Reset |

## 動態效果

- 掃描逐一標記實體，間隔 120ms，每個以其類型顏色在 160ms 內著色，同時對應的類型格從「Checking」交叉淡化為它的權杖。
- 換成預留位置是兩個疊放文字圖層之間的一次交叉淡化（舊圖層 100ms 淡出，新圖層 160ms 淡入），因此不會有字滑動或重新換行，高度也從不改變。「顯示原文」使用同樣的交叉淡化。
- 流程的每一跳是一個圓點以緩入緩出曲線在 460ms 內走完連接線；正在運作的節點標籤會閃爍。
- 回覆每 34ms 流入三到六個字，每塊只在 160ms 內淡入，然後一次交叉淡化為還原後的文字。
- 標籤、狀態與 Reset 按鈕原地交叉淡化；沒有回彈，狀態之間也沒有尺寸變化。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 標記 | 逐一出現，間隔 120ms，160ms 著色 | 同時出現 | 同時出現 |
| 圖層切換 | 100ms 淡出，160ms 淡入 | 100ms 淡出，160ms 淡入 | 立即 |
| 流程跳轉 | 圓點移動 460ms | 無 | 無 |
| 回覆 | 分塊流入，然後交叉淡化 | 整段顯示，然後交叉淡化 | 立即 |
| 自動播放 | 在畫面內時循環 | 關閉 | 關閉 |

## 相關元件

- `ToolCall`：對話紀錄中任意的工具步驟。
- `AgentStatus`：整個代理程式執行的即時狀態。
- `Switch`：單獨使用的開關。
