---
title: 內容選單
summary: 將次要操作放在所選物件附近。
source-hash: d5bb9535fbfa
---

<Demo name="FileTile" />

## 何時使用

內容選單把一個物件的次要操作放在該物件旁邊。檔案卡片、儀表板卡片或表格中的一列負責呈現內容，當使用者需要時，操作會出現在指標處或目標下方。主要操作仍留在頁面的工具列中；內容選單只放使用者對單一項目執行的操作。

- **共用資料夾中的檔案與資產**：工作區資料庫中檔案卡片上的「複製連結」「重新命名」「移至資料夾」和「移至垃圾桶」。管理檔案的人很快就會熟悉這種手勢，而工具列則保留給上傳與新增資料夾。
- **分析儀表板中的卡片**：在「依地區的營收」這類指標卡片上提供「複製圖表」「匯出 CSV」和「從儀表板移除」。卡片本身不是按鈕，因此選單可以新增操作，而不必在每張卡片上都放一個可見的選單按鈕。
- **帳單或訂單主控台中的紀錄**：發票清單的一列上提供「複製發票編號」「重新寄送收據」和「作廢發票」。該列保留開啟紀錄的點擊目標，選單則用於較少見的操作。
- **開發者與記錄工具**：JSON 或記錄檢視器的節點上提供「複製值」「複製路徑」和「依此鍵篩選」。物件即是資料，操作也是針對資料的。
- **簡短、扁平且含一個破壞性列的清單**：對同一物件的少量操作，破壞性列放在最後，並以分隔線隔開。

## 何時不用

- **使用者必須不靠右鍵或點按就能找到的操作**：使用帶可見觸發器的 `DropdownMenu`。透過手勢開啟的選單容易被忽略，鍵盤與螢幕閱讀器使用者需要一個有標籤的控制項作為起點。
- **以觸控為主、透過滑動顯示操作的清單**：使用 `SwipeActions`，它與手機使用者在列上期望的滑動手勢一致。
- **目標需要自己的點擊**：在其旁邊使用 `Button`。對被包裹內容的一般點擊或點按也會開啟選單，因此點擊後開啟紀錄的卡片不應被包裹在 `ContextMenu` 中。
- **一個預設操作加幾個替代選項**：使用 `SplitButton`。它讓預設操作保持在一次點擊之內，並把替代選項放入其選單。
- **為表單提供選擇的清單**：使用 `Select`、`Combobox` 或 `RadioGroup`。它們承載值、標籤與驗證狀態，選單則不具備這些。
- **帳戶設定與登出操作**：使用 `UserMenu`，它位於頂列的大頭貼之後。

## 用法

`ContextMenu` 衍生自 Avalonia 的 `ContentControl`。內容即為目標：任何單一元素都可以，它保留自己的外觀與版面配置。控制項會加上一個選單表面（在 Context 變體中為 `MenuSurface`）、列集合 `Items`，以及在右鍵、ContextMenu 鍵、Shift+F10 和一般點擊或點按時開啟選單的連接邏輯。由於它是 `ContentControl`，常用的內容屬性（`Content`、`ContentTemplate`、`Padding`）都適用於目標。

以 `ContextMenu` 包裹目標並設定其 `Label`。內容即為目標。在程式碼中將列加入 `Items`。下方的程式片段中，`file` 即為該 `ContextMenu`。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:ContextMenu Label="Q3 report.pdf 的操作">
        <Border Width="220" Height="120" Padding="16" CornerRadius="18" BorderThickness="1">
            <TextBlock Text="Q3 report.pdf" />
        </Border>
    </arc:ContextMenu>
</UserControl>
```

```csharp
file.Items.Add(new MenuEntry { Header = "複製連結", Icon = ArcIcons.Copy });
file.Items.Add(new MenuEntry { Header = "刪除檔案", Icon = ArcIcons.Trash2, IsDestructive = true });
```

同樣的列也可以在 XAML 的 `<arc:ContextMenu.Items>` 下宣告，本頁的示範即是這樣撰寫的。

### 結構

1. **目標**（`Content`）：選單所屬的元素。主題將其背景設為透明並移除焦點裝飾，因此選單是對按下動作的唯一回應。應為目標提供它自己可見的形狀（框線、背景與圓角），如示範所示，讓使用者看到自己操作的對象。
2. **選單面板**（`Menu`）：承載各列的表面，以 `Label` 朗讀。在 Context 變體中，列高為 34 px，面板寬度至少為 208 px（13 rem），高度永遠不超過 300 px；較長的清單會在面板內捲動。
3. **圖示位**（`MenuEntry` 上的 `Icon`）：24 × 24 的 Lucide 幾何圖形，繪製為 16 px 描邊的圖示。沒有圖示的列在 `IsChecked` 為 true 時顯示勾號，為 false 時顯示低調的省略號，使每一列保持相同的左邊緣。
4. **標籤**（`Header`）：列的文字。過長的標籤以省略號結尾，而不是換行。`Header` 也是型別比對（typeahead）所比對的文字。
5. **快速鍵提示**（`InputGestureText` 或 `InputGesture`）：列末端低調的文字。它僅用於顯示；應用程式自己繫結按鍵。
6. **破壞性列**（`IsDestructive`）：文字與圖示採用危險色，被反白時反白底色也帶危險色調。
7. **分隔線與分組標籤**（`HasSeparatorBefore`、`MenuEntrySeparator`、`MenuEntryLabel`）：列上方的分隔線，或將其下方各列歸為一組、不可選取的標題。
8. **反白**：一個共用的反白覆蓋指標或鍵盤所在的列。各列不繪製自己的懸停填色。

### 處理選取

啟用一列會引發該列的 `Click` 事件、執行其 `Command`、引發選單的 `ItemClicked`，然後關閉選單，除非該列設定了 `StaysOpenOnClick`。`ItemClicked` 透過 `Entry` 回報被啟用的列，透過 `Item` 回報其背後的資料項目，並透過 `Header` 回報其標題。選單自身的 `Opened` 與 `Closed` 事件會在開啟與關閉動態效果前後觸發。

```csharp
file.ItemClicked += (_, e) => status.Text = $"Q3 report.pdf：{e.Header}";
```

當一個處理常式服務所有列時使用 `ItemClicked`；當某一列需要自己的邏輯時，使用該列的 `Click` 事件。處理常式應保持簡短。選單會在處理常式返回後關閉，因此耗時的工作應放在命令中，或由處理常式啟動的非同步方法中。

### 勾選列

沒有圖示的列在 `IsChecked` 為 true 時顯示勾號。當該列是開關時，設定 `ToggleType="CheckBox"`：啟用時由控制項替你切換 `IsChecked`，列上會顯示勾號。下方範例中的列是一般列，由處理常式自行切換。

```csharp
var pinned = new MenuEntry { Header = "已釘選", IsChecked = true };
pinned.Click += (_, _) => pinned.IsChecked = !pinned.IsChecked;
image.Items.Add(pinned);
```

兩者擇一，即處理常式或 `ToggleType`，不要同時使用，否則值會翻轉兩次。當需要在變更多個選項時保持選單開啟，則設定 `StaysOpenOnClick="True"`。

### 資料驅動的列

`ItemsSource` 會為每個項目建立一列。`MenuEntry` 執行個體會按原樣使用；其他資料需要 `ItemTemplate`，由它呈現每個產生之列的標題。應在位於頁面樹狀結構中的 `ContextMenu` 本身上繫結集合，並在 `ItemClicked` 中讀取資料項目。

```xml
<arc:ContextMenu Label="檔案操作" ItemsSource="{Binding FileActions}">
    <arc:ContextMenu.ItemTemplate>
        <DataTemplate>
            <TextBlock Text="{Binding Name}" />
        </DataTemplate>
    </arc:ContextMenu.ItemTemplate>
    <Border Width="220" Height="96" CornerRadius="18" BorderThickness="1" />
</arc:ContextMenu>
```

### 為任意控制項附加選單

`arc:ContextMenu.Menu` 無需包裹即可為任意控制項提供右鍵選單。除非設定了 `Variant`，附加的表面會成為 Context 選單；除非設定了 `ClosesOnViewportChange`，它會在捲動或調整大小時關閉。當元素已有範本，或你不希望變更其父層時，使用這種寫法。

<Demo name="Attached" />

```xml
<Border Width="220" Height="96" CornerRadius="18" BorderThickness="1">
    <arc:ContextMenu.Menu>
        <arc:MenuSurface>
            <arc:MenuEntry Header="新增資料夾" Icon="{x:Static arc:ArcIcons.Folder}" />
            <arc:MenuEntry Header="貼上" Icon="{x:Static arc:ArcIcons.Copy}" InputGestureText="⌘V" />
            <arc:MenuEntry Header="重新整理" Icon="{x:Static arc:ArcIcons.RefreshCw}" />
        </arc:MenuSurface>
    </arc:ContextMenu.Menu>
    <TextBlock Text="在此處按右鍵" HorizontalAlignment="Center" VerticalAlignment="Center" />
</Border>
```

### 透過程式碼開啟與關閉

`Open` 會在目標下方顯示選單。傳入目標座標系中的一個點，可在該位置開啟選單，例如在畫布中使用者點擊的位置。`Close` 會關閉選單，且不移動焦點。`IsOpen` 是雙向的，因此檢視模型可以透過設定它來開啟選單。

```csharp
file.Open(); // 在目標下方
file.Open(new Point(120, 40), MenuOpenFocus.First); // 在某一點，並反白第一列
file.Close();
```

只有當目標已附加到視窗後，`IsOpen` 才會開啟選單。在此之前將其設為 `True` 不會產生可見的效果，因此應在頁面載入後再設定。

### 繫結到檢視模型

內容選單通常作用於集合中的某個項目，因此檢視模型擁有項目與命令，處理常式把被啟用的列轉交給命令。選單負責顯示項目；執行則由檢視模型完成。

```csharp
public sealed record FileAction(string Name, bool IsDestructive);

public partial class FileListViewModel : ObservableObject
{
    public ObservableCollection<FileAction> FileActions { get; } =
    [
        new FileAction("複製連結", IsDestructive: false),
        new FileAction("移至垃圾桶", IsDestructive: true)
    ];

    [RelayCommand]
    private void RunFileAction(FileAction action)
    {
        // 對此選單所屬的檔案執行該動作。
    }
}
```

```csharp
file.ItemClicked += (_, e) =>
{
    if (e.Item is FileAction action)
    {
        viewModel.RunFileActionCommand.Execute(action);
    }
};
```

在 XAML 中繫結 `ItemsSource`（見資料驅動的列），並如上所示透過 `ItemClicked` 轉交被啟用的項目。對於需要命令的內嵌宣告列，應在程式碼中建立 `MenuEntry` 時設定其 `Command`。檢視模型使用 CommunityToolkit.Mvvm 的 `ObservableObject` 與 `[RelayCommand]`，且不持有任何 UI 型別。

### 焦點與關閉

選單會在使用者操作的位置開啟。右鍵在指標處開啟選單。點擊或點按目標，或在已取得焦點的目標上按 ContextMenu 鍵或 Shift+F10，則會在目標下方開啟選單，距其左邊緣 12 px、距其下邊緣 8 px。以指標開啟時，焦點位於選單表面上，且沒有反白的列；以鍵盤開啟時，反白會落在第一列上。

選單會關閉而不是跟隨。它會在外部按下、任何祖先捲動檢視器的捲動、視窗大小調整，以及目標離開視覺樹時關閉。選單開啟期間在目標上按下主要按鈕會將其關閉，隨後的點擊不會再次開啟它。按 Escape 或 Tab 會關閉選單並將焦點交還目標。當選單靠近視窗邊緣時，Avalonia 的定位會將其翻轉到另一側。

### 範例：營收儀表板卡片

儀表板卡片帶有四個操作，破壞性操作位於最後，並由分隔線隔開。卡片保留自己的框線與標題，因此選單仍屬於該物件，頁面的工具列則保留頁面層級的操作。標籤寫明圖表名稱，因為螢幕閱讀器只會朗讀目標的名稱。「釘選到首頁」列是一個切換項目，因此它的勾選狀態會向輔助技術播報。快速鍵提示僅用於顯示；應用程式必須自行繫結該按鍵。

```xml
<arc:ContextMenu Label="Revenue by region 的操作">
    <arc:ContextMenu.Items>
        <arc:MenuEntry Header="複製圖表"
                       Icon="{x:Static arc:ArcIcons.Copy}"
                       InputGestureText="⌘D" />
        <arc:MenuEntry Header="匯出 CSV" Icon="{x:Static arc:ArcIcons.Download}" />
        <arc:MenuEntry Header="釘選到首頁"
                       ToggleType="CheckBox"
                       IsChecked="True" />
        <arc:MenuEntry Header="從儀表板移除"
                       Icon="{x:Static arc:ArcIcons.Trash2}"
                       IsDestructive="True"
                       HasSeparatorBefore="True" />
    </arc:ContextMenu.Items>
    <Border Width="300"
            Padding="16"
            CornerRadius="16"
            BorderThickness="1"
            Background="{arc:ArcDesignTokenResource SurfaceMuted}"
            BorderBrush="{arc:ArcDesignTokenResource BorderSubtle}">
        <StackPanel Spacing="6">
            <TextBlock Text="依地區的營收"
                       FontSize="14"
                       FontWeight="Medium"
                       Foreground="{arc:ArcDesignTokenResource Foreground}" />
            <TextBlock Text="$1.84M"
                       FontSize="26"
                       FontWeight="SemiBold"
                       Foreground="{arc:ArcDesignTokenResource Foreground}" />
            <TextBlock Text="較 2026 年第二季成長 12.6%"
                       FontSize="12"
                       Foreground="{arc:ArcDesignTokenResource TextMuted}" />
        </StackPanel>
    </Border>
</arc:ContextMenu>
```

### 文案規範

各列都是簡短的命令，選單只需一眼就能讀懂，因此每一列都應寫成「動詞加物件」。`Label` 應保持具體，因為它是螢幕閱讀器給目標的唯一名稱。

- 列的標籤採用句首大寫的寫法，動詞在前：「複製連結」，而不是「連結」。
- 破壞性列要寫明物件，讓使用者知道會刪除什麼：檔案用「移至垃圾桶」，卡片用「從儀表板移除」。
- 每列控制在約 24 個字元以內。較長的標籤會以省略號結尾，因此重要的字詞應放在前面。
- 列尾不要加冒號或句號，標籤文字中也不要放快速鍵；快速鍵應使用 `InputGestureText`。
- 動作使用產品自身的用詞，例如產品會作廢發票時，就用「作廢發票」。

| 不要寫 | 改為 |
|---|---|
| 連結 | 複製連結 |
| 刪除 | 刪除檔案 |
| 操作 | Q3 report.pdf 的操作 |
| 移除 | 移至垃圾桶 |
| 按這裡匯出 | 匯出 CSV |
| 複製（Ctrl+D） | 複製圖表，快速鍵放在 `InputGestureText` 中 |

### 最佳做法

**建議**

- 將清單保持在三到七列。較長的清單應放在帶可見觸發器的 `DropdownMenu` 中，或放在頁面上。
- 為目標提供可見的形狀，例如框線、背景或圓角。目標本身沒有外框，因此選單是唯一的回饋，使用者需要看到自己操作的對象。
- 將破壞性列放在最後，並設定 `HasSeparatorBefore="True"`，避免誤觸。
- 同時在可見的控制項上提供重要操作。右鍵、點按目標與 Shift+F10 都不易被發現，觸控與鍵盤使用者需要一個有標籤的入口。
- 以 `Label` 依物件為目標命名，讓螢幕閱讀器播報這些操作所屬的對象。

**避免**

- 包裹一個需要自己點擊的目標。一般點擊或點按也會開啟選單，目標的操作與選單會爭奪同一次按下。當目標只需要右鍵選單時，將 `OpensOnClick` 設為 `False`。
- 依賴換行的長標籤。列會把長標籤以省略號截斷，因此含義可能被截掉。
- 只用顏色標示破壞性列。該列只靠危險色標示，因此其 `Header` 必須寫明將被移除的內容。
- 期望選單在頁面捲動時跟隨目標。它會在捲動時關閉，使用者需要重新開啟。
- 把通往某個操作的唯一路徑放在選單裡。對最重要的操作，應保留一個可見的入口。

### 常見問題

**如何阻止左鍵點擊開啟選單？** `OpensOnClick` 控制一般點擊與點按。在 `ContextMenu` 上將其設為 `False`，即可保留右鍵、ContextMenu 鍵與 Shift+F10。預設值為 `True`。

**Avalonia 也有一個 `ContextMenu` 型別，如何區分？** 兩個命名空間都定義了名為 `ContextMenu` 的型別。當檔案同時使用 `Avalonia.Controls` 與 `TKCode.Arc` 時，請加上別名，例如 `using ArcContextMenu = TKCode.Arc.ContextMenu;`，並在程式碼中使用該別名。在 XAML 中，`arc:` 前綴已經指定了 Arc 型別。

**如何在不包裹控制項的情況下為它加上選單？** 在該控制項上將 `arc:ContextMenu.Menu` 設為一個 `MenuSurface`（見為任意控制項附加選單）。除非你自己設定其變體，否則該表面採用 Context 變體。

**能否在使用者點擊的位置開啟選單？** 可以。在目標座標系中傳入一個點呼叫 `Open`，例如在指標處理常式中呼叫 `file.Open(e.GetPosition(file))`。不傳點時，選單在目標下方開啟。

**為什麼只有一列顯示勾號，其他列沒有？** 沒有圖示的列在 `IsChecked` 為 true 時顯示勾號，為 false 時顯示省略號，因此各列保持同一左邊緣。有圖示的列則顯示圖示。若某列的狀態應在啟用時切換，請在該列上設定 `ToggleType="CheckBox"`。

## API 參考

<!-- api: ContextMenu -->

<!-- api: MenuEntry -->

## 無障礙

- 目標可以取得焦點，選單以 `Label` 朗讀，該標籤同時為目標與選單命名。
- 目標會暴露一個可展開與收合的選單，因此輔助技術能回報選單是否開啟。
- 選單列具有選單項目角色。Enter 與 Space 選取一列，Escape 關閉選單並將焦點交還目標。
- Shift+F10 與 ContextMenu 鍵可在已取得焦點的目標下方開啟選單，並反白第一列。
- 沒有圖示的已勾選列會顯示勾號，但勾選狀態本身不會被播報。使用 `ToggleType="CheckBox"` 或 `ToggleType="Radio"` 的列會向輔助技術公開其狀態，因此狀態重要的列應使用切換類型，或把狀態寫進 `Header`。
- 破壞性列只以顏色標示，因此應在 `Header` 中寫明將被移除的內容。
- `Label` 應針對物件寫得具體，例如「Q3 report.pdf 的操作」。它是螢幕閱讀器給目標的唯一名稱。

### 鍵盤

已取得焦點的目標可以透過鍵盤開啟選單。選單開啟後，各列遵循選單的鍵盤模型。

| 按鍵 | 操作 |
|---|---|
| ContextMenu 鍵、`Shift+F10` | 在已取得焦點的目標下方開啟選單，並反白第一列 |
| `ArrowDown` / `ArrowUp` | 將反白移到下一列或上一列；清單會循環，並略過停用的列 |
| `Home` / `End` | 將反白移到第一列或最後一列 |
| 字母鍵 | 型別比對會移到文字以所輸入字母開頭的列 |
| `Enter` / `Space` | 啟用反白的列，然後關閉選單，除非該列保持開啟 |
| `Escape` / `Tab` | 關閉選單並將焦點交還目標 |

## 動態效果

- 右鍵時，選單以快速彈簧從指標處展開，並伴隨短暫的淡入。選單列以短促的錯開方式依序淡入。
- 點擊、點按或 Shift+F10 會在目標下方以相同的動態效果開啟選單。
- 捲動或調整大小會關閉選單，選單不會隨目標移動。
- 減少動態效果時去掉縮放與滑動，僅保留快速淡入。關閉動態效果時，選單立即顯示或隱藏。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 右鍵開啟 | 選單從指標處展開，以靈敏的彈簧從 0.96 放大到完整尺寸，並伴隨 160 ms 淡入 | 快速淡入，無縮放 | 立即完成 |
| 點擊、點按或 Shift+F10 開啟 | 選單從目標下方的一點展開，使用相同的彈簧與淡入 | 快速淡入 | 立即完成 |
| 選單關閉 | 選單縮放到 0.98 並在 120 ms 內淡出 | 快速淡入 | 立即完成 |
| 反白移動 | 指標反白以靈敏的彈簧滑動；鍵盤反白直接跳轉 | 反白直接跳轉 | 立即完成 |
| 列出現 | 每列在 240 ms 內淡入，前五列之間錯開 35 ms | 快速淡入 | 立即完成 |
| 列被按下 | 該列著色，不縮放 | 該列著色 | 立即完成 |

## 相關元件

- 操作需要使用者能找到的可見觸發器時，使用 `DropdownMenu`。
- 以觸控為主、透過滑動顯示操作的列，使用 `SwipeActions`。
- 一個操作為預設、選單承載替代選項時，使用 `SplitButton`。
- `MenuSurface` 是共用的選單彈出層，用於將選單錨定到任意控制項。

| 元件 | 適用情境 |
|---|---|
| `DropdownMenu` | 操作需要一個使用者能看到並找到、帶標籤的觸發器 |
| `SwipeActions` | 以觸控為主的列，透過滑動顯示其操作 |
| `SplitButton` | 一個操作為預設，選單承載替代選項 |
| `UserMenu` | 選單屬於頂列中大頭貼背後的帳戶 |
| `Dialog` | 破壞性列在移除任何內容之前必須先經過確認 |
| `MenuSurface` | 需要為控制項提供選單，但不使用右鍵手勢，或從程式碼開啟 |
