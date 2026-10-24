---
title: Workflow canvas
summary: "在點陣畫布上呈現工作流程並播放其執行過程：各步驟依序亮起，資料沿連線繪製，每個步驟都回報其結果與耗時。"
source-hash: 6fe02e81f603
---

<Demo name="Preview" />

## 何時使用

- 呈現自動化、流水線或代理工作流程是如何組成的：它的步驟、分支，以及各分支通往何處。
- 播放正在發生的一次執行，或從歷史紀錄重播一次執行：哪個步驟正在執行、每個步驟發現了什麼、花了多久，以及這次執行跳過了哪個分支。
- 解釋在背景為使用者工作的產品（webhook、資料同步、部署），其中看到步驟執行本身正是重點。
- 讓人們自行建立或變更工作流程：移動步驟、連接步驟、依條件分岔。

## 何時不用

- 人員按順序完成的簡短直線式步驟：請使用 `Stepper`。
- 沒有結構、依時間順序排列的事件：請使用 `Timeline`，代理執行則使用 `AgentTimeline`。
- 可編輯檢查清單形式的代理計畫：請使用 `AgentPlan`。
- 各階段之間流動的數量：請使用桑基圖（Sankey）之類的圖表。

## 用法

以步驟（`WorkflowNode`）及步驟之間的連接（`WorkflowEdge`，或 `Connect`）建立一個 `WorkflowGraph`，再把它交給 `WorkflowCanvas`。沒有 `Position` 的步驟會依層級由上而下排列，排列得到的位置會寫回步驟中。

```xml
<arc:WorkflowCanvas x:Name="Canvas" />
```

```csharp
var graph = new WorkflowGraph();
var trigger = new WorkflowNode("trigger", "Order placed") { Icon = ArcIcons.Zap };
var lookup = new WorkflowNode("lookup", "Find customer account") { Description = "Plan, seats and owner" };
graph.Nodes.Add(trigger);
graph.Nodes.Add(lookup);
graph.Connect(trigger, lookup);
Canvas.Graph = graph;
```

### 播放一次執行

在步驟真正改變時設定其 `State`：步驟開始時設為 `Running`，完成時設為 `Done`（並設定 `Result` 與 `Duration`），失敗時設為 `Failed`（並設定 `Error`），對於分支未經過的步驟則設為 `Skipped`。下一次執行前呼叫 `ResetRun`。連線會自動跟隨其步驟：當一個步驟完成、下一個步驟開始時，資料會以強調色沿連線繪製；通往已略過步驟的連線會變成虛線。

```csharp
lookup.State = WorkflowNodeState.Done;
lookup.Result = "Enterprise, 212 seats";
lookup.Duration = TimeSpan.FromMilliseconds(118);
branch.State = WorkflowNodeState.Running;
```

你可以讓下一個步驟在上一個步驟完成的同一時刻開始執行：開啟 `QueueRunVisuals`（預設開啟）後，下一張卡片會一直顯示為等待中，直到資料繪製進入卡片為止，因此執行過程總是依「步驟、連線、步驟」的順序呈現。只有卡片所顯示的內容會等待；`State` 仍是你設定的值。

### 步驟狀態

每張卡片在列尾顯示其步驟的狀態：沒有執行時不顯示任何東西，等待時為空心圓環，執行時為旋轉的圓環，完成時為耗時加一個勾號，失敗時為警示標記，略過時為減號。執行中的卡片採用強調色，失敗的卡片使用紅色邊框，略過的卡片會淡出。`WorkflowNodeCard` 也可以在畫布之外單獨使用。

<Demo name="States" />

### 已完成的執行

在畫布出現之前就設定好最終狀態，即可呈現一次來自歷史紀錄的執行：不會播放任何動畫，這次執行經過的每條連線都會立即繪製完成。

<Demo name="FinishedRun" />

### 精簡步驟

`Appearance="Compact"` 會將卡片排成兩列，尺寸為 200 × 84：上方為圖示與狀態，下方為標題與描述。適用於從一個步驟分岔出一排動作的情境，如預覽中所示。

### 由左至右

`LayoutDirection="LeftToRight"` 會讓工作流程沿畫布由左至右展開：連線從卡片的右側引出，並進入下一張卡片的左側；排列時各層會並排放置。

<Demo name="LeftToRight" />

### 連線樣式

`EdgeStyle` 會將連線繪製為曲線（`Bezier`，即 Arc 的預設樣式）、圓角的直角（`Step`）或直線（`Straight`）。執行的資料會沿其中任一種連線繪製。

<Demo name="EdgeStyles" />

### 平移與縮放

使用者可以拖曳畫布（或使用中鍵）來平移，以 Ctrl / ⌘ + 滾輪、觸控板雙指縮放或觸控雙指縮放來縮放，也可以使用 + 與 − 鍵。`WheelMode` 決定一般捲動的行為：`Pan`（預設）會平移畫布，適合填滿面板的畫布；`ModifierZoom` 則把一般捲動留給頁面，適合放在可捲動頁面中的畫布。`IsPanZoomEnabled="False"` 會關閉以上所有操作，適用於只需適配顯示的工作流程，例如預覽。在空白畫布上連按兩下會重新適配整個工作流程。

`WorkflowZoomControls` 是一個小型縮放列：縮小、目前的縮放比例（按一下可回到 100%）、放大與適配。將它的 `Target` 指向畫布，並放在某個角落上方。在程式碼中，`ZoomTo`、`ZoomIn`、`ZoomOut`、`FitToContent` 與 `ScrollIntoView` 會移動檢視，預設帶有短暫的緩出動畫，除非你傳入 `animate: false`。

<Demo name="PanZoom" />

```xml
<Panel>
    <arc:WorkflowCanvas x:Name="Canvas" AutoFit="Fit" />
    <arc:WorkflowZoomControls Target="{Binding #Canvas}" Margin="12"
                              HorizontalAlignment="Left" VerticalAlignment="Bottom" />
</Panel>
```

### 排列

畫布會排列沒有 `Position` 的步驟，並記住這件事：當這類工作流程增加或減少步驟時，會重新排列，卡片會滑動到新的位置。`ArrangeNodes()` 會以 Arc 的間距（層與層之間 52，分岔步驟下方 72，相鄰步驟之間 20）重新排列所有步驟，並把你的程式碼放置過的步驟也交還給畫布。

<Demo name="Arrange" />

### 編輯

`IsEditable="True"` 讓使用者可以重新排列工作流程。拖曳步驟即可移動它（選取的步驟會一起移動，放開時吸附到點陣；按住 Alt 則可自由放置），在空白畫布上按住 Shift 拖曳，可選取矩形框觸及的每一個步驟；把步驟拖曳到靠近邊緣的位置則會捲動檢視。使用鍵盤時，Space 選取聚焦的步驟，Ctrl / ⌘ + A 全選，M 開始用方向鍵移動選取項（Shift 移動五個網格步距，Enter 放置，Escape 取消），Delete 刪除選取的步驟與連線。帶有 `IsLocked` 的步驟會留在原位且無法刪除，但仍可以被選取。

Ctrl / ⌘ + C 複製選取的步驟及其之間的連線，Ctrl / ⌘ + X 剪下它們，Ctrl / ⌘ + V 將它們貼到複製位置往右下方一個網格步距處（若該位置不在視野內，則貼到檢視中央），Ctrl / ⌘ + D 則不經剪貼簿直接建立副本；貼上的步驟會取得新的 id，並成為目前的選取項目。在你的應用程式內貼上時，步驟除了執行狀態之外的所有內容都會保留；跨應用程式時，剪貼簿會以 JSON 攜帶步驟，但不含圖示。唯讀畫布同樣可以複製。在程式碼中：`CopySelectionAsync`、`CutSelectionAsync`、`PasteAsync` 與 `DuplicateSelection`。

在任何畫布上都可以選取：按一下選取一個步驟，Ctrl / ⌘ + 按一下切換其選取狀態，Shift + 按一下加選，按一下空白畫布則清除選取。讀取 `SelectedNodes`，以 `Select` 與 `ClearSelection` 變更它，並透過 `SelectionChanged` 追蹤它。每次編輯完成都會觸發一次 `GraphEdited`，附上變更的內容（`Move`、`Delete`、`Connect`、`Disconnect`、`Paste`、`Undo` 或 `Redo`）、步驟與連線：請在那裡儲存工作流程。

<Demo name="Editing" />

```csharp
Canvas.GraphEdited += (_, e) =>
{
    // Move: the steps are at their new Position. Delete: they are already out of Canvas.Graph.
    foreach (var node in e.Nodes)
    {
        Debug.WriteLine($"{e.Kind} {node.Id} at {node.Position}");
    }
};
```

你移動過的步驟從此就歸你管理：工作流程變化或縮減時，畫布不再排列它，直到 `ArrangeNodes` 把它交還給畫布為止。人們在畫布上做的編輯不會讓畫布重新排列工作流程；你的程式碼加入到已由畫布排列過之工作流程的連線，則會。

### 連接步驟

在可編輯的畫布上，當你指向、選取或聚焦某張卡片時，它會顯示連接埠：頂端的輸入，以及底部的輸出（工作流程橫向排列時則為左側與右側）。從一個輸出拖曳到另一個步驟，或拖曳到靠近其某個輸入的位置，即可將兩者連接起來。拖曳時，連線無法通往的步驟會淡出，指向其中一個步驟時會告訴你原因。把連線的一端從其輸入拖開，可將它移到另一個步驟，或在空白畫布上放開以移除它。按一下連線即可選取它（Ctrl / ⌘ + 按一下切換選取，Shift + 按一下加選）；Delete 會刪除它。使用鍵盤時，C 從聚焦的步驟開始連線：若它有多個輸出，用方向鍵選擇其中一個並按 Enter；接著用方向鍵選擇一個步驟並按 Enter 以完成連線。

沒有連接埠的步驟擁有自己的一個輸入與一個輸出（此時連線的連接埠為 null）。在工作流程分岔之處為步驟設定具名的連接埠，例如條件判斷的「Yes」與「No」；它們的標籤一定會顯示在旁邊。若是觸發器（沒有任何東西會通往它），請把 `HasDefaultInput` 關閉。

```csharp
var yes = new WorkflowPort(WorkflowPortDirection.Output, "Yes");
var no = new WorkflowPort(WorkflowPortDirection.Output, "No");
check.Outputs.Add(yes);
check.Outputs.Add(no);
graph.Connect(check, yes, approve, null);
graph.Connect(check, no, refund, null);
```

每個連線在建立前都會經過檢查。以下情況會被拒絕：連接到步驟自己、終點為輸出、重複既有的連線、超過連接埠的 `MaxConnections`、連接 `DataType` 不同的連接埠，或形成迴圈（除非開啟 `AllowCycles`）。`CanConnect` 以程式碼執行相同的檢查，並告訴你原因。把你自己的規則放在 `ConnectionRequested` 中：設定 `Cancel`，並提供讓人們聽到的 `Reason`。在空白畫布上放開的連線會引發 `ConnectionDroppedOnCanvas`，附上以畫布單位表示的位置：在那裡提供一份步驟清單，並加入所選的步驟。

<Demo name="Connecting" />

```csharp
Canvas.ConnectionRequested += (_, e) =>
{
    if (e.SourcePort == yes && e.TargetNode == refund)
    {
        e.Cancel = true;
        e.Reason = "Amounts over $500 need a manager's approval first";
    }
};
Canvas.ConnectionDroppedOnCanvas += (_, e) =>
{
    var step = new WorkflowNode("notify", "Notify the team") { Position = new Point(e.Position.X - 150, e.Position.Y) };
    // One edit: a single undo takes the step and its connection back.
    using (graph.History.BeginGroup())
    {
        graph.Nodes.Add(step);
        graph.Connect(e.SourceNode, e.SourcePort, step, null);
    }
};
```

### 復原與重做

`WorkflowGraph.History` 會保留人們在畫布上做的最近 100 次編輯：移動、刪除、連接、移動或移除連線，以及貼上。Ctrl / ⌘ + Z 復原，Ctrl / ⌘ + Shift + Z 或 Ctrl + Y 重做；畫布的 `Undo()` 與 `Redo()` 則為你的按鈕做同樣的事，被移動的卡片會滑回原位。追蹤 `History.Changed`，即可依 `CanUndo` 與 `CanRedo` 更新那些按鈕。

只有在 `History.BeginGroup()` 之內，你的程式碼對工作流程做的變更才會被記錄：在群組釋放之前新增、移除或移動的所有內容，會成為一次編輯，如上方的處理常式所示。若你的應用程式自己保有復原歷程，請關閉 `History.IsEnabled`，改為追蹤 `GraphEdited`。

### 工具列命令

畫布為工具列按鈕與選單項目提供了命令：`UndoCommand`、`RedoCommand`、`DeleteSelectionCommand`、`CopyCommand`、`CutCommand`、`PasteCommand`、`DuplicateCommand`、`ZoomInCommand`、`ZoomOutCommand` 與 `FitCommand`。每個命令只在確實能發揮作用時才可執行，例如有可復原的編輯時才能復原、有選取內容時才能刪除，因此繫結了命令的按鈕會自動啟用或停用。

```xml
<arc:Button Content="Undo" Command="{Binding #Canvas.UndoCommand}" />
<arc:Button Content="Delete" Command="{Binding #Canvas.DeleteSelectionCommand}" />
```

### 自訂卡片內容

`NodeTemplates` 會取代與其相符的卡片內容：`WorkflowNodeTemplate` 以 XAML 撰寫，對應一種 `Kind` 的步驟，其資料為 `WorkflowNode`，而第一個與步驟相符的範本會勝出。卡片保留其外框、執行的顏色、選取狀態、焦點框與連接埠；沒有任何範本相符的步驟則保留預設卡片。未設定 `Size` 時，卡片的高度與其內容相同。卡片中的控制項，例如輸入框或開關，會保留自己的點擊與按鍵，因此在其中輸入文字絕不會移動或連接步驟。

<Demo name="CustomNodes" />

```xml
<arc:WorkflowCanvas x:Name="Canvas" IsEditable="True">
    <arc:WorkflowCanvas.NodeTemplates>
        <arc:WorkflowNodeTemplate Kind="email" x:DataType="arc:WorkflowNode">
            <StackPanel Spacing="10">
                <TextBlock Text="{Binding Title}" FontWeight="{arc:ArcDesignTokenResource WeightMedium}" />
                <arc:Input Text="{Binding Description, Mode=TwoWay}" />
                <arc:Switch Content="Attach the invoice" IsChecked="True" />
            </StackPanel>
        </arc:WorkflowNodeTemplate>
    </arc:WorkflowCanvas.NodeTemplates>
</arc:WorkflowCanvas>
```

從程式碼中可以加入任何 `IDataTemplate`（例如 `FuncDataTemplate<WorkflowNode>`）。單獨使用的卡片則透過 `ContentTemplate` 接受其範本。

### 大型工作流程

畫布為數百個步驟而設計：無論顯示多少，卡片、陰影與點陣都能以低成本繪製，因此包含 500 個步驟與 800 條連線的工作流程，在桌面上平移、縮放與拖曳時仍能保持反應靈敏。`WorkflowMinimap` 會以小尺寸呈現整個工作流程，並以一個框標示目前螢幕上的範圍：按下它即可前往該處，拖曳則可平移。把它的 `Target` 指向畫布，並像縮放列一樣放在某個角落上方。

<Demo name="LargeGraph" />

```xml
<Panel>
    <arc:WorkflowCanvas x:Name="Canvas" MinZoom="0.05" />
    <arc:WorkflowMinimap Target="{Binding #Canvas}" Margin="12"
                         HorizontalAlignment="Right" VerticalAlignment="Bottom" />
</Panel>
```

調低 `MinZoom`（預設為 0.25），適配時才能顯示大型工作流程的全部內容。

### 窄畫布

低於 `NarrowBelowWidth`（預設 520，與 Arc 相同）時，畫布會切換為 Arc 的手機版配置：單列卡片變為 344 × 52，雙列卡片變為 108 × 72；由畫布排列的工作流程會以更緊湊的間距重新排列（分別為 26、36 與 10），因此能以實際尺寸呈現，而不是縮小。由你的程式碼放置的步驟保留其位置。緊湊步驟的標題請控制在一兩個詞之內，因為手機上的空間很有限。

<Demo name="Narrow" />

### 適配與尺寸

`AutoFit` 決定工作流程如何適配畫布。`ShrinkToFit`（預設）以實際尺寸置中顯示，放不下時會像截圖一樣整體縮小；`Fit` 會將其放大到盡可能大，但不超過 `MaxZoom`；`None` 則保留你透過 `Zoom` 與 `Offset` 設定的檢視。平移、縮放，或自行設定 `Zoom` 或 `Offset` 後，在呼叫 `FitToContent` 之前不再自動適配。在高度不受限的容器中（例如可捲動的頁面），畫布的高度與適配後的工作流程一樣高。`Padding`（預設為 24）讓工作流程與邊緣保持距離。

### 放置步驟

為步驟設定 `Position` 即可自行放置它（即卡片在畫布上的左上角）。新步驟若沒有位置，會依層級排在你的程式碼已放置的步驟之後。

### 文案規範

- 以步驟所做的事為其命名，簡短，採用句首大寫：「Find customer account」，而不是 "HubSpot lookup step"。
- 在 `Description` 中寫明步驟處理的對象，在 `Result` 中寫明它找到的內容；兩者都保持在一行之內。
- 錯誤訊息要具體且簡短："Health check failed in eu-west"，而不是 "Error"。

## API 參考

<!-- api: WorkflowCanvas -->

<!-- api: WorkflowNodeCard -->

<!-- api: WorkflowGraph -->

<!-- api: WorkflowNode -->

<!-- api: WorkflowEdge -->

<!-- api: WorkflowPort -->

<!-- api: WorkflowHistory -->

<!-- api: WorkflowZoomControls -->

<!-- api: WorkflowMinimap -->

<!-- api: WorkflowNodeTemplate -->

<!-- api: WorkflowNodeEventArgs -->

<!-- api: WorkflowEditEventArgs -->

<!-- api: WorkflowSelectionChangedEventArgs -->

<!-- api: WorkflowConnectionRequestedEventArgs -->

<!-- api: WorkflowConnectionDroppedEventArgs -->

## 無障礙

- 畫布預設名稱為 "Workflow, 6 steps"；如果你以 `AutomationProperties.Name` 為它命名，則使用你設定的名稱。
- 每張卡片的名稱依序給出標題、類型、文字形式的狀態、耗時，以及標題下方的那一行，例如 "Find customer account, Done, 118 ms, Enterprise, 212 seats"。狀態從不只靠顏色傳達：每種狀態都有自己的標記。
- 畫布被視為一個群組，每張卡片則是其中的一個項目。連線、連接埠、點陣與標記都是裝飾性的，對輔助技術隱藏；鍵盤的連線模式則能到達指標能做出的每一條連線。小地圖是指標操作的捷徑，其功能鍵盤都已能做到，因此它不會取得焦點，螢幕閱讀器也會略過它。
- 失敗的步驟與一次執行的結束會以禮貌方式播報："Create onboarding task failed: Task service timed out"、"Run finished: 4 done, 1 failed, 1 skipped"。編輯操作同樣會被播報："Moved 2 steps"、"Deleted 1 step"、"Connected Amount over $500? to Manager approval"、"Can't connect: This would create a loop"、"Pasted 2 steps"、"Undone"，鍵盤的移動與連線模式啟動時也會播報其運作方式。
- 選取的卡片會在其名稱後加上 "selected"，並顯示強調色邊框與柔和的光暈，而不只是顏色變化。
- 畫布的說明文字列出了下方的按鍵。

### 鍵盤

| 按鍵 | 動作 |
| --- | --- |
| Tab | 進入畫布，移到上次取得焦點的步驟，再離開畫布；卡片合為一個 Tab 停駐點。 |
| 方向鍵 | 移到該方向上最近的步驟。 |
| Alt + 方向鍵 | 沿著連線移動：順著工作流程移到下一個步驟，逆向移到上一個步驟，橫向移到另一個與它共享同一上一步驟的相鄰步驟。 |
| Home / End | 移到第一個或最後一個步驟。 |
| Enter | 開啟步驟（`NodeActivated`）。 |
| + / − | 以焦點所在步驟為中心放大或縮小。 |
| Shift + 1 | 適配整個工作流程。 |
| Ctrl / ⌘ + 0 | 縮放到 100%。 |
| Shift + F10，選單鍵 | 要求顯示內容選單（`ContextRequested`，其來源為卡片）。 |
| Space | 選取或取消選取聚焦的步驟。 |
| Ctrl / ⌘ + A | 選取所有步驟與連線。 |
| Escape | 取消移動或連線，或清除選取。 |
| M | 在可編輯的畫布上：用方向鍵移動選取的步驟（或聚焦的步驟）；Shift 移動五個網格步距，Enter 放置，Escape 取消。 |
| Delete / Backspace | 在可編輯的畫布上：刪除選取的步驟與連線（或聚焦的步驟）；鎖定的步驟會保留。 |
| C | 在可編輯的畫布上：連接聚焦的步驟。若有多個輸出，用方向鍵選擇其一並按 Enter 繼續；接著用方向鍵依閱讀順序選擇一個步驟，按 Enter 連接，按 Escape 取消。 |
| Ctrl / ⌘ + Z | 在可編輯的畫布上：復原最後一次編輯。 |
| Ctrl / ⌘ + Shift + Z, Ctrl + Y | 在可編輯的畫布上：重做該次編輯。 |
| Ctrl / ⌘ + C | 複製選取的步驟（或聚焦的步驟）及其之間的連線。 |
| Ctrl / ⌘ + X, Ctrl / ⌘ + V | 在可編輯的畫布上：剪下它們，並貼上。 |
| Ctrl / ⌘ + D | 在可編輯的畫布上：為選取的步驟（或聚焦的步驟）建立副本。 |

卡片中控制項內輸入的按鍵（例如自訂內容中的輸入框）屬於該控制項。

以鍵盤到達的步驟會被捲動到檢視中。

## 動態效果

- 執行中卡片的邊框與填滿會在標準時長內漸變為強調色；略過的卡片會淡化至一半的不透明度。
- 狀態變化時，卡片末端的標記會被替換：新標記從 0.6 倍大小放大，舊標記同時收縮消失，採用俐落的彈簧。
- 執行中的圓環每 0.7 秒轉一圈，且僅在其顯示於螢幕上時轉動。
- 當一個步驟完成且下一個步驟已開始時，資料會在 400ms 內以強調色沿連線繪製，採用緩入緩出曲線；下一張卡片要等資料抵達後才會顯示為執行中。
- 適配、使用按鈕或按鍵縮放，以及將步驟帶入檢視，會以 Arc 的標準緩出曲線在 360ms 內移動檢視；拖曳、滾輪與雙指縮放則緊隨手指，不做緩動。
- 排列會以同樣的方式把卡片滑動到新的位置，連線隨之移動。
- 被拖曳的卡片會緊隨指標移動，不做緩動，並浮在其他卡片之上；放開時以俐落的彈簧吸附到點陣上；取消的移動則滑回起點。
- 正在繪製的連線不做緩動地跟隨指標，並在 24px 範圍內跳到最近的輸入，該輸入會放大並採用強調色；連線無法通往的步驟會在標準時長內淡化至 40%。復原與重做會讓被移動的卡片滑回原位。
- 減少動態效果：不做顏色漸變，標記以短暫的淡入淡出切換，圓環保持靜止，資料直接顯示為已繪製完成，檢視與卡片直接跳到目的位置。工作流程處於靜止狀態時，不會有任何循環動畫。

## 相關元件

- `Stepper`：人員按順序完成的簡短直線式步驟。
- `AgentPlan`：可編輯檢查清單形式的代理計畫。
- `AgentTimeline`：在同一時間軸上重播多代理執行。
- `DataFlow`：呈現資料從來源流向目的地的插圖。
