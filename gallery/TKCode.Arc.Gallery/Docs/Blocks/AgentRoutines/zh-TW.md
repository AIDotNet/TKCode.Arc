---
title: Agent routines
summary: 按排程執行的智慧體例行任務：用平實語言描述的 cron 排程和下次執行時間、近期結果條、暫停或恢復的開關、Run now，以及按需查看的指令和執行記錄。
source-hash: fc89f6491b4b
---

<Demo name="Preview" />

## 何時使用

`AgentRoutines` 管理 AI 智慧體按排程完成的工作：早間摘要、夜間檢查、每週報告。每一列顯示例行任務的名稱和智慧體、用平實語言描述的排程（"Weekdays at 08:00"）和下次執行時間，以及一條近期結果條：綠色表示成功，紅色且較高表示失敗，灰色且較短表示略過。一個開關用來暫停或恢復，Run now 立即執行。展開一列可以看到智慧體遵循的指令以及近期執行和結果。

- **按排程執行的智慧體**，用於自動化或助理產品。
- **維運面板**：使用者確認例行工作在執行，並在某次失敗時介入。

## 何時不用

- **一次性任務。** 請使用 `Agent tasks`。
- **編輯排程。** 這個區塊只顯示排程；請用你自己的表單編輯。
- **沒有智慧體的一般 cron 任務。** 表格更合適。

## 用法

區塊衍生自 Avalonia 的 `TemplatedControl`。不設定任何屬性時，它顯示範例例行任務，Run now 會播放一次簡短的替身執行。在應用程式中，給它你的例行任務，並在你的後端啟動執行：

```xml
<arc:AgentRoutines Routines="{Binding Routines}" />
```

```csharp
list.RunHandler = (routine, cancellation) => api.RunNowAsync(routine.Id, cancellation); // returns an AgentRoutineRun
list.EnabledChanged += (_, e) => api.SetEnabled(e.Routine.Id, e.Routine.IsEnabled);
```

### 排程

`Schedule` 是五個欄位的 cron 運算式：分鐘、小時、日期、月份、星期（星期日為 0）。欄位可以是數字、範圍、清單和步長。`AgentRoutineSchedule.Describe` 把常見形式轉成文字，`Next` 給出下次執行時間；兩者都是公開的，伺服器可以使用同樣的規則。暫停的例行任務顯示 Paused，而不是下次執行時間。

### 執行

`Runs` 按從新到舊列出過去的執行，每次都有開始時間、結果、耗時和摘要。結果條顯示最近 `HistoryLength` 次執行。用 Run now 啟動的執行經過 `RunHandler`；結果加在 `Runs` 的最前面，處理程式擲出例外時記為一次失敗的執行並帶上訊息。

<Demo name="Events" />

## API 參考

<!-- api: AgentRoutines -->

<!-- api: AgentRoutine -->

<!-- api: AgentRoutineRun -->

<!-- api: AgentRoutineSchedule -->

<!-- api: AgentRoutinesLabels -->

## 無障礙

- 清單是名為 "Routines" 的區域；標題是標題。
- **Tab** 可到達每個例行任務的開關、標題列和 Run now。**Space** 切換開關；**Enter** 或 **Space** 展開例行任務或開始執行。
- 開關讀作 "Run Morning inbox digest on schedule"；標題列讀出名稱、排程和下次執行時間，並報告自身展開或收起的狀態。
- 結果條讀作一句摘要（"8 of 10 recent runs succeeded"）。
- 一個禮貌式即時區域會在用 Run now 啟動的執行結束時播報。

## 動效

- 例行任務的詳情以彈簧展開並帶短暫淡入；箭頭以靈敏的彈簧轉動。
- 暫停的例行任務把標題列淡到 60%。
- 減少動效時只保留短暫淡入淡出；關閉動效時所有變化立即生效。

## 響應式

- 清單最寬 760，小於這個寬度時跟隨容器。
- 寬度在 560 或以下時，結果條和 Run now 移到排程下方。
- 名稱和排程用省略號縮短。

## 相關元件

- `Agent tasks`，交給智慧體的一次性工作。
- `Agent run`，詳細查看一次執行。
- `Switch`，用來暫停例行任務的控制項。
