---
title: 輕提示
summary: 已完成背景操作的簡短確認。
source-hash: 77303346d233
---

<Demo name="LocalToast" />

## 何時使用

`Toast` 是由頁面自己掌控的單則確認：一段簡短的訊息從邊緣升起，停留幾秒後消失。當頁面本身知道某件事已經完成時（例如表單儲存之後），而使用者仍停留在同一個畫面上，它是合適的工具。頁面負責放置輕提示、設定文字並決定何時開啟，因此訊息總與它所在的檢視保持一致。

- **設定頁與個人資料頁的儲存確認**：請求成功後，在帳單地址表單下方顯示「變更已儲存」。使用者仍停留在頁面上，因此一則簡短、會自行關閉的訊息就足夠了。
- **複製與匯出回饋**：在開發者主控台中，金鑰旁顯示「API 金鑰已複製」，或在發票詳細資料頁顯示「發票連結已複製」。輕提示確認了操作，又不會搶走使用者正在編輯的欄位的焦點。
- **同一畫面上的背景同步結果**：CRM 備註編輯器中草稿儲存後顯示「已同步到所有裝置」。頁面在儲存完成時設定 `IsOpen`，其他部分無需知道這件事。
- **清單頁的批次操作**：表格下方顯示「已封存 3 位聯絡人」。當它遮住使用者需要的列時，使用者可以將其滑走，而且不會留下一排等待清除的舊訊息。

## 何時不用

- **可能同時開啟多則訊息，或結果來自應用程式的任何位置**：用 `ToastStack`。它會排隊、帶類型、可更新並堆疊通知，也可以透過 `Toast.Show` 從任何控制項呼叫。
- **需要一直保留、直到有人處理的訊息**，例如付款失敗或配額警告：在頁面內使用 `Alert`，它會一直顯示到問題解決。
- **必須先回應才能繼續的訊息**：用 `Dialog`，它會取得焦點並阻止頁面操作，直到使用者做出選擇。
- **網站頂部輪播的橫幅**，例如帶倒數計時的維護通知：用 `AnnouncementBar`。

## 用法

`Toast` 顯示其 `Title` 與選用的 `Description`。`IsOpen` 為 `True` 時可見，並在 `Duration`（預設 4.5 秒）後關閉。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:Toast Title="變更已儲存"
               Description="已同步到你的所有裝置。"
               VerticalAlignment="Bottom" />
</UserControl>
```

`Toast` 衍生自 Avalonia 的 `TemplatedControl`，因此它的外觀位於 `ControlTheme` 中，可以用 Avalonia 常用的機制重新設定樣式。它額外提供訊息與動態效果行為：進入與離開、計時器、滑動手勢以及 `Closed` 事件。它不排隊。頁面中的每個 `Toast` 都是獨立的，因此把它放在訊息所屬的位置即可；當多則訊息可能同時開啟時，改用 `ToastStack`。

### 結構

1. **表面**（`PART_Root` 與 `PART_Surface`）：卡片本身。寬度為可用寬度，最大到 `MaxToastWidth`（416 px，26rem），較窄的容器會使它收窄。它有一條顏色為邊框色的 1 px 邊框、面板圓角、浮動陰影，以及在頁面背景上混入 6% 顏色的表面色。內距左側為 16 px，其餘三側為 14 px。
2. **圖示圓盤**（`PART_Icon`）：30 px 的圓，之後有 12 px 的間距。填滿色為表面上 10% 的成功色，外圈是 1 px 的環，顏色為邊框上 24% 的成功色。每則輕提示的圓盤都相同。
3. **勾號**（`PART_Check`）：16 px 的成功色勾號，描邊寬 2.25。進入動態效果開始後它會自行繪製。勾號只是裝飾，不對輔助技術公開。
4. **標題**（`PART_Title`）：`Title`，14 px、中等字重，單行顯示。過長的標題以省略號結尾。其行高為 19.6 px，頂部內距使第一行與 30 px 的圓盤置中對齊。
5. **描述**（`PART_Description`）：`Description`，14 px，使用次要文字顏色，自動換行。它位於標題下方 2 px，為空時不佔用間距。
6. **關閉按鈕**（`PART_Close`）：32 px 的圓形幽靈按鈕，帶 2 px 的 X 圖形。靜止時顏色較淡，懸停時變為前景色。其無障礙名稱為 "Dismiss notification"。

### 顯示與隱藏

關閉之後，將 `IsOpen` 設為 `True` 即可再次顯示輕提示。關閉、滑動和逾時都會把 `IsOpen` 設為 `False`，因此該屬性始終反映畫面上的實際狀態。輕提示開啟時修改 `Title` 或 `Description`，新文字會向上浮現，高度隨之變化；計時器繼續運作。對已經開啟的輕提示設定 `IsOpen` 為 `True` 沒有任何效果。

```csharp
toast.Title = "草稿已發佈";
toast.Description = "讀者幾秒內即可看到。";
toast.IsOpen = true;
```

在同一個訊息位置只保留一個輕提示。當同一則訊息需要再次出現時，更新它的文字並開啟它，而不是每次都建立一個新的 `Toast`。

### 持續時間

`Duration` 設定輕提示自動關閉前的時間，預設為 4.5 秒。值為零或更小時，輕提示會保持開啟，直到使用者將其關閉，或頁面把 `IsOpen` 設為 `False`。計時器在輕提示開啟時啟動，輕提示開啟期間修改 `Duration` 時計時器會重新開始。懸停時計時器不會暫停，因此文案必須在給定的時間內讀完。

```xml
<arc:Toast Title="上傳完成" Duration="0:0:8" />
<arc:Toast Title="仍在同步" Duration="0" />
```

依文案的長度選擇持續時間。單行確認適合預設值。帶描述的訊息需要更多閱讀時間，因此應設定更長的 `Duration`，例如 8 秒。只有當頁面自己關閉輕提示時才使用 0，並確保工作結束時頁面一定會把 `IsOpen` 設為 `False`。

### 關閉事件

`Closed` 在輕提示開始關閉時引發，共有三種原因。`ToastCloseReason` 用於區分它們：`Dismissed` 表示按下了關閉按鈕或呼叫了 `Close()`，`Swiped` 表示滑動超過了距離或速度門檻，`TimedOut` 表示 `Duration` 已經到時。

```csharp
toast.Closed += (_, e) =>
{
    if (e.Reason == ToastCloseReason.TimedOut)
    {
        log.Text = "訊息已自動關閉。";
    }
};
```

`Closed` 只針對由輕提示自身發起的關閉而引發。從程式碼中把 `IsOpen` 設為 `False` 會關閉輕提示，但不會引發該事件，因為頁面已經知道自己設定的狀態。當頁面希望獲得與關閉按鈕相同的結果（包括事件）時，使用 `Close()`。

### 應用程式層級的輕提示

`Toast.Show` 可以從視窗中的任何控制項，把輕提示送到視窗的 `ToastStack`，它提供的正是 `ToastStack` 的同一項服務。當訊息來自不擁有 `Toast` 控制項的程式碼時使用它，例如共用的匯出命令、剪貼簿輔助方法或背景工作。`ToastKind` 決定圖示與顏色：`Success`、`Info`、`Warning`、`Error` 或 `Loading`。`Toast.Show` 會傳回一個 id，`Toast.Update` 與 `Toast.Dismiss` 接收該 id。

<Demo name="FromAnywhere" />

```csharp
var id = Toast.Show(this, "匯出已開始", ToastKind.Loading, "Acme Analytics 正在產生 CSV。");

// 稍後，匯出完成時：
Toast.Update(this, id, new ToastUpdate
{
    Kind = ToastKind.Success,
    Title = "匯出已就緒",
    Description = "請到匯出頁面下載。"
});
```

`owner` 參數可以是附加到視窗的任何 `Visual`。如果視窗沒有 `ToastStack`，第一次呼叫會在視窗的浮層上建立一個。尚未附加的 owner 會引發 `InvalidOperationException`，因此應在檢視載入後再呼叫這些方法。應用程式層級 API 的完整說明見 `ToastStack` 頁面。

### 繫結到檢視模型

`IsOpen` 預設為雙向繫結，因此繫結無需指定 `Mode`。標題與描述是單向的：文字由檢視模型持有，輕提示只負責顯示。下面的檢視模型以 CommunityToolkit.Mvvm 風格撰寫。

```csharp
public partial class ProfileViewModel : ObservableObject
{
    private readonly IProfileService _profiles;

    public ProfileViewModel(IProfileService profiles) => _profiles = profiles;

    [ObservableProperty]
    private bool _isSavedToastOpen;

    [ObservableProperty]
    private string _savedToastTitle = "變更已儲存";

    [ObservableProperty]
    private string? _savedToastDescription;

    [RelayCommand]
    private async Task SaveAsync()
    {
        await _profiles.SaveAsync(Draft);
        SavedToastTitle = "變更已儲存";
        SavedToastDescription = "已同步到你的所有裝置。";
        IsSavedToastOpen = true;
    }
}
```

```xml
<Grid RowDefinitions="*,Auto">
    <!-- 表單內容放在這裡。 -->
    <arc:Toast Grid.Row="1"
               Title="{Binding SavedToastTitle}"
               Description="{Binding SavedToastDescription}"
               IsOpen="{Binding IsSavedToastOpen}"
               HorizontalAlignment="Right"
               VerticalAlignment="Bottom"
               Margin="0,0,0,8" />
</Grid>
```

`IsOpen` 是唯一的雙向屬性，因此無論關閉的原因是什麼，檢視模型都能感知到每一次關閉。如果檢視需要依原因做出反應，在程式碼後置中訂閱 `Closed`。每次儲存時，先設定標題與描述，再把 `IsSavedToastOpen` 設為 `True`：已關閉的輕提示會以新文字開啟，已開啟的則就地更新文字。

### 範例：CRM 中的個人資料儲存

CRM 中的聯絡人記錄在底部有一個儲存按鈕。儲存成功後，表單左下方會升起一則確認訊息，說明發生了什麼，並指出結果在哪裡。輕提示不會阻擋下一次編輯，並會在預設的 4.5 秒後自動關閉，使用者可以繼續工作。儲存失敗時改用 `Alert`，因為失敗必須一直可見，直到使用者解決問題。

```xml
<Grid Width="640" Height="220">
    <Border Background="{arc:ArcDesignTokenResource SurfaceRaised}"
            BorderBrush="{arc:ArcDesignTokenResource Border}"
            BorderThickness="1"
            CornerRadius="{arc:ArcDesignTokenResource RadiusPanel}" />
    <arc:Toast Title="變更已儲存"
               Description="Northwind Logistics 已在你的所有檢視中更新。"
               HorizontalAlignment="Left"
               VerticalAlignment="Bottom"
               Margin="24" />
</Grid>
```

### 文案規範

- **標題**：一句簡短的過去式陳述，說明什麼已經完成：「變更已儲存」「發票已寄出」。它保持單行顯示，因此英文文案應控制在約 30 個字元以內。
- **描述**：一句話，說明結果在哪裡或接下來會發生什麼：「已同步到所有裝置。」「讀者幾秒內即可看到。」標題已說明一切時省略描述。
- **不要使用行動動詞**：輕提示除關閉按鈕外沒有其他按鈕，因此不要在文案中要求使用者去做什麼。如果下一步很重要，把它放在頁面中，或使用 `Alert`。
- **句子式大小寫，不用驚嘆號**：確認訊息陳述事實，不應慶祝。

| 不要寫 | 改為 |
|---|---|
| 成功！您的變更已成功儲存！！！ | 變更已儲存 |
| 已儲存。按這裡檢視。 | 變更已儲存。可在「活動」中找到。 |
| 儲存表單時發生錯誤 | 無法儲存，請再試一次。 |
| 該項目已從您的帳戶中成功刪除 | 聯絡人已刪除 |

### 最佳做法

**建議**

- 用 `Toast` 顯示頁面掌控的一則確認，並把文字放在檢視模型中，使頁面狀態與訊息保持一致。
- 保持標題足夠短，能放在一行內。過長的標題會以省略號結尾，從而隱藏結果。
- 把輕提示放在使用者正在注視的位置，通常是表單或頁面的底部，使確認訊息靠近引發它的操作。
- 將 `IsOpen` 繫結到檢視模型，使開啟狀態與頁面已持有的資料放在一起，而不只存在於檢視中。

**避免**

- 必須閱讀的訊息，例如付款失敗或權限問題。計時器不會暫停，這類訊息應放在 `Alert` 或 `Dialog` 中，直到問題解決前一直保留。
- 用 `Toast` 連續顯示多則通知。每個 `Toast` 都是獨立的，不排隊，每則訊息都需要自己在頁面上的位置，且彼此互不知曉；應改用 `ToastStack`。
- 持續時間為零且頁面不負責關閉。這樣的輕提示永遠不會逾時，也不會被頁面關閉，會在整個工作階段期間一直停留在畫面上。
- 只依賴動態效果傳達資訊。結果應由文字承載；升起與勾號只起強調作用。

### 常見問題

**為什麼輕提示在我讀完之前就關閉了？** 預設持續時間為 4.5 秒，而且指標停在輕提示上時計時器也不會暫停。文案較長時，請設定更長的 `Duration`；如果訊息必須保留到問題解決，請使用 `Alert`。

**如何知道輕提示關閉的原因？** 訂閱 `Closed` 並讀取 `e.Reason`。`Dismissed` 表示按下了關閉按鈕或呼叫了 `Close()`，`Swiped` 表示滑動，`TimedOut` 表示持續時間已到。從程式碼中把 `IsOpen` 設為 `False` 不會引發該事件。

**如何再次顯示同一則訊息？** 輕提示關閉後，將 `IsOpen` 設為 `True`。先設定新的 `Title` 或 `Description`，使輕提示以新文字開啟。已開啟的輕提示在文字變化時會保留目前的計時器，對已開啟的輕提示設定 `IsOpen` 為 `True` 也不會有任何作用。

**檢視模型能否在沒有檢視參照的情況下顯示輕提示？** 可以。在接收到的程式碼中，把視窗的 `Visual`（例如檢視本身）傳給 `Toast.Show`；或者使用 `ToastHost.GetService` 取得視窗的 `IToastService`，一次取得後交給檢視模型。

**輕提示會搶走焦點嗎？** 不會。輕提示以禮貌方式播報，且不移動焦點。鍵盤焦點仍停留在使用者離開的位置，通常是發起這項工作的按鈕。

## API 參考

<!-- api: Toast -->

配套型別。`ToastCloseReason` 是 `ToastClosedEventArgs.Reason` 中的原因：

| 成員 | 含義 |
|---|---|
| `Dismissed` | 關閉按鈕或 `Close()` 關閉了輕提示 |
| `Swiped` | 滑動超過了距離（80 px）或速度（480 px/s）門檻 |
| `TimedOut` | `Duration` 已到時 |

## 無障礙

- `Title` 是輕提示的無障礙名稱，`Description` 是其說明文字，因此輕提示出現時螢幕閱讀器會同時朗讀兩者。
- 輕提示是禮貌的即時區域：它會在目前語音之後播報，不會打斷它。
- 關閉按鈕的名稱為 "Dismiss notification"。Tab 可以移到它，按 Enter 或空白鍵即可關閉輕提示。
- 勾號是裝飾，不對輔助技術公開。
- 輕提示不會取得焦點，因此不會把使用者從正在使用的欄位或按鈕上移開。
- 計時器不會因懸停或焦點而暫停。文字應保持簡短；如果使用者可能需要更多時間閱讀，請使用 `Alert`。
- 滑動是指標與觸控手勢。關閉按鈕可以透過鍵盤得到相同的結果。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 將焦點移到關閉按鈕或從關閉按鈕移出；輕提示沒有其他可聚焦的控制項 |
| `Enter` 或 `Space` | 按下關閉按鈕：輕提示以 `Dismissed` 原因關閉 |

只有指標拖曳可以滑動。從關閉按鈕開始的按下操作不會觸發拖曳。

## 動態效果

- 輕提示以 96% 的縮放比例從下方升起 16 px，並透過彈簧運動；其勾號在 0.12 秒後開始繪製。
- 關閉時，輕提示下沉 8 px，縮小到 97% 並淡出。關閉後的輕提示不佔用空間。
- 變化的文字向上浮現，高度透過彈簧適應新的文案。
- 滑動會跟隨指標；放開且超過門檻時，它會以放開時的速度繼續移動並淡出。短距離滑動會彈回。
- 減少動態效果時，輕提示只做淡入淡出，並關閉滑動。關閉動態效果時，輕提示立即顯示與隱藏。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 輕提示開啟 | 以 96% 的縮放比例升起 16 px，並在變形彈簧上淡入；勾號在 0.12 秒後繪製 | 在最終位置淡入，勾號已繪製完成 | 立即顯示 |
| 輕提示關閉（按鈕、計時器或 `Close()`） | 下沉 8 px，縮小到 97%，並在離開時長內淡出 | 以瞬間預設淡出 | 立即移除 |
| 滑走 | 跟隨指標，然後以放開時的速度甩出並淡出 | 不可用：滑動已關閉 | 不可用：滑動已關閉 |
| 標題或描述變化 | 舊文字向上移出，新文字升起，高度透過彈簧適應 | 瞬間淡入 | 立即完成 |

## 相關元件

- `ToastStack`：用於應用程式任何位置發出、可排隊、帶類型且可更新的通知。
- `Alert`：用於在問題解決前一直保留在頁面上的訊息。
- `Dialog`：用於必須在頁面繼續前做出回應的決定。
- `AnnouncementBar`：帶倒數計時的輪播橫幅。

| 元件 | 適用情境 |
|---|---|
| `ToastStack` | 同時可能開啟多則結果，或訊息來自頁面之外的程式碼 |
| `Alert` | 訊息保留在頁面上，直到使用者或資料將其解決 |
| `Dialog` | 使用者必須做出選擇，頁面才能繼續 |
| `AnnouncementBar` | 站點級通知輪流展示多則訊息，或需要倒數計時 |
| `Toast` + `IsOpen` | 頁面掌控一則簡短確認及其文字，且無需排隊 |
