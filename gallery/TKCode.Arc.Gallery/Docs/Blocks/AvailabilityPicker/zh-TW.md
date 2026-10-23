---
title: Availability picker
summary: 在多位人員的行事曆中找出會議時間，查看每個時段誰有空，並預約合適的那一個。
source-hash: 5ab1af858d62
---

<Demo name="Preview" />

## 何時使用

`AvailabilityPicker` 協助一個人為一群人選擇會議時間。一排日期卡片會顯示每一天有多少個建議時間適合所有人；被選取的日期會列出它的時段，每位與會者在每個時段旁有一個標記，有空時為綠色，忙碌時為灰色；頁尾則總結目前的選擇並負責預約。選擇另一天時，醒目標示會滑到那一天，時段會從該方向滑入，並自動選取第一個對所有人都可行的時段。

- **從應用程式自己的資料安排會議。** 面試流程、設計審查或客戶通話，而你的服務已經知道與會者的行事曆，並提出幾個時段。
- **附有確認步驟的預約流程。** 讀者比較幾天、選定時段後確認；變更選擇會撤銷確認，因此改變主意時不需要額外的按鈕。
- **可能讓某人錯過會議的時段。** 與會者忙碌的時段仍可預約。該列會說明誰忙碌，頁尾則會說明誰需要調整會議。

## 何時不用

- **只選擇一個日期。** 請使用 `Calendar` 或 `DatePicker`。
- **輸入任意時間。** 請使用 `TimePicker`；此選擇器只提供你給它的時段。
- **回覆邀請。** 此選擇器是為一群人預約時間；顯示並回覆單一邀請是另一種介面。
- **很長的日期範圍。** 日期排列會以等寬欄位顯示每一天，適合一週的工作日（五到七天）。若是整個月，請讓讀者在其他地方選擇週，再把該週交給選擇器。

## 用法

### 日期、時段與與會者

`Attendees` 是受邀的人，型別為 `ProjectPerson`；標題會顯示他們的頭像，每個時段都會依此順序為每位與會者放置一個標記。`Days` 是 `AvailabilityDay`，包含 `Date` 與其 `Slots`。每個 `AvailabilitySlot` 有一個 `Start`（一天中的時間）、選用的 `End`（否則為開始後 `Duration` 的時間），以及該時段的 `BusyAttendees`。時段會依開始時間排序。日期卡片上顯示的 `OpenCount`，會計算對所有人都合適、可預約的時段數。

```csharp
var noor = new ProjectPerson("Noor Haddad", "noor");
var tomas = new ProjectPerson("Tomás Ferreira", "tomas");
var ingrid = new ProjectPerson("Ingrid Solberg", "ingrid");
picker.Attendees.AddRange([noor, tomas, ingrid]);

var monday = new DateOnly(2026, 3, 9);
picker.Days.Add(new AvailabilityDay(monday,
    new AvailabilitySlot(new TimeSpan(9, 0, 0)),
    new AvailabilitySlot(new TimeSpan(11, 0, 0)),
    new AvailabilitySlot(new TimeSpan(13, 30, 0), tomas),
    new AvailabilitySlot(new TimeSpan(15, 30, 0))));
```

```xml
<arc:AvailabilityPicker x:Name="Picker"
                        Title="Roadmap check-in"
                        Duration="0:30:00"
                        TimeZoneName="Lisbon time" />
```

`Title` 是標題；標題下方那一行是時長與 `TimeZoneName`（"30 min · Lisbon time"），或你自己設定的 `Subtitle`。若是你自己擁有的集合，請使用 `DaysSource` 與 `AttendeesSource`：`ObservableCollection` 會被監看，對日期、時段或人員的變更會立即顯示。時段的 `Note` 會取代它自動產生的那一行；`IsEnabled="False"` 會讓無法預約的時段（已被佔用、已過去）變暗，並使其無法被選取。

### 選取

`SelectedDay` 與 `SelectedSlot` 為雙向繫結。一開始，選擇器會選取第一天（或 `SelectedSlot` 所在的那一天），除非你設定了時段，否則它會自動選定一個時段。當日期改變而選取的時段不在該天時，由 `AutoSelectSlot` 決定：`FirstEveryoneFree`（預設值）會選取第一個對所有人都合適的時段，若沒有則選取第一個可預約的時段；`First` 會選取第一個可預約的時段；`None` 則由讀者自行選擇。選擇不會按日期分別記住。

將 `SelectedSlot` 設為另一天的時段，會同時選取那一天。`SelectDay` 與 `SelectSlot` 可從程式碼做到同樣的事；`SelectDay(day, animate: false)` 會在沒有動態效果的情況下移動。

`SelectedDayChanged` 會回報舊的與新的日期、`Direction`（1 表示較晚，−1 表示較早），以及同時選取的時段；`SelectedSlotChanged` 會回報舊的與新的時段。兩者都會說明這次變更是否由讀者發起（`IsUserInitiated`）。

<Demo name="Bound" />

### 預約

按鈕會預約目前的選擇：`ConfirmRequested` 會先引發，並可以 `Cancel`（例如可用性檢查失敗、缺少權限）；接著 `IsConfirmed` 變為 true，`Confirmed` 被引發，並執行 `ConfirmCommand`。這些引數包含日期與時段、實際的 `Start` 與 `End`、`Conflicts`（忙碌的與會者），以及頁尾顯示的文字。預約後，按鈕會顯示帶有勾號的 "Booked"，仍可取得焦點，但不會有任何作用；選擇另一天或另一個時段會撤銷預約，並引發 `ConfirmationReset`。`Confirm` 與 `ResetConfirmation` 可從程式碼做到同樣的事。

```csharp
picker.ConfirmRequested += (_, e) =>
{
    // Someone took the room in the meantime: keep the selection unbooked.
    e.Cancel = !IsRoomFree(e.Start, e.End);
};
picker.Confirmed += (_, e) => SendInvites(e.Start, e.End, e.Conflicts);

static bool IsRoomFree(DateTime start, DateTime end) => true;
static void SendInvites(DateTime start, DateTime end, IReadOnlyList<ProjectPerson> conflicts) { }
```

從程式碼設定 `IsConfirmed` 會顯示已預約的狀態，但不會發出通知，適合還原先前已完成的預約。

### 有框或無框

`IsFramed`（預設值）會畫出有邊框、圓角的表面，時段上方有一條線，頁尾為較淡的顏色，適合放在其他內容之間的選擇器。當它佔滿整個頁面或獨立的面板時，請將其設為 false。

<Demo name="Flat" />

### 窄版容器

選擇器讀取的是它自己的寬度，而不是視窗的寬度。寬度低於 520 時，內距會縮小，日期卡片會變小，時段改為單欄，頁尾會堆疊在全寬按鈕的上方；寬度為 360 及以下時，圖例與卡片上的數量會隱藏。`IsLegendVisible` 可在任何寬度下隱藏圖例。

<Demo name="Narrow" />

### 較複雜的資料

若某一天的每個時段都與某人的行程衝突，選擇器會選取它第一個可預約的時段，而頁尾會說明誰需要調整會議。多位忙碌的與會者會以簡短的名字列出（"Noor and Ingrid are busy"）。當某一天的列數較少時，列表的高度會以彈簧動畫變為新的大小；沒有任何時段的日期則會以說明文字取代列表。

<Demo name="TrickyData" />

### 文字

每段文字都有英文預設值與對應的屬性：按鈕（`ConfirmText`、`ConfirmedText`）、各行文字（`EveryoneFreeText`、`BusyFormat`、`BusyManyFormat`、`ConflictFormat`、`ConfirmedLabelText`、`NoSlotText`、`NoSlotsText`）、數量與圖例（`OpenCountFormat`、`FreeLegendText`、`BusyLegendText`、`AndWord`）、日期與時間格式（`DayNameFormat`、`DayNumberFormat`、`DayLabelFormat`、`TimeFormat`、`RangeFormat`、`SelectionFormat`、`DurationFormat`、`SubtitleFormat`），以及螢幕閱讀器會朗讀的內容（`AttendeesAutomationFormat`、`WeekAutomationFormat`、`DayAutomationFormat`、`TimesAutomationFormat`、`SlotAutomationFormat`、`ConfirmedAnnouncementFormat`）。日期與時間會以目前的文化特性格式化。

```xml
<arc:AvailabilityPicker Title="Point d’équipe"
                        ConfirmText="Réserver ce créneau"
                        ConfirmedText="Réservé"
                        EveryoneFreeText="Tout le monde est libre"
                        BusyFormat="{}{0} est occupé"
                        DayLabelFormat="{}{0:dddd d MMMM}"
                        RangeFormat="{}{0} à {1}" />
```

## API 參考

<!-- api: AvailabilityPicker -->

<!-- api: AvailabilityDay -->

<!-- api: AvailabilitySlot -->

<!-- api: AvailabilityDayChangedEventArgs -->

<!-- api: AvailabilitySlotChangedEventArgs -->

<!-- api: AvailabilitySelectionEventArgs -->

<!-- api: AvailabilityConfirmRequestedEventArgs -->

## 無障礙

- **鍵盤。** 每張日期卡片、每個時段與按鈕都是 Tab 停駐點，順序依閱讀方向：日期由左至右，時段逐列排列，最後是按鈕。Enter 或 Space 會選取取得焦點的日期或時段，或預約目前的選擇；選取時焦點會留在原處，預約後焦點停在按鈕上，而按鈕不會再有任何作用。方向鍵不會用於此操作。
- 選擇器是一個以標題命名的群組，標題為第 2 級標題；時段上方的日期則是第 3 級標題。
- 頭像是一張名為 "Invited: Noor Haddad, Tomás Ferreira and Ingrid Solberg" 的圖片。
- 日期排列是一個名為 "Days from 9 March" 的群組。每張卡片是一個切換按鈕，其按下狀態即代表是否被選取，名稱包含日期以及對所有人合適的時段數。
- 時段是一個名為 "Times for Monday, 9 March" 的清單。每個時段是一個切換按鈕，名稱包含時間範圍與其說明行（"13:30 to 14:00, Tomás is busy"）；無法預約的時段會停用。標記與勾號不會被朗讀。
- 預約會以禮貌方式通知（"Booked Monday, 9 March, 13:30 to 14:00."）；當新的選擇撤銷預約時，通知即被清除。日期與時段的變更不會通知，因為取得焦點的控制項已經說明了它們。

## 動態效果

- **日期醒目標示**：所有卡片下方的一塊填色，會以柔和的彈簧（剛度 340、阻尼 34、無過衝）滑到被選取的日期。卡片的文字與邊框顏色會在 240 ms 內變化；被按下的卡片會縮小到 97%（醒目標示也隨之縮小）。
- **時段**：新的清單會從移動方向的一側以 16 px 的距離，依平滑彈簧滑入，同時在 160 ms 內淡入；舊的清單則以 16 px 的距離向另一側離開，並在同一時間於 160 ms 內淡出。快速的變更會相互疊加：正在進入的清單會從目前的位置轉向。當新的日期列數不同時，高度會以彈簧變化。
- **選取**：被選取時段的填色與邊框會在 160 ms 內變化；其勾號以臨界阻尼彈簧從右側滑入，並將標記推開，同時在 160 ms 內淡入。被按下的時段會縮小到 98%。
- **文字**：標題與頁尾的兩行只在內容改變時才替換。舊文字會上升 4 px 並加上 2 px 的模糊同時淡出，新文字則從下方 4 px 處上升，並由模糊中浮現，兩者皆在 160 ms 內完成。
- **按鈕**：其文字與寬度會隨按鈕本身的模糊替換與寬度彈簧而變化。
- 減少動態效果時，醒目標示會立即移動，舊清單立即移除，新清單則在 120 ms 內淡入，文字以 120 ms 交叉淡化；顏色與勾號會立即變化。關閉動態效果時，所有變化都會立即套用。

## 相關元件

- `Calendar` 與 `DatePicker`，用於選擇單一日期。
- `TimePicker`，用於輸入時間。
- `Button`，選擇器用它來預約。
- `GlidingIndicator`，日期醒目標示背後的基礎元件。
