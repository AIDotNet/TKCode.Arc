---
title: 底部面板
summary: 停駐在預覽高度或完整高度的面板，跟隨手指移動。
source-hash: c4dc8fc6229b
---

<Demo name="Detents" />

## 何時使用

`BottomSheet` 是一個模態面板，從視窗底邊升起，並停駐在一或多個高度上，這些高度稱為停駐點（detent）。它適合行動裝置優先畫面中的次要任務：使用者先看到一段有用的預覽內容，只有在需要更多時才展開面板。它以遮罩覆蓋在頁面之上，因此是流程中一個聚焦的步驟，而不是第二層導覽。

- **結帳時的配送與取貨選項**：手機上的買家從預覽中看到最便宜的選項，為訂單 4417 選擇配送時段或取貨點，然後展開查看完整的貨運業者與時間清單。
- **商品或訂單清單上的篩選**：商店應用程式在預覽中顯示已啟用的篩選數量，並讓使用者在完整高度下調整篩選，無需離開正在捲動的清單。
- **分享與匯出選項**：平板上的儀表板提供「複製連結」「透過電子郵件傳送」和「匯出為 PDF」，放在一個簡短的面板中，使用者可以向下輕滑將其關閉。
- **地圖與媒體檢視**：配送追蹤地圖在預覽中顯示快遞員的預計到達時間，使用者向上拖曳面板閱讀停靠站清單，地圖則保持在面板後方可見。
- **行程或預訂詳細資料**：行程以預覽開啟，首日可見，讓使用者知道預訂存在，並可展開閱讀其餘內容。

## 何時不用

- **需要打斷整個任務的決定**，例如刪除專案或取消訂閱：用 `Dialog`，它置中顯示，並把「取消」與破壞性操作保留在同一個可見面板中。
- **寬螢幕版面中需要與清單並排可見的側邊面板**：在右側或左側邊緣使用 `Drawer`。
- **錨定在某個控制項上的小塊內容**，例如欄位選擇器或色彩色塊格：用 `Popover`，它指向觸發器，且不會壓暗頁面。
- **圖示按鈕的一行提示**：用 `Tooltip`。面板對於一個標籤來說太重了。
- **指令清單**：用 `DropdownMenu` 或 `ContextMenu`，它們讓指令與觸發它們的控制項保持在一起。

## 用法

`BottomSheet` 衍生自 `ModalOverlay`，而 `ModalOverlay` 本身是一個 `ContentControl`。它的 `Content` 是可捲動的內文，`Title` 與 `Description` 構成頁首，`Actions` 接收可選的頁尾。控制項在宣告的位置不轉譯任何內容；當 `IsOpen` 為 true 時，面板會顯示在視窗的覆蓋層上，帶有遮罩、焦點陷阱、Escape 與點選外部區域的關閉方式，並且在面板移除之前完成退場動畫。面板還加入下面描述的停駐點、拖曳與捲動規則。

### 基本面板

```xml
<arc:BottomSheet Title="里斯本之旅"
                 Description="3 晚 · 6 月 12 日至 15 日"
                 IsOpen="{Binding ShowTrip}">
    <StackPanel Spacing="12">
        <TextBlock Text="週五 · 14:05 抵達 LIS，搭計程車前往 Alfama" />
        <TextBlock Text="週六 · 28 號電車與城堡" />
    </StackPanel>
</arc:BottomSheet>
```

### 結構

1. **拖曳把手**：一個按鈕，在 36 × 5 px 的橫條外圍有 56 × 20 px 的點選區域。橫條使用邊框強調色，懸停及面板被拖曳時變為弱化文字顏色。它的無障礙名稱在較小的停駐點時為「展開面板」，在最高的停駐點時為「收合面板」。
2. **頁首**（`Title`、`Description`）：兩側 20 px 內距，上方 8 px、下方 16 px（窄螢幕時兩側為 16 px）。標題使用大號字級、中等字重，顏色為前景色。說明使用小號字級、次要文字顏色，位於標題下方 2 px，為空時隱藏。
3. **關閉按鈕**（`ShowCloseButton`、`CloseButtonName`）：位於頁首右側的 32 px 全圓角按鈕，帶 1 px 邊框與弱化的表面填色。預設顯示。
4. **頁首分隔線**：頁首下方一條邊框顏色的 1 px 線。只有內容捲動到頁首下方（`:scrolled`）時才會出現。
5. **內文**（`Content`）：可捲動區域，兩側 20 px 內距，上方 4 px、下方 24 px（窄螢幕時兩側為 16 px）。捲軸只在最高的停駐點出現。停用水平捲動。
6. **操作區**（`Actions`）：可選的頁尾，位於可捲動的內文內，靠右對齊，位於內容下方 20 px。`Actions` 為 null 時隱藏。
7. **表面**：凸起表面背景，頂部與側邊有 1 px 邊框、底部沒有邊框，頂部兩角使用表面圓角，並帶浮動陰影。最寬 576 px（36 rem），置中，並向視窗邊緣以下延伸 160 px，因此向上拉伸時不會露出縫隙。
8. **遮罩**：位於面板後方的壓暗層，使用面板自己的遮罩顏色（淺色與深色兩套）。遮罩不做模糊，其不透明度隨面板位置變化。

### 選擇停駐點

`Detents` 以視窗高度的比例列出停駐高度。預設值為 0.45 與 0.92，即預覽與近乎完整的檢視。數值會被排序、去除重複項；大於 1 的值會被限制為 1，零或負值會被忽略。只有一個值時，面板高度固定。預覽高度應完整顯示第一條有用的內容，因為使用者正是根據這段預覽做決定的。當面板後方的頁面仍需保留上下文時，把最高的停駐點設在完整視窗高度以下；只有行程或媒體檢視器這類全螢幕閱讀場景，才使用 0.92 或更高的值。

`BottomSheetDetents.Parse` 讀取以逗號或空格分隔的字串，因此清單可以來自設定或資源。`SnapPoints` 是 `Detents` 的別名，供使用該名稱的程式碼使用。`EffectiveDetents` 回傳實際使用的排序後數值，適合記錄日誌與測試。

<Demo name="ThreeDetents" />

```csharp
share.Detents = BottomSheetDetents.Parse("0.3, 0.6, 0.95");
share.InitialDetentIndex = 1;
```

三個停駐點適合分享或匯出面板：一個較小的預覽用於快捷操作，一個中間停駐點用於較短的清單，一個完整停駐點用於預覽。面板開啟期間修改 `Detents` 時，會保留相同的索引並限制在新清單範圍內，因此較短的清單可能把面板移到更低的停駐點；面板保持開啟。

### 開啟並在停駐點之間移動

`InitialDetentIndex` 選擇面板開啟時所在的停駐點。預設為 0，即最小的停駐點。該索引指向排序後的清單，並限制在清單範圍內。`DetentIndex` 回報面板目前停駐的停駐點；它是唯讀的，因此請透過 `SnapTo`、`Expand` 與 `Collapse` 來改變它。

`OpenCommand` 可在不設定 `IsOpen` 的情況下從按鈕開啟面板。`Expand` 將面板移到最高的停駐點，`Collapse` 移到最小的停駐點，`SnapTo` 移到任意索引。面板關閉時 `SnapTo` 不會有作用。每當面板停駐到另一個停駐點時都會引發 `DetentChanged`；拖曳過程中不會引發。

```csharp
openTrip.Command = trip.OpenCommand;

trip.DetentChanged += (_, e) => detent.Text = $"停在 {e.Fraction:P0}";
trip.Expand();
```

### 長內容

內容不做虛擬化：面板開啟時會建立內文中的每一項。長清單請保持分頁，或在面板內放置一個自行虛擬化項目的控制項，例如 `ListBox`。低於最高的停駐點時，內容不捲動。向下捲動滾輪、在內容上觸控拖曳、Tab 進入首屏以下的內容，或按下捲動鍵，都會先把面板完全展開。收合面板會讓內容回到頂部，因此預覽始終顯示內容的開頭。

```xml
<arc:BottomSheet Title="行程安排" IsOpen="{Binding ShowItinerary}">
    <ItemsControl ItemsSource="{Binding Items}" />
</arc:BottomSheet>
```

### 關閉

`Closed` 透過 `OverlayClosedEventArgs.Reason` 回報面板關閉的原因。快速滑動或向下拖曳超過最小的停駐點會回報 `Gesture`，Escape 回報 `Escape`，按下遮罩回報 `PressOutside`，關閉按鈕回報 `CloseButton`，設定了結果的按鈕回報 `Action`。使用者關閉之前會引發 `Closing`，可以透過 `e.Cancel` 取消。

```csharp
trip.Closed += (_, e) => detent.Text = $"關閉方式：{e.Reason}";
```

### 拖曳、快速滑動與橡皮筋

整個頁首都是拖曳把手，因此在內容上方的任何位置按下都可以開始拖曳。在內容上觸控拖曳會移動面板，直到它完全展開；之後內容開始捲動，而從內容頂部向下拉會再次移動面板。面板與指標一對一跟隨。超過最高的停駐點後，面板會以最多 120 px 的橡皮筋效果拉伸。放開時，面板會依速度投射 0.2 秒的位移，停駐到最近的停駐點；若投射位置落在最小的停駐點以下，則關閉。放開時速度超過 320 px/s 的快速滑動，一律會使面板至少移動一個停駐點，因此快速輕滑永遠不會被忽略。在拖曳把手上單擊會在最小與最高的停駐點之間切換。

在減少動態效果和關閉動態效果時會停用拖曳。這些情況下請使用拖曳把手、鍵盤或 `SnapTo`，並為所有使用者保留關閉按鈕。

### 焦點與關閉

焦點首先落在面板本身，因此螢幕閱讀器會先播報它的標題。面板為最上層的模態時，Tab 與 Shift+Tab 停留在面板內；面板關閉時，焦點回到開啟它的控制項。當 `CloseOnEscape` 為 true（預設值）時，Escape 關閉面板；當 `CloseOnOutsidePress` 為 true 時，按下遮罩關閉面板。如果頁面必須保持可互動，請將 `IsModal` 設為 false：此時遮罩與焦點陷阱都會被移除。面板堆疊時，只有最上層的面板回應 Escape。

### 繫結到檢視模型

`IsOpen` 預設為雙向繫結，因此透過手勢或 Escape 關閉面板時，會把 `false` 寫回檢視模型。`DetentIndex` 是唯讀的，因此檢視模型透過 `DetentChanged` 事件接收停駐點的變化，並把它們儲存到自己的屬性中。編譯繫結需要在檢視上設定 `x:DataType`；範例為求簡潔省略了它。

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

public interface IOrderService
{
    Task SetDeliverySlotAsync(string slotId);
}

public partial class DeliveryViewModel : ObservableObject
{
    private readonly IOrderService _orders;

    public DeliveryViewModel(IOrderService orders) => _orders = orders;

    [ObservableProperty]
    private bool _showOptions;

    [ObservableProperty]
    private int _restingDetent;

    [ObservableProperty]
    private string _selectedSlotId = string.Empty;

    [RelayCommand]
    private async Task ConfirmSlotAsync()
    {
        await _orders.SetDeliverySlotAsync(SelectedSlotId);
        ShowOptions = false;
    }
}
```

```xml
<arc:BottomSheet Title="配送選項"
                 Description="訂單 4417 從 Northwind Berlin 出貨。"
                 Detents="0.5, 0.9"
                 IsOpen="{Binding ShowOptions}">
    <StackPanel Spacing="12">
        <RadioButton GroupName="slot" Content="明天，08:00 至 12:00（免費）" />
        <RadioButton GroupName="slot" Content="明天，17:00 至 20:00（4.90 EUR）" />
    </StackPanel>
    <arc:BottomSheet.Actions>
        <arc:Button Content="確認配送時段" Command="{Binding ConfirmSlotCommand}" />
    </arc:BottomSheet.Actions>
</arc:BottomSheet>
```

```csharp
delivery.Closed += (_, e) => log.Text = $"選項關閉方式：{e.Reason}";
delivery.DetentChanged += (_, e) => viewModel.RestingDetent = e.Index;
```

### 範例：行動裝置結帳中的配送選項

手機上的結帳頁面會在頁面中顯示訂單總額與所選時段。點選「變更」會以 50% 的預覽高度開啟面板，列出兩個最便宜的時段，同時不遮住上方的訂單總額。第二個選項是 90% 的停駐點，用於查看完整的貨運業者清單。頁首寫明訂單，讓使用者看到這次選擇會變更哪一筆訂單；底部唯一的操作說明了結果。時段儲存後面板關閉：檢視模型把 `ShowOptions` 設為 `false`，而不是由按鈕設定結果，因為儲存可能失敗，此時面板必須保持開啟並顯示其訊息。

```xml
<arc:BottomSheet Title="配送選項"
                 Description="訂單 4417 從 Northwind Berlin 出貨。"
                 Detents="0.5, 0.9"
                 IsOpen="{Binding ShowOptions}">
    <StackPanel Spacing="12">
        <RadioButton GroupName="slot"
                     Content="明天，08:00 至 12:00（免費）"
                     IsChecked="True" />
        <RadioButton GroupName="slot" Content="明天，17:00 至 20:00（4.90 EUR）" />
        <RadioButton GroupName="slot" Content="週四，09:00 至 13:00（免費）" />
        <TextBlock Text="貨運業者：Northwind Courier。超過 500 EUR 需要簽收。"
                   TextWrapping="Wrap"
                   Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
    </StackPanel>
    <arc:BottomSheet.Actions>
        <arc:Button Content="儲存配送時段" Command="{Binding ConfirmSlotCommand}" />
    </arc:BottomSheet.Actions>
</arc:BottomSheet>
```

### 文案規範

頁首要讓使用者在展開之前就知道面板的用途。標題是一個簡短的名詞片語。說明寫明範圍，數字用阿拉伯數字，日期使用簡短格式。操作使用動詞加受詞，而預覽不應依賴只有展開後才看得到的提示。

| 不要寫 | 改為 |
|---|---|
| 選項 | 配送選項 |
| 點選此處查看更多 | 顯示全部 12 個站點 |
| 上滑查看詳情 | 展開面板，或使用拖曳把手展開 |
| 好 | 儲存配送時段 |
| 你能做的事 | 分享行程 |
| 從 6 月 12 日起住 3 晚 | 3 晚，6 月 12 日至 15 日 |

不要在內文第一行重複標題；內文應補充頁首沒有包含的資訊。

### 最佳做法

**建議**

- 把決定或最有用的一列放在預覽中，讓面板值得被展開。使用者根據預覽做決定，需要捲動才找得到的內容常常被略過。
- 讓關閉按鈕保持可見。並非所有輸入裝置都能發現手勢，而可見的控制項是鍵盤與輔助技術使用者的關閉途徑。
- 為每個面板設定 `Title`。它是面板的無障礙名稱；沒有它，面板會被播報為一個未命名的視窗。
- 當面板的版面取決於停駐位置時（例如面板拉高時地圖會平移），用 `DetentChanged` 儲存停駐位置。

**避免**

- 把面板疊在另一個面板之上。每個面板都會增加一層遮罩和一個焦點陷阱，且只有最上層回應 Escape，因此使用者必須關閉多層才能回到頁面。
- 內文中過長且未分頁的清單。內容不做虛擬化，因此面板開啟時會建立每一列。
- 只依賴拖曳來關閉。減少動態效果時會停用拖曳，因此必須始終保留按鈕或鍵盤操作。
- 在內文中水平捲動。水平捲動已停用，因此寬內容會被裁切，而不是捲動顯示。
- 用面板做破壞性確認。`Dialog` 把後果與按鈕放在一個置中的面板中，供使用者在行動前閱讀。

### 常見問題

**為什麼面板會以最小的停駐點開啟？** `InitialDetentIndex` 預設為 0，即排序清單中最小的停駐點。把它設為所需停駐點的索引，或在開啟後呼叫 `Expand`。

**如何在沒有程式碼後置的情況下從按鈕開啟面板？** 透過名稱把按鈕的指令繫結到面板的 `OpenCommand`，例如 `Command="{Binding #TripSheet.OpenCommand}"`，或在檢視模型中繫結 `IsOpen`。

**為什麼預覽時我的內容不能捲動？** 低於最高的停駐點時，內容是固定的。向下捲動滾輪、在內容上觸控拖曳、Tab 進入首屏以下的內容或按下捲動鍵，都會先開啟面板。這樣可以避免預覽與頁首分離。

**如何阻止點選面板外部把它關閉？** 把 `CloseOnOutsidePress` 設為 `False`。若只想在某些時候允許，保持其為 `True`，並在 `e.Reason` 為 `PressOutside` 且有待處理的工作時取消 `Closing` 事件。

**如何為其他語言重新命名關閉按鈕？** 設定 `CloseButtonName`。預設為「關閉」，該名稱會被輔助技術讀取。

**能否在 XAML 中讀取停駐點？** `DetentIndex` 是唯讀的。在程式碼或檢視模型中訂閱 `DetentChanged`，並繫結你儲存的值。

## API 參考

<!-- api: BottomSheet -->

<!-- api: BottomSheetDetents -->

## 無障礙

- 面板從 `Title` 取得名稱，從 `Description` 取得說明文字。焦點首先落在面板本身。
- 拖曳把手是一個按鈕，會公開其展開狀態。其名稱為「展開面板」或「收合面板」。
- 在拖曳把手上，ArrowUp 與 ArrowDown 每次移動一個停駐點，Home 與 End 分別跳到最高與最低的停駐點，Enter 或空白鍵則在最低與最高之間切換。
- 停駐點的變化以禮貌方式播報：「面板已展開」「面板已收合」，或以百分比形式播報新的高度。Escape 關閉面板。關閉按鈕預設名為「關閉」；設定 `CloseButtonName` 可更改它。
- 面板向輔助技術回報自己是一個視窗，因此會被作為獨立的介面播報。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 在面板中的控制項之間移動；面板為最上層時焦點保持在面板內。Tab 進入首屏以下的內容會展開面板 |
| `ArrowUp` / `ArrowDown`（拖曳把手） | 向上或向下移動一個停駐點 |
| `Home` / `End`（拖曳把手） | 移到最高的停駐點或最小的停駐點 |
| `Enter` 或 `Space`（拖曳把手） | 在最小與最高的停駐點之間切換 |
| `ArrowDown`、`PageDown` 或 `Space`（面板或其內容取得焦點時） | 先展開到最高的停駐點；展開後向下捲動內容 |
| `ArrowUp`、`PageUp` 或 `Shift+Space`（面板或其內容取得焦點時） | 向上捲動內容 |
| `Escape` | 關閉面板，除非 `CloseOnEscape` 為 false 或 `Closing` 取消了關閉 |

捲動步長為方向鍵 48 px，翻頁鍵為可見高度的 85%。

## 動態效果

- 一個動態值驅動面板。拖曳時面板跟隨指標；超過最高的停駐點後，面板會以最多 120 px 的橡皮筋效果拉伸。
- 放開後，面板停駐到最近的停駐點，或在向下拖曳時關閉。速度超過 320 px/s 的快速滑動，一律會使面板至少移動一個停駐點。
- 面板後方的遮罩取決於面板位置。在最小的停駐點時保留完整遮罩的 78%，隨面板下移而淡出。
- 減少動態效果時，停駐點直接跳轉，面板淡入淡出。關閉動態效果時，面板立即顯示或移除。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 開啟 | 以平滑彈簧從邊緣下方升起，到達其停駐點 | 在停駐點處以短暫淡入顯示 | 立即完成 |
| 關閉 | 以 0.3 s 的視覺時長彈回，並保留放開時的速度 | 短暫淡出 | 立即完成 |
| 在停駐點之間移動 | 以平滑彈簧移動到新的停駐點 | 直接跳到新的停駐點 | 立即完成 |
| 拖曳 | 與指標一對一跟隨；超過最高的停駐點後出現橡皮筋效果 | 停用拖曳 | 停用拖曳 |
| 遮罩 | 跟隨面板位置 | 跟隨面板位置 | 立即完成 |

## 相關元件

- `Dialog`：必須先回應的決定。
- `Drawer`：寬螢幕版面中的側邊面板。
- `Popover`：錨定在某個控制項上的小塊內容。

| 元件 | 適用情境 |
|---|---|
| `Dialog` | 使用者必須先確認、取消或輸入簡短的值，任務才能繼續 |
| `Drawer` | 篩選、設定或紀錄詳細資料需要與寬螢幕版面中的頁面並排顯示 |
| `Popover` | 內容很小，錨定在一個控制項上，且不需要遮罩 |
| `Tooltip` | 一行文字解釋一個圖示按鈕 |
| `Alert` | 訊息留在頁面中，直到問題被解決 |
| `Toast` | 簡短的結果，例如「配送時段已儲存」，應自行消失 |
