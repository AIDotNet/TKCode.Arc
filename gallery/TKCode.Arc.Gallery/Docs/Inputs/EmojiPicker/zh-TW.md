---
title: Emoji picker
summary: 依名稱尋找表情符號，或瀏覽每個分類；最近用過的表情符號排在最前，人物和手勢可以選擇膚色，表情符號格線可以完全用鍵盤操作。
source-hash: e2e8bd91b485
---

<Demo name="Preview" />

## 何時使用

`EmojiPicker` 是一次選擇一個表情符號的面板。頂部的搜尋框會隨著輸入依名稱和關鍵字篩選；下面是一排分類圖示，可以跳到各個 Unicode 分組；格線顯示所有表情符號，最近選過的單獨放在最上面的一節。搜尋框旁邊的手勢按鈕為所有帶膚色的人物和手勢設定膚色。格線可以完全用鍵盤操作，只繪製可見的列；底部一行顯示指標或鍵盤所在表情符號的名稱。

- **聊天或留言中的回應和回覆**：從訊息框旁邊的笑臉按鈕開啟。
- **狀態和個人資料欄位**：狀態訊息或工作區名稱前面放一個表情符號。
- **標籤和標記**：用表情符號作為頻道、專案或儲存檢視的圖示。

## 何時不用

- **固定的幾個回應**：直接把這幾個顯示為按鈕；為一個讚開啟完整的選擇器是繞遠路。
- **為介面挑選圖示**：表情符號使用系統字型，在每個系統上看起來都不同；結果必須處處一致時，用圖示集。
- **輸入可能包含表情符號的文字**：系統內建的表情符號鍵盤在所有文字方塊中都能用；把選擇器作為捷徑提供，而不是唯一的方式。

## 用法

把選擇器放在使用者搆得到的地方，通常是訊息框旁邊的 `Popover` 中，並處理 `EmojiSelected`。`e.Emoji` 是要插入的文字，已經套用了所選的膚色。

```xml
<arc:EmojiPicker MaxRecent="16" />
```

```csharp
picker.EmojiSelected += (_, e) => message.Text += e.Emoji;
```

`EmojiPicker` 位於 Special 套件中：在 `UseArcControls()` 旁邊用 `UseArcSpecial()` 註冊。

### 放在彈出面板中

選擇器自帶一個凸起的面板。放在本身已經是面板的 `Popover` 中時，把 `Background` 設為 `Transparent`，`BorderThickness` 和 `Padding` 設為 0。彈出面板開啟時焦點進入搜尋框，使用者可以直接輸入；在 `EmojiSelected` 中關閉彈出面板。

<Demo name="InComposer" />

### 最近使用

每次選擇都會把表情符號（不含膚色）移到 `Recent` 的最前面，最多 `MaxRecent`（24）個。這一節顯示的是選擇器上次顯示時的清單，所以使用者挑選時表情符號不會移動。填入 `Recent` 可以還原儲存的清單，監聽它的 `CollectionChanged` 可以儲存清單；把 `MaxRecent` 設為 0 可以隱藏這一節。

```csharp
foreach (var emoji in settings.RecentEmoji)
{
    picker.Recent.Add(emoji);
}
picker.Recent.CollectionChanged += (_, _) => settings.RecentEmoji = picker.Recent.ToList();
```

### 膚色

`SkinTone` 為所有帶膚色的表情符號套用五種膚色之一，或預設的黃色；其他表情符號不變。它是雙向的，所以選擇器中的手勢按鈕和你自己的設定保持一致。

<Demo name="SkinTone" />

### 搜尋

搜尋比對詞的開頭：「hea」找到所有愛心，「red hea」找到紅色愛心。名稱中的相符項排在前面，然後是 CLDR 關鍵字中的相符項（「happy」「lol」）。`SearchText` 是雙向的，頁面可以帶著查詢字詞開啟選擇器。在搜尋框中按下方向鍵進入結果，按 Enter 選擇第一個結果。

### 表情符號版本

表情符號資料來自 Unicode 16.0，但圖形來自系統的表情符號字型。`MaxEmojiVersion`（預設 15.0）會排除較新的表情符號，所以缺少它們的系統不會顯示空白方框。只針對較新系統時可以調高。

### 結構

1. **搜尋**（`IsSearchVisible`）：搜尋框和膚色手勢按鈕。
2. **分類**：每節一個圖示；格線捲動時，可見分節下方的短條會滑過去。
3. **格線**：分節標題（頂部的標題保持固定）和一列列 36px 的格子。
4. **預覽**（`IsPreviewVisible`）：指標或鍵盤所在的表情符號，放大顯示，並帶有名稱。

### 文案建議

| 不要寫 | 改為 |
|---|---|
| Emoji | Add emoji（觸發按鈕的無障礙名稱） |
| Type to search for an emoji... | Search emoji |

## API 參考

<!-- api: EmojiPicker -->

<!-- api: EmojiSelectedEventArgs -->

<!-- api: EmojiPickerLabels -->

列舉：

| 型別 | 值 |
|---|---|
| `EmojiCategory` | `Recent`、`Smileys`、`People`、`Nature`、`Food`、`Travel`、`Activities`、`Objects`、`Symbols`、`Flags` |
| `EmojiSkinTone` | `Default`、`Light`、`MediumLight`、`Medium`、`MediumDark`、`Dark` |

## 無障礙

- 搜尋框以 `Labels.Search` 命名；每次變化後回報相符的數量（「12 emoji found」）或沒有相符項。
- 分類條是一個 Tab 停駐點，以有焦點的分類命名。左右方向鍵在分類之間移動，並讓格線跳過去；Home 和 End 到第一個和最後一個；Enter 或 Space 跳到有焦點的分類。
- 格線是一個 Tab 停駐點。它的無障礙名稱是目前表情符號的名稱，說明文字指出所在分節和位置（「Smileys and emotion, 3 of 168」）。
- 膚色手勢按鈕名為「Skin tone: Medium」這樣的文字；它會展開成一排六種膚色。

### 鍵盤

| 按鍵 | 位置 | 作用 |
|---|---|---|
| 輸入文字 | 搜尋 | 篩選格線 |
| `Down` | 搜尋 | 進入格線 |
| `Enter` | 搜尋 | 選擇第一個結果 |
| `Escape` | 搜尋 | 清除搜尋 |
| 方向鍵 | 格線 | 移動目前的表情符號，可以跨過分節標題 |
| `Home` / `End` | 格線 | 本列的第一個或最後一個表情符號 |
| `Ctrl` + `Home` / `End` | 格線 | 所有表情符號中的第一個或最後一個 |
| `Page Up` / `Page Down` | 格線 | 移動一個畫面的列 |
| `Enter` / `Space` | 格線 | 選擇目前的表情符號 |
| 字母 | 格線 | 繼續在搜尋框中輸入 |
| `Left` / `Right` | 膚色（展開時） | 在膚色之間移動；`Enter` 選擇，`Escape` 收合 |

## 動效

- 選擇分類後，格線以平滑的彈簧動畫捲動過去；滾輪或拖動會立即接手。
- 分類下方的短條以靈敏的彈簧動畫滑到可見的分節。
- 膚色列以寬度彈簧動畫展開和收合。
- 搜尋時，分類條在 160ms 內淡到一半不透明度。
- 減少動態效果時跳轉和短條移動立即完成，淡出為 120ms；關閉動態效果時一切立即完成。

## 響應式

- 選擇器預設寬 352px；格線在可用寬度內放下盡可能多的 36px 欄，並置中排列。
- 格線高 288px，可以捲動；只繪製可見的列，每個表情符號的文字版面在它第一次顯示時才建立。
- 放在較窄的彈出面板中時，設定合適的 `Width`；分類會鋪滿可用的寬度。

## 相關元件

- `Popover`：從按鈕開啟選擇器。
- `MentionInput` 和 `TokenTextEditor`：帶有行內標記的文字輸入。

| 元件 | 適用情境 |
|---|---|
| `EmojiPicker` | 使用者依名稱或瀏覽來選擇任意表情符號 |
| `Popover` | 顯示幾個你自己固定的回應 |
| `SegmentedControl` | 幾個類似表情符號的選項中的一個是一項設定 |
