---
title: 浮動按鈕群組
summary: 將柔和按鈕分隔放置在安靜的托盤中，一個共用的反白區塊隨游標在按鈕之間變形移動，按下狀態則原地回落。
source-hash: b9e2bcc71894
---

<Demo name="MutedTray" />

## 何時使用

`FloatingButtonGroup` 把浮在其他內容之上的表面所需的操作放進一個小托盤裡：畫布、文件、圖表或地圖。它讓這些操作靠近所作用的內容，因此頁首可以留給導覽，畫面的焦點仍停留在表面本身。

- **白板或設計工具中的畫布操作**：復原、重做、分享和簡報浮在畫板之上。只顯示圖示並附上快速鍵提示，可以讓托盤保持窄小，名稱則由工具提示承載。
- **文件上的選取工具列**：在事故檢討或說明中心文章中，使用者選取一段文字後，粗體、斜體和連結會出現在文字上方。托盤讀起來像是位於文字之上的一層。
- **圖表旁的直向工具列**：選取、筆和橡皮擦在畫布邊緣直向排列。工具提示向右展開，避開繪圖區域。
- **分析圖表上的縮放控制項**：`ItemsSource` 把一組縮放等級（適應、50%、100%）轉為按鈕，因此清單保存在檢視模型中，托盤只負責顯示。
- **檢視開啟期間保持開啟的模式**：儀表板編輯器中的簡報，或長文件中的專注模式，透過 `IsChecked` 切換，開啟時保持淡淡的底色。

## 何時不用

- **需要保持選取的選擇，例如文字對齊或檢視模式**：用 `SegmentedControl`，它把選取的區段顯示為對某個問題的回答。單一的開關式模式可以在某個項目上使用 `IsChecked`。
- **一列放不下的過多操作**：溢出的操作用 `DropdownMenu`，或把少用的操作移入該表面的選單。換行或捲動的托盤很難掃視。
- **頁首或表單底部的相關操作組合**：用 `ButtonGroup`，它把按鈕連成一個帶框線的表面，並用分隔線隔開。
- **卡片或訊息上的精簡圖示操作，且標籤應在取得焦點時出現**：用 `ExpandingButtonGroup`。
- **單一操作**：用 `Button`。只有一個按鈕的托盤只會替它多加一層表面。

## 用法

每個按鈕都是一個 `FloatingButtonGroupItem`。為托盤設定 `Label`，它會成為無障礙名稱，例如「畫板操作」或「文字格式」。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:FloatingButtonGroup Label="畫板操作">
        <arc:FloatingButtonGroupItem Label="復原" Shortcut="⌘Z" IsIconOnly="True" />
        <arc:FloatingButtonGroupItem Label="重做" Shortcut="⇧⌘Z" IsIconOnly="True" />
        <arc:FloatingButtonGroupSeparator />
        <arc:FloatingButtonGroupItem Label="分享" />
    </arc:FloatingButtonGroup>
</UserControl>
```

`FloatingButtonGroup` 衍生自 Avalonia 的 `ItemsControl`，因此 `Items`、`ItemsSource`、`ItemsPanel` 以及無選取的項目模型，與任何項目控制項的用法一致。每個 `FloatingButtonGroupItem` 衍生自 Avalonia 的 `Button`：`Click`、`Command`、`CommandParameter`、`IsEnabled` 以及鍵盤啟動的行為都與按鈕相同。托盤額外提供高亮、鍵盤模型、版面配置（列或欄）、尺寸和表面變體。`FloatingButtonGroupSeparator` 是兩組按鈕之間的細分隔線。

### 結構

1. **托盤**（`FloatingButtonGroup`）：帶 1 px 邊緣的圓角表面。圓角半徑為 18 px，每側內距為 4 px。`Size="Small"` 時兩者分別縮小為 15 px 和 3 px。
2. **高亮**：按鈕後方的一個圓角形狀，使用高亮色繪製（前景色的 7%，深色模式下為 9%）。它是唯一在按鈕之間移動的元素。圓角半徑為 13 px，小尺寸時為 11 px。
3. **項目**（`FloatingButtonGroupItem`）：高 36 px 的按鈕（小尺寸時為 30 px），兩側各內縮 12 px（小尺寸時為 10 px）。只顯示圖示的項目是同樣高度的正方形。圓角半徑為 13 px，小尺寸時為 11 px。
4. **圖示**（`Icon`）：16 px 的 Lucide 圖形，線寬 1.75 px，位於標籤前 6 px 處。
5. **標籤**（`Label`）：項目的文字，使用托盤的次要文字顏色和中等字重。`IsIconOnly` 為 true 時隱藏。
6. **分隔線**（`FloatingButtonGroupSeparator`）：1 px 的線，長 18 px（小尺寸時為 15 px），兩側各留 2 px 間距。在欄中它是同樣長度的水平線。顏色為前景色的 12%，由主題中的 `RuleBrush` 設定。
7. **快速鍵提示**（`Shortcut`）：不繪製在按鈕上，而是出現在項目的工具提示中，位於標籤之後。

表面有兩種變體。`Muted`（預設）是前景色的半透明底色，淺色模式下為 4.5%，深色模式下為 6%，邊緣為 3%，在柔和的背景上無需陰影即可辨識。`Floating` 使用浮起表面的顏色、標準框線顏色和浮動陰影，因此它會浮在所覆蓋的內容之上。

### 變體與尺寸

根據托盤所在的層級選擇變體。位於內容旁邊的托盤使用 `Muted`。覆蓋在內容之上的托盤（例如選取工具列）可以使用 `Floating`，以表明它是獨立的一層。

```xml
<arc:FloatingButtonGroup Label="選取操作" Variant="Floating">
    <arc:FloatingButtonGroupItem Label="留言" Icon="{x:Static arc:ArcIcons.MessageSquare}" />
</arc:FloatingButtonGroup>
```

`Size` 可取 `Medium`（預設，36 px 按鈕）或 `Small`（30 px 按鈕）；`Large` 按 `Medium` 處理。在表格工具列或圖表角落等密集區域使用 `Small`；畫布上的按鈕是主要工具，需要更大的點擊範圍，應使用 `Medium`。

### 圖示與僅圖示項目

在程式碼中把 `Icon` 設為 24 × 24 的 Lucide 圖形，或在 XAML 中使用 `{x:Static arc:ArcIcons.Name}`。圖示以 16 px 繪製。項目上的 `IsIconOnly` 會隱藏其標籤；在托盤上設定 `IsIconOnly` 會讓每個項目都只顯示圖示。項目自身的設定優先於托盤的設定，因此單一文字按鈕可以留在僅圖示的托盤中。只有當每個操作都有使用者熟悉的圖示時（例如粗體、復原或連結），才在托盤上設定 `IsIconOnly`。

僅圖示的項目以及帶有 `Shortcut` 的項目，會在工具提示中顯示其標籤。僅圖示的項目仍需要 `Label`，因為它是無障礙名稱。`IsIconOnly` 對沒有 `Icon` 的項目不起作用：這樣的項目保留文字。

<Demo name="IconOnly" />

```csharp
bold.Icon = ArcIcons.Bold;
link.Icon = ArcIcons.Link;
```

### 標籤變化與保留寬度

標籤可以變化以確認某個操作，例如連結放入剪貼簿後，從「分享」變為「已複製」。把 `ReserveLabels` 設為該項目可能顯示的其他所有標籤。這樣按鈕會保持最寬標籤的寬度，文字變化時旁邊的按鈕不會移動。若沒有保留標籤，高亮會跟隨新的寬度。

```csharp
var share = new FloatingButtonGroupItem { Label = "分享", ReserveLabels = ["已複製"] };
share.Click += async (_, _) =>
{
    share.Label = "已複製";
    await Task.Delay(1600);
    share.Label = "分享";
};
```

確認標籤保持在一到兩個字以內，並在短暫停頓後還原原文（範例使用 1.6 秒）。

### 由資料產生項目與直向配置

`ItemsSource` 把一般資料轉為按鈕。每個項目的標籤是物件的文字（`ToString()`），除非項目容器已經有標籤。`Orientation="Vertical"` 把按鈕直向排成一欄，此時工具提示預設向右展開。設定 `TooltipPlacement` 可以為所有項目統一展開方向：`Top`、`Bottom`、`Left` 或 `Right`。列預設使用 `Top`。

<Demo name="FromItemsSource" />

<Demo name="Vertical" />

```xml
<arc:FloatingButtonGroup Label="縮放" Size="Small" ItemsSource="{Binding ZoomLevels}" />

<arc:FloatingButtonGroup Label="工具" Orientation="Vertical" IsIconOnly="True">
    <arc:FloatingButtonGroupItem Label="選取" Shortcut="V" />
    <arc:FloatingButtonGroupItem Label="筆" Shortcut="P" />
</arc:FloatingButtonGroup>
```

直向托盤透過上下方向鍵移動，分隔線會變為水平線。

### 選取項目

`IsChecked` 把一個項目變為保持開啟的模式：簡報、專注、顯示格線。選取的項目會在按鈕後保留淡淡的底色（開啟控制項的顏色取 12%，深色模式下為 16%），並以「開」或「關」向輔助技術回報其狀態。`IsChecked` 設為 `null`（預設）時，它就是沒有狀態的一般操作。

```xml
<arc:FloatingButtonGroupItem Label="簡報"
                             Icon="{x:Static arc:ArcIcons.Monitor}"
                             IsChecked="{Binding IsPresenting, Mode=TwoWay}" />
```

不要用 `IsChecked` 在多個選項之間做選擇；這種情況請用 `SegmentedControl`。

### 快速鍵提示與工具提示

`Shortcut` 只是提示，不是繫結。該字串顯示在工具提示中，`AutomationProperties.HelpText` 將其提供給螢幕閱讀器。分組本身從不處理按鍵。應在視窗或檢視上註冊該按鍵，並執行按鈕所執行的同一命令，這樣即使托盤沒有焦點，快速鍵也能生效。

```csharp
KeyBindings.Add(new KeyBinding
{
    Gesture = new KeyGesture(Key.Z, KeyModifiers.Control),
    Command = vm.UndoCommand
});
```

除非 `TooltipPlacement` 另有設定，否則工具提示會在列的上方、欄的右側展開。

### 事件

`ActionInvoked` 在項目自身的 `Click` 處理常式及其 `Command` 之後執行。`e.Item` 是被執行的按鈕，`e.Data` 是資料來源：對於內嵌項目是按鈕本身，對於 `ItemsSource` 則是資料物件。訂閱它可以在一處處理托盤的所有操作；每個按鈕自身的行為仍放在 `Click` 或 `Command` 中。

```csharp
board.ActionInvoked += (_, e) => log.Text = $"操作：{e.Item.Label}";
```

對於 `ItemsSource` 托盤，從 `e.Data` 讀取資料物件：

```csharp
zoom.ActionInvoked += (_, e) => viewModel.Zoom = (string)e.Data!;
```

### 繫結到檢視模型

白板編輯器把其操作公開為命令，並把呈現狀態保存為屬性。托盤直接繫結到它們，高亮、按下狀態和鍵盤模型都保留在控制項內部。

```csharp
public partial class BoardViewModel : ObservableObject
{
    private readonly IBoardHistory _history;

    public BoardViewModel(IBoardHistory history)
    {
        _history = history;
        ZoomLevels = new ObservableCollection<string> { "適應", "50%", "100%", "200%" };
    }

    [ObservableProperty]
    private bool _isPresenting;

    [ObservableProperty]
    private string _zoom = "適應";

    public ObservableCollection<string> ZoomLevels { get; }

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo() => _history.Undo();

    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void Redo() => _history.Redo();

    private bool CanUndo => _history.CanUndo;

    private bool CanRedo => _history.CanRedo;
}
```

```xml
<Grid RowDefinitions="Auto,*">
    <arc:FloatingButtonGroup Grid.Row="0" Label="畫板操作" HorizontalAlignment="Center">
        <arc:FloatingButtonGroupItem Label="復原"
                                     Icon="{x:Static arc:ArcIcons.Undo2}"
                                     IsIconOnly="True"
                                     Shortcut="⌘Z"
                                     Command="{Binding UndoCommand}" />
        <arc:FloatingButtonGroupItem Label="重做"
                                     Icon="{x:Static arc:ArcIcons.Redo2}"
                                     IsIconOnly="True"
                                     Shortcut="⇧⌘Z"
                                     Command="{Binding RedoCommand}" />
        <arc:FloatingButtonGroupSeparator />
        <arc:FloatingButtonGroupItem Label="簡報"
                                     Icon="{x:Static arc:ArcIcons.Monitor}"
                                     IsChecked="{Binding IsPresenting, Mode=TwoWay}" />
    </arc:FloatingButtonGroup>
    <arc:FloatingButtonGroup Grid.Row="1" Label="縮放" Size="Small"
                             HorizontalAlignment="Left"
                             ItemsSource="{Binding ZoomLevels}" />
</Grid>
```

- `IsChecked` 是雙向的：項目把狀態寫入 `IsPresenting`，檢視模型也可以改變它。
- `Command` 驅動操作項目。當 `CanExecute` 為 false 時，該項目會被停用：它淡化到 50% 不透明度，並退出 Tab 與方向鍵的路徑。
- 縮放托盤是單向的；它的選擇透過 `ActionInvoked` 傳到檢視模型，如上所示。

### 範例：檢討草稿上方的選取工具列

一個托盤位於事故檢討草稿的一段文字上方。使用者選取了關於憑證輪替的那句話，托盤提供行內格式和留言操作。格式項目只顯示圖示，因為使用者能透過圖示認出粗體、斜體和連結，每一項的工具提示中都帶有快速鍵提示。留言和引用保留文字標籤，因為它們的圖示不夠通用。托盤在文字上左側對齊，因此看起來像附著在選取範圍上。

![浮動選取工具列，包含格式與留言操作，位於檢討段落上方](images/SelectionToolbar.png)

```xml
<StackPanel Width="600" Spacing="14">
    <arc:FloatingButtonGroup Label="Selection actions"
                             HorizontalAlignment="Left"
                             Margin="24,0,0,0">
        <arc:FloatingButtonGroupItem Label="Bold"
                                     Icon="{x:Static arc:ArcIcons.Bold}"
                                     IsIconOnly="True"
                                     Shortcut="⌘B" />
        <arc:FloatingButtonGroupItem Label="Italic"
                                     Icon="{x:Static arc:ArcIcons.Italic}"
                                     IsIconOnly="True"
                                     Shortcut="⌘I" />
        <arc:FloatingButtonGroupItem Label="Link"
                                     Icon="{x:Static arc:ArcIcons.Link}"
                                     IsIconOnly="True"
                                     Shortcut="⌘K" />
        <arc:FloatingButtonGroupSeparator />
        <arc:FloatingButtonGroupItem Label="Comment"
                                     Icon="{x:Static arc:ArcIcons.MessageSquare}" />
        <arc:FloatingButtonGroupItem Label="Quote"
                                     Icon="{x:Static arc:ArcIcons.Quote}" />
    </arc:FloatingButtonGroup>
    <Border Padding="24"
            Background="{arc:ArcDesignTokenResource Surface}"
            BorderBrush="{arc:ArcDesignTokenResource Border}"
            BorderThickness="1"
            CornerRadius="{arc:ArcDesignTokenResource RadiusPanel}">
        <StackPanel Spacing="10">
            <TextBlock Text="Postmortem: checkout outage on March 12"
                       FontSize="18"
                       FontWeight="SemiBold"
                       Foreground="{arc:ArcDesignTokenResource Foreground}" />
            <TextBlock Text="A certificate rotation on the payment gateway stopped card authorizations for 47 minutes. About 1,200 orders were retried after service returned."
                       TextWrapping="Wrap"
                       LineHeight="22"
                       Foreground="{arc:ArcDesignTokenResource Foreground}" />
            <TextBlock Text="Owner: Priya Raman, Platform team. Follow-up due April 3, 2026."
                       FontSize="13"
                       Foreground="{arc:ArcDesignTokenResource TextMuted}" />
        </StackPanel>
    </Border>
</StackPanel>
```

### 文案規範

- **標籤**：一到兩個字，使用使用者可以執行的動詞或名詞：「復原」「分享」「簡報」。托盤的 `Label` 是整組的名詞，例如「畫板操作」或「縮放」。
- **僅圖示的標籤**：標籤就是無障礙名稱，因此必須說明完整的操作（「粗體」「插入連結」），而不是描述圖示的形狀。
- **快速鍵提示**：顯示平台所用的按鍵，符號與範例一致（⌘Z、⇧⌘Z）。提示只寫按鍵組合；操作名稱已由工具提示承載。
- **確認標籤**：一個簡短的詞（「已複製」），與操作相符。保留的確認標籤要短，讓按鈕寬度保持較小。

| 不要寫 | 改為 |
|---|---|
| 點選這裡復原上一次變更 | 復原 |
| share | 分享 |
| Ctrl + Z | ⌘Z（或應用程式執行時的平台按鍵） |
| 已複製到您的剪貼簿！ | 已複製 |

### 最佳做法

**建議**

- 把托盤放在它所作用的內容附近：選取範圍之上、畫布旁邊，或畫板頂端。使用者會在工作的地方尋找操作。
- 只在內容之上使用 `Floating`。放在頁面背景上的托盤看起來像一個凸起的面板，卻沒有可以讓它浮起的東西。
- 為每個僅圖示的項目設定 `Label`，有快速鍵時也設定 `Shortcut`。標籤提供無障礙名稱，快速鍵則讓使用者逐漸學會鍵盤操作路徑。
- 將命令繫結到項目，讓停用狀態由檢視模型決定。被停用的項目仍保持可見。
- 預先保留確認標籤，使相鄰的按鈕不會移動。

**避免**

- 用托盤承載需要保持選取的選擇。一組檢視模式的托盤會隱藏目前是哪一個；請用 `SegmentedControl`。
- 在一個托盤中放入超過六到七個操作。超過之後托盤難以掃視，溢出的操作應放入 `DropdownMenu`。
- 只在工具提示中展示快速鍵。分組本身不繫結按鍵，因此只顯示卻未註冊的快速鍵，會教使用者一個毫無作用的按鍵。
- 沒有理由就在同一列中混用僅圖示和帶標籤的項目。寬度不一會讓這一列參差不齊；應在托盤上設定 `IsIconOnly` 以保持一致，只在例外情況下使用項目層級的設定。

### 常見問題

**為什麼我的僅圖示項目把標籤顯示為文字？** 只有項目具有 `Icon` 時，`IsIconOnly` 才會生效。沒有圖示的項目一律顯示標籤，因此請加上圖示或移除 `IsIconOnly`。

**為什麼指標移出後，高亮仍留在某個按鈕上？** 指標在托盤內時，由它掌控高亮。指標離開後，若有鍵盤焦點，高亮會回到取得焦點的按鈕；否則逐漸淡出。

**項目的 `IsIconOnly` 與托盤的設定，哪個優先？** 當項目自身的值不為 `null` 時，以項目自身的值為準。

## API 參考

<!-- api: FloatingButtonGroup -->

### 項目與事件引數

`FloatingButtonGroupItem` 是托盤中的按鈕。它在繼承的 `Button` 成員之外增加了這些屬性：

| 屬性 | 類型 | 意義 |
|---|---|---|
| `Label` | `string` | 可見文字；僅顯示圖示時作為無障礙名稱。 |
| `Icon` | `Geometry?` | 16 px 的 Lucide 圖形，例如 `ArcIcons.Undo2`。 |
| `IsIconOnly` | `bool?` | 只顯示圖示。`null` 跟隨托盤的 `IsIconOnly`。 |
| `IsChecked` | `bool?` | 可開啟的模式。`null` 為一般操作。 |
| `Shortcut` | `string?` | 顯示在工具提示中的快速鍵提示。托盤不繫結按鍵。 |
| `ReserveLabels` | `IReadOnlyList<string>?` | 該項目可能顯示的其他標籤；按鈕保持最寬的寬度。 |

`FloatingButtonGroupActionEventArgs`（`ActionInvoked` 的引數）公開 `Item`（被執行的按鈕）和 `Data`（資料來源物件）。`FloatingButtonGroupVariant` 有兩個值：`Muted` 與 `Floating`。

## 無障礙

- 托盤具有工具列角色，名稱取自 `Label`。它會回報其方向。
- 托盤是 Tab 順序中的單一停駐點。焦點會回到上一次取得鍵盤焦點的按鈕。
- 每個項目都以其標籤命名，包括僅圖示的項目。快速鍵提示作為說明文字提供。
- 帶 `IsChecked` 的項目會回報切換狀態「開」或「關」。數值變化時會播報新的狀態。
- 分隔線會作為分隔線被播報，因此即使看不到視覺效果，也能感知分組。
- 停用的項目仍可閱讀，方向鍵會略過它們。它們保留標籤，因此使用者仍能得知該操作存在。
- 共用高亮只在指標懸停或鍵盤焦點時顯示，因此不會標記一個使用者未曾到達的按鈕。

### 鍵盤

托盤在 Tab 順序中只佔一個停駐點。方向鍵在按鈕之間移動高亮。

| 按鍵 | 操作 |
|---|---|
| `Tab` | 進入托盤，停在上次取得焦點的按鈕上，或第一個可用的按鈕。下一次 `Tab` 離開托盤。 |
| `Left` / `Right`（列）或 `Up` / `Down`（欄） | 移到下一個或上一個可用的按鈕。兩端循環。 |
| `Home` / `End` | 移到第一個或最後一個可用的按鈕。 |
| `Enter` / `Space` | 執行取得焦點的按鈕：先執行其 `Click` 和 `Command`，再執行 `ActionInvoked`。 |

## 動態效果

- 一個共用高亮滑向指標下方或鍵盤焦點所在的按鈕。當指標越過間隙或分隔線時它保持不動，因此高亮不會在按鈕之間閃爍。
- 托盤首次取得目標時，高亮直接落位，並從 94% 放大到 100%。指標離開托盤時，高亮淡出。
- 按下的按鈕會加深高亮，高亮隨之略為收縮。按鈕本身不縮放。
- 標籤變化在原位交叉淡化。帶有 `ReserveLabels` 的按鈕保持寬度；否則高亮跟隨新的寬度。
- 減少動態效果時，高亮以短暫淡入移動到目標位置，標籤交叉淡化且不發生位移。關閉動態效果時，所有元素立即到達最終位置。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 高亮移到另一個按鈕 | 高亮在位置和大小上滑動，採用變形彈簧 | 以短暫淡入跳到目標 | 立即完成 |
| 高亮首次出現在目標上 | 以俐落的彈簧從 94% 放大到 100% | 立即完成 | 立即完成 |
| 按鈕被按下 | 高亮加深，並以俐落的彈簧收縮到 96.5% | 立即完成 | 立即完成 |
| 標籤變化 | 原位交叉淡化，伴隨短暫的向上移動和柔和模糊 | 交叉淡化且不位移 | 立即完成 |

## 相關元件

- `ButtonGroup`：把相關操作連成一個帶分隔線的表面。
- `ExpandingButtonGroup`：精簡的圖示操作，焦點時展開標籤。
- `SegmentedControl`：保持選取的選擇。
- `Button`：單一操作。

| 元件 | 適用情境 |
|---|---|
| `ButtonGroup` | 相關操作應放在頁首或表單底部的同一個帶框線表面上 |
| `ExpandingButtonGroup` | 卡片或訊息上的精簡圖示操作，應在焦點時展示標籤 |
| `SegmentedControl` | 選擇會保持選取，例如檢視模式、對齊方式或期間 |
| `DropdownMenu` | 操作溢出托盤，可收在一個觸發器之後 |
| `SplitButton` | 一個主要操作在選單中有替代選項 |
| `Button` | 只有一個沒有狀態的操作 |
