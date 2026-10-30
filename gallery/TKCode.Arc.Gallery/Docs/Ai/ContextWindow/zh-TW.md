---
title: Context window
summary: 模型的上下文視窗呈現為 200 格的方格，逐段填滿，並帶有壓縮門檻、圖例，以及顯示還剩多少空間的動作。
source-hash: 9f00beff2aa3
---

<Demo name="Preview" />

## 何時使用

`ContextWindow` 顯示語言模型的上下文視窗裡填入了什麼，以及還剩多少空間。視窗是固定的 200 格預算，因此每一格都是總量的二百分之一。格子依閱讀順序填滿：系統提示、工具定義、檔案與記憶、較早訊息的摘要與對話，接著是剩餘空間；保留給回覆的部分則以空心格子畫在最末端。虛線標示壓縮開始的位置。方格上方的大數字顯示已使用的 token；旁邊的圖例列出每個部分的 token 與占比。Send message、Attach file 和 Compact now 會改變視窗，它們下方的狀態列會說明發生了什麼。

- **開發者工具與代理主控台**：在對話紀錄旁顯示工作階段的預算，讓人看出為何長時間執行時會開始遺忘內容，以及一個大型附件會耗用多少空間。
- **會壓縮長工作階段的程式設計代理**：讓壓縮過程可見。較舊的訊息會收合成一段簡短摘要，格子隨之釋放，狀態列會說明收合了多少內容。
- **在上下文中選擇模型**：切換到視窗較小的模型時，方格會立即重新排列，並顯示工作階段是否仍放得下；若放不下，卡片會收合對話並說明此事。
- **文件與新手導覽**：用一段可以實際操作的工作階段說明上下文視窗、輸出保留空間與壓縮，就像本頁的預覽那樣。

## 何時不用

- **簡單的配額或方案額度**：請使用 `Progress` 或用量計量表。方格呈現的是視窗內部的內容，而不是方案用掉了多少。
- **選擇模型**：請使用模型選擇器。這裡的膠囊只會在你傳入的視窗之間切換。
- **帳單或隨時間變化的花費**：請使用圖表。卡片只呈現某個工作階段在某一時刻的狀態。
- **同時有許多工作階段**：每列附一個小長條的表格比較容易閱讀。預期用法是每個畫面一張卡片。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:ContextWindow>
        <arc:ContextWindow.Models>
            <arc:ContextWindowModel Id="your-model" Name="Your model" Window="200000" Reserve="16000" />
        </arc:ContextWindow.Models>
        <arc:ContextWindowItem Category="System" Label="System prompt" Tokens="3800" />
        <arc:ContextWindowItem Category="Messages" Label="Plan the importer rewrite" Tokens="9400" />
    </arc:ContextWindow>
</UserControl>
```

`ContextWindow` 是 Special 套件中的 `TemplatedControl`。請在 `UseArcControls()` 旁呼叫 `UseArcSpecial()` 來註冊該套件。卡片最寬為 744px，並依自身寬度調整，而非視窗的寬度。

沒有資料（沒有模型、項目、回合或檔案）時，卡片是空的：沒有模型膠囊，所有格子都空著，也沒有 Send message 與 Attach file。請提供你的模型與項目，並為這兩個按鈕提供 `Turns` 與 `Files`。本頁的展示使用真實模型及其廠商公布的上下文視窗（核對於 2026-10-29：Claude Sonnet 5.5 為 1M，Grok 4.7 為 500K，Mistral Medium 3.5 為 256K），以及一個虛構的、436K token 的程式設計工作階段；輸出保留空間是展示應用程式單次回覆所請求的量（32K），絕不超過該模型公布的輸出上限。

### 結構

1. **讀數**：已使用的 token 以大字顯示，基線處為 "of 1M tokens"；其下是已使用的百分比（整數，視窗溢出時會超過 100）、"used"、一個圓點，以及剩餘的 token。
2. **模型膠囊**：模型的標記、名稱、以淡色數字顯示的視窗大小，以及一個箭頭。點擊會開啟模型選單；目前選取的模型會有勾號。沒有模型時，膠囊會隱藏。
3. **方格**：20 欄 × 10 列的正方形格子，順序為系統提示、工具、檔案與記憶、摘要、對話、剩餘空間，以及輸出保留空間。門檻線之後的剩餘空間格子會稍微淡一點。
4. **門檻線**：一條虛線，位於最接近門檻的列邊界上（80% 時位於第 8 列之後）。
5. **說明文字**：左側為 "Each cell is 5K tokens"，右側為一條虛線範例加上 "Compacts at 80%"。
6. **圖例**：七列（系統提示、工具、檔案與記憶、摘要、對話、剩餘空間、輸出保留空間），每列有色塊、token 數與占視窗的比例。空列會以淡色文字顯示 "No summary yet" 或 "0"，且不顯示比例。一條較粗的分隔線把已使用與剩餘的部分分開。
7. **動作**：Send message（主要）、Attach file（次要）與 Compact now（幽靈樣式，視窗仍有空間時位於右側）。沒有可傳送或可附加的內容時，Send message 與 Attach file 會隱藏。
8. **狀態列**：一個圖示加一句話：一般資訊用資訊圓圈，接近或超過門檻時用警告三角形，訊息收合時用收縮符號，壓縮成功時用勾選圓圈。

### 格子如何計算

視窗分為 200 格，因此一格代表視窗的 1/200：在 1M 視窗上為 5K，512K 上為 2.6K，400K 上為 2K。各部分以累計四捨五入排列：卡片依序加總各部分，並把每個累計值四捨五入為整數格（遇到一半時進位），每個部分取得兩個此類邊界之間的格子。對累計值做四捨五入，能讓方格誠實反映實際狀況：即使每個部分都很小，已使用的格數仍會與已使用的總量相符。保留空間單獨四捨五入，並從最後一格往回計算。視窗溢出時，各部分會在保留空間前停止，之後的部分不會分到格子；保留空間則一定會畫出來。

Token 數的顯示方式：低於 1,000 時顯示 "996"；低於 10K 時以一位小數顯示 "4.1K"（不保留結尾的 ".0"，因此顯示 "2K"）；低於一百萬時顯示 "509K"；高於一百萬則顯示 "1M" 或 "1.2M"。占比在低於 10% 時以一位小數顯示 "0.4%"（保留 "1.0%"），10% 起則顯示 "29%"。每列各自四捨五入，因此各列加起來可能是 99.8% 或 100.1%。

### 傳送、附加與壓縮

Send message 會加入 `Turns` 中的下一則，放在視窗末端；Attach file 會把 `Files` 中的下一個加到檔案與記憶的末端，摘要與對話隨之往後移。兩者在最後一項之後都會從頭開始。狀態列會說明加入了什麼，以及它花了多少 token。

從門檻的 85% 起（預設 80% 時即視窗的 68%），傳送或附件會發出警告：狀態列轉為警告，並加上壓縮即將發生的說明。一旦達到門檻，卡片會表示已經超過門檻，並在 540ms 後自動收合。切換到一個工作階段已超出其視窗的模型時，會立即發出警告，並在 600ms 後收合。

收合需要 700ms。收合期間，所有動作與模型膠囊都會停用，對話會從最舊的訊息到最新的訊息逐漸變淡。接著，除了最新的兩則之外，其餘所有訊息會合併為一段摘要（約為被收合 token 的 6%，至少 4K，至多 24K），摘要會緊接在檔案之後出現，而它所釋放的格子會回到剩餘空間。之後的壓縮會加進同一段摘要；摘要本身永遠不會被收合。當訊息數量多於最新的兩則時，Compact now 會依需求執行相同的操作。

| 時機 | 狀態 | 時間 |
|---|---|---|
| 超過門檻時傳送 | Added “Why does the nightly sync time out?” (+35K) · past 80% | 0 |
| 開始收合 | Past 80%: folding 35 earlier messages… | 540ms |
| 摘要出現 | Folded 608K into a 24K summary | 1,240ms |
| 切換到較小的模型 | 436K is over 80% of Grok 4.7 | 0 |
| 開始收合 | Too full for Grok 4.7: folding 22 earlier messages… | 600ms |

檔案永遠不會被收合。視窗超過門檻且只剩最新的訊息時，狀態列會說明目前還沒有可收合的內容；壓縮後若視窗仍超過門檻，狀態列會說明收合了多少，並建議移除檔案。已使用的 token 可以超過視窗大小：此時讀數會顯示超過 100%，且沒有剩餘 token。

### 你自己的工作階段

傳入你的模型、項目、回合與檔案。每個 `ContextWindowModel` 都有以 token 計的視窗與保留空間、一個名稱，以及一個可選的標記（24 × 24 的描邊字符，例如 `ArcIcons` 的成員）與其顏色。每個 `ContextWindowItem` 都有一個類別、一個標籤與它的 token 數；方格會依類別分組，因此順序只在同一類別內才有意義。

<Demo name="YourSession" />

```xml
<arc:ContextWindow>
    <arc:ContextWindow.Models>
        <arc:ContextWindowModel Id="heron-2" Name="Heron 2" Window="256000" Reserve="16000" />
    </arc:ContextWindow.Models>
    <arc:ContextWindowItem Category="System" Label="Style guide and instructions" Tokens="2400" />
    <arc:ContextWindowItem Category="Files" Label="handbook/billing.md" Tokens="14600" />
    <arc:ContextWindowItem Category="Messages" Label="Outline the refunds page" Tokens="8200" />
</arc:ContextWindow>
```

```csharp
card.ItemsChanged += (_, e) => log.Add($"{e.Change}: {e.Items.Count} items");
card.Compacted += (_, e) => log.Add($"Folded {e.FoldedCount} messages into {e.SummaryTokens} tokens ({e.Reason})");
```

卡片會隨著進行而改寫 `Items`：傳送或附加會插入一個項目，壓縮會移除被收合的訊息並加進摘要。`ItemsChanged` 會在每次變動後回報新的項目與造成變動的原因，`Compacted` 回報收合了什麼、它變成的摘要，以及前後的 token 數，`ModelChanged` 則追蹤膠囊。`SelectedModel` 為雙向繫結。`UsedTokens`、`FreeTokens`、`CanCompact` 與 `Phase` 為唯讀且可繫結。

### 從你的應用程式驅動

在真實的工作階段中，項目來自你的應用程式。若將 `Turns` 與 `Files` 留空，Send message 與 Attach file 就會隱藏；你可以使用 `AddItem` 加入真實的項目：它會把項目插入其所屬類別的位置，並執行與內建動作相同的波次、狀態與門檻檢查。你對 `Items` 或某個項目的 `Tokens` 所做的變更會立即顯示，且安靜無聲：沒有狀態列，也不會自動壓縮。

<Demo name="HostDriven" />

```csharp
// The summary your summarizer will write: here 5% of the folded tokens, at least 2,000.
card.Summarizer = folded => Math.Max(2_000, folded.Sum(item => item.Tokens) / 20);
card.AddItem(new ContextWindowItem(ContextWindowCategory.Messages, "Summarize the open tickets", 9_400));
```

`Summarizer` 會回傳被收合訊息的摘要大小；若未設定，卡片會使用這些訊息的 6%，範圍在 4K 到 24K 之間。若要自行執行摘要，請使用 `Compacted`。`AutoCompact` 會關閉自動收合：狀態列仍會說明何時超過門檻，而 Compact now 仍然可用。`KeepRecentMessages` 設定壓縮後保留最新幾則訊息（預設為兩則）。

### 不同的門檻

`Threshold` 是視窗中觸發壓縮的比例（預設 0.8）。虛線位於最接近它的列邊界上，因此 0.6 會在第 6 列之後畫出，0.75 會在第 8 列之後畫出，而判斷本身使用精確的值。警告從門檻的 85% 開始。

<Demo name="Threshold" />

### 窄容器

版面依卡片的內容寬度調整，即卡片寬度減去兩側各 24px 的內距。內容寬度在 640px 以內時，圖例會移到方格下方，格子間距變小（間距 2px、圓角 2px，而非 3px），圖例佔滿整個寬度。420px 以內時，模型膠囊會移到讀數下方。動作會換行；當 Compact now 無法與其他動作並排時，會移到新的一行開頭，狀態列也會換行。

<Demo name="Narrow" />

### 減少動態效果

在減少動態效果下，格子與數字會立即變化，狀態列會直接替換而沒有淡入淡出，對話不會變淡，壓縮也會立即完成。每個狀態仍會依序被朗讀。

<Demo name="ReducedMotion" />

### 文案規範

以你產品中的實際用途來命名各部分，並保持狀態語句簡短：每次變化都會被朗讀。項目標籤只會在狀態列中顯示，訊息會加上引號，因此請使用幾個字即可。

| 不要寫 | 改為 |
|---|---|
| 使用者訊息 #42 | 列出退款頁面的大綱 |
| /Users/me/projects/docs/handbook/billing.md | handbook/billing.md |
| 上下文壓縮已成功完成 | 已將 279K 收合為 17K 摘要 |
| 模型 gpt-like-large-2025 | Heron 2 |

`Labels` 接受一個 `ContextWindowLabels` 物件，其中包含卡片顯示或播報的每一個文字，包括圖例名稱與狀態語句。含有數字的語句是函式，因此其他語言可以調整字序：

```csharp
card.Labels = new ContextWindowLabels
{
    Messages = "Chat",
    Folded = (folded, summary) => $"{folded} folded into a {summary} summary"
};
```

### 最佳做法

**建議**

- 提供工作階段的真實用量：系統提示、每個工具定義、附加的檔案與記憶、摘要，以及每個回合與其所產生的工具結果。
- 將保留空間設為你要求的最大輸出，讓方格顯示回覆所需的空間。
- 傳入會回報真實摘要大小的 `Summarizer`，前提是你的摘要器已經執行過。
- 在選單中保留兩到三個模型，標記使用通用字符，而不要用廠商標誌。

**避免**

- 用計時器重建 `Items`。每次變化都會有動畫；只要更新有變動的項目即可。
- 把虛構的工作階段當成真實用量呈現。
- 把方格用於單一視窗以外的用途。它永遠有 200 格；其比例只對應一種預算才有意義。

### 常見問題

**為什麼有時小的部分沒有格子？** 格子是從累計值計算的，因此小於半格的部分可能被四捨五入為零，而總數仍然精確。圖例一定會顯示它的 token 數。

**為什麼摘要不會變小？** 壓縮會把新的摘要加進既有的摘要，而摘要永遠不會被收合。當你的應用程式改寫摘要時，請在 `Items` 中移除或替換摘要項目。

**切換到較大的模型會發生什麼？** 方格會重新排列，狀態列會說明新的模型與視窗，且不會收合任何內容。

**為什麼沒有 Send message 與 Attach file？** 當 `Turns` 或 `Files` 為空時，它們會隱藏。

**視窗可以溢出嗎？** 可以。讀數會超過 100%，剩餘空間顯示 0，而那些放不進保留空間之前的部分不會分到格子。

## API 參考

<!-- api: ContextWindow -->

<!-- api: ContextWindowModel -->

<!-- api: ContextWindowItem -->

<!-- api: ContextWindowTurn -->

<!-- api: ContextWindowLabels -->

<!-- api: ContextWindowCompactedEventArgs -->

<!-- api: ContextWindowModelChangedEventArgs -->

<!-- api: ContextWindowItemsChangedEventArgs -->

列舉：

| 類型 | 值 |
|---|---|
| `ContextWindowCategory` | `System`, `Tools`, `Files`, `Summary`, `Messages` |
| `ContextWindowPhase` | `Idle`, `Summarizing` |
| `ContextWindowCompactReason` | `Manual`, `Threshold`, `ModelSwitch` |
| `ContextWindowChange` | `Sent`, `Attached`, `Added`, `Compacted` |

## 無障礙

- 卡片是一個名為 "Context window" 的群組。方格是一張圖像，其名稱完整描述它：模型、視窗已使用的 token、每個部分及其 token 數與門檻。每次變化都會更新，但不會隨動畫的每個影格更新。
- 讀數會被朗讀為一行（"436K of 1M tokens, 44% used, 532K free"）。圖例是一個名為 "Context breakdown" 的清單，每列朗讀其名稱、token 數與占比，因此不依賴顏色。
- 膠囊以模型及其視窗命名，並說明它會切換模型。選單中的模型是單選項目，目前選取的會打勾。
- 每次傳送、附加、切換模型與壓縮，都會透過禮貌的即時區域播報，每個狀態都同時有圖示與文字。
- 訊息收合期間，所有動作都會關閉。卡片不會讓鍵盤焦點掉到頁面上，而是把它保留在一個以狀態命名的隱藏容器上，並在壓縮完成時交還：交還給原本擁有焦點的動作，若該動作已停用，則交給 Send message。
- 鍵盤焦點會顯示 2px 的焦點環；滑鼠焦點則不顯示。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 在模型膠囊、Send message、Attach file 與 Compact now 之間移動 |
| `Enter` / `Space` 於膠囊 | 開啟模型選單，並停留在目前選取的模型 |
| `Up` / `Down` 於膠囊 | 開啟模型選單 |
| `Up` / `Down` 於選單 | 在模型之間移動，並循環繞回 |
| `Enter` / `Space` 於選單 | 選擇模型、關閉選單並回到膠囊 |
| `Escape` 於選單 | 關閉選單並回到膠囊 |
| `Enter` / `Space` 於動作 | 傳送、附加或壓縮 |

## 動態效果

- 新的 token 會在一個短暫的波次中，從第一個變化的格子開始重新上色：末端加入的訊息每格 9ms，中間插入的檔案以及壓縮釋放的格子每格 3ms，最多不超過 360ms。每格會在 240ms 內以標準曲線轉色，並在 OKLab 中混合其顏色。
- 模型切換會從第一格開始，一次通過重新上色所有變化的格子，每格 1.4ms。
- 訊息收合期間，對話會從最舊的訊息到最新的訊息逐漸變淡（每格在 720ms 內降到 0.35 再回到 0.5，每格間隔至多 6ms，總計不超過 420ms）。摘要出現時，格子會立即恢復完全不透明，並在 3ms 的波次中重新上色。
- 大數字、百分比，以及圖例的 token 與占比，會在 400ms 內以 ease-out 曲線滾動到新值，起點是它們當下顯示的數值，因此快速連續的傳送不會倒退跳動。剩餘 token、視窗與說明文字會立即變化。
- 狀態列會在原位交叉淡化：舊的狀態在 100ms 內淡出，新的狀態在 160ms 內淡入並上移 3px。
- 指向圖例的某一列時，該列的格子會亮起，其他格子在 160ms 內淡至 0.28，其他列淡至 0.45。
- 模型選單會以 CSS 彈簧效果從膠囊處展開，箭頭隨之翻轉。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 格子 | 以波次重新上色 | 立即變化 | 立即變化 |
| 數字 | 在 400ms 內滾動 | 立即變化 | 立即變化 |
| 狀態列 | 交叉淡化 | 直接替換 | 直接替換 |
| 收合 | 變淡 700ms 後完成 | 立即完成 | 立即完成 |
| 自動壓縮 | 540ms 或 600ms 後 | 立即 | 立即 |
| 圖例懸停 | 在 160ms 內淡入 | 立即 | 立即 |

## 相關元件

- `Progress`：用於帶標籤的單一配額。
- `PromptCache`：說明共用的提示前綴如何在多個請求之間重複使用。
- `AgentLoop`：說明代理如何逐步收集上下文。

| 元件 | 適用時機 |
|---|---|
| `Progress` | 一個數字對應一個上限就足夠 |
| `PromptCache` | 你解釋的是快取，而非預算 |
| `AgentLoop` | 你解釋的是代理如何運作，而非什麼填滿了它的視窗 |
