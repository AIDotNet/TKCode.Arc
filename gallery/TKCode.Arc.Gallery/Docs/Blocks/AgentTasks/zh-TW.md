---
title: Agent tasks
summary: 交給 AI 智慧體的任務看板：Queued、Running、Needs you 和 Done 四欄卡片，顯示負責的智慧體、進度或提問，可按智慧體篩選，按下卡片即可開啟任務的執行。
source-hash: 4a36d0bbdc01
---

<Demo name="Preview" />

## 何時使用

`AgentTasks` 是使用者交給 AI 智慧體的工作總覽。每個任務是四欄之一中的一張卡片：等待開始、進行中、需要人處理（要回答的問題或要查看的錯誤）以及已完成。進行中的卡片顯示進度和目前步驟，等待的卡片顯示智慧體的提問，每張卡片都顯示負責的智慧體和最後一次變化的時間。頂部的篩選按鈕可以只看一個智慧體。按下卡片會開啟該任務的執行。

- **團隊的智慧體工作區**：多個智慧體同時處理交辦的任務。
- **智慧體工作的收件匣**：不時需要有人處理。

## 何時不用

- **詳細查看一次執行。** 請使用 `Agent run`。
- **核准智慧體想執行的操作。** 請使用 `Agent approvals`。
- **由人規劃和拖動的專案。** 請使用 `Kanban board` 或 `Project board`，它們支援拖動。

## 用法

區塊衍生自 Avalonia 的 `TemplatedControl`。不設定任何屬性時，它顯示範例任務，其中進行中的任務會自己推進。在應用程式中，給它你的任務（`ObservableCollection<AgentTaskItem>` 中任務的增減都會被跟隨），並在按下卡片時開啟執行：

```xml
<arc:AgentTasks Tasks="{Binding Tasks}" />
```

```csharp
board.TaskOpened += (_, e) => navigation.OpenRun(e.Task.Id);
task.Progress = 0.4;
task.Status = AgentTaskStatus.NeedsInput; // the card moves to Needs you
```

### 欄與卡片

卡片所在的欄由 `Status` 決定：`Queued`、`Running`、`NeedsInput` 和 `Failed`（都在 Needs you 欄，Failed 帶紅色標籤）以及 `Done`。進行中的卡片用進度條和百分比顯示 `Progress`；`Note` 放目前步驟、提問或錯誤。`Updated` 顯示為相對時間。`AgentFilter`（雙向）讓看板只顯示一個智慧體；篩選按鈕列出看板上的每個智慧體。

<Demo name="OpenRun" />

## API 參考

<!-- api: AgentTasks -->

<!-- api: AgentTaskItem -->

<!-- api: AgentTasksLabels -->

## 無障礙

- 看板是名為 "Delegated tasks" 的區域；每欄以標題和數量命名。
- **Tab** 可到達篩選按鈕和卡片。**Up** 和 **Down** 在一欄中移動；**Left** 和 **Right** 移到相鄰一欄中最近的卡片；**Home** 和 **End** 到一欄的兩端。**Enter** 或 **Space** 開啟執行。
- 卡片會讀出標題、智慧體、狀態以及進度或備註，並提示 "Press Enter to open the run"。
- 一個禮貌式即時區域會在任務換欄時播報。

## 動效

- 按下卡片時略微縮小，指標停留時邊框變色；已完成的卡片稍微變淡。
- 減少動效時顏色變化很短；關閉動效時立即生效。

## 響應式

- 看板填滿容器。
- 寬度在 720 或以下時各欄上下排列，Up 和 Down 會繼續進入下一欄。
- 標題最多兩行；備註用省略號縮短。

## 相關元件

- `Agent run`，卡片開啟的檢視。
- `Agent approvals`，等待人處理的操作。
- `Agent routines`，按排程執行的智慧體工作。
