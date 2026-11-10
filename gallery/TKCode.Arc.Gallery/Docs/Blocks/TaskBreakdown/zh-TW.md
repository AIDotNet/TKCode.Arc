---
title: Task breakdown
summary: 把一個大任務拆成根據描述建議的子任務，然後編輯每個標題、估時和負責人，通過拖曳或按鍵調整順序，並在創建前查看總時長和每個人的工作量。
source-hash: d98e03f2adb6
---

<Demo name="Preview" />

## 何時使用

`TaskBreakdown` 把一個大任務變成計劃。先描述任務；Suggest 會根據描述建議子任務，每個都帶有估時和按工作量選出的負責人；之後每個子任務都可以改名、重新估時、改派、拖到新的位置或刪除，也可以再添加。底部合計時長，顯示每個人的工作量，並創建子任務。

- **規劃一個功能或一次上線。** 寫下要做的事，得到一份可編輯的初始清單。
- **拆分過大的任務。** 把順序和負責人放在同一個地方。
- **接入模型。** 把你自己的服務交給 `SuggestAction`；編輯交給本區塊。

## 何時不用

- **持續追蹤任務。** 創建之後，請在 `Kanban board` 上追蹤子任務。
- **依賴關係和日期。** 子任務需要開始和結束日期時，請使用 `Gantt timeline`。
- **一句話就能說完的任務。** 對已經很小的任務，拆分沒有意義。

## 用法

本區塊派生自 `TemplatedControl`。當 `IsSampleDataEnabled` 為 true 且沒有提供任何內容時，它顯示一個虛構的任務、團隊和建議。提供 `TaskTitle`、`Description`（都是雙向）和 `People`；清單直接編輯 `Subtasks`。

```xml
<arc:TaskBreakdown TaskTitle="Move the blog" HoursPerDay="7" IsSampleDataEnabled="False" />
```

```csharp
breakdown.SuggestAction = async (request, cancellation) => await planner.SplitAsync(request.Title, request.Description, cancellation);
breakdown.Created += (_, e) => tracker.CreateSubtasks(e.Title, e.Subtasks);
```

### 建議

沒有 `SuggestAction` 時，內置的建議功能會把描述拆成步驟（按行、清單項目、句子和 "then"），根據每一步提到的工作類型估時，沒有提到審查時加上一個審查步驟，並按工作量把任務分給各人。`SuggestFromText` 不需要區塊就能運行它。建議運行期間 `IsSuggesting` 為 true，按鈕顯示載入指示；失敗時顯示一行簡短說明，並保留現有清單。

### 編輯與創建

行是一個 `ReorderableList`：拖曳手柄，或使用手柄的按鍵。`TotalHours` 合計估時；合計行還會按 `HoursPerDay` 給出工作天數。`Create` 引發 `Created`，帶上有標題的子任務。文字都在 `Labels`（`TaskBreakdownLabels`）中。

<Demo name="OwnSuggester" />

## API 參考

<!-- api: TaskBreakdown -->

<!-- api: TaskBreakdownSubtask -->

<!-- api: TaskBreakdownRequest -->

## 無障礙

- 標題和描述輸入框都有標籤；每行的標題輸入框名為 "Subtask N"，估時和負責人也有名稱。
- 清單手柄遵循 `ReorderableList`：**↑/↓** 移動一行，**Space** 拿起和放下，**Escape** 取消；每次移動都會被播報。
- 建議和創建會以禮貌方式播報；建議失敗會以強調方式播報。

## 動效

- 行像 `ReorderableList` 一樣抬起、滑動和落下；建議按鈕顯示載入指示並淡入淡出地切換文字。
- 減少動效時仍可拖曳，但沒有彈簧。

## 響應式

- 在 `CompactWidth`（600 px）或更窄時，每行把估時和負責人放到標題下方；內邊距收緊到 16。
- 底部的工作量標籤會換行。

## 相關元件

- `ReorderableList`、`NumberField` 和 `Select`，組成本區塊的控制項。
- `Kanban board` 和 `Gantt timeline`，用於追蹤結果。
