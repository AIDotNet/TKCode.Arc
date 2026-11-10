---
title: Goal tracker
summary: 一個週期內的團隊目標和關鍵結果：標出到今天應達到位置的進度條、負責人、信心、帶數值和備註的原地進度更新，以及對照目標直線的時間軸。
source-hash: 5e55b5cb34c0
---

<Demo name="Preview" />

## 何時使用

`GoalTracker` 追蹤一個團隊在某個週期（例如一個季度）裡的目標及其關鍵結果。每個關鍵結果都有起始值、目標值和目前值，所以進度條顯示它走了多遠，條上的標記顯示如果進展均勻，到今天應該在哪裡。負責人的信心（正常、有風險、偏離）決定進度條的顏色。進度更新在原地記錄新值、信心和備註；時間軸檢視把所有更新畫在通向目標的直線旁邊。

- **每週更新進度。** 負責人不用離開頁面就能更新關鍵結果。
- **管理層回顧。** 按信心統計的數量和總體進度條概括整個週期。
- **及早發現偏差。** 進度條落後於今天的標記，或時間軸低於直線，都需要關注。

## 何時不用

- **任務清單。** 工作項請使用 `Kanban board` 或 `Project board`。
- **是或否的裡程碑。** 關鍵結果需要一個數字；清單請使用 `Release readiness`。
- **整個公司的目標樹。** 本區塊只顯示一層目標及其關鍵結果。

## 用法

本區塊派生自 `TemplatedControl`。沒有目標時，它顯示本季度的虛構目標。提供 `Goals`、`PeriodStart` 和 `PeriodEnd`；負責人是 `ProjectPerson`。

```xml
<arc:GoalTracker Title="Support goals" View="Timeline" CurrentUser="Mia" />
```

```csharp
var reply = new GoalTrackerKeyResult("First reply under 2 hours", 35, 90, 62, "%") { Owner = mia };
tracker.Goals.Add(new GoalTrackerGoal("Answer every customer faster", "Support", mia) { KeyResults = [reply] });
tracker.CheckedIn += (_, e) => okrs.Save(e.KeyResult, e.CheckIn);
```

### 進度更新

"Check in" 在關鍵結果下方打開一個編輯區：數值、信心和備註。保存會添加一條日期為 `Today`、署名為 `CurrentUser` 的 `GoalTrackerCheckIn`，更新 `Current` 和 `Confidence`，並引發 `CheckedIn`。`CheckIn` 在程式碼中做同樣的事；`OpenCheckIn` 打開編輯區。按 Escape 關閉它。

### 團隊與檢視

`TeamFilter`（雙向）把目標縮小到一個團隊；團隊選擇列出目標中出現的團隊。`View` 在清單和時間軸之間切換。文字都在 `Labels`（`GoalTrackerLabels`）中。

<Demo name="Timeline" />

## API 參考

<!-- api: GoalTracker -->

<!-- api: GoalTrackerGoal -->

<!-- api: GoalTrackerKeyResult -->

<!-- api: GoalTrackerCheckIn -->

<!-- api: GoalTrackerConfidence -->

<!-- api: GoalTrackerView -->

## 無障礙

- 目標標題是三級標題；區塊標題是二級標題。
- 每個進度條都是 0 到 100 的只讀進度條，以關鍵結果及其數值命名。
- 進度更新編輯區會把焦點移到數值輸入框；Escape 關閉它；結果會被播報。
- 檢視切換和團隊選擇分別名為 "View" 和 "Team"。

## 動效

- 進度更新後，進度條以彈簧移到新值（Smooth，無過衝）。
- 減少動效和關閉動效時，新值立即到位。

## 響應式

- 在 `CompactWidth`（640 px）或更窄時，關鍵結果標題會換行，進度更新編輯區的輸入框上下排列；內邊距收緊到 16。
- 時間軸的標籤欄最多占寬度的 40%。

## 相關元件

- `Progress` 和 `SegmentedControl`。
- `Release readiness` 和 `Gantt timeline`，其他專案類區塊。
