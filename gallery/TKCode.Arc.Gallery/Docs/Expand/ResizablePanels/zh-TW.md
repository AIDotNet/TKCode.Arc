---
title: 可調整大小的面板
summary: 拖曳窗格之間的分隔線，在各窗格之間調配空間。
source-hash: 5582f18f23b0
---

<Demo name="Default" />

## 何時使用

`ResizablePanels` 將一個區域分割為並排的窗格，並由使用者決定每個窗格佔用多少空間。它適用於應用程式的工作區：頁首下方的主體區域，其中清單、詳細檢視和次要窗格需要共用同一個視窗。此元件需要父層提供確定的高度，因此應放在外殼的內容區域中，而不是放在自動調整大小的列裡。

- **郵件與訊息用戶端**：資料夾清單、郵件清單和閱讀窗格。閱讀長串郵件時，使用者會拉寬閱讀窗格；需要空間時，則摺疊資料夾。
- **管理後台中的工單與訂單分流**：左側是佇列，右側是選取的工單或訂單。客服人員縮窄佇列以便密集閱讀，整理積壓工單時再拉寬它。
- **分析工具中的查詢與程式碼編輯器**：結構瀏覽器、編輯器和結果窗格。撰寫 SQL 或 DAX 查詢時，編輯器需要大部分寬度。
- **帶側邊檢閱器的儀表板**：圖表窗格與篩選或詳細資料檢閱器並排。檢閱器不需要時會摺疊，使圖表獲得全寬。
- **使用者調整一次後即保留的版面配置**：尺寸透過 `Sizes` 儲存，並在下次工作階段中還原，因此團隊偏好的分割在重新啟動後依然保留。

## 何時不用

- **覆蓋頁面的面板**：用 `Drawer` 作為從內容上方滑出、必須在使用者返回頁面前關閉的窗格。
- **同一頁面的堆疊段落**：`ResizablePanels` 只進行水平分割。上下排列、依序展開收合的段落用 `Accordion`。
- **每次只顯示一個窗格**：當使用者在多個檢視間切換，且從不需要同時看到兩個時，用 `Tabs`。
- **窄螢幕或手機版面配置**：窗格永遠不會堆疊。在窄寬度下使用 `Tabs`，或讓頁面改變結構。
- **無人調整的固定分割**：使用普通的 Avalonia `Grid` 並設定 star 尺寸的欄更簡單，也沒有需要操作的分隔線。
- **內容比窗格更長**：保持分割，並在窗格內放置 `ScrollArea`。此元件不會捲動其窗格。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:ResizablePanels Label="郵件版面配置" Height="320">
        <arc:ResizablePanel Id="folders"
                            Label="資料夾"
                            DefaultSize="22"
                            MinSize="140"
                            IsCollapsible="True">
            <TextBlock Text="收件匣、草稿、已傳送、封存" />
        </arc:ResizablePanel>
        <arc:ResizablePanel Id="messages"
                            Label="郵件清單"
                            DefaultSize="33"
                            MinSize="200">
            <TextBlock Text="42 個對話" />
        </arc:ResizablePanel>
        <arc:ResizablePanel Id="reading"
                            Label="閱讀窗格"
                            DefaultSize="45"
                            MinSize="220"
                            IsCollapsible="True">
            <TextBlock Text="季度檢討" />
        </arc:ResizablePanel>
    </arc:ResizablePanels>
</UserControl>
```

`ResizablePanels` 衍生自 Avalonia 的 `Control`，`ResizablePanel` 衍生自 `ContentControl`。元件將其 `Panels` 集合作為內容，因此窗格直接寫在其中即可。元件自行配置各窗格：每個窗格的寬度由其佔比與限制決定，而不是你為它設定的 `Width`。窗格之間的分隔線由元件建立，並且可以取得焦點，因此無需額外標記即可用鍵盤操作。

`Label` 為輔助技術提供元件的名稱。每個窗格的 `Label` 為其後的分隔線命名。

### 結構

1. **元件**（`ResizablePanels`）：裁剪到自身邊界內，從左到右配置。除分隔線和還原標籤外，它沒有自身的視覺元素。元件內的文字使用內文字型，字級為 `TypeSM`，顏色為前景色，除非某個窗格另有設定。
2. **窗格**（`ResizablePanel`）：內容拉伸以填滿窗格。當寬度低於窗格的有效最小值時，內容保持該最小寬度，並錨定在移動的邊緣上，因此關閉的窗格會滑出，而不是被擠壓。
3. **分隔線**：1 px 的線，使用邊框顏色。指標懸停、正在拖曳或分隔線取得鍵盤焦點時，變為強邊框顏色。
4. **抓取區域**：線兩側各 6 px，共 13 px。在此區域內按下即開始拖曳。
5. **握把**：以線為中心的 4 × 32 px 圓角膠囊。懸停、聚焦或拖曳分隔線時出現，使用強邊框顏色；拖曳時變為前景色。
6. **還原標籤**：距離分隔線 5 px、18 × 40 px 的膠囊，位於被隱藏窗格返回的一側。相鄰窗格被隱藏時出現。它使用表面填色、懸停或聚焦時加深的邊框筆畫、次要文字顏色的箭頭以及靜止時的陰影。隱藏分隔線的點擊區域會延伸 30 px 進入可見窗格，使還原標籤更容易按下。
7. **游標**：在展開的分隔線上為水平調整大小游標，在隱藏的分隔線上為手形游標。

### 窗格尺寸

每個窗格都有 `DefaultSize`，它是佔比而不是寬度。各佔比是相對的：`22`、`33` 和 `45` 與 `2.2`、`3.3`、`4.5` 產生相同的版面配置，它們無需加總為 100。元件依佔比分配空間，然後讓每個窗格遵守其限制。若某個窗格會突破 `MinSize` 或 `MaxSize`，它會被固定在該限制處，剩餘空間再在其他窗格之間重新分配。

| 屬性 | 預設值 | 意義 |
|---|---|---|
| `DefaultSize` | `1` | 初始佔比。也是呼叫 `Reset()` 時元件返回的尺寸。 |
| `MinSize` | `80` px | 窗格開始產生阻力的寬度。其內容永遠不會在此寬度以下重新排版。 |
| `MaxSize` | 無限制 | 以 px 為單位的寬度上限。值為零或小於零表示無限制。 |
| `IsCollapsible` | `False` | 允許透過分隔線、鍵盤或甩動將窗格隱藏。 |
| `IsCollapsed` | `False` | 使窗格以隱藏狀態開始。僅在 `IsCollapsible="True"` 時生效。雙向繫結。 |
| `Id` | 無 | 窗格的穩定鍵。當 `Label` 為空時用於為分隔線命名。 |

當元件寬度小於所有窗格最小值之和時，最小值會依比例縮小，使所有窗格保持可見。

### 預設摺疊

在可摺疊的窗格上設定 `IsCollapsed="True"`，使其以隱藏狀態開始。此時其分隔線會顯示一個還原標籤，點選即可將窗格還原；在窗格還原之前，它會從點擊測試與 Tab 順序中移除。

<Demo name="CollapsedAndPersisted" />

從程式碼摺疊與拖曳或 Enter 鍵使用相同的路徑。將 `IsCollapsed` 設為 `True` 會透過分隔線隱藏窗格，設為 `False` 則還原它。對於不可摺疊的窗格，兩者都會被忽略。

### 限制

`MinSize` 是窗格開始產生阻力的像素寬度。`MaxSize` 限制寬度上限，預設無限制。超過限制後，分隔線會停在限制處，並產生最多 44 px 的橡皮筋回彈阻力，之後便不再移動，讓使用者知道已達到限制。

```xml
<arc:ResizablePanel Id="code" Label="程式碼" DefaultSize="50" MinSize="240" MaxSize="800" />
```

選擇 `MinSize` 時，應取窗格內容仍能清楚閱讀的最窄寬度。郵件清單需要容納寄件者與主旨，資料夾清單需要容納最長的標籤，閱讀窗格需要容納一行內文。預設的 80 px 只適用於只放圖示或短值的窗格。

### 繫結到檢視模型

元件的 `Sizes` 屬性即是版面配置，因此版面配置由檢視模型持有。`Sizes` 與每個窗格的 `IsCollapsed` 預設為雙向繫結，這表示檢視模型會收到每一次拖曳或鍵盤操作結束後的尺寸變化，以及每一次摺疊，並可以設定它們來還原版面配置。

```csharp
public interface IUserSettings
{
    IReadOnlyList<double>? MailPaneSizes { get; set; }
    bool MailFoldersCollapsed { get; set; }
}

public partial class MailViewModel : ObservableObject
{
    private readonly IUserSettings _settings;

    public MailViewModel(IUserSettings settings)
    {
        _settings = settings;
        _paneSizes = settings.MailPaneSizes;
        _isFoldersCollapsed = settings.MailFoldersCollapsed;
    }

    [ObservableProperty]
    private IReadOnlyList<double>? _paneSizes;

    [ObservableProperty]
    private bool _isFoldersCollapsed;

    partial void OnPaneSizesChanged(IReadOnlyList<double>? value) => _settings.MailPaneSizes = value?.ToArray();

    partial void OnIsFoldersCollapsedChanged(bool value) => _settings.MailFoldersCollapsed = value;
}
```

```xml
<arc:ResizablePanels Label="郵件版面配置" Sizes="{Binding PaneSizes}" Height="480">
    <arc:ResizablePanel Id="folders"
                        Label="資料夾"
                        DefaultSize="22"
                        MinSize="140"
                        IsCollapsible="True"
                        IsCollapsed="{Binding IsFoldersCollapsed}">
        <!-- 資料夾清單 -->
    </arc:ResizablePanel>
    <arc:ResizablePanel Id="messages" Label="郵件清單" DefaultSize="33" MinSize="200">
        <!-- 郵件清單 -->
    </arc:ResizablePanel>
    <arc:ResizablePanel Id="reading" Label="閱讀窗格" DefaultSize="45" MinSize="220">
        <!-- 郵件詳細內容 -->
    </arc:ResizablePanel>
</arc:ResizablePanels>
```

- `Sizes` 依順序為每個窗格保存一個值，單位為可用空間的百分比，各值加總為 100。隱藏的窗格值為 0。檢視模型會在尺寸變化穩定後收到新值（`LayoutChanged` 與之同時觸發），而不是在每次指標移動時收到，因此在屬性變更處理常式中儲存時，每次操作只會寫入儲存空間一次。
- 將版面配置與其所屬的窗格集合一同保存。元件在整個生命週期內應使用相同的窗格，因此當窗格清單變化時應捨棄已保存的陣列，並使用預設值。
- `Reset()` 是方法而不是屬性。請在檢視的程式碼後置檔中，從按鈕的 Click 處理常式呼叫它，因為它會將每個窗格還原為預設佔比與預設摺疊狀態。

### 拖曳、甩動與連按兩下

拖曳分隔線時，窗格會與指標一對一地移動。當拖曳越過限制時，以下規則決定使用者所看到的效果：

- **限制**：超過窗格的最小值或最大值後，分隔線會產生阻力；若指標在橡皮筋區域內放開，分隔線會停在限制處。
- **拖曳摺疊**：可摺疊窗格的寬度低於其最小值減去「最小值的一半」與 40 px 中較大者時，會自動收起。對於預設的 80 px 最小值，該門檻為 40 px；對於 200 px 的最小值，則為 100 px。
- **甩動**：放開時，元件會將指標的速度沿移動方向投射一小段距離。朝邊緣甩動會摺疊窗格，朝外甩動則還原窗格。沒有甩動時，分隔線停留在放開的位置。
- **放開動態效果**：窗格會以保留指標速度的彈簧穩定下來，因此快速甩動會自然地延續到穩定過程中。
- **按下還原**：按下隱藏窗格的分隔線而不移動，即可還原該窗格。
- **連按兩下**：連按兩下任一分隔線會呼叫 `Reset()`，使整個元件還原為預設值。

這些規則是針對滑鼠與觸控板調校的。觸控使用者可以拖曳分隔線，但甩動門檻是根據指標取樣測量的，因此請在你支援的裝置上測試摺疊手勢。

### 窄視窗

窗格永遠不會堆疊。視窗變窄時，最小值會依比例縮小；低於其最小值的窗格會隨寬度淡出，其內容保持在最小寬度並隨之滑出。手機寬度的版面配置需要不同的結構：在該寬度下切換為 `Tabs`，或改變頁面版面配置。

### 範例：郵件工作區

以閱讀為中心的郵件檢視。資料夾清單預設隱藏，使郵件清單與閱讀窗格獲得全部視窗空間；左邊緣的還原標籤展示了如何把資料夾重新顯示出來。郵件清單的每一列顯示寄件者與主旨，閱讀窗格則提供所開啟郵件的操作。標籤與可見標題保持一致，使分隔線的名稱與使用者所見相符。

![郵件工作區：資料夾窗格摺疊為還原標籤，並顯示郵件清單與閱讀窗格](images/MailWorkspace.png)

```xml
<Border Width="720"
        Height="340"
        BorderThickness="1"
        CornerRadius="18"
        ClipToBounds="True"
        BorderBrush="{arc:ArcDesignTokenResource Border}">
    <arc:ResizablePanels Label="Mail workspace">
        <arc:ResizablePanel Id="folders"
                            Label="Folders"
                            DefaultSize="22"
                            MinSize="140"
                            IsCollapsible="True"
                            IsCollapsed="True">
            <Border Padding="16" Background="{arc:ArcDesignTokenResource SurfaceMuted}">
                <StackPanel Spacing="10">
                    <TextBlock Text="Folders"
                               FontSize="14"
                               FontWeight="Medium"
                               Foreground="{arc:ArcDesignTokenResource Foreground}" />
                    <TextBlock Text="Inbox  12"
                               FontSize="13"
                               Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
                    <TextBlock Text="Drafts  2"
                               FontSize="13"
                               Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
                </StackPanel>
            </Border>
        </arc:ResizablePanel>
        <arc:ResizablePanel Id="messages"
                            Label="Messages"
                            DefaultSize="33"
                            MinSize="200">
            <StackPanel Background="{arc:ArcDesignTokenResource Surface}">
                <Border Padding="36,12,16,12" BorderBrush="{arc:ArcDesignTokenResource Border}"
                        BorderThickness="0,0,0,1">
                    <StackPanel Spacing="4">
                        <TextBlock Text="Priya Raman  ·  Oct 19"
                                   FontSize="12"
                                   Foreground="{arc:ArcDesignTokenResource TextMuted}" />
                        <TextBlock Text="Invoice INV-2026-0412 is overdue"
                                   FontSize="14"
                                   FontWeight="Medium"
                                   Foreground="{arc:ArcDesignTokenResource Foreground}" />
                    </StackPanel>
                </Border>
                <Border Padding="36,12,16,12">
                    <StackPanel Spacing="4">
                        <TextBlock Text="Marco Lind  ·  Oct 18"
                                   FontSize="12"
                                   Foreground="{arc:ArcDesignTokenResource TextMuted}" />
                        <TextBlock Text="Q3 seat count for Northwind"
                                   FontSize="14"
                                   Foreground="{arc:ArcDesignTokenResource Foreground}" />
                    </StackPanel>
                </Border>
            </StackPanel>
        </arc:ResizablePanel>
        <arc:ResizablePanel Id="reading"
                            Label="Reading pane"
                            DefaultSize="45"
                            MinSize="300">
            <Border Padding="20" Background="{arc:ArcDesignTokenResource SurfaceMuted}">
                <StackPanel Spacing="12">
                    <TextBlock Text="Invoice INV-2026-0412 is overdue"
                               FontSize="16"
                               FontWeight="Medium"
                               TextWrapping="Wrap"
                               Foreground="{arc:ArcDesignTokenResource Foreground}" />
                    <TextBlock Text="From Priya Raman, Finance. Sent Oct 19, 2026."
                               FontSize="12"
                               TextWrapping="Wrap"
                               Foreground="{arc:ArcDesignTokenResource TextMuted}" />
                    <TextBlock Text="The March invoice is 14 days past due. Payment is due by Oct 31."
                               FontSize="13"
                               TextWrapping="Wrap"
                               Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
                    <StackPanel Orientation="Horizontal" Spacing="8">
                        <arc:Button Content="Reply" Variant="Secondary" Size="Small" />
                        <arc:Button Content="Archive" Variant="Ghost" Size="Small" />
                    </StackPanel>
                </StackPanel>
            </Border>
        </arc:ResizablePanel>
    </arc:ResizablePanels>
</Border>
```

### 文案規範

- **窗格標籤**：簡短的名詞片語，使用句子式大小寫，與窗格可見的標題一致：「資料夾」「郵件清單」「閱讀窗格」。該標籤是分隔線對輔助技術的名稱，因此「窗格 2」這類泛稱會讓螢幕閱讀器使用者無從下手。
- **元件標籤**：說明版面配置是什麼的名詞片語：「郵件工作區」「查詢編輯器」。命名版面配置本身，而不是其機制，使標籤中不出現「可調整大小」。
- **還原文案**：窗格被隱藏時，分隔線會播報「資料夾已隱藏，按 Enter 顯示」。保持窗格標籤簡短，使這句話能完整讀完。
- **窗格中的數字**：數量使用阿拉伯數字，例如「42 個對話」，使數值能一眼掃讀。

| 不要寫 | 改為 |
|---|---|
| Label="窗格 2" | Label="郵件清單" |
| Label="可調整大小的區域" | Label="郵件工作區" |
| Label="閱讀"（僅用於層級很深、版面配置密集的場景） | Label="閱讀窗格" |
| Label="右側的內容" | Label="查詢結果" |

### 最佳做法

**建議**

- 為每個窗格提供穩定的 `Id` 與可見的 `Label`。`Id` 用於在已保存的設定中標識窗格，標籤則是分隔線播報的內容。
- 將 `MinSize` 設為窗格內容仍能清楚閱讀的最窄寬度；僅當更寬的窗格會浪費空間時才設定 `MaxSize`，例如程式碼窗格在 800 px 處停止。
- 讓元件的高度是確定的，由頁面外殼決定。放在自動調整大小列中的元件沒有可分配的高度，將不顯示任何內容。
- 在拖曳結束後儲存 `Sizes`，而不是在每次指標移動時儲存；檢視模型載入時從已保存的設定中設定它。

**避免**

- 摺疊包含主要操作的窗格。找不到還原標籤的使用者將無法再次執行該操作，因此應將主要操作放在保持展開的窗格中。
- 為一個隱藏後會遺漏使用者需要注意的資訊的窗格設定 `IsCollapsible`，例如錯誤清單或待核准事項。
- 在元件使用期間更改窗格集合。元件是為固定的窗格集合建置的，來自另一集合的已保存版面配置無法描述它。
- 在窄窗格中巢狀放置元件，因為外層與內層的最小值會爭奪同樣少的像素。
- 期望垂直分割。元件只支援水平分割，因此堆疊的部分應放在元件之外建置。

### 常見問題

**為什麼元件什麼都不顯示？** 它需要父層提供確定的高度。在 `StackPanel` 或自動調整大小的列中，元件沒有可分配的高度，因此請設定 `Height`，或將其放入高度為 star 的 `Grid` 列中。

**為什麼拖曳分隔線時我的窗格沒有摺疊？** 只有 `IsCollapsible="True"` 的窗格才能摺疊。拖曳只有在超過摺疊門檻後才會摺疊窗格；較短的拖曳會停在窗格的 `MinSize` 處並顯示橡皮筋效果。

**如何從工具列按鈕隱藏窗格？** 將窗格的 `IsCollapsed` 繫結到檢視模型屬性，並修改該屬性。設為 `True` 會透過分隔線摺疊窗格，設為 `False` 則還原。當使用者用滑鼠或鍵盤摺疊窗格時，同一繫結也會收到變化。

**窗格隱藏時 `Sizes` 包含什麼？** 隱藏窗格的值為 0，其餘窗格分享 100%。

## API 參考

<!-- api: ResizablePanels -->

<!-- api: ResizablePanel -->

| 事件引數成員 | 類型 | 意義 |
|---|---|---|
| `ResizablePanelsLayoutChangedEventArgs.Sizes` | `IReadOnlyList<double>` | 每個窗格佔元件的百分比份額；隱藏的窗格為 0。 |

## 無障礙

- `Label` 設定元件的無障礙名稱，並以群組的形式播報。
- 每個分隔線都是可聚焦的滑桿。其值為窗格的佔比（百分比），其範圍為該窗格對能夠達到的佔比限制。隱藏的窗格會附帶「按 Enter 顯示」的提示。
- 分隔線使用其前一個窗格的標籤；若標籤為空，則使用該窗格的 `Id`。
- 摺疊的窗格會從點擊測試與 Tab 順序中移除，並在無障礙樹的控制項檢視中隱藏。
- 左右方向鍵以小步幅調整分隔線大小；按住 Shift 加方向鍵則以大步幅調整。
- Home 與 End 將分隔線移到最小與最大位置。Enter 隱藏或還原可摺疊的相鄰窗格。
- 指標拖曳過程中按 `Escape`，會將分隔線返回到拖曳開始的位置。

### 鍵盤

每個分隔線都是一個 Tab 停駐點。以下按鍵在分隔線取得焦點且沒有指標拖曳進行時生效。

| 按鍵 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 移到下一個或上一個分隔線或可聚焦控制項 |
| `Left` / `Right` | 將分隔線移動 16 px。到達限制時，若該側的窗格可摺疊，則將其摺疊 |
| `Shift` + `Left` / `Right` | 將分隔線移動 64 px |
| `Left` / `Right` 朝向隱藏的窗格 | 還原被隱藏的窗格 |
| `Home` / `End` | 將分隔線移到最小或最大位置。相鄰窗格被隱藏時忽略 |
| `Enter` | 還原被隱藏的相鄰窗格。否則，若分隔線前的窗格可摺疊則摺疊它，否則摺疊其後的窗格 |
| `Escape`（拖曳過程中） | 將分隔線返回到拖曳開始的位置 |

## 動態效果

- 拖曳直接跟隨指標。超過窗格的最小值後，分隔線會以短促的橡皮筋效果產生阻力。
- 放開時，窗格會以保留指標速度的彈簧穩定下來。將可摺疊的窗格拖到其最小值之外較遠處，或朝邊緣甩動，即可摺疊它。
- 鍵盤調整大小使用靈敏的彈簧。
- 窗格摺疊時，還原標籤彈出，其內容隨窗格收縮而淡出。
- 減少動態效果時，窗格直接移動到目標位置，還原標籤只淡入。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 指標拖曳 | 分隔線跟隨指標，越過限制時出現橡皮筋效果 | 同上 | 同上 |
| 放開、拖曳摺疊或還原 | 保留指標速度的平滑彈簧 | 窗格直接跳到目標位置 | 窗格直接跳到目標位置 |
| 鍵盤步進 | 靈敏的彈簧 | 窗格直接跳到目標位置 | 窗格直接跳到目標位置 |
| 連按兩下重設 | 平滑彈簧回到預設佔比 | 窗格直接跳到目標位置 | 窗格直接跳到目標位置 |
| 還原標籤出現 | 以靈敏的彈簧從 60% 縮放並淡入 | 僅淡入 | 立即完成 |
| 還原標籤消失 | 淡出並縮放到 60% | 僅淡出 | 立即完成 |
| 懸停或聚焦時握把出現 | 以類似 CSS 的彈簧從 60% 縮放 | 立即完成 | 立即完成 |

## 相關元件

- `Drawer`：用於以覆蓋層形式蓋住頁面的窗格。
- `Tabs`：用於每次只顯示一個窗格的場景，或窄版面配置。
- `ScrollArea`：用於溢出窗格的內容。

| 元件 | 適用情境 |
|---|---|
| `Drawer` | 窗格從頁面上方滑出，並在使用者返回頁面前關閉 |
| `Tabs` | 使用者在多個檢視間切換，從不需要同時看到兩個，或視窗較窄 |
| `Accordion` | 段落縱向堆疊，一次展開一個或同時展開多個 |
| `ScrollArea` | 內容溢出窗格，需要自己的捲軸 |
