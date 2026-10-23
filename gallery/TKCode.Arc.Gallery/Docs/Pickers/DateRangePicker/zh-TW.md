---
title: 日期範圍選擇器
summary: 從觸發器展開為兩個月份面板的範圍選擇器，附帶預設選項與可延伸的範圍高亮。
source-hash: 07b0d1fff7fc
---

<Demo name="Report" />

## 何時使用

`DateRangePicker` 把一段以整天為單位的範圍當成一個值來選取：一個開始日與一個結束日。它位於篩選列、預訂表單或工具列中。關閉狀態下，觸發器以一行簡短文字顯示已套用的範圍；點選後會開啟面板，其中有預設選項清單與一或兩個月份，因此使用者可以一鍵選取常用期間，也能精確選定一段日期。

- **分析儀表板中的報表篩選**：營收或轉換率報表，附帶「最近 7 天」「最近 30 天」「年初至今」等預設選項。預設選項涵蓋常用期間，日曆負責其餘部分。
- **住宿與請假申請**：HR 入口網站中的飯店訂房或請假申請需要入住日與退房日。將 `MinDate` 設為今天，就無法選取過去的夜晚；將 `MaxDate` 設為最後一個可預訂的日期。
- **帳單期間與發票匯出**：管理後台依 `Oct 1 – 31, 2026` 這類期間匯出發票。數值只在按下「套用」後才會變更，因此尚未選完的範圍不會進入匯出查詢。
- **精簡工具列與側邊面板**：日誌檢視器或事件時間軸的篩選列，在變更之前只顯示一個時間區間標籤。當工具列位於側邊欄中時，`MonthCount="One"` 可讓面板維持窄小。

## 何時不用

- **單一天，例如到期日或生日**：用 `DatePicker`。它只保存一天，沒有預設選項清單，也沒有範圍高亮。
- **需要停留在頁面上的月曆**：當使用者要掃視可用情況，或比較一個月內的多個日期時，用 `Calendar`。範圍選擇器只在需要時才開啟，因此面板開啟時，頁面上的其他內容都無法同時看到。
- **一天中的時刻，例如班次開始或會議時段**：每個時刻都用 `TimePicker`。`DateRange` 保存的是 `DateOnly` 類型的兩端，因此範圍選擇器從不攜帶時間。
- **只有固定的命名期間清單，沒有自訂範圍**：用 `Select`。預設選項清單是本元件的一部分；如果從不使用自訂月曆，一般清單更簡單。
- **自由輸入日期範圍**：觸發器沒有文字輸入，因此使用者無法輸入 `10/05 - 10/09`。若產品需要手動輸入日期，請給表單兩個 `DatePicker` 欄位，並驗證結束日期晚於開始日期。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:DateRangePicker Label="日期範圍" />
</UserControl>
```

```csharp
var today = DateOnly.FromDateTime(DateTime.Today);
picker.SelectedRange = new DateRange(today.AddDays(-6), today);
```

`DateRangePicker` 衍生自 Avalonia 的 `TemplatedControl`，因此它的外觀來自 ControlTheme，而面板是由元件自己擁有的浮層。它不是文字方塊，也不是 Avalonia 的日期選擇器：觸發器中不輸入任何內容，因此沒有游標、沒有輸入法，也沒有日期解析。欄位在與其他表單控制項相同的 `Field` 基礎之上加入標籤、說明與錯誤列，面板則以 Arc 的月份格線繪製。`DateRange` 是由兩個 `DateOnly` 組成的記錄結構，兩端都包含在內，因此 `Days` 也會計入兩端。

### 結構

1. **標籤**（`Label`、`IsLabelVisible`）：觸發器與面板的無障礙名稱。標籤預設隱藏；設定 `IsLabelVisible="True"` 後，它會以欄位標籤的樣式顯示在觸發器上方。
2. **觸發器**（由 `DateRangePicker.TriggerPart` 指定的範本部件）：高 44 px（Medium 控制項高度），圓角 18 px，1 px 邊框位於抬起的表面上，並帶有靜止陰影。內容前的內距為 14 px，內容後為 12 px，各部分之間相距 10 px。懸停時，觸發器以 3% 的前景色浮層填滿，邊框加深為強邊框色。
3. **日曆圖示**：16 px，次要文字顏色。
4. **值**（由 `DateRangePicker.ValuePart` 指定的範本部件）：已套用的範圍，中等字重，例如 `Oct 5 – 9, 2026`。沒有範圍時，以次要文字顏色顯示 `Placeholder`。變動的詞會在原位捲動。
5. **箭頭**：16 px，淡化文字顏色，位於觸發器末端。
6. **說明**（`Description`）與**錯誤列**（`ErrorMessage`）：觸發器下方的說明文字與錯誤文字，來自欄位基礎。非空的 `ErrorMessage` 即為錯誤文字；控制項沒有獨立的無效旗標。
7. **面板**（開啟時）：內距為 10 px（精簡版面為 8 px）。雙月版面：148 px（9.25 rem）寬的預設選項欄，與月份之間有 10 px 間距。精簡版面：預設選項變成分隔線下方的一列水平清單。
8. **月份導覽**：兩個 32 px 的圓形按鈕，分別是上一個月與下一個月。當按鈕要翻過去的月份會超出 `MinDate` 或 `MaxDate` 時，該按鈕會停用；停用狀態的不透明度為 35%。
9. **頁尾**：一條 1 px 分隔線，下方是一列：左側為摘要（天數，例如 `12 days`；第一次點選後顯示 `Pick an end date`），右側為取消與套用按鈕。兩個按鈕高 36 px。套用按鈕至少 76 px 寬，沒有草稿時停用。

### 版面：一個或兩個月份

`MonthCount` 決定面板的形式。`Auto`（預設）在邊界寬度至少為 712 px 時，於預設選項欄旁顯示兩個月份；否則在可水平捲動的預設選項列下顯示一個月份。`One` 一律使用精簡面板，`Two` 則一律在預設選項欄旁顯示兩個月份。

<Demo name="OneMonth" />

在會隨視窗伸縮的頁面與對話方塊中使用 `Auto`，讓面板展示盡可能多的脈絡。當視窗調整大小並跨過 712 px 的邊界時，面板會在一個月與兩個月之間切換，表面也會以彈簧動畫過渡到新的形狀。在較窄的側邊欄與行動裝置寬度的版面中使用 `One`，以免第二個月被截掉。只有在確知頁面夠寬時才使用 `Two`，因為 `Two` 不會退回精簡面板。

面板一律位於 `Boundary` 之內，預設為視窗。當觸發器靠近可捲動頁面的底部時，頁面會捲動，使面板可見，面板仍會從觸發器處開啟。

### 預設選項

`Presets` 會取代預設的八個選項清單。預設選項由一個標籤與一個函式組成，該函式以今天的日期為基準，回傳一個 `DateRange`。函式接收的是檢視者本地的今天，因此「最近 7 天」這類預設選項不需要頁面中的任何程式碼，就會隨月曆變動。`DateRangePresets.Default` 保存預設清單，因此自訂清單可以以它為起點，再加入自己的項目。

| 預設選項 | 範圍 |
|---|---|
| 今天 | 今天到今天 |
| 昨天 | 昨天到昨天 |
| 最近 7 天 | 今天之前的六天到今天 |
| 最近 30 天 | 今天之前的二十九天到今天 |
| 本月 | 本月第一天到今天 |
| 上個月 | 上一個完整的曆月 |
| 本季 | 本季第一天到今天 |
| 年初至今 | 1 月 1 日到今天 |

```csharp
picker.Presets =
[
    new DateRangePreset("最近 7 天", t => new DateRange(t.AddDays(-6), t)),
    new DateRangePreset("未來 14 天", t => new DateRange(t, t.AddDays(13))),
    new DateRangePreset("過去三個月", t => new DateRange(t.AddMonths(-3), t.AddDays(-1))),
];
```

當草稿與某個預設選項的範圍相同時，該選項會被高亮，因此無論使用者選的是預設選項，還是手動選中相同的日期，清單都會顯示目前的選擇。高亮會從一個選項平滑滑動到下一個。請把清單維持在六項左右：預設選項清單每列 32 px，在精簡版面中，該列會水平捲動。

### 限制與每週起始日

`MinDate` 與 `MaxDate` 會停用範圍之外的日期，以及會翻到範圍之外的月份按鈕。鍵盤同樣受限：方向鍵與翻頁鍵不會把焦點移到停用的日期上。`FirstDayOfWeek` 設定格線的第一欄，預設為星期日，也可以設為星期一。

<Demo name="Booking" />

當限制來自商業規則（例如六個月的訂房期間）時，請在檢視模型中設定它們，並把預設選項限制在該期間之內。超出 `MaxDate` 的預設選項不會自動裁剪，因此當範圍必須維持在期間之內時，應以同一限制來計算預設選項。

### 地區文化

`Culture` 設定月份標題、星期標題、範圍標籤與天數的文字。為 null 時使用目前的 UI 地區文化。控制項顯示的文字都位於屬性之中，因此在地化頁面需要像表單其他部分一樣，為這些屬性提供翻譯。

<Demo name="Culture" />

| 屬性 | 預設值 | 用於 |
|---|---|---|
| `Placeholder` | `Select dates` | 未套用範圍時的觸發器 |
| `ApplyText` / `CancelText` | `Apply` / `Cancel` | 頁尾按鈕 |
| `NoDatesText` | `No dates` | 草稿為空時的觸發器與摘要 |
| `PickEndText` | `Pick an end date` | 第一次點選之後、選定結束日期之前的摘要 |
| `DayCountFormat` | `{0} day` | 一天的天數；只有數量為 1 時才使用單數形式 |
| `DaysCountFormat` | `{0} days` | 兩天或以上的天數；`{0}` 為數字 |
| `PreviousMonthLabel` / `NextMonthLabel` | `Previous month` / `Next month` | 導覽按鈕的無障礙名稱 |

為每種語言同時設定兩種天數格式，因為控制項只有在數量為 1 時才使用單數形式。`FirstDayOfWeek` 與 `Culture` 彼此獨立：以星期一作為每週起始日是一項商業選擇，因此範例會明確設定它，而不是依賴地區文化。

### 由程式碼驅動

`SelectedRange` 是值，`IsDropDownOpen` 用來開啟或關閉面板。面板關閉時設定 `SelectedRange`，會立即更新觸發器。`FormattedValue` 會回傳觸發器顯示的文字，未套用範圍時為預留位置文字，因此狀態列或匯出標籤可以重複使用它。

```csharp
picker.SelectedRangeChanged += (_, e) =>
{
    status.Text = e.NewRange is { } range ? $"{range.Days} 天" : "無範圍";
};
```

在按下「套用」之前，所選內容都只是草稿。`SelectedRangeChanged` 只在按下「套用」時觸發，其事件引數包含 `OldRange` 與 `NewRange`。取消、Escape 以及面板外的按下都不會觸發它。

### 繫結到檢視模型

在 MVVM 應用程式中，檢視模型擁有範圍，頁面以雙向方式繫結它。使用 CommunityToolkit.Mvvm 時：

```csharp
public partial class ReportFilterViewModel : ObservableObject
{
    private readonly IReportService _reports;

    public ReportFilterViewModel(IReportService reports)
    {
        _reports = reports;
        _period = new DateRange(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 21));
    }

    [ObservableProperty]
    private DateRange? _period;

    [ObservableProperty]
    private string? _periodError;

    partial void OnPeriodChanged(DateRange? value)
    {
        PeriodError = value is { Days: > 90 } ? "最多選取 90 天。" : null;
    }

    [RelayCommand]
    private Task RunReportAsync()
    {
        if (Period is not { } range || PeriodError is not null)
        {
            return Task.CompletedTask;
        }
        return _reports.LoadAsync(range.Start, range.End);
    }
}
```

```xml
<StackPanel Spacing="16" Width="360">
    <arc:DateRangePicker Label="報表期間"
                         SelectedRange="{Binding Period, Mode=TwoWay}"
                         ErrorMessage="{Binding PeriodError}" />
    <arc:Button Content="執行報表" Command="{Binding RunReportCommand}" />
</StackPanel>
```

`SelectedRange` 是雙向繫結的。它只在使用者按下「套用」時才會寫入，因此 `Period` 永遠不會保存草稿。選擇器本身沒有命令：表單中的按鈕負責執行報表，只有當頁面需要對每次套用作出反應（例如更新狀態列）時，才需要 `SelectedRangeChanged`。編譯繫結需要在檢視上設定 `x:DataType`；範例為求簡潔而省略了它。

### 驗證

控制項只強制執行它所知道的限制：`MinDate`、`MaxDate` 以及停用的日期。關於範圍的規則屬於檢視模型，結果交給 `ErrorMessage`。

| 規則 | 何時檢查 | 顯示位置 |
|---|---|---|
| 超出 `MinDate` 或 `MaxDate` 的日期 | 選取時（日期會停用） | 無：該日期無法選取 |
| 範圍超過報表允許的長度 | 套用時，透過 `Period` 的變更 | 觸發器下方的 `ErrorMessage` |
| 表單要求填寫期間 | 送出時 | `ErrorMessage`；把焦點移到選擇器 |
| 伺服器拒絕該期間 | 請求之後 | `ErrorMessage`；`Period` 變更時清除 |

套用會把面板中的草稿擋在檢視模型之外，因此規則作用於已提交的值。使用者套用新範圍時清除錯誤；上面的 `OnPeriodChanged` 處理常式會這樣做，因為每次提交的變更都會經過它。

### 焦點與關閉

- **開啟**：在觸發器上按 Enter 或 Space 會開啟面板，並把焦點放在已提交範圍的開始日。沒有範圍時，焦點從今天開始。
- **套用**：套用會提交草稿、關閉面板，並把焦點交還給觸發器。存在草稿時，套用按鈕才可用。
- **取消**：取消、Escape 或面板外的按下都會捨棄草稿。取消與 Escape 會把焦點交還給觸發器；面板外的按下不會移動焦點，因為使用者已經自己移動了焦點。
- **尚未選完的範圍**：第一次點選之後，套用會把開始日當天作為一天的範圍提交。此時摘要顯示 `Pick an end date`，因此使用者在套用之前就能看到自己的選擇。
- **Tab 離開**：焦點移出面板會關閉面板並捨棄草稿，因此面板永遠不會在其他控制項後面保持開啟。

### 範例：訂房表單中的飯店住宿

某飯店集團的訂房表單。住宿資訊是一個欄位，入住期間限制在未來六個月之內，說明文字告訴使用者這些日期的作用。表單只在套用之後才讀取住宿資訊，因此價格報價永遠與已提交的日期一致。

```xml
<StackPanel Width="560" Spacing="20">
    <arc:DateRangePicker Label="住宿"
                         IsLabelVisible="True"
                         Description="從今天起可辦理入住，最遲到未來六個月。"
                         Culture="en-US"
                         FirstDayOfWeek="Monday"
                         Placeholder="選擇入住與退房日期" />
    <arc:Input Label="住客姓名" Placeholder="與護照上的姓名一致" />
    <arc:Button Content="繼續付款" IsDefault="True" />
</StackPanel>
```

`MinDate` 與 `MaxDate` 在程式碼中設定，取值與檢視模型使用的今天相同，因此訂房期間與停用的日期永遠不會與伺服器的規則互相矛盾。

### 文案規範

以商業意義為欄位命名：`Report period`、住宿、`Billing period`。預設選項的標籤應簡短，並採用句首大寫的格式。按鈕以動詞加受詞的形式撰寫，日期數量則遵循產品所用的格式。

| 不要寫 | 改為 |
|---|---|
| `Date`（用於範圍） | `Report period`、住宿、`Billing period` |
| `Last 7 Days` | `Last 7 days` |
| `OK` | `Apply`（當按鈕用於篩選清單時，用 `Apply filter`） |
| `Pick end date` | `Pick an end date`（預設文字） |
| `1 days` | 為一天設定 `DayCountFormat`，為更多天設定 `DaysCountFormat` |
| `Select a date range from the calendar below` | `Select dates` |

預留位置文字要保持簡短，使它能與 44 px 觸發器中的日曆圖示和箭頭並排顯示。

### 最佳做法

**建議**

- 把 `Label` 設為商業意義，並在表單中以 `IsLabelVisible="True"` 顯示它。隱藏的標籤仍會為輔助技術命名該欄位，但瀏覽表單的人需要可見的名稱。
- 雙向繫結 `SelectedRange`，並且只讀取已提交的值。草稿只在面板開啟期間存在，因此頁面的其餘部分永遠看不到尚未選完的範圍。
- 放入報表或訂房常用的預設選項，並保持清單簡短。使用者選預設選項比捲動月曆更快。
- 依商業規則設定 `MinDate` 與 `MaxDate`。停用的日期本身就能說明限制，無需訊息。
- 在較窄的欄位中使用 `MonthCount="One"`，以免第二個月被截掉。

**避免**

- 用選擇器來選取單一日期。範圍高亮與預設選項會增加單一日期並不需要的步驟。
- 期望面板開啟時 `SelectedRange` 會跟隨鍵盤或指標變動。數值只在套用時才會變更，因此對每個選取的日期作出反應的程式碼永遠不會執行；應改為訂閱 `SelectedRangeChanged`。
- 在在地化產品中依賴預設的英文文字。請同時設定 `Placeholder`、`ApplyText`、`CancelText` 以及兩種天數格式，使觸發器與頁尾使用同一種語言。
- 在包含大量列的可捲動清單中使用範圍選擇器。每一列都會開啟自己的面板，難以瀏覽；應把篩選移到清單的標頭。

### 常見問題

**我選了新的日期，為什麼觸發器仍顯示舊的範圍？** 在套用之前，選取都只是草稿。按下「套用」即可提交；取消、Escape 或面板外的按下會捨棄草稿，並保留先前的 `SelectedRange`。

**可以從程式碼開啟面板嗎？** 可以。把 `IsDropDownOpen` 設為 `True`，面板會開啟，焦點位於已提交的開始日，與用鍵盤開啟時一致。把它設回 `False` 即可關閉面板，而不套用草稿。

**如果第二天早於第一天會怎樣？** 選擇器會對兩天排序，範圍從較早的日期到較晚的日期。在程式碼中同樣如此：`DateRange.Ordered(a, b)` 會依順序回傳範圍。

**範圍一定有結束日嗎？** `DateRange` 總是包含兩天，且兩端都計入。如果使用者只選了開始日就套用，範圍就是那一天。若要求必須選兩天，請在檢視模型中檢查 `range.Days > 1`。

**如何計算住宿的晚數？** `Days` 會計入兩端，因此從 3 月 3 日到 3 月 5 日的入住有三天、兩晚。若訂房依晚計費，晚數為 `range.Days - 1`。

## API 參考

<!-- api: DateRangePicker -->

<!-- api: DateRange -->

| 配套類型 | 成員 | 摘要 |
|---|---|---|
| `DateRangePreset` | `DateRangePreset(string label, Func<DateOnly, DateRange> range)` | 以標籤與「今天」的函式建立預設選項 |
| `DateRangePreset` | `Label` | 按鈕文字，例如 `Last 7 days` |
| `DateRangePreset` | `Range` | 給定「今天」時的範圍 |
| `DateRangePresets` | `Default` | Arc 的八個預設選項，從今天到年初至今 |

## 無障礙

- 觸發器會公開其展開狀態，其名稱包含目前的範圍。`Label` 是欄位名稱。
- 每個月份都是以其標題命名的格線。範圍內的日期會被公開為已選取，今天會被標記。每個日期都以完整的長日期命名。
- 在某一天上按 Enter 或 Space 會先選取開始日，再選取結束日。選定開始日之後，方向鍵會延伸範圍預覽。
- 預設選項清單用 ArrowUp 與 ArrowDown 移動。精簡版面中，預設選項列用 ArrowLeft 與 ArrowRight 移動。
- 取消、Escape 以及指標在面板外的按下都會捨棄草稿，並把焦點交還給觸發器。套用會提交草稿，並透過禮貌型即時區域播報開始日、範圍與天數。
- 導覽按鈕由 `PreviousMonthLabel` 與 `NextMonthLabel` 命名，因此請以頁面所用的語言設定它們。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| `Enter` / `Space`（在觸發器上） | 開啟面板，並把焦點放在已提交範圍的開始日，或今天 |
| `Enter` / `Space`（在某一天上） | 第一次按下選取開始日；第二次按下選取結束日並產生草稿 |
| `Left` / `Right` | 把焦點移動一天；已選取開始日時，預覽會延伸到該日 |
| `Up` / `Down` | 把焦點移動一週 |
| `Home` / `End` | 移到所在週的第一天或最後一天，週的起始由 `FirstDayOfWeek` 決定 |
| `PageUp` / `PageDown` | 把焦點移動一個月；按住 `Shift` 則移動一年 |
| 在預設選項上的 `Up` / `Down`（精簡版面中為 `Left` / `Right`） | 在預設選項之間移動 |
| 在預設選項、取消或套用上的 `Enter` / `Space` | 啟用取得焦點的按鈕 |
| `Tab` / `Shift+Tab` | 在面板內移動；離開面板會關閉它並捨棄草稿 |
| `Escape` | 關閉面板、捨棄草稿，並把焦點交還給觸發器 |

鍵盤焦點永遠不會落在停用的日期上：方向鍵與翻頁鍵會在 `MinDate` 與 `MaxDate` 處停止。

## 動態效果

- 觸發器表面會展開為面板，其圓角與尺寸同時變化。套用之後，表面縮回觸發器，範圍標籤飛回原位。
- 範圍高亮會逐日延伸，兩端的端帽滑動到新的日期。作用中的預設選項會在各選項之間滑動。
- 月份朝行進方向滑入，並在身後淡出。標籤中的詞朝時間的方向捲動。
- 減少動態效果時，表面尺寸直接跳變，並省去共享元素與滑動。內容在 0.08 到 0.12 秒內淡入淡出。關閉動態效果時直接到達最終狀態。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 面板開啟 | 觸發器以彈簧動畫展開為面板，耗時 0.48 s | 尺寸直接跳變；內容在 0.08 到 0.12 秒內淡入 | 立即完成 |
| 套用後面板關閉 | 面板在 0.38 s 內縮回觸發器，標籤飛回原位 | 尺寸直接跳變；摘要淡出 | 立即完成 |
| 月份變化 | 新月份從行進方向滑入，耗時 0.4 s | 內容在 0.08 到 0.12 秒內交叉淡化 | 立即完成 |
| 觸發器的值變化 | 文字朝時間的方向捲動 | 內容在 0.08 到 0.12 秒內交叉淡化 | 立即完成 |

## 相關元件

- `DatePicker`：表單欄位中的單一日期。
- `Calendar`：需要停留在頁面上的月曆。
- `TimePicker`：一天中的時刻，當日期範圍需要時間時使用。

| 元件 | 適用情境 |
|---|---|
| `DatePicker` | 值是一天，例如到期日或生日 |
| `Calendar` | 月份必須保持可見以便掃視，例如可用性 |
| `TimePicker` | 值是一天中的時刻，例如班次開始 |
| `Select` | 選項是固定的短清單，只有命名期間，沒有自訂範圍 |
| `Field` + `FieldFrame` | 你要自建觸發器，並希望它與標籤和訊息列保持一致 |
