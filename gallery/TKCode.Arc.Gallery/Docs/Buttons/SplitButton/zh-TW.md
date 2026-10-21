---
title: 分割按鈕
summary: 一個主要動作，加上一組相近備選項的選單。
source-hash: a0106aa99a5c
---

<Demo name="MergeStrategies" />

## 何時使用

`SplitButton` 把一個預設動作與一個箭頭連在一起，箭頭會開啟附近的備選項。它在一個控制項中回答兩個問題：這裡通常會發生什麼，以及還能發生什麼。常用路徑只需一次點按，備選項再多一次點按即可，因此它適合工具列、頁首或頁尾中以某一個動作為主的場景。

- **程式碼審查畫面中合併提取要求**：預設是「使用合併認可合併」。「壓縮並合併」與「變基並合併」放在選單中，「關閉提取要求」作為分隔線之後的危險列。
- **營收報表中的匯出**：「匯出為 CSV」是多數財務團隊使用的格式，因此作為預設動作。JSON 與 PDF 只需再點一次，無論選哪一個，報表都會保留篩選條件。
- **發票編輯器中的傳送**：「傳送發票」是預設動作。「傳送提醒」「存為草稿」與「作廢發票」是備選項，選單用分隔線把危險動作與其餘動作分開。
- **原地顯示結果的複製**：「複製頁面」會在同一個按鈕上短暫顯示「已複製」，確認資訊就出現在使用者視線所在之處，無需輕提示。
- **CI 主控台中的部署**：「部署到正式環境」是發行分支的預設動作。「部署到預備環境」放在選單中，用於需要先行演練的發行。

## 何時不用

- **選項地位對等、沒有預設動作**，例如「透過電子郵件、Slack 或連結分享」：用 `DropdownMenu`，它只有觸發器，沒有主要區域。
- **只有一個動作、沒有可選項**，例如設定表單中的「儲存變更」：用 `Button`。
- **針對單列或單張卡片物件的動作**，例如訂單上的重新命名、封存與刪除：用 `ContextMenu`，即該物件的指標與鍵盤內容選單。
- **多個相關動作、沒有預設動作，並排顯示**：用 `ButtonGroup`。
- **需要保持選取狀態的選擇**，例如時間區段或檢視：用 `SegmentedControl` 或 `Select`。分割按鈕只執行它的動作，不保存值。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:SplitButton Label="合併提取要求">
        <arc:MenuEntry Header="壓縮並合併" />
        <arc:MenuEntry Header="變基並合併" />
        <arc:MenuEntry Header="關閉提取要求" IsDestructive="True" HasSeparatorBefore="True" />
    </arc:SplitButton>
</UserControl>
```

`SplitButton` 衍生自 Avalonia 的 `TemplatedControl`。它的兩個區域都是真正的按鈕，因此各自可以取得焦點、顯示焦點環，並獨立回應鍵盤。選單與 `DropdownMenu` 使用同一個 `MenuSurface`，因此它的列、反白與鍵盤移動和程式庫中的其他部分保持一致。選單列是 `MenuEntry` 項目，可以直接寫在 `Items` 中，也可以透過 `ItemsSource` 提供。

### 結構

1. **主要區域**：預設動作。顯示 `Label`，設定了 `Icon` 時在其前面顯示圖示。內距兩側各 16 px（`Secondary` 時為 12 px），最小高度為中等控制項高度。
2. **圖示**（`Icon`）：16 px 的 Lucide 圖示，線寬 1.75，圖示與文字之間間隔 8 px。新圖示淡入，舊圖示縮小消失。
3. **分隔線**：兩個區域之間 1 px 的線，在主要變體上為文字顏色的淡色調。
4. **箭頭區域**：寬 38 px（`Secondary` 時為 32 px）。16 px 的箭頭在選單開啟時轉動，按下時從不縮放。
5. **選單**（`Menu`）：錨定在箭頭上的下拉選單，與箭頭末端對齊，並在箭頭下方 4 px 處開啟。選單列是 `MenuEntry` 項目。帶有 `HasSeparatorBefore` 的列會開始新的群組；`IsDestructive` 會把它顯示為危險動作的顏色。
6. **焦點環**：2 px 的環，與每個區域相距 2 px，形狀與該區域的圓角一致。

主要變體是填滿的膠囊形，以前景色作為填滿色。`Secondary` 變體是外框膠囊形：高 36 px，文字 12 px，主要區域至少寬 142 px，背景為表面色。

### 選擇變體與預設動作

把最常見的動作放在主要區域。選單列依使用頻率由高到低排列，使清單讀起來像一份排名。把危險列放在最後，並用 `HasSeparatorBefore` 與其他列隔開，這樣誤點箭頭時不會直接觸發它。

在頁首、對話方塊頁尾或卡片中，當動作就是該畫面的目標時使用 `Primary`。在密集的工具列或表頭中使用 `Secondary`，避免填滿的按鈕與內容爭奪注意力。同一個工具列中不要混用兩種變體：外框膠囊與填滿膠囊會被理解為兩個重要程度不同的層級。

### 點按與選單選取

主要區域會觸發 `Click` 並執行 `Command`。`Click` 在命令執行之前觸發，因此處理常式可以先準備命令所讀取的狀態。箭頭區域開啟選單，選取某一列時該列觸發 `ItemClicked`。選取一列後，選單隨即關閉。

```xml
<arc:SplitButton Label="匯出為 CSV"
                 Variant="Secondary"
                 Command="{Binding ExportCommand}">
    <arc:MenuEntry Header="匯出為 JSON" />
    <arc:MenuEntry Header="匯出為 PDF" />
</arc:SplitButton>
```

```csharp
merge.Click += (_, _) => log.Text = "已透過合併認可完成合併。";
merge.ItemClicked += (_, e) => log.Text = $"選單：{e.Header}";
```

### 來自資料的列

對於來自檢視模型的列，使用 `ItemsSource`，並用 `ItemTemplate` 繪製每一列。`MenuEntry` 執行個體按原樣使用。其他項目會顯示為列的標題，因此當某一列需要自己的外觀時，請為它提供 `MenuEntry` 或範本。`ItemClicked` 透過 `e.Item` 傳回資料項目，使處理常式無需進行字串比對。

```xml
<arc:SplitButton Label="分享" ItemsSource="{Binding ShareTargets}" />
```

### 圖示與原地結果

`Icon` 接收 24 乘 24 的 Lucide 幾何圖形，在程式碼中設定。更改 `Label` 與 `Icon` 時，主要區域會變形為新的文字，因此結果可以直接顯示在按鈕本身上：「複製頁面」變為「已複製」，勾選圖示取代複製圖示。相同的字母保持原位，只有變化的字母移動，視線不會失去這個按鈕。

<Demo name="ResultInPlace" />

結果標籤只用一個詞，並在短暫停頓後恢復原標籤。「已傳送」或「已儲存」這類確認也採用同樣的模式；對於需要一段時間的程序則不要使用，那時應顯示進度而不是標籤。

### 停用

將 `IsEnabled` 設為 `False` 可同時停用兩個區域。被停用的分割按鈕也會保持選單關閉，因此在預設動作無法使用時，無法選取任何備選項。控制項會淡化到約 52% 的不透明度，仍保留在版面配置中。

<Demo name="Disabled" />

只在動作無法適用時停用控制項，並在附近說明原因。例如，因分支存在失敗的檢查而停用的「部署」按鈕，配上一行點明失敗檢查名稱的文字會更清楚。

### 次要樣式

`Variant="Secondary"` 是緊湊的外框膠囊，文字為 12px，主要區域寬 142px。在此範例中，選擇一種格式會使其成為預設動作。

<Demo name="Secondary" />

次要變體適合工具列和表頭。在此範例中，選取的備選項會成為新的標籤。若該選擇必須在重新啟動後保留，請將它保存在檢視模型中，並在檢視載入時據此設定 `Label`。

### 繫結到檢視模型

發票編輯器把它的傳送動作公開為命令，並在檢視模型中保存選單狀態。主要動作是一個命令，備選項來自一個集合，開啟狀態為雙向繫結，使檢視模型可以在傳送後關閉選單。

```csharp
public partial class InvoiceViewModel : ObservableObject
{
    private readonly IInvoiceService _invoices;

    public InvoiceViewModel(IInvoiceService invoices)
    {
        _invoices = invoices;
        AlternativeSends = new ObservableCollection<SendOption>
        {
            new("傳送提醒", SendKind.Reminder),
            new("存為草稿", SendKind.Draft),
            new("作廢發票", SendKind.Void)
        };
    }

    [ObservableProperty]
    private bool _isMenuOpen;

    public string InvoiceId { get; } = "inv_2026_0412";

    public ObservableCollection<SendOption> AlternativeSends { get; }

    [RelayCommand]
    private async Task SendInvoiceAsync()
    {
        var result = await _invoices.SendAsync(InvoiceId, SendKind.Invoice);
        IsMenuOpen = false;
    }

    public async Task RunAsync(SendOption option) =>
        await _invoices.SendAsync(InvoiceId, option.Kind);
}

public enum SendKind { Invoice, Reminder, Draft, Void }

public sealed record SendOption(string Label, SendKind Kind);
```

```xml
<arc:SplitButton Label="傳送發票"
                 Command="{Binding SendInvoiceCommand}"
                 IsOpen="{Binding IsMenuOpen, Mode=TwoWay}"
                 ItemsSource="{Binding AlternativeSends}"
                 MenuButtonLabel="其他傳送方式" />
```

```csharp
invoice.ItemClicked += (_, e) => _ = viewModel.RunAsync((SendOption)e.Item!);
```

- `IsOpen` 為雙向繫結。檢視模型可以開啟或關閉選單，使用者開啟選單時，箭頭會把狀態寫回。
- `Command` 執行預設動作。
- 備選項在 `ItemClicked` 中處理，因為列的資料項目是最自然的引數；檢視模型負責動作，控制項負責選單。

### 範例：發票頁尾

帳務主控台中的發票編輯器。頁尾左側顯示總額，右側是傳送動作。「傳送發票」是預設動作，箭頭提供提醒、草稿與作廢選項；其中「作廢」位於分隔線之下，因為它是唯一的危險列。次要的「取消」按鈕是幽靈按鈕，因此分割按鈕是頁尾中唯一的填滿控制項。

![帶有「傳送發票」分割按鈕及其備選項的發票頁尾](images/InvoiceFooter.png)

```xml
<StackPanel Width="620" Spacing="16">
    <Border Padding="24"
            Background="{arc:ArcDesignTokenResource Surface}"
            BorderBrush="{arc:ArcDesignTokenResource Border}"
            BorderThickness="1"
            CornerRadius="{arc:ArcDesignTokenResource RadiusPanel}">
        <StackPanel Spacing="16">
            <Grid ColumnDefinitions="*,Auto">
                <StackPanel Spacing="2">
                    <TextBlock Text="Invoice inv_2026_0412"
                               FontSize="16"
                               FontWeight="SemiBold"
                               Foreground="{arc:ArcDesignTokenResource Foreground}" />
                    <TextBlock Text="Northwind Traders, due May 14, 2026"
                               FontSize="13"
                               Foreground="{arc:ArcDesignTokenResource TextMuted}" />
                </StackPanel>
                <TextBlock Grid.Column="1"
                           Text="Draft"
                           VerticalAlignment="Center"
                           Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
            </Grid>
            <Border Height="1" Background="{arc:ArcDesignTokenResource Border}" />
            <Grid ColumnDefinitions="*,Auto,Auto">
                <StackPanel Spacing="2">
                    <TextBlock Text="Total"
                               FontSize="13"
                               Foreground="{arc:ArcDesignTokenResource TextMuted}" />
                    <TextBlock Text="$12,480.00"
                               FontSize="20"
                               FontWeight="SemiBold"
                               Foreground="{arc:ArcDesignTokenResource Foreground}" />
                </StackPanel>
                <arc:Button Grid.Column="1"
                            Content="Cancel"
                            Variant="Ghost"
                            VerticalAlignment="Center"
                            Margin="0,0,12,0" />
                <arc:SplitButton Grid.Column="2"
                                 Label="Send invoice"
                                 Icon="{x:Static arc:ArcIcons.Send}"
                                 VerticalAlignment="Center">
                    <arc:MenuEntry Header="Send reminder" Icon="{x:Static arc:ArcIcons.Bell}" />
                    <arc:MenuEntry Header="Save as draft" Icon="{x:Static arc:ArcIcons.FileText}" />
                    <arc:MenuEntry Header="Void invoice"
                                   Icon="{x:Static arc:ArcIcons.CircleX}"
                                   IsDestructive="True"
                                   HasSeparatorBefore="True" />
                </arc:SplitButton>
            </Grid>
        </StackPanel>
    </Border>
</StackPanel>
```

### 文案規範

- **主要標籤**：動詞加物件，句首大寫：「合併提取要求」「匯出為 CSV」「傳送發票」。它說明預設動作會做什麼，讓使用者在閱讀選單之前就知道點按的結果。
- **選單列**：與主要標籤相同的語法與名詞。危險列要說明後果：用「作廢發票」，而不是「刪除」。
- **箭頭名稱**：預設的 `{Label} more actions` 適用於大多數控制項。當標籤是「傳送」或「匯出」之類的通用動詞時，請設定 `MenuButtonLabel`，使名稱說明選單提供什麼。
- **結果標籤**：一個確認動作的詞，例如「已複製」或「已傳送」，與預設動作使用相同的時態。

| 不要寫 | 改為 |
|---|---|
| 點此處傳送 | 傳送發票 |
| 匯出 | 匯出為 CSV |
| 刪除發票（在選單中） | 作廢發票 |
| 複製成功！ | 已複製 |
| 更多選項（箭頭名稱） | 其他傳送方式 |

### 最佳做法

**建議**

- 把大多數人會執行的動作放在主要區域。預設動作很少被使用的分割按鈕，會讓人每次都得開啟選單。
- 把危險列放在最後，位於分隔線之下，這樣誤點箭頭時不會直接觸發它。
- 為預設動作繫結 `Command`，並讓選單列只包含能在同一情境中執行的動作。
- 標籤要夠短，使主要區域的寬度保持在它所在工具列的寬度之內。

**避免**

- 在所有選項都地位對等時使用分割按鈕。沒有預設動作時，主要區域暗示了一個使用者並未做出的選擇；應使用 `DropdownMenu`。
- 為隱藏需要前提條件的動作而停用控制項。停用的控制項無法說明原因；應在附近說明。
- 同一個工具列中使用多個不同的分割按鈕。選單會互相爭奪注意力，使用者必須逐一查看每個箭頭。
- 為長時間執行的程序更改標籤。結果標籤用於確認已完成的動作；正在執行的程序需要進度。
- 在同一列中把 `Secondary` 變體與 `Primary` 控制項放在一起。兩種字重會被理解為兩個重要程度不同的層級。

### 常見問題

**如何更改箭頭的無障礙名稱？** 設定 `MenuButtonLabel`。為 `null`（預設值）時，名稱為 `{Label} more actions`。

**在箭頭上按 Enter 會怎樣？** 它會開啟選單，並反白第一列。主要動作只在主要區域上執行。

**程式碼可以開啟或關閉選單嗎？** 可以。呼叫 `Open()` 會開啟選單並把焦點放在選單面板上，呼叫 `Open(true)` 則會反白第一列。`Close()` 關閉選單，且不把焦點返回。`IsOpen` 是雙向的，因此繫結它與呼叫這些方法效果相同。

**Click 會在 Command 之前執行嗎？** 會。先觸發 `Click`，然後執行命令。對於命令所依賴的狀態使用 `Click`，命令本身只負責實際工作。

**為什麼某一列看起來不是危險動作？** 在 `MenuEntry` 上設定 `IsDestructive="True"`。該列會採用危險色，而 `HasSeparatorBefore="True"` 會在其上方加入分隔線。

## API 參考

<!-- api: SplitButton -->

<!-- api: MenuEntry -->

`SplitButtonVariant` 有兩個值：`Primary`（填滿的預設樣式）與 `Secondary`（外框膠囊）。`ItemClicked` 的事件引數是 `MenuEntryClickedEventArgs`，它公開 `Entry`（被啟動的列）、`Item`（列背後的資料項目，內嵌列則為項目本身）以及 `Header`（列的標題）。

## 無障礙

- 主要區域與箭頭是兩個按鈕。主要區域由 `Label` 命名。箭頭名稱為 `{Label} more actions`，除非 `MenuButtonLabel` 設定了其他名稱；箭頭會回報選單是否展開。
- 選單使用 Menu 與 MenuItem 角色。方向鍵在各列之間移動，並在兩端循環。
- `Label` 變更時會以禮貌的方式播報，因此「已複製」之類的結果無需移動焦點即可傳達給螢幕閱讀器。
- 當標籤本身不足以說明箭頭提供什麼時，設定 `MenuButtonLabel`。「傳送發票 more actions」準確但冗長；「其他傳送方式」說明了選單的內容。
- 焦點環跟隨每個區域，因此鍵盤使用者能看到哪個區域會執行動作。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 在主要區域與箭頭之間移動，然後移到下一個控制項。 |
| 主要區域上的 `Enter` / `Space` | 觸發 `Click` 並執行 `Command`。 |
| 箭頭上的 `Enter` / `Space` | 開啟選單，並反白其第一列。 |
| 箭頭上的 `ArrowDown` | 開啟選單，並反白其第一列。 |
| 選單中的 `ArrowDown` / `ArrowUp` | 移到下一列或上一列。跳過停用的列，並在兩端循環。 |
| `Escape` | 關閉選單，並把焦點返回到箭頭。 |

## 動態效果

- 文字逐字母變化：相同的字母平滑滑動，變化的字母從柔和的模糊中升起。圖示交叉淡化，主要區域以彈簧過渡到新的寬度。
- 主要區域會輕微下壓。箭頭從不縮放，選單開啟時它會轉動。
- 選單在箭頭下方 4px 處開啟，各列依序淡入。
- 減少動態效果時移除按下效果、寬度彈簧與選單變換，僅保留短暫的淡入。關閉動態效果時，選單與文字變化立即呈現。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 標籤變化 | 相同字母滑動，變化的字母從柔和模糊中升起，移除的字母上浮並淡出 | 120 ms 內交叉淡化 | 立即替換 |
| 圖示變化 | 新圖示從較小尺寸、帶輕微模糊地漸長，舊圖示縮小消失 | 短暫淡入 | 立即 |
| 主要區域按下 | 整個膠囊輕微下壓 | 無按下效果 | 立即 |
| 選單開啟 | 在箭頭下方 4 px 處開啟，各列依序淡入 | 短暫淡入，無變換 | 立即 |

## 相關元件

- `DropdownMenu`：選項地位對等、沒有預設動作的選單。
- `Button`：沒有備選項的單一動作。
- `ButtonGroup`：並排顯示的多個相關動作。
- `MenuEntry` 是分割按鈕與選單共用的列類型。

| 元件 | 適用情境 |
|---|---|
| `DropdownMenu` | 所有選項地位對等，且沒有預設選項 |
| `Button` | 只有一個動作，沒有可選擇的內容 |
| `ButtonGroup` | 多個相關動作並排放置，沒有預設動作 |
| `FloatingButtonGroup` | 圖示動作浮在畫布或選取內容之上 |
| `SegmentedControl` | 某個值保持選取，例如時間區段或檢視 |
