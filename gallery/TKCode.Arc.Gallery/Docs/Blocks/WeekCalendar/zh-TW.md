---
title: Week calendar
summary: 週規劃器，可拖曳來新增、移動與調整行程，並復原上一次的變更；格線旁有月份與行事曆開關，另有全天列，窄版時提供三日與議程檢視。
source-hash: 95480e02214a
---

<Demo name="Preview" />

## 何時使用

`WeekCalendar` 是一次呈現一週的完整規劃介面，就像桌面行事曆應用程式的樣子。工具列用來切換週次與檢視；側邊欄提供一個可跳到任一月份的小月曆，以及用來顯示或隱藏行事曆的開關；格線上方有日期標題列、全天列與各時數，同一天重疊的行程會並排排列，今天會畫出目前時間，過去的時段則變暗。讀者可以直接編輯：在空白時段上拖曳即可新增行程並為它命名，拖曳行程可更換日期與時間，拖曳底邊可改變其長度，點一下行程即可編輯標題、日期、時間與行事曆，並可從通知列或以 Ctrl+Z 復原上一次的變更。

- **排程與時間區塊安排。** 讀者可在同一週看到會議、專注時段與出差，看出每天的排程有多滿，並拖曳來重新安排。
- **團隊與資源行事曆。** 每個行事曆（團隊、會議室或個人）都有自己的顏色與開關，讀者不必離開頁面就能比較或縮小範圍。
- **以另一地時間顯示的預約。** 行程是絕對的時間點；`TimeZone` 可讓它們依任何時鐘顯示，標題角落會標示使用的是哪個時區。

## 何時不用

- **選擇單一日期。** 請使用 `Calendar` 或 `DatePicker`；此區塊著重於各天發生的事，而不是選一個日期。
- **月與年。** 此區塊一次顯示一到七天。以行程為主的月格線或年度總覽是另一種設計。
- **簡短的近期行程清單。** 大多是空白時數的格線不如純清單或 `Timeline` 好讀。
- **同時有數千筆行程。** 只傳入目前顯示週次前後的幾週；此區塊會為它收到的每一筆行程建立索引。

## 用法

### 行程與行事曆

每個 `WeekCalendarEvent` 都有 `Id`、`Title`、以 `DateTimeOffset` 表示的 `Start` 與 `End`（不含結束時間）、`CalendarId`，以及選用的 `Location` 與 `Attendees`（`ProjectPerson` 物件；沒有圖片的會得到產生的頭像）。若行程設定了 `IsAllDay`，則改以整個日期計算：`Start` 與 `End` 的日期照寫，`End` 不含在內，因此從星期三開始、住三晚的飯店住宿，會在星期五的午夜結束。每個 `WeekCalendarSource` 都有 `Id`、`Name`、`Color`（`Blue`、`Green`、`Violet`、`Amber`、`Rose`、`Teal`、`Neutral`，或搭配 `CustomColor` 的 `Custom`）以及 `IsVisible`，側邊欄中對應的開關會改變它。未知行事曆的行程會顯示為灰色。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:WeekCalendar x:Name="Planner">
        <arc:WeekCalendar.Calendars>
            <arc:WeekCalendarSource Id="studio" Name="Studio" Color="Blue" />
            <arc:WeekCalendarSource Id="home" Name="Home" Color="Green" />
            <arc:WeekCalendarSource Id="health" Name="Health" Color="Rose" IsVisible="False" />
        </arc:WeekCalendar.Calendars>
    </arc:WeekCalendar>
</UserControl>
```

```csharp
var zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
DateTimeOffset At(int day, int hour, int minute = 0) =>
    new(new DateTime(2026, 10, day, hour, minute, 0), zone.GetUtcOffset(new DateTime(2026, 10, day, hour, minute, 0)));

Planner.TimeZone = zone;
Planner.Events.Add(new WeekCalendarEvent("brief", "Write launch brief", At(19, 9, 30), At(19, 11, 30), "studio"));
Planner.Events.Add(new WeekCalendarEvent("lunch", "Lunch with Noor", At(19, 12), At(19, 13), "home") { Location = "Corner Bistro" });
Planner.Events.Add(new WeekCalendarEvent("hotel", "Hotel: Casa Azul",
    new DateTimeOffset(2026, 10, 21, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 10, 24, 0, 0, 0, TimeSpan.Zero), "home")
    { IsAllDay = true });
```

若要使用自己擁有的集合，請改用 `EventsSource` 與 `CalendarsSource`；`ObservableCollection` 會被監看，每個行程與行事曆的每個屬性也都會被監看。同一時間點發生的變更會一起套用，格線會原地更新。

```csharp
var events = new ObservableCollection<WeekCalendarEvent>();
calendar.EventsSource = events;

// Later, when a booking comes in, it appears at once and the day's events make room for it.
var start = new DateTimeOffset(DateTime.Today.AddHours(15));
events.Add(new WeekCalendarEvent("call-42", "Client call", start, start.AddMinutes(45), "studio") { Location = "Video room" });
```

### 編輯

除非設定了 `IsReadOnly`，否則可以編輯。所有項目都會貼齊到 15 分鐘，且至少持續 15 分鐘。

- **繪製行程。** 在空白時段上按下並拖曳（移動 4 像素即開始拖曳）。你按下的 15 分鐘時段為錨點，移動端會貼齊到最近的一刻鐘，因此往上拖曳也能繪製；草稿會留在你按下的那一天。放開滑鼠後，快速建立彈出層會出現在草稿旁：輸入標題並按 Enter（空白標題會儲存為 "Untitled event"），或先選擇行事曆。在空白時段上點一下，會從指標下方的半點開始草擬一小時的行程。新行程會放入 `DefaultCalendarId`，若未設定，則放入第一個顯示中的行事曆。
- **移動行程。** 拖曳它：每次跳一個時段，並讓你抓住的那一點維持在指標下，跨天也一樣。放下時，當天其他行程會讓出位置。若拖曳結束時仍在起始時段，則不會有任何變更。
- **調整行程長度。** 拖曳它的底邊（懸停時會顯示把手）；結束時間會貼齊到一刻鐘。
- **編輯行程。** 點一下（或在其上按 Enter）即可開啟詳細資料：標題、日期、以 15 分鐘為單位的開始與結束時間、地點、人員與行事曆。時間、日期與行事曆在你選擇時即套用；標題與地點則在彈出層關閉時套用。全天行程會以文字顯示日期，並可變更標題與行事曆。
- **刪除與復原。** 在聚焦或選取的行程上按 Delete 或 Backspace，或在其詳細資料中按 Delete，即可移除它。行事曆底部會升起一則通知，顯示六秒（`UndoDuration`），上面有 Undo；Ctrl+Z（Mac 上為 Cmd+Z）效果相同。復原只有一層，涵蓋上一次的建立、移動、調整大小、時間、日期或行事曆變更，或刪除；同一行程連續的鍵盤移動，或同一個詳細資料彈出層中所做的變更，會一起復原。`CanUndo` 表示是否有可復原的變更。
- **格線會捲動。** 拖曳到其頂部或底部附近時會捲動，越靠近越快，指標靜止時仍會持續捲動。
- 在已開啟的彈出層以外按下，只會關閉它。Escape 會關閉彈出層、取消拖曳（行程會彈回），然後清除選取。`SelectedEvent` 是被選取的行程，為雙向繫結。

<Demo name="Editing" />

### 儲存變更

行事曆會直接編輯你的資料：它會在 `WeekCalendarEvent` 物件上寫入 `Start`、`End`、`CalendarId`、`Title` 與 `Location`，並在 `Events` 中新增或移除行程；若 `EventsSource` 是可寫入的清單，則改為在其中操作（`ObservableCollection` 會被監看；一般的 `List` 則會重新讀取）。接著它會引發 `EventCreated`（復原帶回已刪除的行程時也會引發）、`EventUpdated`（其中的 `Previous` 是變更前行程的副本；復原時會再次引發，且數值互換），或 `EventDeleted`（復原移除新行程時也會引發）。請在這些處理常式中儲存資料。

```csharp
calendar.EventCreated += async (_, e) => await api.CreateAsync(e.Event);
calendar.EventUpdated += async (_, e) => await api.UpdateAsync(e.Event, e.Previous);
calendar.EventDeleted += async (_, e) => await api.DeleteAsync(e.Event.Id);
```

唯讀的 `EventsSource`（陣列、`ReadOnlyObservableCollection`）不能新增或移除行程：行事曆仍會移動並編輯行程物件，但對於新增或刪除的行程，它只會引發事件，並由你的處理常式變更它所公開的集合。設定 `IsReadOnly` 可完全關閉編輯：不能繪製、拖曳、快速建立、刪除或復原，詳細資料也會以文字顯示。新行程（以及其 `NewEventRequested` 事件）會從聚焦那天的下一個空閒整點、早上 9 點起開始草擬；設定 `Handled` 則可改開啟你自己的表單。`BeginCreate`、`OpenDetails`、`CloseEditor`、`DeleteEvent`、`Undo` 與 `CancelDrag` 可從程式碼執行相同的操作。

```xml
<arc:WeekCalendar IsReadOnly="{Binding IsArchived}" DefaultCalendarId="studio" UndoDuration="0:0:8" />
```

### 觸控

在觸控螢幕上，長按（約 380 毫秒且不移動）即可開始在空白時段繪製，或拿起行程來移動；否則垂直滑動會捲動時數。寬度低於 640 時，橫向滑動會切換日期：相鄰的日期會跟著你的手指一起移動，放開時若超過寬度的五分之一（或快速輕掃），就會直接切換到該日期而不跳動；較短的滑動則會彈回。這裡的彈出層會開在行程下方（接近底部時則在上方），並與左右邊緣保持 8 像素的距離。

### 檢視與導覽

`View` 可為 `Week`（從 `FirstDayOfWeek` 開始的七天，預設為星期一）、`WorkWeek`（星期一至星期五）或 `Day`；為雙向繫結。`Date` 是聚焦的那一天，同樣為雙向繫結：顯示的範圍一定包含它，若將它設為範圍之外的日期，行事曆會移動到該日期。"Today"、上一個與下一個會以一週為單位移動（日檢視為一天，三日檢視為三天），並保留聚焦的星期幾；側邊欄中的月份可跳到包含任一天的範圍。`GoToToday`、`Next`、`Previous`、`GoTo`、`FocusDay` 與 `ScrollToTime` 可從程式碼執行相同的操作。`RangeStart`、`RangeEnd`、`RangeTitle` 與 `EffectiveView` 會回報目前顯示的內容；`RangeChanged` 則說明是什麼引起了變動，以及變動的方向。

格線會在畫面中第一個行程的時刻開啟（沒有行程則為上午 8 點），或在 `InitialScrollTime` 指定的時間開啟。若顯示的是今天，目前時間也會捲動到由上往下三分之一處。`StartHour` 與 `EndHour` 會限制顯示的時數；超出範圍的行程會在邊緣被截斷。

<Demo name="WorkHours" />

### 窄版容器

行事曆讀取的是它自己的寬度。寬度 1100 以上時會顯示側邊欄（`IsSidebarVisible` 可強制開啟或關閉）；低於此寬度時，行事曆會把各行事曆移到工具列中的 "Calendars" 彈出層。寬度低於 900 時，標題與標題列的日期會縮小，New event 只顯示加號；低於 760 時，Calendars 按鈕只顯示顏色；低於 680 時，工具列會分成兩列。寬度低於 640 時，檢視會變為三日、日與議程：週檢視會從聚焦日起顯示三天，議程則依日期列出該週，全天行程排在最前面，沒有安排的日子會顯示 "Nothing planned"，每天的標題會固定在頂部，而其下的行程可以捲動。行程區塊在變小時會省略地點、頭像與時間；有足夠高度時，標題最多換成三行。

<Demo name="Phone" />

### 時區

行程是絕對時間；`TimeZone`（null 表示電腦的時區）決定它們以哪個時鐘顯示，以及目前時間線與「今天」。標題角落會顯示該時區的時區偏移（"GMT+8"、"GMT+5:30"、"GMT"）。`Culture` 設定日期與時間的語言：英文會合併範圍兩端共同的部分（"Sep 28 to Oct 4, 2026"、"9:30 – 11:30 AM"）；中文則使用年月日的標題與 24 小時制。`Now` 可固定目前時間，供截圖與測試使用。

<Demo name="TimeZones" />

### 全天列

全天行程是橫跨其日期的長條，會排入數條泳道。若所需的泳道超過 `AllDayLaneLimit`（3）條，最後一條泳道會在有隱藏行程的每一天顯示 "+N more"；"All day" 下方的箭頭可展開此列，`IsAllDayExpanded` 為雙向繫結的狀態。

<Demo name="BusyAllDay" />

### 文字

每個文字都有 `WeekCalendarLabels` 中的英文預設值：工具列、檢視名稱、供螢幕閱讀器使用的日期、行程與全天長條的名稱（這些是函式，因此其各部分的順序可以改變），以及各項通報。替換 `Labels` 即可翻譯；其中的日期與時間則來自 `Culture`。

```csharp
calendar.Culture = CultureInfo.GetCultureInfo("de-DE");
calendar.Labels = new WeekCalendarLabels
{
    Today = "Heute",
    WeekView = "Woche",
    WorkWeekView = "Arbeitswoche",
    DayView = "Tag",
    AllDay = "Ganztägig",
    NewEvent = "Neuer Termin",
    TimedEvent = (title, day, start, end, calendar, place) => $"{title}, {day}, {start} bis {end}, {calendar}",
};
```

### 效能

- 每個行程方塊、格子與標題日期都是自行繪製並快取的；範圍滑動時不會重新配置版面，只會移動。
- 拖曳時只移動被拖曳的方塊；當天其他行程會在放下時重新配置一次，並以變換動畫就定位。
- 範圍會繪製到最多三個可重複使用的窗格中；滑出的窗格會被重新用於下一個範圍（連同其方塊），因此逐週翻頁時不會建立任何新物件。
- 目前時間每分鐘移動一次，計時器會在行事曆離開視窗或設定了 `Now` 時停止。

## API 參考

<!-- api: WeekCalendar -->

<!-- api: WeekCalendarEvent -->

<!-- api: WeekCalendarSource -->

<!-- api: WeekCalendarLabels -->

<!-- api: WeekCalendarEventUpdatedEventArgs -->

### 事件

| 事件 | 引發時機 | 引數 |
|---|---|---|
| `RangeChanged` | 顯示的日期已改變 | `OldStart`, `OldEnd`, `NewStart`, `NewEnd`, `Direction`, `Reason` |
| `ViewChanged` | `View` 已改變 | `OldView`, `NewView`, `EffectiveView` |
| `CalendarVisibilityChanged` | 使用了某個行事曆的開關 | `Calendar`, `IsVisible` |
| `EventInvoked` | 點擊行程，或以 Enter 或 Space 啟動它 | `Event` |
| `NewEventRequested` | 按下了 New event；`Handled` 可讓行事曆不草擬行程 | `Date`（聚焦的那一天） |
| `EventCreated` | 新行程已儲存，或復原帶回了已刪除的行程 | `Event` |
| `EventUpdated` | 行程被移動、調整大小或編輯，或變更被復原 | `Event`, `Previous` |
| `EventDeleted` | 行程被刪除，或復原移除了新行程 | `Event` |

## 無障礙

- **鍵盤。** Tab 會依序到達工具列（Today、上一個、下一個、檢視切換、Calendars、New event）、月份（只有一個 Tab 停駐點；方向鍵在日期間移動，Page Up 與 Page Down 在月份間移動，Enter 選擇日期）、行事曆開關、全天長條、格線（只有一個 Tab 停駐點：聚焦的那一天），然後依時間順序逐一到達每個行程。
- **在格線中**，左右鍵會移到前一天或後一天；超過第一天或最後一天時，下一個範圍會滑入，焦點會落在其第一天或最後一天。在行程上，上下鍵移到前一個或後一個行程，左右鍵移到前一天或後一天。T 前往今天，D 開啟聚焦的那一天，W 開啟其所在的那一週。按下 T、D、W 之後，以及範圍滑動之後，焦點都會留在聚焦的那一天。
- **編輯按鍵。** N 會在聚焦那天的下一個空閒整點草擬一個行程，並開啟快速建立彈出層；在某一天上按 Enter 也有相同效果，在行程上按 Enter 則開啟其詳細資料，在快速建立的標題中按 Enter 則儲存。在聚焦或選取的行程上按 Delete 或 Backspace 會將它刪除。Alt 加上上或下會以 15 分鐘為單位移動聚焦的行程，Alt 加上左或右則以一天為單位移動；再加上 Shift 按上或下，可改變其長度。焦點會留在該行程上。Ctrl+Z（Cmd+Z）會復原上一次的變更。Escape 會關閉彈出層、取消拖曳，然後清除選取。
- **彈出層**是名稱為 "New event" 與 "Event details" 的對話框。焦點會移到標題中，Tab 會停留在對話框內，直到按下 Escape、"Done" 或 "Cancel"；之後焦點會回到該行程（若是取消的草稿，則回到其所在的那一天）。行事曆的選擇是一組單選按鈕，只有一個 Tab 停駐點；方向鍵可選擇行事曆。刪除後焦點會移到該行程所在的那一天；在通知上按下 Undo 後，焦點會回到行事曆。
- 格線是一個以其範圍命名的表格（"Oct 19 to 25, 2026"），附有說明按鍵的提示文字；每一天是一個儲存格，名稱如 "Thursday, October 22, today, 6 events"。行程是按鈕，名稱為 "title, day, start to end, calendar, place"；全天長條則為 "title, all day, first day to last day, calendar"；議程列會省略其標題已說明的那一天。標題列的日期是名稱為 "Show Monday, October 19" 的按鈕；月份中的日期則以 "Today" 作為狀態。
- 行事曆開關是以行事曆名稱命名的核取方塊；側邊欄的名稱為 "Calendar navigation"。
- 一個禮貌模式的即時區域會朗讀範圍變更（"Oct 26 to Nov 1, 2026"）與檢視變更（"Work week, Oct 19 to 23, 2026"、"Day view, Thursday, October 22"），以及每一次編輯：新增、移動、調整大小或重新排程的行程、新的行事曆或標題、刪除、還原、復原、取消的拖曳，以及清除的選取。通知也顯示相同的文字。所有這些文字都在 `WeekCalendarLabels` 中。
- 行事曆的顏色從來不是唯一的提示：每個行程都會說出其行事曆的名稱。

## 動態效果

- **範圍**：標題列、全天列與格線會一起在同一個彈簧上滑動一個寬度（臨界阻尼，340 毫秒後約剩 10% 的距離，約一秒後靜止）。滑動中再次按下下一個，會讓每個窗格從目前位置、以目前速度繼續；畫面上最多同時有三個範圍。跨越數週的跳轉同樣滑動一個寬度。
- **檢視**：舊檢視在 160 毫秒內淡出，新檢視則在 240 毫秒內淡入，原地進行。
- **全天列**在展開或收合時，於 240 毫秒內增長或收起。
- **懸停與聚焦**：行程的填色、指標下的半小時時段、標題列與月份的日期，都在 160 毫秒內淡入。檢視切換的反白會在各選項之間滑動。
- **拖曳**：被拖曳的行程會以快速的臨界阻尼彈簧，在 15 分鐘時段與日期之間跳動（約三分之一秒內靜止），每次跳到新時段都從它目前繪製的位置重新開始；調整長度則是逐時段改變長度，沒有動態效果。放下後、在詳細資料中編輯後、鍵盤移動後、復原後或刪除後，移動過的行程會以 Smooth 彈簧定位；取消的拖曳也會以它彈回。
- **彈出層**會在 160 毫秒內淡入，並以 Smooth 彈簧從 0.96 放大，從面向其行程的那一側展開；離開時為 120 毫秒。**復原通知**以 Smooth 彈簧上升 12 像素，離開時於 160 毫秒內下降 8 像素；較新的變更會以兩者同時移動的方式取代它。觸控螢幕上的滑動會從手指放開的位置，以範圍彈簧繼續。
- 減少動態效果時，範圍滑動會變為同樣的交叉淡化，檢視切換的反白立即移動，行程的跳動與彈簧動畫則取消（行程會立即移動），彈出層與通知只保留淡入淡出；關閉動態效果時，所有變化都會立即套用。

## 相關元件

- `Calendar` 與 `DatePicker`，用於選擇日期。
- `SegmentedControl`，單獨用於檢視切換。
- `Popover`，用於較窄容器中的行事曆清單。
- `Select`、`DatePicker` 與 `Input`，單獨用於詳細資料彈出層中的欄位。
- `Timeline`，用於依時間順序排列的行程動態。
