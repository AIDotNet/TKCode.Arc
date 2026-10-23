---
title: Project board
summary: 精簡的衝刺看板：箭頭按鈕會把每張卡片推到下一個或上一個階段，卡片飛到它的新欄位；看板下方有衝刺進度線、團隊頭像，以及 All work / My work 切換。
source-hash: 1eaac2413b0e
---

<Demo name="Preview" />

## 何時使用

`ProjectBoard` 是用於單一衝刺或單一專案的小型看板：幾個階段作為欄位，加上數十項任務作為卡片。每張卡片上有兩個箭頭按鈕，可把任務往左或往右送一個階段；卡片會沿直線飛到新欄位的末端，而它離開的欄位會同時收合。標頭會統計已完成的任務數，細細的衝刺進度線會隨著任務抵達最後一個階段而填滿，團隊頭像位於 All work / My work 切換旁，而 Add task 會把一張新卡片放進第一個階段。

- **團隊或專案頁面。** 看板讓人一眼看出「所有事情在哪裡」，並讓成員不必離開頁面就能移動自己的工作。
- **儀表板上的方塊。** 加上框線時，它可以與其他卡片並列；不加框線時，它會佔滿整個頁面。
- **以觸控與鍵盤為優先。** 移動卡片是按一下按鈕，而不是拖曳：無論使用滑鼠、手指、Tab 與 Enter，或螢幕閱讀器，操作方式都相同，而且每次移動都會被朗讀出來。

## 何時不用

- **大型看板。** 每張卡片都是即時元素，且沒有虛擬化；請把數量控制在數十項任務以內。若有數百項任務，且需要在欄位之間拖放、泳道或在製品上限，請使用完整的看板。
- **自由重新排序。** 移動一定會落在下一個或上一個欄位的末端；卡片無法被拖到指定位置，也無法從卡片上跳過階段。需要這種功能時，請從你自己的控制項以索引呼叫 `MoveTask`。
- **編輯。** 卡片只顯示任務，不會編輯它。請從 `AddRequested` 或頁面其他地方的選取項目開啟你自己的表單。
- **報表。** 若要燃盡圖或跨衝刺比較，請使用圖表；衝刺進度線只顯示一個比例。

## 用法

### 階段、任務與人員

`Stages` 是由左至右的欄位；兩到六個階段最為合適。每個 `ProjectBoardStage` 都有一個供任務參照的 `Key`，以及一個 `Header`。標記為 `IsDone` 的階段會被視為已完成（若沒有任何階段標記，則以最後一個階段為準）：該階段的任務會填滿衝刺進度線，其標題會轉為淡色，到期標籤也不再發出警示。

每個 `ProjectBoardTask` 是一張卡片：頂部是 `Area` 與 `DueText`（`IsDueSoon` 會讓到期標籤以警告色顯示），中間是 `Title`，接著是 `Owner` 的頭像、`CommentCount`（為零時隱藏），以及兩個箭頭。`StageKey` 指定它所在的階段；若鍵值與任何階段都不相符，卡片會顯示在第一欄。每一欄會依集合中的順序顯示其階段的任務。

人員是 `ProjectPerson` 物件，包含名稱與鍵值，並可選擇性地提供圖片。沒有圖片時，看板會以鍵值挑選的柔和色彩繪製姓名縮寫，因此同一個人永遠是同一種顏色。`Team` 是標頭中的一列頭像，`CurrentUser` 則代表「我」：也就是 My work 篩選的對象，以及新草稿的負責人。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:ProjectBoard Title="Launch prep">
        <arc:ProjectBoard.Stages>
            <arc:ProjectBoardStage Key="planned" Header="Planned" />
            <arc:ProjectBoardStage Key="progress" Header="In progress" />
            <arc:ProjectBoardStage Key="review" Header="Review" />
            <arc:ProjectBoardStage Key="done" Header="Done" IsDone="True" />
        </arc:ProjectBoard.Stages>
        <arc:ProjectBoardTask Title="Split the settings API" StageKey="progress" Area="Platform" DueText="Wed" CommentCount="6" />
        <arc:ProjectBoardTask Title="Contrast audit for receipts" StageKey="review" Area="Checkout" DueText="Today" IsDueSoon="True" />
    </arc:ProjectBoard>
</UserControl>
```

```csharp
var rhea = new ProjectPerson("Rhea Calloway", "rhea");
board.Team.AddRange([rhea, new ProjectPerson("Tomas Vierra", "tomas")]);
board.CurrentUser = rhea;
board.TasksSource = sprint.Tasks; // an ObservableCollection<ProjectBoardTask>
```

`Tasks` 用於在 XAML 中撰寫或以程式碼新增的任務；`TasksSource` 接受你自己的集合，且設定後優先使用。會發出變更通知的集合會被監看；可寫入的集合會隨移動重新排序，並接收新任務，因此永遠與看板顯示的內容一致。從外部變更任務的 `StageKey` 時，它的卡片也會跟著移動。

### 移動任務

右箭頭會把任務送到下一個階段，左箭頭送到上一個階段；會離開看板的那個箭頭會被停用。被移動的任務會放到新欄位的末端，無論方向為何。`TaskMoving` 最先引發，可以讓任務留在原處（`Cancel`）；`TaskMoved` 在卡片開始飛行時引發，帶有移動前後的階段與位置，因此適合在此儲存變更。以箭頭完成移動後，會執行 `MoveCommand`，並帶入 `ProjectBoardMoveRequest`。

`MoveNext`、`MovePrevious` 與 `MoveTask` 從程式碼執行同樣的操作；`MoveTask` 可移到任何階段，並能以索引指定該階段中的位置。`FocusTask` 會把鍵盤焦點放到一張卡片上，`ScrollToStage` 則會把某一欄捲動到可見範圍。

<Demo name="Persisted" />

```csharp
board.TaskMoving += (_, e) =>
{
    if (e.ToStage.Key == "review" && e.Task.CommentCount == 0)
    {
        e.Cancel = true; // the app's rule: review needs a comment first
    }
};
board.TaskMoved += (_, e) => SaveStage(e.Task.Key, e.ToStage.Key, e.ToIndex);

void SaveStage(string taskKey, string stageKey, int position)
{
    // Send the change to your store; the board has already moved the card.
}
```

### All work 與 My work

具備 `CurrentUser` 時，標頭會顯示切換開關。My work 會保留目前使用者所擁有的卡片（`Filter` 為 `ProjectBoardFilter.Mine`）；其他卡片會在原位淡出，其餘卡片同時收合，切回 All work 時它們再淡入。各欄會統計自己顯示的數量，為空時顯示「Nothing assigned to you」；標頭與衝刺進度線則永遠統計所有任務。`Filter` 為雙向繫結，並會引發 `FilterChanged`。沒有 `CurrentUser` 時，切換開關會隱藏。

### 新增任務

Add task 會引發 `AddRequested`。把任務放入事件的 `Task` 即可新增它；或設定 `Handled` 而不新增任何內容，例如先開啟你自己的表單，待填寫完成後再呼叫 `AddTask(task, stageKey)`。若沒有處理常式，看板會新增一張草稿：使用 `DraftTitles` 中的下一個標題、`DraftArea`，以 `NoDateText` 作為到期標籤，並以目前使用者為負責人。新任務會放到 `AddStageKey` 的末端（預設為第一個階段），淡入顯示，引發 `TaskAdded`，執行 `AddCommand` 並被朗讀出來；焦點會留在 Add 按鈕上，方便接著新增下一筆。`IsAddVisible` 可隱藏此按鈕。

### 你自己的階段

階段是資料，因此看板可以遵循任何工作流程。階段的 `EmptyText` 會取代該欄的看板空白文字。

<Demo name="CustomStages" />

### 有框或無框

`IsFramed` 會把看板畫成帶有 1px 邊框與 24px 圓角的卡片，適合放在其他內容之間的看板。預設為無框：看板會以表面顏色填滿所佔空間，如同一個獨立的頁面。`Title` 與進度文字為選用（`IsProgressVisible` 會隱藏進度文字與進度線）。

<Demo name="Framed" />

### 窄版容器

看板讀取的是自己的寬度，而不是視窗的寬度。欄位平分寬度，每欄最小為 200（`MinColumnWidth`）；低於此寬度時，欄位會橫向捲動。寬度為 700 及以下（`NarrowWidth`）時，每欄的寬度為整體寬度減 76，使下一欄能露出一點邊緣；團隊列會隱藏，篩選、Add 與箭頭按鈕會放大為 40px 的觸控目標，且卡片不會因此變高。寬度為 400 及以下（`CompactWidth`）時，操作按鈕會獨占一行，Add 只顯示圖示，名稱則改為工具提示。捲動停止後，若偏移量在某欄起點的三分之一欄寬以內，會自動對齊到該欄；被移動的卡片所在的欄位會捲動到可見範圍，以便持續追蹤該卡片。

<Demo name="Narrow" />

### 文字

每段文字都有英文預設值與對應的屬性：進度文字（`ProgressFormat`，「{0} of {1} shipped this sprint」）、切換開關（`AllWorkText`、`MyWorkText`、`FilterLabel`）、`TeamLabel`、`AddText`、空白文字（`EmptyText`、`EmptyMineText`）、草稿（`DraftTitles`、`NewTaskTitle`、`DraftArea`、`NoDateText`），以及螢幕閱讀器所朗讀的內容（`ColumnLabelFormat`、`ColumnLabelSingularFormat`、`MoveToFormat`、`MovePreviousText`、`MoveNextText`、`MovedFormat`、`AddedFormat`、`DueLabelFormat`、`OwnerLabelFormat`、`CommentsFormat`、`CommentsSingularFormat`）。在 XAML 中，以大括號開頭的格式字串前面需要加上 `{}`。

```xml
<arc:ProjectBoard Title="Lancement"
                  ProgressFormat="{}{0} sur {1} livrées ce sprint"
                  AllWorkText="Tout"
                  MyWorkText="Mes tâches"
                  AddText="Ajouter"
                  MoveToFormat="Déplacer {0} vers {1}"
                  MovedFormat="{}{0} est maintenant dans {1}." />
```

## API 參考

<!-- api: ProjectBoard -->

<!-- api: ProjectBoardStage -->

<!-- api: ProjectBoardTask -->

<!-- api: ProjectPerson -->

<!-- api: ProjectBoardMoveRequest -->

### 事件

| 事件 | 引發時機 | 引數 |
|---|---|---|
| `TaskMoving` | 任務即將變更階段 | `Task`, `FromStage`, `ToStage`, `Cancel` |
| `TaskMoved` | 任務已變更階段，卡片開始飛行時 | `Task`, `FromStage`, `ToStage`, `FromIndex`, `ToIndex` |
| `AddRequested` | 按下 Add task 時 | `Stage`, `Task`（設定它即可新增你自己的任務）, `Handled` |
| `TaskAdded` | 看板已新增一項任務 | `Task`, `Stage` |
| `FilterChanged` | `Filter` 變更時 | `OldFilter`, `NewFilter` |

## 無障礙

- 鍵盤：`Tab` 與 `Shift+Tab` 依序經過 All work、My work、Add task，然後是每個已啟用的箭頭，逐欄、由上至下；`Enter` 或 `Space` 會按下焦點所在的按鈕（選擇篩選、新增任務或移動卡片）。
- 看板是一個名為「Project board」的群組。每一欄是一個群組，名稱取自其階段與它所顯示的卡片數量（「Review, 1 task」）；欄內的卡片是清單項目，名稱取自任務、其領域、到期標籤、負責人與留言數（「Split the settings API, Platform, due Wed, assigned to Rhea Calloway, 6 comments」）。
- 箭頭會以它送達的位置命名（「Move Split the settings API to Review」）；停用的箭頭會說「the previous stage」或「the next stage」。切換開關是一個名為「Show」的群組，內含兩個會回報是否按下的切換按鈕。團隊頭像名為「Team」，Add 按鈕即使只顯示圖示，也名為「Add task」。
- 移動與新增會以禮貌模式朗讀（「Moved Split the settings API to Review.」、「Added Draft the pricing FAQ to Planned.」）；篩選則不朗讀，因為欄位名稱本身已經改變。
- 移動後，焦點會停留在同一張卡片的同一個箭頭上；若該箭頭在邊緣時已停用，焦點則移到另一個箭頭上，因此再按一次 Enter 就能繼續操作。在窄版看板上，欄位會捲動到可見範圍。
- 懸停樣式只跟隨滑鼠或觸控筆；手指操作時，按鈕永遠不會呈現懸停外觀。狀態不只依賴顏色：到期標籤保留文字，已完成的卡片則留在已完成欄位中。

## 動態效果

- **移動**：卡片以「Gentle」彈簧（剛度 340、阻尼 34，約 600 ms）從目前繪製的位置沿直線飛到新欄位的末端，並位於所有其他卡片之上。舊欄位中位於它下方的卡片會以同一彈簧向上滑動，兩個位置都會跟著它們移動：正在收合的位置永遠不會讓卡片懸空在下方。飛行途中再次按下，會從卡片目前繪製的位置重新開始。
- **篩選**：高亮會以臨界阻尼彈簧（剛度 400、阻尼 40）滑動到所選的按鈕。離開的卡片會在原位淡出並縮小到 98%，其餘卡片則收合；回來的卡片會淡入並從 98% 放大，全部使用「Gentle」彈簧。
- **新增**：新卡片淡入並從 98% 放大；衝刺進度線會彈到新的比例（任務抵達或離開已完成階段時也會如此）。
- **懸停與按下**：顏色在 160 ms 內變化；箭頭按下時縮到 92%，Add 縮到 97%，滑鼠懸停時 Add 會淡化到 88%。
- 減少動態效果與 `AnimatesMoves="False"` 會讓移動、新增與篩選立即在原位套用，但保留顏色變化。關閉動態效果則會立即套用所有變化。看板靜止時，不會執行任何動畫。

## 相關元件

- 其他 Projects 區塊 Release readiness、Kanban board、Week calendar 與 Availability picker，與本區塊共用人員、頭像與進度線。
- `SegmentedControl`，用於區塊外的檢視切換。
- `AvatarGroup`，用於頁面其他位置的一疊頭像。
- `LayoutMorph`，用於以多種版面呈現同一組卡片。
