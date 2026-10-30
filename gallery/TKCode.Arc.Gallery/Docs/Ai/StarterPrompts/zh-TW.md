---
title: Starter prompts
summary: AI 應用程式空狀態裡的問候語與起始卡片：附分類分頁、以交叉淡化切換卡片的「換一批」，以及由所選卡片填入的輸入框。
source-hash: 6969be5a1813
---

<Demo name="Preview" />

## 何時使用

- 聊天、助理或代理程式在第一則訊息之前的空狀態。
- 介紹助理能藉由已連結的工具做什麼，例如郵件、程式碼存放庫或文件空間。
- 用幾則量身打造、可以直接傳送的提示詞引導新的工作區。

## 何時不用

- 對話已經開始時；請改用對話裡的後續建議標籤。
- 附搜尋的指令啟動器；請使用指令面板。
- 文字方塊裡的單一行內建議；請使用幽靈文字。

## 用法

### 空狀態

沒有資料時，控制項只顯示「全部」分頁和輸入框，沒有卡片，也沒有問候語。為它提供問候語、一行說明、分頁和起始提示（預覽中的 24 則分布在 Write、Code、Analyze、Plan 四個分類中，皆為虛構），它每次顯示 4 則。選擇一張卡片會把它完整的 `Prompt` 填入輸入框、讓輸入框取得焦點並引發 `PromptSelected`；按 Enter
傳送並引發 `SubmitRequested`。「換一批」以短暫的錯開交叉淡化顯示該分頁的下一批起始提示，而且至少帶來一張原本不在
畫面上的卡片。

```xml
<arc:StarterPrompts Greeting="Good morning, Maya" ModelName="Qwen3.8-Max" />
```

使用者瀏覽的是卡片標題，輸入框裡顯示的是提示詞。標題要簡短並以動詞開頭，把完整、具體的指示放進 `Prompt`。

### 你自己的起始提示與分頁

<Demo name="OwnStarters" />

在 `Categories` 加入 `StarterPromptCategory`（一個 id、一個標籤和選用的 24 × 24 描邊 `Icon`），並把 `StarterPrompt`
作為內容加入（`Id`、`Category`、`Title`、`Description`、`Prompt`、`App`）。「全部」會自動加在最前面，沒有起始提示的
分頁會隱藏。`Count` 決定畫面上同時有幾張卡片；格線始終保留這麼多個位置，所以起始提示較少的分頁會留下空位，下方
內容不會移動。

`App` 標示使用了已連結應用程式的卡片：它的標誌畫在一個淺色底的方塊裡，名稱顯示在旁邊，也會讀入卡片的無障礙名稱。
預設值（`Mail`、`Repository`、`Docs`、`Issues`、`Calendar`）是中性的圖示；真實的整合請傳入它自己的名稱與標誌：

```csharp
var gmail = new StarterPromptApp("Gmail", myGmailGeometry);
```

沒有 `Mark` 的 `StarterPromptApp` 會顯示其名稱的第一個字母。

### 傳送

<Demo name="Sending" />

`SubmitRequested` 交出去除前後空白的 `Message`、它來自的 `Prompt`（由使用者手寫時為 null）以及一個 `CancellationToken`。
把請求的工作放進 `Sending`：工作完成之前，頁尾顯示「傳送中」，傳送按鈕變成停止按鈕。工作完成後輸入框清空，頁尾顯示
「已傳送」。工作擲回例外時，頁尾顯示警示圖示、例外的訊息和「重試」，文字保留。停止按鈕與 Escape 會取消該權杖並保留
文字；控制項離開視窗時也一樣。

```csharp
prompts.SubmitRequested += (_, e) =>
    e.Sending = chat.SendAsync(e.Message, e.CancellationToken);
```

如果沒有處理常式設定 `Sending`，傳送是模擬的，頁尾會註明這一點。`SendStatus` 與 `SendError` 回報狀態，`Submit()` 與
`StopSending()` 的作用與按鍵相同，`Text` 是輸入框的文字（雙向）。

### 你自己的輸入框

<Demo name="OwnComposer" />

`ShowsComposer="False"` 隱藏內建的輸入框，讓卡片為應用程式既有的輸入框提供內容。選擇卡片仍會設定 `Text`，並帶著該
起始提示引發 `PromptSelected`。

### 載入

`IsLoading` 顯示與真實卡片尺寸、節奏相同的預留位置卡片；它們緩慢呼吸、無法選擇，「換一批」也會停用。關閉後，起始
提示在同樣的框裡淡入。

### 窄容器

<Demo name="Narrow" />

控制項寬度達到 560px 時卡片排成兩欄，更窄時排成一欄。分頁在自身內部捲動並帶有邊緣漸隱，寬度小於 440px 時「換一批」
只顯示圖示，小於 480px 時內距、圓角與問候語都會縮小一級。控制項填滿它的容器，寬度從不跟著卡片或文字變化。

### 你自己的文字

`Greeting`、`Description`（設為 null 即隱藏）、`Placeholder` 以及 `ModelName` 與 `ModelMark` 都是屬性。其餘所有文字
都在 `Labels` 中：

```csharp
prompts.Labels = new StarterPromptsLabels
{
    All = "Alle",
    Shuffle = "Mischen",
    Send = "Nachricht senden",
    CardName = (title, app, category) => app is null ? title : $"{title}, mit {app}",
};
```

### 內容準則

- 標題只有幾個詞，並以動詞開頭（「規劃我的一週」）；說明補上讓它具體的細節。
- 只有提示詞確實用到某個整合時才設定 `App`，這樣標誌才有意義。
- 問候語簡短而親切；下面一行安靜的說明就夠了。

### 最佳做法

- 把它當作新聊天或新代理程式對話的第一個畫面，訊息送出後換成對話本身。
- 遵守取消權杖，讓停止真的停止請求。
- 讓起始提示的總數多於 `Count`，「換一批」才有內容可換；數量不夠時它會停用。

### 常見問題

**它會轉譯每一則起始提示嗎？** 不會。只有畫面上的卡片存在；提示池可以容納數百則起始提示。

**能用程式碼選擇分頁嗎？** 可以。`SelectedCategory` 是雙向的；不對應任何可見分頁的 id 會顯示「全部」。

## API 參考

<!-- api: StarterPrompts -->

<!-- api: StarterPrompt -->

<!-- api: StarterPromptCategory -->

<!-- api: StarterPromptApp -->

<!-- api: StarterPromptsLabels -->

<!-- api: StarterPromptsSubmitEventArgs -->

<!-- api: StarterPromptSelectedEventArgs -->

## 無障礙

- 問候語是標題。每張卡片都是按鈕，名稱依序讀出標題、已連結的應用程式與分類（「Plan my week, uses Calendar, Plan」）。
- 一個禮貌（polite）的即時區域播報選擇、換一批、傳送中、已傳送與錯誤，並說明傳送是否為模擬。
- 狀態從不只靠顏色表達：已傳送顯示勾號與文字，失敗顯示警示圖示與訊息。
- 傳送按鈕名為「Send message」，訊息傳送途中名為「Stop sending」。
- 所有文字都在 `StarterPromptsLabels` 或屬性中。

### 鍵盤

| 按鍵 | 動作 |
|---|---|
| Tab | 依序經過分頁、換一批、每張卡片和輸入框 |
| Left、Right | 在分類分頁之間移動 |
| Enter、Space | 在卡片上：用它的提示詞填入輸入框並讓輸入框取得焦點 |
| Enter | 在輸入框中：傳送訊息 |
| Shift+Enter | 在輸入框中：換行 |
| Escape | 傳送途中：停止傳送並保留文字 |

## 動態效果

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 換一批或切換分頁 | 每張卡片的內容在 100ms 內淡出，接著新內容在 160ms 內淡入並上升 2px；卡片之間相隔 35ms 開始（總共約 100ms），都在固定的框內 | 立即 | 立即 |
| 載入結束、起始提示到達 | 內容以同樣的方式錯開淡入 | 立即 | 立即 |
| 選擇卡片 | 輸入框取得焦點，新文字在 160ms 內淡入 | 立即 | 立即 |
| 頁尾狀態 | 原地交叉淡化：淡出 100ms，淡入 160ms | 120ms 淡化 | 立即 |
| 傳送與停止圖示 | 在 160ms 內交叉淡化；沒有移動或尺寸變化 | 立即 | 立即 |
| 載入預留位置 | 每 1.6 秒呼吸一次 | 靜止 | 靜止 |
| 分頁選取 | 選取塊沿著分頁的彈簧滑動 | 立即 | 立即 |

## 相關元件

- `AgentStatus`：執行開始後代理程式的即時狀態。
- `ToolCall`：代理程式發出的工具呼叫。
- `Tabs` 與 `SegmentedControl`：在其他地方切換檢視。
- `EmptyState`：沒有內容可顯示時提供有用的下一步。
