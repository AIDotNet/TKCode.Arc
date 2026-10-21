---
title: 活動熱圖
summary: 一眼看清整年的活動，每天一個方格。
source-hash: 5100b63c7993
---

<Demo name="Contributions" />

## 何時使用

`ActivityHeatmap` 展示一個計數在各天中的分布，每天一個方格，以週為欄。它回答的是節奏與連續性的問題：團隊是否每週都在發布、八月的沉寂持續了多久、哪些工作日比較忙。它適合放在儀表板面板或個人資料頁中，此時整個週期比任何單一數值都更重要。

- **開發者主控台中的部署與發佈頻率**：平台團隊按天查看最近一季的正式環境部署。連續與空檔是重點，沒有部署的那一天應被視為空檔，而不是缺漏的資料。
- **SaaS 管理儀表板上的參與度**：某個工作區每天的活躍使用者數或完成的入門步驟數，與該帳號的方案放在一起，讓團隊看出全年的使用量是否成長。
- **消費類應用程式中的運動、讀書或學習**：個人資料頁上每天一個方格，連續紀錄一目了然。圖例不需要另外的圖表，就能說明五個等級。
- **維運工具中的事件或客服單量**：一季內每天新開的客服單數。忙碌的一週會很醒目，選取的那一天還能開啟當天的客服單清單。
- **內部工具中的活動稽核**：合規審查中每天的登入或核准次數，審查人員先選取某一天，讀出其計數，再開啟詳細檢視。

## 何時不用

- **精確比較不同天或不同時段**：用 `BarChart`，每根長條的長度可以被精確比較。
- **為輸入選擇日期**：用 `Calendar` 選擇預訂、到期日或報表區間等由人挑選的日期。`ActivityHeatmap` 是為了檢視而選取某一天，而不是用於資料輸入。
- **某個值隨時間的趨勢**：當連續值的形態很重要（例如營收或延遲），並且每個點的確切值很關鍵時，用 `LineChart`。
- **單一數字旁邊的精簡趨勢**：用 `Sparkline`，它小到可以放進表格列或 KPI 卡片中，呈現的是數值的方向，而不是每一天。
- **不足數週的期間**：只有寥寥幾天時，方格太少，無法呈現規律，帶標籤數值的 `BarChart` 比較容易閱讀。
- **並排呈現多年資料**：跨兩年的網格寬度會加倍，並需要橫向捲動。請每年使用一個 `ActivityHeatmap`，或用 `BarChart` 比較各年的總計。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:ActivityHeatmap Label="2025 年的貢獻"
                         Period="2025"
                         ItemsSource="{Binding Days}" />
</UserControl>
```

`ActivityHeatmap` 衍生自 Avalonia 的 `Control`，而不是項目控制項（items control）。它自行繪製摘要列、月份與星期標籤、日期網格、工具提示和圖例，因此一個控制項就包含整個日曆，不需要任何項目範本。由於網格是一個可聚焦的控制項，並以算術方式進行命中測試，它處理一整年約 370 個方格時，不必為每個方格建立一個視覺物件。

### 結構

1. **摘要列**：總計數以動畫數字顯示，接著是單位名詞，再接「在」和週期。總數以 14 px 的內文字級呈現，資料變動時捲動到新值。`Period` 為空時會隱藏「在」和週期。
2. **動作**（`Actions`）：任意控制項，放在摘要列的右端。當摘要與動作無法並排放下時，動作會移到摘要下方自己的一列。
3. **星期欄**：網格左側 30 px 寬的星期標籤欄。網格較窄需要橫向捲動時，它會留在原位。
4. **月份標籤**：每個月第一週上方的月份縮寫，依 `Culture` 格式化。如果下個月在三週之內開始，首個不完整的月份會失去標籤，因此標籤不會相互重疊。
5. **日期網格**：每個日曆日一個方格，以週為欄。方格之間相距 3 px。方格大小隨容器寬度變化，維持在 9 px 到 15 px 之間。資料範圍之外的日期為空白儲存格。
6. **工具提示**：整個網格共用一個氣泡，顯示指標或鍵盤焦點所在那一天的計數與日期。
7. **圖例**：一段說明文字，指出處於醒目等級的天數，然後是「較少」、五個色塊（等級 0 到 4）和「較多」。懸停或聚焦某個色塊會在網格上預覽其等級，點擊則固定該等級，醒目顯示該等級的方格。再次點擊即可取消固定。

頭部、網格與圖例之間相隔 16 px。五種色調由強調色推導：等級 0 是前景色以 6.5% 混入表面色，等級 1、2、3 分別是強調色以 26%、50%、76% 混入該底色。等級 4 為完整的強調色。

### 切換年份與控制項

切換年份時，請同時取代 `ItemsSource`、`Period` 和 `Label`，使摘要、網格以及網格的無障礙名稱保持一致。`Actions` 把控制項放在摘要旁，例如年份切換。網格以掃過式的重新著色來更新，而不是整個重繪，因此切換年份時頁面保持平穩。

```xml
<arc:ActivityHeatmap Label="2025 年的貢獻" Period="2025" ItemsSource="{Binding Days}">
    <arc:ActivityHeatmap.Actions>
        <StackPanel Orientation="Horizontal" Spacing="4">
            <arc:Button Content="2024" Variant="Ghost" Size="Small" Command="{Binding ShowPrevious}" />
            <arc:Button Content="2025" Variant="Ghost" Size="Small" Command="{Binding ShowCurrent}" />
        </StackPanel>
    </arc:ActivityHeatmap.Actions>
</arc:ActivityHeatmap>
```

網格一律跨越資料中最早到最晚的日期。檢視單一年份時，只傳入一個曆年的資料。如果資料包含兩年，網格寬度會加倍並需要橫向捲動，這通常不是讀者想要的效果，因此應依年份拆分資料。

### 單位與週

`UnitOne` 與 `UnitOther` 設定計數的單數與複數名詞。只有當總數恰好為 1 時才使用單數，因此「1 workout」和「2 workouts」都讀起來正確。`FirstDayOfWeek` 設定每個週欄的第一列，預設為星期日；工作行事曆可以選擇星期一。`Culture` 用於格式化日期與數字，預設為 en-US。

<Demo name="CustomUnit" />

```xml
<arc:ActivityHeatmap Label="最近六個月的運動"
                     Period="最近六個月"
                     UnitOne="workout"
                     UnitOther="workouts"
                     FirstDayOfWeek="Monday"
                     MaxWidth="520"
                     ItemsSource="{Binding Workouts}" />
```

`MaxWidth` 讓六個月的網格不會在寬版頁面上被拉伸。方格最大不超過 15 px，因此容器很寬時，右側會留出空白。

### 門檻

`Thresholds` 保存等級一到三的上限值；只使用前三個值。計數為零時為等級 0，無色調；計數不超過第一個上限為等級 1，不超過第二個為等級 2，不超過第三個為等級 3，超過第三個上限則為等級 4。

未設定 `Thresholds` 時，控制項會依資料範圍內最忙的那一天推導上限：第一個上限為該天計數的四分之一（無條件進位，至少為 1），第二個為其一半（至少為 2），第三個為其四分之三（至少為 3）。因此，某年有一天特別突出、部署了 31 次，上限就會設為 8、16 和 24，而大多數一般的日子都落在等級 1。當讀者要比較不同年份或團隊時，應設定固定上限，因為推導出的上限會隨每組資料變動。

```csharp
workouts.Thresholds = [1, 2, 3];
```

設定 `Thresholds` 也會固定圖例的範圍。`EffectiveThresholds` 會傳回正在使用的三個上限。

### 選取某一天

`SelectedDate` 是選取的日期，並且是雙向的。選取環會滑動到該日期，並在首次落在該日期時淡入。`DateSelected` 回報透過點擊、Enter 或 Space 選取的日期。其引數包含 `Date`、該日的 `Count`，以及從 0（無）到 4（最忙）的 `Level`，因此處理常式不必重新查詢資料，就能顯示摘要或開啟詳細檢視。

```csharp
heatmap.DateSelected += (_, e) => selected.Text =
    $"已選取 {e.Date:MMM d, yyyy}：{e.Count} 次貢獻（等級 {e.Level}）";
```

在程式碼中設定 `SelectedDate` 會移動選取環，但不會觸發 `DateSelected`，因為該事件回報的是人所做的選擇。還原選取時（例如頁面重新開啟），使用該屬性；需要對選擇作出反應時，使用事件。

### 繫結到檢視模型

檢視模型擁有日期、週期和選取的日期。使用 CommunityToolkit.Mvvm 風格的屬性，並將熱圖繫結到它們。

```csharp
public sealed record DailyDeploys(DateOnly Date, double Deploys);

public interface IDeployHistory
{
    Task<IReadOnlyList<DailyDeploys>> DailyCountsAsync(int year);
}

public partial class ActivityViewModel : ObservableObject
{
    private readonly IDeployHistory _history;

    public ActivityViewModel(IDeployHistory history) => _history = history;

    [ObservableProperty]
    private ObservableCollection<ActivityDay> _days = [];

    [ObservableProperty]
    private string _periodLabel = "過去一年";

    [ObservableProperty]
    private DateOnly? _selectedDate;

    [ObservableProperty]
    private string _selectedSummary = "選取一天以查看其部署次數。";

    [RelayCommand]
    private async Task ShowYearAsync(string year)
    {
        var counts = await _history.DailyCountsAsync(int.Parse(year));
        Days = new ObservableCollection<ActivityDay>(
            counts.Select(c => new ActivityDay(c.Date, c.Deploys)));
        PeriodLabel = year;
    }

    partial void OnSelectedDateChanged(DateOnly? value)
    {
        SelectedSummary = value is { } date
            ? $"{date:MMM d, yyyy} 已選取。"
            : "選取一天以查看其部署次數。";
    }
}
```

```xml
<StackPanel Width="620" Spacing="16">
    <arc:ActivityHeatmap Label="正式環境部署"
                         Period="{Binding PeriodLabel}"
                         UnitOne="deploy"
                         UnitOther="deploys"
                         ItemsSource="{Binding Days}"
                         SelectedDate="{Binding SelectedDate, Mode=TwoWay}">
        <arc:ActivityHeatmap.Actions>
            <arc:Button Content="2025" Variant="Ghost" Size="Small"
                        Command="{Binding ShowYearCommand}" CommandParameter="2025" />
        </arc:ActivityHeatmap.Actions>
    </arc:ActivityHeatmap>
    <TextBlock Text="{Binding SelectedSummary}"
               Foreground="{arc:ArcDesignTokenResource TextMuted}" />
</StackPanel>
```

- `ItemsSource` 是單向的：熱圖讀取集合並重建網格。`ObservableCollection` 會被監看，因此對它的變更會立即反映出來。像命令那樣指派一個新集合，是重新整理整年資料最簡單的方法。
- `SelectedDate` 是雙向的。在網格上所做的選取會更新檢視模型，而檢視模型設定的值會移動選取環，但不會觸發 `DateSelected`。
- `Period` 與 `Label` 是一般屬性；請與 `ItemsSource` 在同一次更新中繫結，使摘要和網格的名稱永遠不會描述不同的資料。
- `DateSelected` 是事件，而不是命令。請在程式碼後置中附加它，讓處理常式讀取事件引數中的 `Date`、`Count` 和 `Level`，並把這些值複製到檢視模型自己的屬性中。

### 資料結構與效能

`ActivityDay` 包含一個 `DateOnly` 和一個 `double` 型別的計數。控制項會在最早與最晚日期之間為每個日曆日建立一筆連續的紀錄，因此缺漏的日期視為零，不需要列資料。同一日期的兩筆資料會相加，負計數視為零。一年約 370 個方格，網格會一次繪製完成；跨多年的範圍會成倍增加欄數，並需要橫向捲動。

當資料來源不是 `ActivityDay` 時，可用 `DateSelector` 與 `CountSelector` 讀取每一項，或用 `DateMemberBinding` 與 `CountMemberBinding`。成員繫結會讀取 `DateOnly`、`DateTime` 或 `yyyy-MM-dd` 格式的字串。兩者同時設定時，`DateSelector` 優先於 `DateMemberBinding`。

```csharp
var heatmap = new ActivityHeatmap
{
    ItemsSource = rows,
    DateSelector = row => DateOnly.ParseExact(((Row)row).Day, "yyyy-MM-dd", CultureInfo.InvariantCulture),
    CountSelector = row => ((Row)row).Deploys
};
```

當日期或數字必須跟隨讀者的語言時，設定 `Culture`。總計會使用該文化特性下的 `#,0.##` 格式，因此小數計數最多保留兩位小數，較大的總數會使用當地的千分位分隔符號。

更新資料時，取代集合，或變更熱圖所監看之集合中的項目。日曆是對天數的彙總，因此應依資料變化的節奏安排重新整理，例如每次載入頁面或每小時一次，而不是每個事件都重新整理。

### 範例：平台團隊的部署頻率

平台團隊過去五週的部署紀錄，顯示在工程概覽頁上。標籤說明了度量與期間，單位名詞讀作「deploys」，每週從星期一開始，即團隊工作週的第一天。匯出按鈕作為動作放在摘要旁。週末大多比較安靜，因此不需要額外的標籤，也能看出工作日的節奏。

![過去五週正式環境部署的熱圖，週末較為安靜](images/DeployHeatmap.png)

```xml
<StackPanel Width="560" Spacing="16">
    <arc:ActivityHeatmap Label="Production deploys, last five weeks"
                         Period="the last five weeks"
                         UnitOne="deploy"
                         UnitOther="deploys"
                         FirstDayOfWeek="Monday">
        <arc:ActivityHeatmap.Items>
            <arc:ActivityDay Date="2026-09-14" Count="4" />
            <arc:ActivityDay Date="2026-09-15" Count="5" />
            <arc:ActivityDay Date="2026-09-16" Count="3" />
            <arc:ActivityDay Date="2026-09-17" Count="6" />
            <arc:ActivityDay Date="2026-09-18" Count="4" />
            <arc:ActivityDay Date="2026-09-19" Count="0" />
            <arc:ActivityDay Date="2026-09-20" Count="1" />
            <arc:ActivityDay Date="2026-09-21" Count="3" />
            <arc:ActivityDay Date="2026-09-22" Count="5" />
            <arc:ActivityDay Date="2026-09-23" Count="0" />
            <arc:ActivityDay Date="2026-09-24" Count="4" />
            <arc:ActivityDay Date="2026-09-25" Count="2" />
            <arc:ActivityDay Date="2026-09-26" Count="0" />
            <arc:ActivityDay Date="2026-09-27" Count="0" />
            <arc:ActivityDay Date="2026-09-28" Count="5" />
            <arc:ActivityDay Date="2026-09-29" Count="6" />
            <arc:ActivityDay Date="2026-09-30" Count="4" />
            <arc:ActivityDay Date="2026-10-01" Count="5" />
            <arc:ActivityDay Date="2026-10-02" Count="3" />
            <arc:ActivityDay Date="2026-10-03" Count="1" />
            <arc:ActivityDay Date="2026-10-04" Count="0" />
            <arc:ActivityDay Date="2026-10-05" Count="4" />
            <arc:ActivityDay Date="2026-10-06" Count="2" />
            <arc:ActivityDay Date="2026-10-07" Count="5" />
            <arc:ActivityDay Date="2026-10-08" Count="6" />
            <arc:ActivityDay Date="2026-10-09" Count="4" />
            <arc:ActivityDay Date="2026-10-10" Count="0" />
            <arc:ActivityDay Date="2026-10-11" Count="1" />
            <arc:ActivityDay Date="2026-10-12" Count="3" />
            <arc:ActivityDay Date="2026-10-13" Count="4" />
            <arc:ActivityDay Date="2026-10-14" Count="2" />
            <arc:ActivityDay Date="2026-10-15" Count="5" />
            <arc:ActivityDay Date="2026-10-16" Count="3" />
            <arc:ActivityDay Date="2026-10-17" Count="0" />
            <arc:ActivityDay Date="2026-10-18" Count="0" />
        </arc:ActivityHeatmap.Items>
        <arc:ActivityHeatmap.Actions>
            <arc:Button Content="Export" Variant="Ghost" Size="Small" />
        </arc:ActivityHeatmap.Actions>
    </arc:ActivityHeatmap>
</StackPanel>
```

### 文案規範

- **標籤**：用句首大寫的方式說明度量與期間，例如「正式環境部署，最近五週」。標籤是網格的無障礙名稱，因此即使沒有圖片也必須說得通。
- **期間**：用一個簡短片語補全「……中的貢獻」：「2025」「最近六個月」「2026 年第三季」。只有當標籤已經說明期間時才可省略。
- **單位**：將 `UnitOne` 與 `UnitOther` 設為人們描述該計數時使用的名詞，例如「部署」或「運動」。當計數有精確含義時，避免使用「項目」這類泛稱。
- **門檻**：設定門檻時，在說明文字或註記中加以描述，例如「顏色越深表示部署越多」，讓讀者知道色調是相對於上限而言的。
- **動作**：保持一到兩個小按鈕，使用簡短的動詞或年份，例如「匯出」或「2025」。

| 不要寫 | 改為 |
|---|---|
| 活動資料 | 正式環境部署，最近五週 |
| 2025 | 2025 年的貢獻 |
| 1 contributions | 1 contribution（名詞隨總數單複數變化） |
| 熱圖，內容 | 最近六個月的運動 |
| 點擊此處查看詳情 | 選取一天以查看其部署次數 |

### 最佳做法

**建議**

- 為每個熱圖設定 `Label` 和 `Period`。標籤是螢幕閱讀器為網格提供的唯一名稱，期間則補全摘要，讓讀者永遠看不到孤零零的總數。
- 每個檢視都傳入完整的曆年或固定範圍。從隨機日期開始的網格會讓兩年的資料難以比較。
- 比較不同期間或團隊時設定 `Thresholds`。推導出的上限會跟隨最忙的那一天，因此同樣的計數在某一年可能是等級 2，在另一年卻是等級 1。
- 將 `FirstDayOfWeek` 設為符合受眾行事曆的值：工作週用星期一，個人活動用星期日。
- 用 `DateSelected` 開啟某一天的詳細資料。該事件攜帶日期、計數和等級，處理常式不需要再次查找該日資料。

**避免**

- 用熱圖呈現精確數值。方格呈現的是色調而非數字，需要精確計數的讀者必須選取該日，或查看表格。
- 在一個熱圖中傳入多年資料。網格會變得很寬，讀者必須橫向捲動才能看到最近幾週。
- 為色調賦予好壞的含義。等級只表示某天的忙碌程度，單一的強調色並不是狀態訊號，因此應在標籤或說明文字中解釋其含義。
- 以比資料變化還短的計時器重新整理資料。總數和色調每天變化，頻繁更新只會讓頁面晃動而不增加資訊。
- 將熱圖放在狹窄的欄中。寬度不超過 440 px 時，圖例說明只顯示計數；方格達到 9 px 的最小值後，網格會從最新一週開始橫向捲動，因此應給它面板的全部寬度。

### 常見問題

**為什麼網格從一週的中間開始？** 網格從資料中最早的日期開始，並從 `FirstDayOfWeek` 開始填滿第一個週欄。該欄中開始日期之前的日子為空白，因此第一個方格可能位於任一列。

**為什麼我的大多數日子都是同一種淺色？** 預設的上限是最忙那一天的四分之一等分。一個異常值會拉高上限，因此大多數一般的日子都落在等級 1。請將 `Thresholds` 設為符合典型範圍的固定值。

**為什麼設定 `SelectedDate` 不會觸發我的處理常式？** `DateSelected` 只在透過點擊、Enter 或 Space 做出選擇時觸發。設定該屬性只會移動選取環，而不會引發事件，因此需要時請在 setter 或檢視模型中更新你自己的狀態。

**如何顯示哪些天處於某一等級？** 圖例已經提供了這項功能。懸停或聚焦某個色塊可預覽其等級，點擊色塊則將其固定。此時說明文字會統計該等級的天數，例如「96 天」；當圖例寬度超過 440 px 時，還會附上該等級的範圍，並且對應的方格會被突顯。

## API 參考

<!-- api: ActivityHeatmap -->

<!-- api: ActivityDay -->

## 無障礙

- `Label` 為網格命名，例如「2025 年的貢獻」。摘要以總數和期間補全名稱。
- 上下方向鍵每次移動一天，左右方向鍵每次移動一週。Home 與 End 移到第一天和最後一天，Enter 或 Space 選取目前聚焦的那一天。
- Escape 隱藏工具提示。單一工具提示在各天之間平滑滑動，不會為每個方格重複朗讀。
- 每一天都是一個有名稱的網格儲存格。這些儲存格作為圖形對輔助技術隱藏，資訊由摘要與網格結構承載。
- 總數會在狀態區域中朗讀，指標或焦點所在的那一天會以禮貌的方式朗讀。圖例顯示一段說明，指出處於醒目等級的天數，因此不會只靠顏色來辨識等級。
- 聚焦的那一天會被朗讀為其計數、星期和日期，例如「31 次貢獻，星期三，2025 年 12 月 31 日」。
- 摘要是整個控制項的說明文字，因此打開控制項描述的讀者會先聽到總數和期間。

### 鍵盤

網格作為一個 Tab 停駐點取得焦點。透過鍵盤取得焦點時，首先聚焦的是最新的那一天。

| 按鍵 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 將焦點移入網格並定位到最新的一天，然後移到圖例或下一個控制項 |
| `Up` / `Down` | 向前或向後移動一天 |
| `Left` / `Right` | 向前或向後移動一週 |
| `Home` / `End` | 移到範圍內的第一天或最後一天 |
| `Enter` / `Space` | 選取聚焦的那一天並引發 `DateSelected` |
| `Escape` | 隱藏工具提示 |

圖例是第二個 Tab 停駐點。`Left` 與 `Right` 在五個色塊之間移動並預覽其等級，`Home` 與 `End` 移到第一個和最後一個色塊，`Enter` 或 `Space` 固定一個等級，`Escape` 清除預覽。在已固定的等級上再次按下 `Enter` 或 `Space` 即可取消固定。`Escape` 清除預覽但保留固定。

## 動態效果

- 首次顯示時，方格會一次性波浪式浮現，總時長有上限，因此大型網格仍然很快。
- 新的年份或範圍會以對角掃過的方式重新著色，各方格依欄錯開時間。
- 工具提示以靈敏的彈簧在方格之間滑動，其文字也捲動到新的那一天。
- 總數以平滑彈簧計數到新值。
- 減少動態效果會去掉波浪、掃過和滑動，改用 160 ms 的短淡入。關閉動態效果則直接跳到最終狀態。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 首次顯示 | 方格一次性波浪式浮現，總時長有上限 | 160 ms 的短淡入 | 立即完成 |
| 新範圍或年份 | 對角掃過，依欄錯開 | 160 ms 的短淡入 | 立即完成 |
| 工具提示移到另一天 | 以靈敏彈簧在方格之間滑動 | 160 ms 的短淡入 | 立即完成 |

掃過效果只保留給範圍變化，例如切換年份。波浪只在首次顯示時執行一次，因此重新載入同一年份的頁面不會從中獲得任何效益。

## 相關元件

- `BarChart` 用於不同期間之間的精確比較。
- `Calendar` 用於選擇日期。
- `LineChart` 用於呈現某個值隨時間的趨勢。
- `Sparkline` 用於在數值旁邊呈現精簡的趨勢。

| 元件 | 適用情境 |
|---|---|
| `BarChart` | 讀者需要比較不同天、週或類別之間的精確值 |
| `Calendar` | 人需要為預訂或到期日等輸入選擇日期 |
| `LineChart` | 連續值隨時間的變化比單日的節奏更重要 |
| `Sparkline` | 在表格列或 KPI 卡片中，數字旁邊需要一個小趨勢 |
| `DatePicker` | 表單欄位需要一個單獨的日期，而不是活動的檢視 |
