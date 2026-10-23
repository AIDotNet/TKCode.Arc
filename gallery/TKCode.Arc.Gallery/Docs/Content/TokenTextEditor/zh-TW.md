---
title: 權杖文字編輯器
summary: 多行文字編輯區，其中的命令與提及等權杖作為單一單位運作，觸發符會開啟你自己清單的查詢。
source-hash: f2eeb20780e2
---

<Demo name="Preview" />

## 何時使用

權杖文字編輯器是提示框、留言欄位或範本編輯器背後的寫作區：純文字，其中某些部分不只是文字。`/recap` 命令或 `@file` 提及是一個單位，你可以整體移動、刪除與選取，而你的程式碼可以讀回它的意義。

- **帶有命令與提及的提示框**：行首的「/」選擇命令，「@」選擇檔案或人員。
- **帶有變數的範本編輯器**：以雙大括號包住的變數（例如客戶名稱）不應逐字元編輯。
- **留言欄位**：會通知某人的提及，文字仍保持可讀。
- **你自己的提及控制項**：`MentionInput` 建構於此編輯器之上；當權杖不是人員與頻道時，直接使用此編輯器。

## 何時不用

- **一般多行純文字**：使用 `Textarea`。
- **已有現成清單的 @人員與 #頻道**：使用 `MentionInput`，它自帶建議清單與序列化。
- **富文字**（粗體、連結、圖片）：權杖是唯一的格式，文字為純文字。
- **單一簡短的值**：使用 `Input`，或用 `TagInput` 處理一組值。

## 用法

`TokenTextEditor` 衍生自 Avalonia 的 `TemplatedControl`。它內含一個真正的 `TextBox`，其文字為透明，並在其後以著色的權杖繪製相同的文字，因此插入點、選取、輸入法、復原與剪貼簿都是平台本身的行為。它不繪製外框：請將它放在你自己的表面上。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:TokenTextEditor Placeholder="Type / for a command or @ for a file" MinLines="2" MaxLines="5">
        <arc:TokenTextEditor.Triggers>
            <arc:TokenTrigger Symbol="/" Kind="command" Position="LineStart" />
            <arc:TokenTrigger Symbol="@" Kind="mention" Position="AfterWhitespaceOrLineStart" />
        </arc:TokenTextEditor.Triggers>
    </arc:TokenTextEditor>
</UserControl>
```

### 觸發符

`TokenTrigger` 是開啟查詢的符號：`Symbol`、它產生的權杖 `Kind`，以及它可以開始的位置（`Position`）：`Anywhere`、`AfterWhitespaceOrLineStart`（文字開頭、行首或空白之後）或 `LineStart`（僅限作為行的第一個字元）。查詢在空白處結束，除非 `AllowSpacesInQuery` 或 `MaxQuerySpaces` 允許部分空白；另一個觸發符的符號同樣會結束它。`AllowAfterOpeningBracket` 讓提及可以緊接在「(」之後開始。

編輯器不顯示清單。它會引發 `TriggerChanged`，其中帶有查詢的狀態（觸發符、其後輸入的文字，以及符號與插入點的矩形範圍，以便你放置清單）。你篩選自己的項目，並以 `InsertToken(kind, id, label)` 回應；這會以權杖加一個空白取代輸入中的查詢，並將插入點留在其後。`CancelTrigger()` 告訴編輯器你的清單已被關閉。

<Demo name="Preview" />

### 權杖

`Tokens` 列出目前文字的 `TokenSpan`：你給的 `Kind`、`Id` 與 `Label`，以及 `Start` 與 `End` 位移量。當其前方的文字變動時它們會移動；當有人在權杖內編輯時，它們會變回純文字。以 `SetTextWithTokens(text, tokens)` 設定整個值，以 `InsertTokenAtCaret` 在插入點新增一個權杖，並在 `TextChanged` 中讀取變更。

方向鍵會跳過一個權杖；緊接其後按 Backspace、緊貼其前按 Delete 會移除整個權杖；點在權杖內部會落在最近的邊緣；Shift 加方向鍵會逐字元選取（設定 `ExpandSelectionToTokens` 可整個權杖選取）。復原與重做會將被移除的權杖與文字一併帶回。

### 設定權杖樣式

每種類型都可以有自己的顏色：在 `TokenStyles` 中加入一個 `TokenStyle`，指定 `Kind`、`Fill` 與 `Foreground`。沒有對應項目時，套用編輯器的 `TokenFill` 與 `TokenForeground`。`TokenCornerRadius`、`TokenPadding` 與 `TokenOutset` 塑造方框的外形；跨越換行的權杖預設會分段繪製（`TokenDecorationBreak`）。

### 高度與送出

高度會依文字從 `MinLines` 到 `MaxLines` 行而變化，之後文字捲動。Enter 會引發 `SubmitRequested`，帶著文字與權杖；Shift+Enter 新增一行，而輸入法組字期間不會送出任何內容。將 `SubmitOnEnter` 設為 false，即可讓 Enter 新增一行。將事件標記為已處理可保留文字；當你想重新開始時，請自行清空。

<Demo name="Growth" />

### 讀取狀態

`TriggerState` 是目前的查詢（或 null），`CaretIndex` 為插入點位置，`IsComposing` 表示輸入法是否正在組字。`InsertText`、`Select`、`Clear`、`Undo` 與 `Redo` 的作用即其名稱所示。

## API 參考

<!-- api: TokenTextEditor -->

<!-- api: TokenTrigger -->

<!-- api: TokenStyle -->

### TokenSpan

| 屬性 | 說明 |
|---|---|
| `Kind` | 你給權杖的類型，例如 `command` 或 `mention` |
| `Id` | 你為權杖所代表之物所設的識別碼 |
| `Label` | 符號之後的文字 |
| `Start` / `End` | 權杖在文字中的範圍（包含符號） |

### 事件

| 事件 | 引發時機 | 引數 |
|---|---|---|
| `TextChanged` | 文字或權杖變更 | `OldText`、`OldTokens`、`Text`、`Tokens` |
| `TriggerChanged` | 查詢開啟、變更或關閉 | `PreviousState`、`State`（關閉時為 null） |
| `TokenInserted` | `InsertToken` 產生了一個權杖 | `Token` |
| `SubmitRequested` | 未按 Shift 的 Enter，且無輸入法組字 | `Text`、`Tokens`；設定 `Handled` 以保留文字 |

## 無障礙

- 對輔助技術而言，它是一個文字方塊：保有平台的插入點、選取與輸入法行為，其讀取的文字是純文字，包括權杖在內。
- 當你的清單開啟時，設定 `SuggestionsExpanded`，使編輯器回報自己為展開狀態；當輔助技術要求關閉時，它會引發 `CollapseRequested`。請在你自己的即時區域中播報符合項目的數量。
- 權杖不會被讀為獨立的控制項：為它們提供清楚的標籤（「recap」而非識別碼）。
- 權杖顏色在淺色與深色主題中都保有對比；不要只靠顏色傳達意義。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| 輸入觸發符號 | 在觸發符的位置規則允許之處開啟查詢 |
| `ArrowLeft` / `ArrowRight` | 移動插入點，一步跳過一個權杖 |
| 權杖之後的 `Backspace` / 之前的 `Delete` | 移除整個權杖 |
| `Shift` + 方向鍵 | 逐字元選取 |
| `Enter` | 引發 `SubmitRequested` |
| `Shift+Enter` | 新增一行 |
| `Ctrl/Cmd+Z`、`Ctrl/Cmd+Shift+Z` | 復原與重做，包括權杖 |

## 動態效果

- 高度在 `MinLines` 到 `MaxLines` 之間隨文字變化，以短暫的緩動進行（預設 160 毫秒；設定 `HeightMotion` 可更改）。
- 權杖是繪製出來的，而非動畫：插入時立即出現，其著色隨主題變化。
- 減少動態效果與關閉時，高度變化立即套用。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 高度增加或減少 | 短暫緩動 | 立即 | 立即 |
| 插入或移除權杖 | 立即 | 立即 | 立即 |

## 相關元件

- `MentionInput` 用於具內建清單的 @人員與 #頻道。
- `Textarea` 用於一般多行純文字。
- `TagInput` 用於一組簡短的值。

| 元件 | 適用時機 |
|---|---|
| `MentionInput` | 你需要開箱即有建議的人員與頻道 |
| `Textarea` | 文字沒有權杖 |
| `TagInput` | 值是一組標籤 |
| `AiComposer` | 你需要完整的提示框：附件、工具、模型與傳送 |
