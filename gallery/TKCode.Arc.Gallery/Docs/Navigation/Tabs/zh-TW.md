---
title: 標籤頁
summary: 在同一脈絡中切換相關內容。
source-hash: d1602006c5ed
---

<Demo name="Default" />

## 何時使用

`Tabs` 把一個物件的畫面拆分為並列的檢視，例如客戶紀錄上的概覽、活動與設定。使用者每次選擇一個檢視，每個檢視在標籤列下方都有自己的面板。切換檢視時標籤列保持不動，因此使用者在同一筆紀錄、訂單或工作區內不會迷失方向。

- **CRM 中的客戶紀錄**：概覽、訂單、發票與備註都是同一個帳號的檢視。帳號標題位於標籤頁上方，選取的標籤決定面板中顯示哪個清單。
- **工作區或專案頁面**：概覽、活動與設定。各檢視彼此並列，因此沒有哪一個是步驟順序中的一環。
- **依狀態篩選同一份資料**：在同一份發票清單上使用「全部」「未結」「逾期」與「已付」標籤，每個標籤在標題中顯示其計數。
- **桌面工具中的大量檢視**：郵件用戶端的資料夾（收件匣、草稿、寄件備份、排程寄送），視窗較窄時帶有邊緣捲動按鈕。選取的資料夾一定會捲動到可見的位置。

## 何時不用

- **選取一個沒有面板的精簡值**，例如日、週或月的範圍：使用 `SegmentedControl`。選項本身沒有內容，標籤面板不會帶來任何價值。
- **依序閱讀的堆疊內容**，例如結帳時的條款：使用 `Accordion`，每個區段在原位展開，使用者可以同時閱讀多個區段。
- **翻閱結果的頁面**：使用 `Pagination`。標籤頁用於切換檢視，而不是翻閱同一個集合的頁面。
- **顯示頁面在階層結構中的位置**：使用 `Breadcrumb`。麵包屑指向上層路徑，而標籤頁彼此並列。
- **必須依序完成的步驟**，例如配送、付款與確認：使用 `Stepper`。在結帳時，使用者不能像切換標籤頁那樣跳過前面的步驟。

## 用法

項目是 `TabItem` 容器。標題是觸發器，內容是面板。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Tabs AutomationProperties.Name="工作區">
        <arc:TabItem Header="概覽">
            <TextBlock Text="本季營收成長 12%。" />
        </arc:TabItem>
        <arc:TabItem Header="活動">
            <TextBlock Text="Maya 在兩小時前合併了 Billing v2。" />
        </arc:TabItem>
        <arc:TabItem Header="設定">
            <CheckBox Content="電子郵件摘要" IsChecked="True" />
        </arc:TabItem>
        <arc:TabItem Header="已封存" IsEnabled="False" />
    </arc:Tabs>
</UserControl>
```

`Tabs` 衍生自 Avalonia 的 `SelectingItemsControl`，因此選取行為與任何可選取的清單一致：`SelectedIndex`、`SelectedItem` 與 `SelectionChanged` 的用法相同，控制項載入時會選取第一個標籤頁。標籤列本身不接收焦點，每個 `TabItem` 才接收。`TabItem` 衍生自 `HeaderedContentControl`，因此 `Header` 是它的觸發器，`Content` 是它的面板。停用的標籤頁會被方向鍵略過，也無法被選取。

### 結構

1. **標籤列**（`Tabs`）：次要表面色上的 1 px 邊框，顏色為邊框色，圓角與控制項一致。它會裁剪內容，並在外框內保留 4 px 的內距。
2. **選取膠囊**：表面色、1 px 邊框與靜止陰影。它位於選取的觸發器之後，並在各觸發器之間滑動。
3. **觸發器**（`TabItem.Header`）：至少 88 px 寬、36 px 高，水平內距 12 px，圓角 15 px。標籤使用小字級與中等字重，顏色為淡化文字色。懸停時變為主要文字色，選取的標籤頁使用強調色，停用的標籤頁降至 50% 不透明度。
4. **間距**：觸發器之間為 4 px。
5. **邊緣捲動按鈕**（`ScrollLeftLabel`、`ScrollRightLabel`）：寬 33 px，只有在清單寬於外框時才顯示，內含 17 px 的箭頭。某一側有隱藏的標籤頁時，該側按鈕才可用。
6. **邊緣漸隱**：有隱藏標籤頁的每一側都有 35 px 的漸隱效果。
7. **面板**：選取標籤頁的內容，位於清單下方 16 px 處，使用次要文字色、小字級，行高 19.6 px。
8. **焦點環**：獲得焦點的觸發器周圍有 2 px 的焦點環；面板獲得焦點時，面板周圍也有焦點環。

### 由資料驅動的標籤頁

設定 `ItemsSource`。`ItemTemplate` 負責渲染觸發器，`ContentTemplate` 負責渲染選取項目的面板。透過 `SelectedItem` 或 `SelectionChanged` 讀取選取結果。

<Demo name="Overflow" />

```xml
<arc:Tabs ItemsSource="{Binding Folders}"
          SelectedItem="{Binding CurrentFolder}"
          MaxWidth="460"
          HorizontalAlignment="Left">
    <arc:Tabs.ItemTemplate>
        <DataTemplate>
            <TextBlock Text="{Binding Name}" />
        </DataTemplate>
    </arc:Tabs.ItemTemplate>
    <arc:Tabs.ContentTemplate>
        <DataTemplate>
            <TextBlock Text="{Binding Summary}" />
        </DataTemplate>
    </arc:Tabs.ContentTemplate>
</arc:Tabs>
```

不是 `TabItem` 的資料項目會直接成為面板內容，並由 `ContentTemplate` 渲染。

### 繫結到檢視模型

客戶紀錄在檢視模型中保存其各個區段。標籤頁呈現這些區段，選取的區段是雙向屬性，檢視模型會為使用者打開的區段載入資料。使用 CommunityToolkit.Mvvm 時，檢視模型是一個 `ObservableObject`，帶有 `ObservableCollection<T>` 形式的區段集合，以及用於選取項目的 `[ObservableProperty]`。

```csharp
public sealed record CustomerSection(string Title, string Summary);

public partial class CustomerViewModel : ObservableObject
{
    public ObservableCollection<CustomerSection> Sections { get; } = new()
    {
        new("概覽", "帳號負責人 Maya Chen。商業版方案，2026 年 11 月 30 日續約。"),
        new("訂單", "最近 90 天內有 12 筆訂單。最近一筆下單於 2026 年 4 月 8 日。"),
        new("發票", "2 張未結發票，其中一張已逾期 6 天。"),
        new("備註", "2026 年 3 月 2 日詢問了增加 40 個席位的報價。")
    };

    [ObservableProperty]
    private CustomerSection? _selectedSection;

    partial void OnSelectedSectionChanged(CustomerSection? value)
    {
        // 載入使用者打開的區段的資料，而不是預先載入每個區段。
    }
}
```

```xml
<arc:Tabs ItemsSource="{Binding Sections}"
          SelectedItem="{Binding SelectedSection, Mode=TwoWay}"
          AutomationProperties.Name="客戶區段">
    <arc:Tabs.ItemTemplate>
        <DataTemplate>
            <TextBlock Text="{Binding Title}" />
        </DataTemplate>
    </arc:Tabs.ItemTemplate>
    <arc:Tabs.ContentTemplate>
        <DataTemplate>
            <TextBlock Text="{Binding Summary}" TextWrapping="Wrap" />
        </DataTemplate>
    </arc:Tabs.ContentTemplate>
</arc:Tabs>
```

`SelectedItem` 是應雙向繫結的值：它是使用者選取的區段，而檢視模型中的變更（例如從通知開啟「發票」區段）也會選取對應的標籤。當檢視本身必須對變更作出反應時（例如儲存上一個區段的捲動位置），請在程式碼後置中使用 `SelectionChanged`。不要為同一目的繫結 `SelectedIndex`：新增或移除區段時索引會移動，而項目本身不變。

### 啟動模式

`ActivationMode` 決定方向鍵的行為。`Automatic`（預設）會選取獲得焦點的標籤頁。`Manual` 只移動焦點；按 Enter 或空白鍵才會選取標籤頁。

<Demo name="ManualActivation" />

當面板顯示成本低、使用者正在瀏覽時使用 `Automatic`：用方向鍵在標籤頁之間移動即可預覽每個面板。當面板顯示成本高、使用者可能因預覽了並非本意打開的面板而遺失工作，或標籤頁用於篩選每次選取都會重新載入的清單時，使用 `Manual`。`Manual` 模式還能避免使用者仍在判斷時，方向鍵就改變了資料。

### 在程式碼中選取

```csharp
tabs.SelectTab(2);
tabs.SelectionChanged += (_, _) =>
{
    status.Text = $"已選取：{tabs.SelectedItem}";
};
```

`SelectTab` 在標籤頁存在且已啟用時選取該索引處的標籤頁，否則回傳 `false`。`SelectedIndex` 不會檢查標籤頁是否啟用，因此當目標可能被停用（例如被權限檢查關閉的標籤頁）時，請使用 `SelectTab`。

### 長標籤清單

寬於外框的標籤清單會水平捲動。邊緣按鈕只在還有內容可捲動時出現，清單會以平滑彈簧顯示選取或獲得焦點的標籤頁。點擊邊緣按鈕會讓清單移動約可見寬度的四分之三。程式碼改變選取時，清單會保持選取的標籤頁可見，因此長清單深處的標籤頁無需搜尋即可找到。

請讓標籤使用簡短的文字，使其在頁面支援的最窄寬度下仍能放入清單；其餘資訊移入面板。十個資料夾的清單配合邊緣按鈕可以使用；十五個且標籤很長的清單，更適合改用 `Select` 或側邊欄，因為使用者每次選取都得捲動。

### 資料結構與效能

只顯示選取標籤頁的內容。選取變更時，面板宿主會替換所顯示的內容，並在淡出期間保留離開的面板。應把繁重的載入放在選取之後，例如放在檢視模型的選取處理常式中，而不是在頁面打開時載入每個區段。請保持標籤數量較少：大約五到七個標籤頁的清單易於掃視，更長的清單需要不同的結構。

### 範例：客戶紀錄標籤頁

CRM 中的一筆客戶紀錄。帳號名稱及其方案位於標籤頁上方，因此使用者在概覽、訂單、發票與備註之間切換時，脈絡不會改變。概覽面板是一個兩欄的簡短事實清單，標籤使用淡化顏色，值使用主要文字色。標籤文字是與其打開的清單相符的名詞，第一個標籤頁被選取，因為它是使用者打開最頻繁的檢視。

![客戶紀錄標題，帶四個標籤頁，面板中顯示概覽資訊](images/CustomerTabs.png)

```xml
<StackPanel Width="680" Spacing="16">
    <StackPanel Spacing="4">
        <TextBlock Text="Northwind Trading"
                   FontSize="22"
                   FontWeight="SemiBold"
                   Foreground="{arc:ArcDesignTokenResource Foreground}" />
        <TextBlock Text="Business plan, renews November 30, 2026"
                   FontSize="14"
                   Foreground="{arc:ArcDesignTokenResource TextMuted}" />
    </StackPanel>
    <arc:Tabs AutomationProperties.Name="Customer sections">
        <arc:TabItem Header="Overview">
            <StackPanel Spacing="12">
                <Grid ColumnDefinitions="180,*">
                    <TextBlock Text="Account owner" FontSize="14"
                               Foreground="{arc:ArcDesignTokenResource TextMuted}" />
                    <TextBlock Grid.Column="1" Text="Maya Chen" FontSize="14" />
                </Grid>
                <Grid ColumnDefinitions="180,*">
                    <TextBlock Text="Plan" FontSize="14"
                               Foreground="{arc:ArcDesignTokenResource TextMuted}" />
                    <TextBlock Grid.Column="1" Text="Business, billed annually" FontSize="14" />
                </Grid>
                <Grid ColumnDefinitions="180,*">
                    <TextBlock Text="Renewal date" FontSize="14"
                               Foreground="{arc:ArcDesignTokenResource TextMuted}" />
                    <TextBlock Grid.Column="1" Text="November 30, 2026" FontSize="14" />
                </Grid>
                <Grid ColumnDefinitions="180,*">
                    <TextBlock Text="Open invoices" FontSize="14"
                               Foreground="{arc:ArcDesignTokenResource TextMuted}" />
                    <TextBlock Grid.Column="1" Text="2, one 6 days overdue" FontSize="14" />
                </Grid>
            </StackPanel>
        </arc:TabItem>
        <arc:TabItem Header="Orders">
            <TextBlock Text="12 orders in the last 90 days." FontSize="14" />
        </arc:TabItem>
        <arc:TabItem Header="Invoices">
            <TextBlock Text="inv_2026_0412 and inv_2026_0399 are open." FontSize="14" />
        </arc:TabItem>
        <arc:TabItem Header="Notes">
            <TextBlock Text="Asked for a quote for 40 more seats on March 2, 2026." FontSize="14" />
        </arc:TabItem>
    </arc:Tabs>
</StackPanel>
```

### 文案規範

- **標籤文字**：一到兩個字，命名其所打開清單的名詞，例如「發票」「備註」。保持各標籤的措辭一致，讓清單讀起來是一組並列的檢視。
- **標籤中的計數**：只有當計數會改變使用者接下來的操作時才加上，例如「未結（2）」。從不變動的計數只會替每個標籤增加雜訊。
- **面板文字**：以狀態或數字開頭（「2 張未結發票」「尚無備註」），而不是要求使用者去看標籤頁。
- **停用的標籤頁**：停用某個標籤頁時，在附近說明原因，例如在相鄰標籤頁的面板中或頁面標題中說明，讓使用者知道檢視是缺少還是對其不可用。

| 不要寫 | 改為 |
|---|---|
| 標籤文字 「Misc」 | 備註 |
| 標籤文字 「Stuff about the customer」 | 帳號 |
| 面板文字 「點擊上方標籤查看更多」 | 沒有未結發票。 |
| 標籤文字 「INVOICES」 | 發票 |

### 最佳做法

**建議**

- 保持標籤簡短，並讓清單在頁面支援的最窄寬度內放得下。被捲動隱藏的標籤比同時可見的標籤更難找到。
- 把共用的脈絡（紀錄名稱、狀態、主要操作）保留在標籤頁上方，使使用者切換檢視時它始終可見。
- 對 `SelectedItem` 使用雙向繫結，並用 `AutomationProperties.Name` 為清單命名。檢視模型保存選取狀態，輔助技術會透過名稱朗讀清單。
- 當面板顯示成本高時使用 `Manual` 啟動模式，讓方向鍵只移動焦點，而不載入每個檢視。

**避免**

- 用標籤頁表示步驟。使用者會預期標籤頁可以依任意順序造訪，而必須先完成的步驟需要 `Stepper` 或一系列頁面。
- 在標籤頁內巢狀放置標籤頁。兩層並列的選取會讓使用者分不清自己所在的層級，而且內層清單會在外層清單中捲動。
- 只在一個標籤頁中放置頁面的主要操作。使用者來到紀錄的原因通常在每個檢視中都看得到，而不是藏在其中一個裡。
- 在頁面上沒有原因地停用標籤頁。沒有說明的停用標籤頁看起來像是壞了。

### 常見問題

**為什麼選取標籤頁時我的頁面不會捲動？** 標籤清單會在它自己的檢視區中顯示選取的標籤頁，而不會把整個控制項帶入可見範圍。位於首屏以下的標籤組會停留在使用者捲動到的位置，因此選取標籤時頁面不會跳動。

**頁面載入時如何打開某個特定標籤頁？** 在控制項載入之前或之後設定 `SelectedIndex` 或 `SelectedItem`。如果該標籤頁可能被停用，改為呼叫 `SelectTab(index)`：對於停用或不存在的標籤頁它會回傳 `false`，因此你可以退回到第一個啟用的標籤頁。

**為什麼在 `Manual` 模式下方向鍵不會選取標籤頁？** 在 `Manual` 模式下，方向鍵只移動焦點。在獲得焦點的標籤頁上按 Enter 或空白鍵即可選取它。

**可以為溢出狀態設定標籤清單的樣式嗎？** 可以。當清單寬於外框時，`Tabs` 會設定 `:overflow` 虛擬類別；當某一側有隱藏的標籤頁時，會設定 `:scrolled-start` 與 `:scrolled-end`。在樣式選取器中使用它們，即可在不改變控制項範本的情況下修改邊緣效果。

## API 參考

<!-- api: Tabs -->

## 無障礙

- 清單具有 Tab 角色，每個 `TabItem` 都公開其選取狀態。請用 `AutomationProperties.Name` 為清單命名。
- 字串形式的標題會作為每個 `TabItem` 的名稱；其他類型的標題需要明確命名。
- 只有選取的標籤頁在 Tab 順序中。方向鍵在標籤頁之間移動，Home 與 End 分別跳到第一個與最後一個。
- Tab 鍵從清單移動到選取標籤頁的面板。
- 溢出清單上的捲動按鈕由 `ScrollLeftLabel` 與 `ScrollRightLabel` 命名，預設分別為「向左捲動標籤頁」與「向右捲動標籤頁」。
- 正在離開的標籤頁的面板會對輔助技術隱藏，並且在淡出期間不能獲得焦點。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 進入清單中的選取標籤頁，或最後一個獲得過焦點的標籤頁；從清單移入選取標籤頁的面板 |
| `Left` / `Right` | 移到上一個或下一個啟用的標籤頁，到達兩端時循環；在 `Automatic` 模式下該標籤頁同時被選取 |
| `Home` / `End` | 移到第一個或最後一個啟用的標籤頁；在 `Automatic` 模式下該標籤頁同時被選取 |
| `Enter` / `Space` | 選取獲得焦點的標籤頁。在 `Manual` 模式下鍵盤透過此方式選取；滑鼠按下在兩種模式下都會選取 |
| 滑鼠按下 | 以左鍵按下時選取標籤頁；觸控在放開時選取 |

停用的標籤頁會被方向鍵、Home 與 End 略過，滑鼠按下也無法選取它。

## 動態效果

- 選取指示膠囊以 morph 彈簧在各觸發器之間滑動。
- 新面板從切換方向滑入 8 px 並淡入。舊面板保持原位，向後移動 6 px 後快速淡出。
- 面板區域以彈簧從舊面板高度過渡到新高度，因此下方的內容隨之移動。
- 減少動態效果時，指示膠囊立即切換，面板在原位交叉淡化，不做高度動畫。關閉動態效果時一次完成所有變化。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 選取移動 | 膠囊以 morph 彈簧在觸發器之間滑動 | 膠囊立即切換 | 立即完成 |
| 新面板出現 | 從切換方向滑入 8 px，並在標準時長內淡入 | 在 120 ms 內原位交叉淡化 | 立即完成 |
| 舊面板離開 | 保持原位，向後移動 6 px，並在 120 ms 內淡出 | 在 100 ms 內淡出 | 立即完成 |
| 面板區域改變高度 | 以彈簧從舊高度過渡到新高度，帶動下方內容 | 沒有高度動畫 | 立即完成 |
| 清單捲動到某個標籤頁 | 以 smooth 彈簧捲動 | 直接跳轉 | 直接跳轉 |
| 懸停與選取的觸發器 | 標籤顏色漸變 | 立即完成 | 立即完成 |

## 相關元件

- `SegmentedControl`：用於精簡地選取一個沒有面板的值。
- `Accordion`：用於依序閱讀的堆疊區段。
- `Pagination`：用於翻閱同一個集合的頁面。

| 元件 | 適用情境 |
|---|---|
| `SegmentedControl` | 選取的是一個值，例如範圍或檢視模式，本身沒有面板 |
| `Accordion` | 堆疊的區段依序閱讀，並且可以同時展開多個 |
| `Breadcrumb` | 頁面位於階層結構中，使用者需要沿路徑返回上層 |
| `Pagination` | 檢視是同一個集合的頁面，而不是並列的檢視 |
| `Stepper` | 使用者依固定順序完成步驟 |
| `Select` | 檢視清單很長，表單中單次選取即可滿足需要 |
