---
title: 分段控制項
summary: 在一組相關的檢視之間切換。
source-hash: 3c08999d73b0
---

<Demo name="ViewSwitcher" />

## 何時使用

分段控制項是一排兩到五個簡短選項，其中恰好一項處於啟用狀態，選取某一項會立即切換檢視。它位於頁面或面板的頁首中，位於其所控制的內容上方，並讓選取始終可見：使用者總能看到每個選項，以及目前正在顯示哪一個。當選項是同一份資料的不同檢視、而不是獨立的頁面時使用它，這樣內容會原地切換，控制項也保持不動。

- **分析儀表板中的時間範圍**：日、週、月和季會切換營收圖表及其合計數。圖表會依所選範圍重繪，控制項在過程中持續顯示目前的範圍。
- **事件或工單清單中的狀態篩選**：全部、未解決和已解決會縮小同一清單的範圍。附加內容顯示未解決項目的數量，使用者在選取之前就能看到每個篩選項的規模。
- **資料表格中的版面或密度切換**：緊湊與舒適會改變畫面上既有格線的列高，這是一項立即生效的設定。
- **共用工作區中的檢視模式**：看板、清單和行事曆以三種排列方式呈現同一批工作，目前的排列方式以選取的分段保持可見。
- **管理主控台中的環境或帳號切換**：正式環境、預備環境與沙箱會改變頁面讀取的資料。選取的分段說明了頁面上每個數字所依據的情境。

## 何時不用

- **選項需要說明、垂直版面配置或較長的標籤**：用 `RadioGroup`，它為每個選項提供一列，可容納說明並自動換行。
- **選項需要在卡片上顯示價格、中繼資訊或圖示**：用 `RadioCards`，它把每個選項排成可選取的卡片。
- **單一的開或關設定**：用 `Switch`。只有兩個分段的分段控制項會被讀成檢視之間的選擇，而不是一項設定。
- **選項很多，或選項放不進一排短列**：用 `Select`，它能把長清單放在一個欄位中。
- **在各自帶有標籤頁的整塊內容面板之間切換**：用 `Tabs`，它是為內容區域及其導覽設計的，而不是為一排檢視模式設計的。

## 用法

每個 `SegmentedOption` 接受一個值和一個標籤。控制項透過 `SelectedValue` 選取值對應的分段。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:SegmentedControl AutomationProperties.Name="範圍" SelectedValue="week">
        <arc:SegmentedOption Value="day" Label="日" />
        <arc:SegmentedOption Value="week" Label="週" />
        <arc:SegmentedOption Value="month" Label="月" />
    </arc:SegmentedControl>
</UserControl>
```

`SegmentedControl` 衍生自 Avalonia 的 `SelectingItemsControl`，因此 `SelectedIndex`、`SelectedItem`、`SelectedValue` 和 `SelectionChanged` 的行為與任何 Avalonia 選取類控制項一致。控制項在此之上增加了在分段之間滑動的藥丸形醒目提示、方向鍵導覽模型、溢出捲動以及 `ItemIntent` 事件。

### 結構

1. **外框**：1 px 邊框，控制項圓角，表面淡化色背景。分段在其內部捲動，因此分段溢出時外框形狀保持不變。
2. **軌道**：四周各內縮 3 px，分段之間相距 2 px。
3. **選取藥丸**：一個圓角圖形，表面色背景，帶 1 px 邊框和靜止陰影。它位於標籤之下，並移動到選取的分段。
4. **分段**：至少為 Arc 小型控制項高度（預設主題中為 36 px），左右各 12 px 內距，圓角 14 px。分段從不縮小，標籤也從不換行。
5. **標籤**（`Label`）：小型字級，中等字重。使用淡化文字顏色；分段被懸停、按下或選取時，改為前景色。
6. **附加內容**（`SegmentedOption.Accessory`）：標籤之後的選用內容，與標籤間隔 6 px。`Accessory` 為空時隱藏。
7. **焦點環**：分段取得焦點時在其內部繪製 2 px 的環。捲動區域會裁切繪製在外部的外框線，因此焦點環保持在分段之內。

使用預設參數且無附加內容時，控制項高 44 px：36 px 的分段，上下各 3 px 的軌道內縮，以及 1 px 的外框。

### 選擇變體

頁面上的每次檢視切換都使用控制項的預設參數，這樣所有切換看起來一致。控制項沒有尺寸屬性：其高度由分段與軌道決定，字型跟隨 Arc 本文字級。規劃分段數量時要參照頁首的寬度。兩到五個簡短分段可以舒適地放進頁首；六個或更多標籤可能使一排超出頁首寬度，此時 `Select` 更適合承載選擇。

把控制項放在它所控制內容的頂部，與頁面起始端對齊。控制項預設靠左對齊，只有當它是置中頁首中的唯一元素時，才設定 `HorizontalAlignment="Center"`。

### 從程式碼設定項目

將 `ItemsSource` 繫結到選項清單。控制項也接受純字串，`SelectedIndex` 可依位置選取分段。

```csharp
var sections = new SegmentedControl
{
    MaxWidth = 300,
    ItemsSource = new[] { "總覽", "活動", "部署", "分析", "記錄", "設定" },
    SelectedIndex = 0
};
```

純字串本身就是它的值，因此 `SelectedValue` 會傳回字串本身。當值與標籤不同時使用 `SegmentedOption`，例如值是列舉或 ID，而標籤是給人閱讀的文字。

### 附加內容

`Accessory` 是顯示在標籤之後的內容，例如數量。它接受任意內容，因此數量可以是放在小型徽章中的 `TextBlock`。

<Demo name="AccessoryAndIntent" />

```xml
<arc:SegmentedControl SelectedValue="all">
    <arc:SegmentedOption Value="all" Label="全部" />
    <arc:SegmentedOption Value="unread" Label="未讀">
        <arc:SegmentedOption.Accessory>
            <TextBlock Text="3" />
        </arc:SegmentedOption.Accessory>
    </arc:SegmentedOption>
    <arc:SegmentedOption Value="archived" Label="已封存" IsDisabled="True" />
</arc:SegmentedControl>
```

附加內容應只放一個數字或一個簡短的狀態標記。附加內容位於分段之內，因此過長的標籤或過寬的徽章會讓分段變寬，並擠壓相鄰的分段。資料變動時更新附加內容中的數字即可，無需重建分段。

### 繫結到檢視模型

使用 CommunityToolkit.Mvvm 時，檢視模型保存選項清單和選取的值，控制項負責顯示。`SelectedValue` 是雙向的，因此檢視模型可以讀取滑鼠或鍵盤所做的選擇。

```csharp
public partial class ActivityViewModel : ObservableObject
{
    public ObservableCollection<SegmentedOption> Ranges { get; } = new()
    {
        new SegmentedOption("day", "日"),
        new SegmentedOption("week", "週"),
        new SegmentedOption("month", "月")
    };

    [ObservableProperty]
    private string? _selectedRange = "week";
}
```

```xml
<arc:SegmentedControl ItemsSource="{Binding Ranges}"
                      SelectedValue="{Binding SelectedRange}"
                      AutomationProperties.Name="範圍" />
```

- `SelectedValue` 是雙向的。將它繫結到保存目前檢視的屬性，由檢視模型根據該值決定檢視顯示什麼。
- `ItemsSource` 接受 `ObservableCollection<SegmentedOption>`，因此頁面開啟期間選項也可以變動。
- 當變更必須載入資料時使用 `SelectionChanged`，並把載入邏輯放在命令或服務中，而不是控制項中。
- `AutomationProperties.Name` 為群組命名。請在 XAML 中設定它，因為控制項本身沒有標題。

### 意圖事件

當滑鼠指標或焦點到達某個分段時，會在該分段被選取之前引發 `ItemIntent`。用它做預覽，例如提示。不要用它來切換檢視。

```csharp
segmented.ItemIntent += (_, e) => hint.Text = $"預覽：{e.Value}";
segmented.SelectionChanged += (_, _) => ShowView(segmented.SelectedValue);
```

事件攜帶分段的 `Item` 和 `Value`。滑鼠進入分段會引發它，鍵盤焦點移動也會引發。觸控與觸控筆在懸停時不會引發它，但點按時會選取分段，隨後引發 `SelectionChanged`。當載入耗時較長時，可在 `ItemIntent` 中預先載入分段的內容，使檢視在選取之後無需等待即可出現。

### 溢出

當選項比控制項更寬時，選項列會在控制項內部捲動。捲軸隱藏，每個仍有更多內容的邊緣會顯示 20 px 的淡出效果。選取的分段始終被完整捲動到可見範圍內，其兩側保留 20 px 空間，從而避開淡出區域。

<Demo name="Overflow" />

溢出讓控制項在窄面板中仍可使用，但會把部分選項隱藏起來。請選擇分段數量，使一排能放進頁面所支援的最寬版面配置。分段在畫面之外時，使用者可能不知道它的存在，因此較長的選項組請使用 `Select`。

### 範例：事件狀態篩選

支付平台事件清單中的狀態篩選。三個分段都是簡短的名詞，「Open」分段帶有數量，使用者在選取之前就知道每個選項包含多少筆記錄。控制項下方的清單顯示篩選後的列，標題說明清單涵蓋的範圍。

![事件清單中的狀態篩選，選取 Open 分段並顯示數量 2](images/IncidentFilter.png)

```xml
<StackPanel Width="680" Spacing="16">
    <StackPanel Spacing="4">
        <TextBlock Text="Incidents"
                   FontSize="18"
                   FontWeight="SemiBold"
                   Foreground="{arc:ArcDesignTokenResource Foreground}" />
        <TextBlock Text="Payments API, last 7 days"
                   FontSize="13"
                   Foreground="{arc:ArcDesignTokenResource TextMuted}" />
    </StackPanel>
    <arc:SegmentedControl AutomationProperties.Name="Incident status" SelectedValue="open">
        <arc:SegmentedOption Value="all" Label="All" />
        <arc:SegmentedOption Value="open" Label="Open">
            <arc:SegmentedOption.Accessory>
                <Border Padding="6,0"
                        CornerRadius="999"
                        VerticalAlignment="Center"
                        Background="{arc:ArcDesignTokenResource Surface}">
                    <TextBlock Text="2"
                               FontSize="11"
                               Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
                </Border>
            </arc:SegmentedOption.Accessory>
        </arc:SegmentedOption>
        <arc:SegmentedOption Value="resolved" Label="Resolved" />
    </arc:SegmentedControl>
    <StackPanel Spacing="8">
        <Border Padding="16,12"
                BorderBrush="{arc:ArcDesignTokenResource Border}"
                BorderThickness="1"
                CornerRadius="{arc:ArcDesignTokenResource RadiusControl}">
            <Grid ColumnDefinitions="*,Auto">
                <StackPanel Spacing="2">
                    <TextBlock Text="Checkout latency above 900 ms"
                               FontWeight="Medium"
                               Foreground="{arc:ArcDesignTokenResource Foreground}" />
                    <TextBlock Text="inc_2026_0412 · Sev 2 · Started 20 October 2026, 09:14 UTC"
                               FontSize="12"
                               Foreground="{arc:ArcDesignTokenResource TextMuted}" />
                </StackPanel>
                <TextBlock Grid.Column="1"
                           Text="Investigating"
                           FontSize="12"
                           VerticalAlignment="Center"
                           Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
            </Grid>
        </Border>
        <Border Padding="16,12"
                BorderBrush="{arc:ArcDesignTokenResource Border}"
                BorderThickness="1"
                CornerRadius="{arc:ArcDesignTokenResource RadiusControl}">
            <Grid ColumnDefinitions="*,Auto">
                <StackPanel Spacing="2">
                    <TextBlock Text="Webhook deliveries delayed in the EU region"
                               FontWeight="Medium"
                               Foreground="{arc:ArcDesignTokenResource Foreground}" />
                    <TextBlock Text="inc_2026_0415 · Sev 3 · Started 21 October 2026, 06:40 UTC"
                               FontSize="12"
                               Foreground="{arc:ArcDesignTokenResource TextMuted}" />
                </StackPanel>
                <TextBlock Grid.Column="1"
                           Text="Monitoring"
                           FontSize="12"
                           VerticalAlignment="Center"
                           Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
            </Grid>
        </Border>
    </StackPanel>
</StackPanel>
```

### 文案規範

- **標籤**：一到兩個詞，說明檢視或狀態，例如「日」「未解決」「緊湊」。分段從不換行，因此過長的標籤會加寬整個控制項。使用句首大寫。
- **附加內容**：以數字表示的數量，或一個簡短標記。不要在附加內容中放一句話。
- **群組名稱**：`AutomationProperties.Name` 為輔助技術提供群組名稱。請寫成名詞，例如「範圍」「事件狀態」「檢視」。
- **分段順序**：把跨度最短或最常用的檢視放在最前，並且在使用同一控制項的每個頁面上保持相同的順序。

| 不要寫 | 改為 |
|---|---|
| 顯示最近一天的資料 | 日 |
| 未解決的問題（共 12 個） | Open，附加內容為 12 |
| 點選這裡查看已解決的項目 | 已解決 |
| 檢視模式 | 檢視 |

### 最佳做法

**建議**

- 讓每個分段都是同一內容的檢視，使切換改變的是資料而不是頁面結構。
- 把選取的值保存在檢視模型中，並據此更新內容，這樣重新載入之後控制項與內容不會不一致。
- 同一選擇在每個頁面上使用相同的順序和標籤，讓使用者只需學習一次。
- 當頁首中沒有可見標題說明控制項的選擇內容時，用 `AutomationProperties.Name` 為群組命名。

**避免**

- 用控制項在頁面之間導覽。頁面切換應屬於導覽，而分段控制項只在目前的頁面內保持選取。
- 在 `ItemIntent` 中執行開銷大的工作。它不做節流，因此指標掃過一排時，會為經過的每個分段都引發一次。
- 無理由地把停用的分段放在一排中間。停用分段仍然可見，使用者需要知道它為何不能被選取。
- 同一控制項中的標籤長短不一。長度相同的標籤能讓藥丸寬度保持均勻，也更易於掃讀。

### 常見問題

**為什麼控制項沒有填滿它的容器？** 它靠左對齊，並依分段大小決定尺寸。如需固定寬度，請設定 `Width`；如需在頁首中置中，請設定 `HorizontalAlignment="Center"`。當分段放不進寬度時，該列會捲動。

**如何在程式碼中選取分段？** 依值設定 `SelectedValue`，或依位置設定 `SelectedIndex`。分段溢出時，選取的分段會在被選取時捲動到可見範圍內。

**`ItemIntent` 會在滑鼠點選之前觸發嗎？** 會。滑鼠進入分段時就會引發 `ItemIntent`，因此預覽發生在點選選取分段並引發 `SelectionChanged` 之前。觸控輸入在懸停時不會引發它。

**能繫結純字串清單嗎？** 可以。`ItemsSource` 接受字串，`SelectedValue` 傳回字串本身。當值必須與文字不同時，例如列舉或 ID，請使用 `SegmentedOption`。

## API 參考

<!-- api: SegmentedControl -->
<!-- api: SegmentedOption -->

## 無障礙

- `AutomationProperties.Name` 為群組命名。對於沒有可見標題的控制項，請為其設定該屬性。
- 每個分段都是一個按鈕，會回報其按下狀態。滑動的藥丸形醒目提示對輔助技術隱藏。
- Tab 鍵從選取的分段進入控制項；若未選取任何分段，則從第一個分段進入。再按一次 Tab 鍵即離開。
- 方向鍵選取並聚焦下一個或上一個分段，並循環切換。Home 和 End 選取第一個和最後一個分段。已停用的分段會被略過。
- 鍵盤焦點會在分段內部繪製 2px 的外框線，因此捲動區域不會裁切它。滑鼠焦點不會繪製。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| `Tab` | 從選取的分段進入控制項；若未選取任何分段，則從第一個分段進入；再按一次 `Tab` 即離開 |
| `Right` / `Down` | 選取並聚焦下一個分段，從最後一個循環到第一個 |
| `Left` / `Up` | 選取並聚焦上一個分段，從第一個循環到最後一個 |
| `Home` / `End` | 選取並聚焦第一個或最後一個分段 |
| `Space` / `Enter` | 選取已聚焦的分段 |

焦點每到達一個分段都會引發 `ItemIntent`。方向鍵隨後選取該分段，因此預覽與切換都來自同一次移動。

## 動態效果

- 一個藥丸形醒目提示以形變彈簧滑動到選取的分段之下。醒目提示位於標籤之下，並隨標籤一同捲動。
- 分段本身不會縮放或改變字重，只有醒目提示移動，標籤顏色快速變化。
- 分段溢出時，該列會以短促的緩動捲動，以保持選取的分段可見。首次載入控制項後的定位是即時的。
- 減少動態效果時，醒目提示立即移動，並取消顏色過渡。關閉動態效果時效果相同。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 選取項移動 | 藥丸形醒目提示以形變彈簧滑動 | 醒目提示立即移動 | 醒目提示立即移動 |
| 標籤顏色（懸停、按下、選取） | 在快速緩動上交叉淡變 | 立即完成 | 立即完成 |
| 溢出時捲動到選取的分段 | 在首次版面配置之後沿標準緩動平滑捲動 | 直接跳轉 | 直接跳轉 |
| 邊緣淡出出現或消失 | 跟隨捲動位置 | 跟隨捲動位置 | 跟隨捲動位置 |
| 載入後的首次定位 | 立即 | 立即 | 立即 |

## 相關元件

- `RadioGroup`：適用於需要說明或垂直版面配置的選項。
- `BillingToggle`：用於按月或按年切換計費週期，並附帶節省說明。
- `Switch`：用於立即生效的單一設定。

| 元件 | 適用情境 |
|---|---|
| `RadioGroup` | 每個選項都需要說明，或垂直清單更適合表單 |
| `RadioCards` | 選項需要在卡片上顯示價格、中繼資訊或圖示 |
| `Tabs` | 每個選項都會開啟一整塊帶有自身導覽的內容面板 |
| `Select` | 選項很多，或放不進一排短列 |
| `Switch` | 一項設定立即生效，且沒有其他取值 |
| `BillingToggle` | 選擇的是按月或按年的計費週期，並附帶節省說明 |
