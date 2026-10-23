---
title: Kanban board
summary: 團隊看板，卡片被拿起時會隨指標傾斜，放下時彈進為它開出的槽位；可依負責人分泳道，並有即時的在製品上限、快速新增與詳細資訊抽屜。
source-hash: 103005c79514
---

<Demo name="Preview" />

## 何時使用

`KanbanBoard` 是一個完整的看板，適用於依固定階段流動、且同一階段內順序很重要的工作。卡片可以用滑鼠、觸控筆、手指（短暫按住後）或鍵盤拿起；卡片被拿起時，會開出它將落下的槽位，周圍的卡片會讓出空間，各欄的數量也會顯示該卡片會落在哪裡。欄位可以設定上限，超過時該欄會轉為琥珀色。

- **團隊的議題或任務看板。** 從你的追蹤系統載入欄、人員與卡片，並儲存 `CardsChanged` 在每次移動或編輯後交出的有序清單。
- **需要手動排序的管線。** 銷售管線、招募漏斗或內容行事曆，其中階段最上方的卡片就是下一個要處理的。
- **需要依人檢視的看板。** `GroupBy="Assignee"` 會為每個人提供一條擁有相同欄位的泳道；將卡片放到另一條泳道即可轉交。

## 何時不用

- **依日期規劃的工作。** 當問題是*何時*而非*在哪個階段*時，請使用週行事曆或時間軸。
- **唯讀的狀態摘要。** 有狀態欄的表格或清單更容易掃視與排序；只有在多人同時編輯的看板要提供共享檢視時，才設定 `IsReadOnly`。
- **單一欄內有數百張卡片。** 每張卡片都是為了能滑動而存在的即時元素，此看板在總計數百張卡片以內都還順暢。大量待辦請分頁或篩選，或改用 `DataGrid`。
- **自由排版。** 卡片一律貼齊有序的欄位；若要便利貼風格的排列，請使用畫布。

## 用法

### 欄、人員、標籤與卡片

看板接受四種清單。`Columns` 是依序排列的階段；為欄位設定 `Limit` 即可在卡片數超過時發出警示。`People` 是 `ProjectPerson` 物件：它們會繪製頭像、泳道與負責人選單（沒有圖片時會產生姓名縮寫）。`Labels` 讓卡片擁有一個彩色圓點與名稱。`Cards` 是單一扁平清單：清單的順序就是每一欄內的順序，也是儲存時的順序。

卡片透過鍵值參照它的欄、負責人與標籤（`ColumnKey`、`AssigneeKey`、`LabelKey`），因此可以直接來自追蹤系統的 JSON。若卡片所屬的欄不在看板上，該卡片便不會顯示；在泳道中，負責人不在 `People` 之中的卡片會被放進尾端的「Unassigned」泳道。

```xml
<arc:KanbanBoard Title="Sprint 18" Height="640">
    <arc:KanbanBoard.Columns>
        <arc:KanbanColumn Key="todo" Name="To do" />
        <arc:KanbanColumn Key="doing" Name="Doing" Limit="3" />
        <arc:KanbanColumn Key="done" Name="Done" />
    </arc:KanbanBoard.Columns>
    <arc:KanbanBoard.People>
        <arc:ProjectPerson Key="ines" Name="Ines Duarte" />
        <arc:ProjectPerson Key="tom" Name="Tomasz Bielski" />
    </arc:KanbanBoard.People>
    <arc:KanbanCard Key="SP-41" Title="Export the audit log as CSV" ColumnKey="todo" AssigneeKey="ines" Points="3" />
    <arc:KanbanCard Key="SP-42" Title="Rate limit the sign-in form" ColumnKey="doing" AssigneeKey="tom" Points="2" />
</arc:KanbanBoard>
```

將 `CardsSource` 繫結到你的檢視模型所擁有的集合。可寫入的清單（`ObservableCollection` 或 `AvaloniaList`）會在每次放下與每次新增卡片時就地重新排序，因此永遠與看板一致；唯讀集合則不會被改動，看板保留自己的順序。來自外部的變更，例如新的清單，或 `ColumnKey` 已改變的卡片，會一併套用，卡片也會滑到它們的新位置。

### 儲存變更

`CardsChanged` 會在每次移動、編輯、檢查項目變動與新增卡片後觸發。它的 `Reason` 說明發生了什麼，`Cards` 則是整份清單依看板順序的快照：照原樣儲存即可。`CardMoved` 會補充移動的細節：移動前後的欄與負責人、卡片在清單中移動前後的位置，以及 `ChangeSource`（指標、觸控、鍵盤、抽屜，或方法呼叫）。

```csharp
var saved = new List<(string Key, string Column, string? Owner)>();
board.CardsChanged += (_, e) =>
{
    // Store the snapshot as it is: its order is the order inside every column.
    saved = e.Cards.Select(c => (c.Key, c.ColumnKey, c.AssigneeKey)).ToList();
};
board.CardMoved += (_, e) => Debug.WriteLine($"{e.Card.Key}: {e.FromColumn?.Name} → {e.ToColumn?.Name}");
```

<Demo name="Controlled" />

### 泳道

`GroupBy` 可在「一列欄位」與「每人一條泳道」之間切換；工具列中的切換器也會做同樣的事，且每張卡片都會飛到新位置。在看板水平捲動時，泳道標題會留在左緣；垂直向下捲動時，欄標題會留在頂端。欄的數量與上限一律計入所有泳道；每條泳道的標題則計算它自己的卡片數。將卡片放到另一人的泳道會改變它的 `AssigneeKey`。

<Demo name="Swimlanes" />

### 在製品上限

只有當欄內卡片數多於 `Limit` 時，該欄才算超過上限（剛好滿額是可以的）。它的數量會轉為琥珀色，出現「Above limit」標籤，欄中的儲存格也會帶上暖色調；即將放下卡片的槽位同樣會說明這一點。數量會即時跟隨被拖曳的卡片，因此警示會在放下之前就出現。看板只負責警示：若必須確保上限，請處理 `CardMoving` 並設定 `Cancel`，讓卡片被退回。`ColumnLimitStateChanged` 會回報某欄超過上限或恢復正常。

```csharp
board.CardMoving += (_, e) => e.Cancel = e.IsTargetOverLimit && e.From.ColumnKey != e.To.ColumnKey;
```

<Demo name="WipLimits" />

### 快速新增與詳細資訊抽屜

每一欄標題中的「+」，以及每一欄底部的 Add card 列，都會在該欄末端開啟編輯框（在泳道中，則是在 `CurrentUser` 的泳道中，預設為第一位人員）。按 Enter 會新增卡片並保持編輯框開啟以便輸入下一張；Shift+Enter 換行；Escape 關閉。新卡片會取得看板上最大編號之後的下一個編號（「FLD-244」→「FLD-245」）；若要自訂 id，請設定 `CardKeyFactory`；若要在新增前變更或拒絕卡片，請處理 `CardCreating`。

點擊卡片，或在已聚焦的卡片上按 Enter，會引發 `CardInvoked` 並開啟詳細資訊抽屜：標題、狀態、負責人、標籤、到期日、估計、備註與檢查項目。編輯會在輸入時即時套用到卡片上。變更狀態時，卡片在清單中的位置不變，因此它會落在較後面那一欄的最上方，或較前面那一欄的最下方。若要從程式碼開啟抽屜，請設定 `OpenedCard`（或呼叫 `OpenCard`）；若要改用你自己的檢視，請在 `CardInvoked` 中設定 `Handled`，或關閉 `IsDetailsEnabled`。

### 窄版容器與唯讀看板

看板讀取的是它自己的寬度。欄位會平分寬度，直到 `MinColumnWidth`（251）為止，之後看板便可水平捲動。在 `CompactWidth`（560）及以下，欄寬為 `CompactColumnWidth`（235），內距收緊，水平捲動後看板會貼齊最近的欄，且第一欄緊貼左緣。抽屜的寬度不會超過看板寬度減 16，因此能覆蓋窄版看板；其狀態選項會每兩個一行換行。

<Demo name="Narrow" />

`IsReadOnly` 會讓卡片維持原位：不能拖曳、不能快速新增，也不能編輯，但抽屜仍會開啟以顯示卡片。`IsDragEnabled` 與 `IsQuickAddEnabled` 則可分別單獨關閉這兩項功能。

<Demo name="ReadOnly" />

### 文字

看板顯示或朗讀的每個字，都放在 `Strings` 中，這是一個帶有英文預設值的 `KanbanBoardLabels` 物件。帶有數值的文字是函式，因此翻譯時可以改變字詞順序。

```csharp
board.Strings = new KanbanBoardLabels
{
    StatusView = "Statut",
    LanesView = "Par personne",
    AddCard = "Ajouter une carte",
    CardsWord = n => n == 1 ? "carte" : "cartes",
    Dropped = (title, column) => $"{title} déposée dans {column}."
};
```

## API 參考

<!-- api: KanbanBoard -->

<!-- api: KanbanColumn -->

<!-- api: KanbanCard -->

<!-- api: KanbanLabel -->

<!-- api: KanbanChecklistItem -->

<!-- api: KanbanBoardLabels -->

<!-- api: KanbanCardsChangedEventArgs -->

<!-- api: KanbanCardMovedEventArgs -->

## 無障礙

- **鍵盤。** Tab 會依序移到檢視切換器、每個「+」，然後是每一欄的卡片（由上到下），接著是該欄的 Add card 列。在卡片上按 Enter 會開啟詳細資訊。Space 拿起卡片：Up 與 Down 在欄內移動，Left 與 Right 移到相鄰欄的同一位置，Shift 加上 Up 或 Down 移到相鄰泳道；Space 或 Enter 將它放下，Escape 則讓它退回原位。將焦點從被拿起的卡片移開，也會讓它退回原位。
- 每一次拿起、移動與放下都會立即朗讀（「Signature pad on the handover form lifted. Building, place 1.」），卡片換泳道時還會朗讀負責人。新卡片、抽屜中的狀態變更，以及欄位超過上限，都會以禮貌模式朗讀。
- 卡片是按鈕，名稱為「標題、id、負責人」，並以其標籤、到期日、檢查項目進度與估計作為說明。
- 上限同時以文字與顏色呈現：數量讀作「4 of 3 allowed」，標籤與槽位也會說明已超過上限。
- 抽屜在看板內是模態的：焦點移到其關閉按鈕，Tab 停留在抽屜內，按 Escape、點擊關閉按鈕，或點擊變暗的看板，都會關閉抽屜並把焦點送回卡片。在負責人選單中按 Escape 只會關閉選單。文字欄位會顯示鍵盤焦點環。關閉編輯框時，焦點會回到開啟它的按鈕。
- 檢視切換器與抽屜的狀態是單選群組，只有一個 Tab 停駐點；方向鍵變更選擇。

## 動態效果

- **拿起**：指標移動超過 `DragThreshold`（5px）後，或觸控按住 `TouchHoldDelay`（250ms）後，卡片會被拿起；較快的滑動則會捲動看板。卡片會從被抓取的位置跟隨指標，以快速彈簧放大到 1.035 倍，並依水平速度傾斜（每 px/s 0.0064°，最多 7°）；指標靜止時，傾斜會回到水平。卡片繪製於整個看板之上，因此能越過欄標題與標題列而不被裁切。
- **騰出空間**：槽位會出現在卡片原本的位置並淡入；周圍的卡片與槽位本身，會在版面彈簧上滑動（臨界阻尼，約 0.6 秒），目標改變時仍保持速度。
- **放下**：卡片以一個小彈簧（剛度 690、阻尼 51）滑入槽位，初速度為拋出時的速度；若槽位仍在滑動，卡片會跟著槽位走。它的陰影與邊框會在 240 與 160ms 內緩和回復。Escape 會以同樣的方式把卡片飛回原位。
- **邊緣**：靠近看板邊緣（64px）時，會在兩個軸向上捲動，指標越近捲動越快，最高 1200px/s；若指標靜止，槽位會跟著在其下移動的內容一起前進。
- **切換檢視、鍵盤移動與抽屜變更**會讓所有受影響的卡片在版面彈簧上移動。抽屜沿同一彈簧滑入，看板則在 240ms 內變暗；編輯框隨之放大與縮小。數量會滾動更新。
- 減少動態效果時，槽位、數量與短暫的淡入會保留，但會取消傾斜與所有飛行動畫：卡片立即落定。關閉動態效果時，所有變化都會立即套用。

## 相關元件

- `ReorderableList`，用於單一清單的重新排序。
- `ProjectPerson` 與其他專案區塊，用於看板上的人員。
- `SegmentedControl`、`DropdownMenu` 與 `RollingDigits`，即看板工具列、負責人選單與數量所依循的元件。
