---
title: Smart scheduler
summary: 用自然語言說出你在意的條件來找會議時間：最合適的時段顯示在一週檢視上，旁邊是每個人的忙碌時間，每個時段都附有理由，可以直接預訂。
source-hash: f9c24e5b370d
---

<Demo name="Preview" />

## 何時使用

`SmartScheduler` 為一組人找會議時間。選擇參加的人和會議時長，然後用自然語言說出你在意的條件："afternoons, not Friday, keep lunch free"。調度器會把讀懂的條件顯示成小標籤，比對每個人的行事曆，並在一週檢視上顯示最合適的幾個時段；檢視中的每個格子按當時忙碌的人數加深顏色。每個提議都會說明被選中的理由（"Everyone is free"、"Leaves a break before and after"）。在週檢視或列表中選一個，然後預訂。

- **團隊會議。** 每週例會、評審、跨多個行事曆的面試安排。
- **客戶電話。** 邀請同事，並讓規劃器遵守 "before 4pm" 這樣的限制。
- **行程助理。** 接入你自己的查找邏輯，同時保留相同的週檢視和預訂流程。

## 何時不用

- **預訂某一個人的空閒時間。** 帶日期選擇器的預訂頁更簡單。
- **在完整行事曆上手動選時間。** 使用 `Week calendar` 或 `Availability picker` 區塊。
- **規則複雜的週期性會議。** 規劃器一次只看一週。

## 用法

本區塊派生自 Avalonia 的 `TemplatedControl`。不設定任何資料時顯示一個虛構的團隊和一週（`SmartSchedulerSamples`）。文字由 `SmartSchedulerPlanner.Parse` 解讀，時段由 `SmartSchedulerPlanner.Find` 查找，除非你設定了 `SlotFinder`。

```xml
<arc:SmartScheduler WeekStart="2026-11-16" ConstraintsText="Afternoons, not Friday" />
```

```csharp
scheduler.People = team.Select(p => new SmartSchedulerPerson
{
    Id = p.Id, Name = p.Name, Detail = p.Role,
    Busy = calendar.BusyTimes(p.Id, week).Select(b => new SmartSchedulerBusy(b.Day, b.Start, b.End)).ToArray(),
}).ToArray();
scheduler.BookAction = (e, cancellation) => calendar.InviteAsync(e.Start, e.End, e.People, cancellation);
scheduler.SlotBooked += (_, e) => toast.Show($"Booked {e.Start:t}");
```

### 規劃器能讀懂什麼

上午和下午；"not"、"no"、"except" 或 "avoid" 加上星期幾；"after 10"、"after 2pm"、"before 4pm"；午餐；"asap" 或 "as soon as possible"；"breaks" 或 "buffer"；以及像 "45 min" 或 "1 hour" 這樣的時長，它也會改變時長選項。讀懂的每一項都會在輸入框下方顯示為小標籤，讓人看到被讀出的內容。規劃器會在一週的每一天、工作日（`DayStartHour` 到 `DayEndHour`）的每半小時逐一嘗試，保留所有受邀者都空閒且滿足全部限制的時段，然後排序：前後留有空當、當天團隊較空閒以及一天中的時間都會加分，"as soon as possible" 則偏向最早的時段。它會提議 `SlotCount` 個時段，能做到時每天最多一個。

<Demo name="Soonest" />

### 狀態

`State` 從 `Idle` 進入 `Finding`，然後是 `Proposed` 或 `NoTimes`；預訂會經過 `Booking` 到達 `Booked`，之後可以用 "Find another time" 重新開始。`Slots`、`SelectedSlot` 和 `Constraints` 報告當前顯示的內容。`FindAsync()`、`SelectSlot(index)`、`BookAsync()`、`SetInvited(person, value)` 和 `StartOver()` 從程式碼完成控制項所做的事。改變受邀的人或時長會清除提議，因為它們可能已不再合適。

### 文字

`Labels`（`SmartSchedulerLabels`）包含各個標題、按鈕、狀態提示、時段和小時的格式，以及 `Reasons` 中規劃器給出的理由和小標籤文字。

## API 參考

<!-- api: SmartScheduler -->

<!-- api: SmartSchedulerPerson -->

<!-- api: SmartSchedulerBusy -->

<!-- api: SmartSchedulerSlot -->

<!-- api: SmartSchedulerConstraints -->

<!-- api: SmartSchedulerState -->

<!-- api: SmartSchedulerLabels -->

## 無障礙

- 調度器是一個有名稱的區域；標題是二級標題，表單的各部分是三級標題。
- 每個人都是一個以姓名命名、以說明文字描述的核取方塊；時長是一個分段控制項。
- 週檢視是一個 Tab 停靠點，作為影像讀出，說明文字是所選的提議。**Left**、**Right**、**Up** 和 **Down** 在提議的時段之間移動，**Home** 和 **End** 跳到第一個和最後一個。
- 每個提議都是一個按鈕，命名為 "Option 1: Tue, Nov 17 · 2:00–2:30 PM"，以理由作為說明，並報告 "selected"。
- "Comparing calendars…"、第一個提議、"沒有合適時間" 和預訂結果都會禮貌地朗讀。

## 動效

- 選擇框以 snappy 彈簧在時段之間滑動；提議在停留和選中時於 160 ms 內變色。
- 查找和預訂按鈕在工作期間顯示載入圈。
- 減弱動效和關閉動效時選擇框立即移動。

## 響應式

- 調度器最寬 1040。寬布局時，表單是週檢視旁寬 300 的一列。寬度不超過 760 時（`:compact`），表單移到週檢視上方，內邊距從 24 收緊到 16。
- 週檢視保持高度，並把寬度分給每一天；小時標簽留在寬 48 的邊欄中。
- 小標籤會換行；提議的理由會換行。沒有任何功能依賴停留。

## 相關元件

- `Availability picker`，用於跨行事曆手動選擇時間。
- `Week calendar`，支持拖放的完整行事曆。
- `Textarea`、`Checkbox` 和 `SegmentedControl`，組成其表單的控制項。
