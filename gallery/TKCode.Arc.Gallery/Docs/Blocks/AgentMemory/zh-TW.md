---
title: 助手記憶
summary: 一張顯示助手記住什麼的卡片：依群組篩選、搜尋、就地編輯一句話、忘記後有幾秒鐘可以復原，並可把剛記下的記憶歸入群組，也能手動新增一則記憶。
source-hash: 2f0de5bce8d3
---

<Demo name="Preview" />

## 何時使用

助手記憶是人們查看、修正並刪除助手為他們保存之內容的頁面。它是一張卡片：容量計量條、兩個開關、篩選膠囊、搜尋、依類型分組的記憶，以及一個新增記憶的欄位。

- **會學習的助手設定頁面**："Reference saved memories"（參考已儲存的記憶）與 "Reference chat history"（參考聊天紀錄）是人們會找的兩個開關，它們下方的清單就是已保存內容的證明。
- **聊天後的信任介面**：助手記下了一件事（"Noted: you run builds with the dotnet CLI"）。擷取膠囊會顯示它，並在按下一次後歸入群組。
- **需要能夠審查的記憶**：每一句話都可以就地編輯或忘記，而忘記的內容會先保留幾秒鐘的復原時間，之後才真正生效。
- **有容量上限的記憶**：計量條顯示已使用的容量比例，並在將滿之前發出警告。

## 何時不用

- **事件或訊息的紀錄**：一則記憶是一句沒有句號結尾的短句。歷史紀錄請使用 `Timeline` 或表格。
- **長篇筆記或文件**：每則記憶的長度上限為 `MaxLength` 個字元（預設 200）。長文字請使用編輯器。
- **不是記憶的設定**：開關與選項請放在使用 `Switch` 與 `Select` 的設定頁面。
- **無法復原的刪除**：復原列就是這個設計的一部分。如果你的資料在幾秒內無法還原，請在說明文字中說明，並縮短 `UndoDuration`。
- **儲存記憶**：卡片只會變更清單並告知你。儲存、同步，以及助手如何使用這些記憶，都是你的程式碼負責的事。

## 用法

`AgentMemory` 衍生自 Avalonia 的 `TemplatedControl`。一則記憶是一個 `MemoryItem`，即 `AvaloniaObject`，帶有 id、一句話、所屬群組的鍵、來源，以及日期。群組是 `MemoryGroup`；若未設定，則使用四個預設群組（About you、Preferences、Projects、People）。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:AgentMemory AssistantName="Orbit" Capacity="2000">
        <arc:AgentMemory.Memories>
            <arc:MemoryItem Id="1" GroupKey="preferences" SourceKind="Chat"
                            Text="Prefers tables to long bullet lists" />
            <arc:MemoryItem Id="2" GroupKey="people" PersonName="Mina Okafor"
                            Text="Mina Okafor reviews every database migration" />
        </arc:AgentMemory.Memories>
    </arc:AgentMemory>
</UserControl>
```

### 結構

- **標頭**：標題、點名助手的副標題，以及容量計量條（"62% full" 配上一條細長的條）。
- **開關**："Reference saved memories" 與 "Reference chat history"，每個都附一行說明其狀態的文字。
- **工具列**：篩選膠囊，以及最末端的搜尋按鈕。
- **擷取膠囊**：助手最新的筆記。按下它即可把記憶歸入其群組。
- **清單**：記憶，依標題分組並附上數量；高度超過 `ListMaxHeight` 時，清單會在卡片內捲動。
- **新增欄位**：輸入一句話並按下 Enter；卡片會把它歸入某個群組。

### 篩選與搜尋

膠囊依群組篩選；All 檢視會顯示標題下有記憶的每個群組，單一群組則顯示其記憶且不帶標題（若群組為空，則顯示它自己的一句話）。搜尋按鈕會在膠囊上方開啟一個輸入框。它會比對句子中的任何位置、不分大小寫，並醒目提示每一個符合的地方。結果依群組分組，並顯示每個群組的符合數；無論篩選為何，搜尋都涵蓋所有群組。按下 Escape 或關閉按鈕會清除搜尋，並回到你原本的篩選。

`SelectedGroupKey`、`SearchText` 與 `IsSearchOpen` 是雙向的，因此你可以在程式碼中設定它們。

<Demo name="Bindings" />

若要讓卡片跟隨一個清單，請使用 `MemoriesSource`（`ObservableCollection<MemoryItem>` 即可）：卡片會在該清單上進行變更，而你在清單中所做的修改也會出現在卡片裡。

### 編輯與忘記

每一列上的 Edit 按鈕（滑鼠懸停或取得焦點時可見）會開啟一個小型編輯器：Enter 儲存，Shift+Enter 加入一行，Escape 取消。空白或過長的句子會被拒絕並顯示訊息；超過 `MaxLength` 時會出現計數器（"212 of 200 characters"）。忘記會把該列變成一條列，文字為 "Forgot “…”"，並附上一個 **Undo** 按鈕，其倒數時間為 `UndoDuration`。數量與計量條會立即下降；變更會在時間結束時才提交，並且只在那時才引發 `MemoryChanged`。多條復原列可以同時倒數。

### 儲存變更

設定 `CommitAsync` 可讓變更在生效前先儲存。任務執行時，編輯器會顯示儲存中的狀態。如果它拋出例外，訊息會顯示在原處，且該變更會被復原；如果它被取消，變更會被靜默復原。忘記的內容在其復原時間結束後才會提交。

<Demo name="Commit" />

```csharp
memory.CommitAsync = async (change, cancellation) =>
{
    await api.SaveAsync(change.Kind, change.Memory.Id, change.Memory.Text, cancellation);
};
```

`MemoryChange` 包含 `Kind`（`Capture`、`Add`、`Edit` 或 `Forget`）、`Memory`，以及編輯時的 `PreviousText`。`MemoryChanged` 會回報該變更，以及變更後的整個清單。

### 擷取的記憶

把助手剛記下的內容放入 `Captured`：一個 `CapturedMemory`，帶有簡短的 `Note` 與要歸入的 `Memory`。第一個尚未歸入的項目會顯示為膠囊。按下它時，若有需要，篩選會切換到該記憶的群組，記憶會被加入，其列會短暫著色，而膠囊會變成 "Saved to Preferences"（已儲存至 Preferences），然後收合。`FileCaptured()` 會從程式碼做同樣的事。

### 手動新增

底部的欄位用來新增記憶。卡片會把它歸入某個群組：若沒有 `ClassifyAsync`，它會用關鍵字猜測（兩個大寫開頭的名字表示 People，"prefer" 或 "want" 表示 Preferences，"project" 或 "moving" 表示 Projects，其他則歸入第一個群組）。設定 `ClassifyAsync` 可交由你自己的程式碼或模型判斷。若新增會超過容量，新增會被拒絕並顯示 "Memory is full"，文字會保留在欄位中。若要唯讀的卡片，請把 `IsAddEnabled` 設為 false。

### 容量

`Capacity` 會計算所有記憶的字元總數。計量條會從 `WarningRatio`（預設 90%）開始以顏色與圖示發出警告，而不只靠顏色。容量為零時，會隱藏計量條與上限。

<Demo name="Capacity" />

### 暫停

把 "Reference saved memories" 關閉後，清單會變淡，擷取膠囊會換成一句說明記憶已暫停的文字。清單仍可編輯；新增與忘記依然可用。`IsSavedMemoriesEnabled` 與 `IsChatHistoryEnabled` 是雙向的，而 `SettingToggled` 會回報變更。

<Demo name="Paused" />

### 文案

所有文字都來自 `Labels`（`AgentMemoryLabels`）：替換這個實例即可翻譯或改寫。格式化的文字是函式，因此可以改變字詞的順序。

```csharp
memory.Labels = new AgentMemoryLabels
{
    Title = "Souvenirs",
    SavedTo = group => $"Rangé dans {group}",
    NoMatches = query => $"Aucun souvenir ne correspond à « {query} »"
};
```

若設定了控制項上的 `Title` 與 `Subtitle`，它們會優先於 labels。

### 文案規範

- **一則記憶是一句沒有句號結尾的話**，以第三人稱或平述事實的方式書寫："Prefers tables to long bullet lists"。
- **具體而中立**："Is training for a spring half marathon"，而不是 "Loves running!"。
- **人物**：把名字放進句子並填入 `PersonName`；該列會顯示一個帶有名字首字母的小頭像（或 `PersonImage`）。
- **來源**：`Chat` 顯示 "From a chat"，`Manual` 顯示 "Added by you"，並附上簡短日期。設定 `SourceUri` 可讓來源成為連結，並處理 `SourceRequested` 或 `SourceCommand` 以開啟對話。

### 最佳做法

- 使用 `CommitAsync` 儲存並顯示真實的錯誤；絕不要吞掉失敗，卡片會替你復原變更。
- `UndoDuration` 保持在五秒或以上；較短的時間視窗對螢幕閱讀器使用者很難操作。
- 把卡片顯示的同一份清單提供給助手：人們看不到的內容，就無法修正。
- 保持四到六個群組；更多的話，篩選膠囊就會捲動。

### 常見問題

**為什麼我的忘記沒有立即呼叫 `CommitAsync`？** 它會在復原時間結束時才提交；在那之前都可以復原。

**我可以使用自己的群組嗎？** 可以。新增具有唯一鍵的 `MemoryGroup`，並設定每則記憶的 `GroupKey`。鍵與任何群組都不符的記憶，會顯示在第一個群組中。

**這張卡片也適合放在狹窄的側欄嗎？** 寬度低於 440 px 時，它會收緊內距、讓副標題換行，並讓篩選可以捲動。

## API 參考

<!-- api: AgentMemory -->

<!-- api: MemoryItem -->

<!-- api: MemoryGroup -->

<!-- api: CapturedMemory -->

### MemoryChange

| 屬性 | 說明 |
|---|---|
| `Kind` | `Capture`、`Add`、`Edit` 或 `Forget` |
| `Memory` | 已變更的記憶（忘記時為其變更前的樣子） |
| `PreviousText` | 編輯前的文字；其他類型為 null |

### 事件

| 事件 | 引發時機 | 引數 |
|---|---|---|
| `MemoryChanged` | 一項變更已提交 | `Change`、`Memories`（變更後的整個清單） |
| `SettingToggled` | 兩個開關之一發生變更 | `Setting`（`SavedMemories` 或 `ChatHistory`）、`IsOn` |
| `SourceRequested` | 按下了帶有連結的來源 | `Memory` |

## 無障礙

- 計量條會朗讀為 "Memory used, 62% full"，並讀取真實的數值，而非動畫中的數值。
- 開關有名稱，且會隨其狀態更新的說明。篩選膠囊是一個群組，名稱為 "Filter memories"，會回報哪一個被按下，並且只佔一個 Tab 停駐點；Left 與 Right 可移動並選取。
- 列是群組內的清單項目，群組以其標題命名。Edit 與 Forget 按鈕以句子命名（"Edit memory: …"），並帶有工具提示；它們在滑鼠懸停或鍵盤取得焦點時出現，且不佔用額外空間。
- 一個禮貌的即時區域會朗讀搜尋結果、已儲存的編輯、帶有復原時間的忘記、復原時間結束、還原、歸入與新增、記憶已滿，以及每個開關。
- 焦點會移到使用者接下來會看的地方：進入編輯器，儲存或取消後回到 Edit；忘記後移到 Undo，復原後回到 Forget；移到搜尋框，之後回到搜尋按鈕；以鍵盤歸入擷取的記憶後，移到新列的 Edit 按鈕。
- 狀態從不只靠顏色傳達：警告計量條會加上圖示，錯誤會加上圖示與一句說明，已儲存的狀態則會顯示勾號。
- 減少動態效果會保留每一種狀態、每一則朗讀與復原時間。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| `Tab` | 開關、已選取的篩選膠囊、搜尋按鈕、擷取膠囊、每列的 Edit 與 Forget、新增欄位 |
| 膠囊上的 `ArrowLeft` / `ArrowRight` | 移到前一個或下一個膠囊並選取它；會循環 |
| 膠囊上的 `Home` / `End` | 第一個或最後一個膠囊 |
| `Enter` 或 `Space` | 按下取得焦點的按鈕、開關或膠囊 |
| 編輯器中的 `Enter` | 儲存；`Shift+Enter` 加入一行 |
| 編輯器中的 `Escape` | 取消 |
| 搜尋中的 `Escape` | 清除、關閉並回到搜尋按鈕 |
| 新增欄位中的 `Enter` | 歸入並新增記憶 |

## 動態效果

- 篩選時，離開的群組與列會在 100 ms 內淡出，進入的群組與列會在短暫延遲後於 160 ms 內淡入，其高度則依臨界阻尼彈簧跟隨變化。被選取膠囊的背景會在膠囊之間滑動。
- 搜尋框會在 240 ms 內於膠囊上方展開，同時膠囊淡出；其欄位與關閉按鈕則淡入。
- 一列、它的編輯器與它的復原列會交叉淡化（100 ms 淡出、160 ms 淡入），而該列的高度會在它們之間以彈簧變化。
- 復原圓環會沿直線在復原時間內耗盡；秒數每秒倒數一次。
- 歸入或新增的列會以平滑彈簧展開其位置，然後該列會著色 1.6 秒並淡出。
- 計量條的填充與數字共用同一個彈簧；警告顏色則立即改變。
- 說明文字、擷取膠囊的文字與新增欄位的狀態會交叉淡化，並在進入時上移 2 px。
- 減少動態效果會移除彈簧、移動與耗盡動畫：高度直接跳動，交叉淡化變為短暫的淡入淡出，圓環保持全滿，秒數仍會倒數。關閉動態效果則會立即套用所有變化。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 篩選或搜尋 | 100 ms 淡出，160 ms 淡入；高度以彈簧變化 | 短暫淡入淡出；高度直接跳動 | 立即 |
| 列變為編輯器或復原列 | 交叉淡化；高度以彈簧變化 | 120 ms 交叉淡化 | 立即 |
| 已選取的膠囊 | 以彈簧滑動 | 直接跳動 | 立即 |
| 搜尋展開 | 240 ms 內寬度變化 | 立即 | 立即 |
| 復原圓環 | 在復原時間內耗盡 | 保持全滿 | 保持全滿 |
| 已歸入或新增的列 | 槽位以彈簧展開；1.6 秒著色 | 無槽位動畫；著色保留 | 立即 |

## 相關元件

- `ModelPicker`：用於選擇使用這些記憶的模型。
- `Switch`：用於與記憶無關的設定。
- `Timeline`：用於事件的歷史紀錄。

| 元件 | 適用情境 |
|---|---|
| `Switch` | 一個設定的開與關 |
| `Timeline` | 清單是依時間順序排列的歷史紀錄 |
| `Textarea` | 人們撰寫較長的文字 |
| `Toast` | 短訊息確認一個動作後即消失 |
