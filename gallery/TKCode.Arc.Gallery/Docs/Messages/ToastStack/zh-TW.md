---
title: 輕提示堆疊
summary: 將簡短結果堆疊在邊緣，直到你需要檢視時再展開。
source-hash: c4993f77de27
---

<Demo name="Stack" />

## 何時使用

`ToastStack` 承載使用者發起、且無需等待的工作的簡短結果：匯出已完成、邀請已寄出、檔案上傳失敗。它位於視窗邊緣、覆蓋在頁面之上，始終把最新的結果放在最前，較早的結果則收在它後面。視窗中的任何控制項都可以向它加入內容，因此背景工作、儲存處理常式或命令面板都能回報自己做了什麼，而無需了解頁面配置。

- **管理後台中的背景工作結果**：帳單匯出完成後顯示「匯出已就緒」，或顯示附帶列數的「匯入失敗」，操作人員可以繼續在表格中工作。
- **附帶復原時限的確認**：專案清單中顯示附復原按鈕的「專案已封存」，該操作可在幾秒內撤回；撤回後，輕提示會變為「專案已還原」。
- **由載入轉為結果的提示**：載入狀態中顯示「正在上傳 report.pdf」，完成後就地更新為「報告已上傳」，或更新為附帶原因的錯誤，而不是兩則獨立的訊息。
- **來自多個來源的類型化通知**：儲存空間或配額警告、結帳流程的成功訊息以及同步錯誤，全部集中在一處，具有統一的閱讀順序和同一種關閉方式。

## 何時不用

- **頁面用自身狀態控制的一則確認**：用 `Toast`。它沒有佇列，透過 `IsOpen` 設定，只有一則訊息時更簡單。
- **需要保留在頁面上、直到問題解決的訊息**，例如超出限額的方案或失敗的付款：用 `Alert`。堆疊會把較早的訊息藏在最前面的訊息之後，計時器也會關閉其餘訊息。
- **輪播訊息的網站級橫幅**，例如維護通知或促銷：用 `AnnouncementBar`。
- **使用者必須先回應才能繼續的訊息**，例如刪除確認或權限要求：用 `Dialog`，它會取得焦點並阻止頁面操作，直到做出選擇。

## 用法

將 `ToastStack` 放在內容之上，例如作為視窗根面板的最後一個子元素。在視窗中的任何控制項裡呼叫 `Toast.Show` 即可加入輕提示。如果視窗沒有 `ToastStack`，`ToastHost` 會在浮層上建立一個。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <Grid>
        <!-- 頁面內容放在這裡。 -->
        <arc:ToastStack Label="通知" />
    </Grid>
</UserControl>
```

`ToastStack` 衍生自 Avalonia 的 `TemplatedControl`，並實作了 `IToastService`，即應用程式層級 API 的介面：`Show`、`Update`、`Dismiss` 和 `Count`。持有堆疊的程式碼可以直接呼叫這些成員。只知道視窗的程式碼則使用 `Toast.Show`、`Toast.Update` 和 `Toast.Dismiss`，或使用 `ToastHost.GetService`，它為視窗的堆疊傳回同一個介面。一個視窗只有一個作用中的堆疊：放置好的 `ToastStack` 優先於由 `ToastHost` 建立的堆疊。

### 結構

1. **區域**（堆疊本身）：一個由 `Label` 命名的區域地標，預設名稱為「通知」。啟用 `IsHotkeyEnabled` 時，名稱末尾會附加「(Alt+T)」，使螢幕閱讀器使用者了解該快速鍵。
2. **清單**（`PART_List`）：容納卡片的區域。只有清單矩形接收指標輸入，因此堆疊其餘部分之下的頁面仍可點選。清單寬 360 px（22.5rem），或視窗寬度減去 32 px，取兩者中較小的值。
3. **前卡片**：最新的輕提示，帶有浮動陰影（堆疊展開時也始終使用同樣的陰影）。
4. **收起的卡片**：位於前卡片後面的較早輕提示。每往後一層，會在前面的卡片之外露出 14 px，縮小 5%，使用淡化的表面色，並隱藏其內容。`VisibleToasts`（預設 3）設定顯示的數量，其餘的在後面等待。
5. **圖示槽**（`PART_Icon`）：18 × 20 px 的槽位，內含 18 px、描邊為 1.75 的 Lucide 圖示，之後是 12 px 的間距。顏色隨類型變化：成功、強調、警告或危險。載入中的輕提示改為顯示 15 px 的旋轉指示器。
6. **文案**（`PART_Title` 與 `PART_Description`）：標題為 14 px 中等字重，描述使用次要文字顏色，兩者都依 20 px 的行高換行。描述位於標題下方 2 px。
7. **操作按鈕**（`PART_Action`）：28 px 高的膠囊按鈕，左右內距 12 px，小尺寸的次要變體。它位於文案右側；在寬度小於 336 px 的卡片中則位於文案下方。
8. **關閉按鈕**（`PART_Close`）：28 px 的圓形幽靈按鈕，帶 16 px 的 X 圖形。其無障礙名稱為 "Dismiss notification"。

卡片內容左側內距 16 px，其他方向 12 px。堆疊距視窗邊緣 24 px（啟用 `IsContained` 時為 16 px），在寬度小於 480 px 的視窗中置中於底邊。

### 顯示輕提示

`Show` 接收一個 `ToastOptions` 並傳回輕提示的 id。`Kind` 選擇圖示、顏色與朗讀前綴，`Title` 為必填。`Toast.Show` 以參數形式接收相同的值，並將其送到擁有 `owner` 視覺物件的視窗之堆疊。

```csharp
stack.Show(new ToastOptions
{
    Kind = ToastKind.Warning,
    Title = "儲存空間即將用盡",
    Description = "已使用 10 GB 中的 92%。"
});

Toast.Show(button, "邀請已寄出", ToastKind.Success, "Maya 很快會收到一封電子郵件。");
```

每則輕提示都會以其類型與文案播報，堆疊始終把最新的輕提示放在最前面。`Count` 回報排隊的輕提示數量，包括顯示中的和等待在後面的；每當輕提示被顯示、更新或關閉時，都會引發 `ToastsChanged`。可以使用該事件更新標題列中的計數或徽章。

### 類型與生命週期

`ToastKind` 設定圖示、顏色以及預設生命週期。堆疊的預設生命週期為 5 秒。

| 類型 | 圖示 | 預設生命週期 | 朗讀前綴 |
|---|---|---|---|
| `Success` | 成功色的圓形勾號 | `Duration`（5 秒） | 「成功：」 |
| `Info` | 強調色的資訊圖示 | `Duration`（5 秒） | 「資訊：」 |
| `Warning` | 警告色的三角警示 | `Duration` 的 1.6 倍（8 秒） | 「警告：」 |
| `Error` | 危險色的圓形叉號 | `Duration` 的 1.6 倍（8 秒） | 「錯誤：」（以強提示方式播報） |
| `Loading` | 旋轉的圓環 | 無：等待更新 | 「進行中：」 |

警告與錯誤的停留時間較長，因為使用者可能需要閱讀並採取行動。載入中的輕提示沒有計時器，會一直保留，直到工作更新它或堆疊將其關閉。設定 `ToastOptions.Duration` 或 `ToastUpdate.Duration` 可為單則輕提示覆寫預設值。`Timeout.InfiniteTimeSpan` 會使輕提示保留到被關閉，而零或更小的值同樣使其保持開啟。

### 載入與更新

在 `ToastOptions` 中傳入 id，再以相同的 id 呼叫 `Update`，即可就地修改輕提示。以已在堆疊中的 id 呼叫 `Show`，會更新該輕提示而不是新增一則。將 `Duration` 設為 `Timeout.InfiniteTimeSpan`，可使輕提示保留到被更新或關閉。輕提示的圖示、文字與高度會變形為新狀態，計時器則以新的生命週期重新開始。

```csharp
var id = stack.Show(new ToastOptions
{
    Kind = ToastKind.Loading,
    Title = "正在上傳 report.pdf",
    Duration = Timeout.InfiniteTimeSpan
});

await UploadAsync();

stack.Update(id, new ToastUpdate
{
    Kind = ToastKind.Success,
    Title = "報告已上傳",
    Description = "已與 4 位使用者共用。"
});
```

`ToastUpdate` 只改變它所設定的成員。`Kind`、`Title`、`Description` 或 `Action` 為 null 時保持目前的值。`Duration` 為 null 時，輕提示會以其類型的預設生命週期重新開始。使用 `RemoveDescription` 或 `RemoveAction` 可以移除描述或操作。
對已不在堆疊中的 id 進行更新不會產生任何效果，因此傳送一個遲到的、針對已關閉輕提示的結果是安全的。

### 操作

`ToastAction` 會為輕提示加入一個按鈕。按鈕接收文字標籤以及一個接收輕提示 id 的回呼。回呼執行後，除非回呼更新了輕提示，否則輕提示會關閉，因此「檢視」這類簡單操作無需額外程式碼。復原操作可以把輕提示更新為結果，並透過 `RemoveAction` 移除按鈕。

```csharp
stack.Show(new ToastOptions
{
    Title = "專案已封存",
    Action = new ToastAction("復原", id => stack.Update(id, new ToastUpdate
    {
        Kind = ToastKind.Success,
        Title = "專案已還原",
        RemoveAction = true
    }))
});
```

回呼決定輕提示接下來做什麼。如果它既沒有更新也沒有關閉輕提示，堆疊會在回呼傳回後將其關閉。回呼在 UI 執行緒上執行，因此應從回呼中啟動耗時的工作，而不是在回呼裡直接執行。更新輕提示並移除按鈕後，輕提示只剩關閉按鈕；鍵盤焦點會移到該按鈕，使使用者可以繼續用鍵盤操作。

### 位置與數量限制

`Position` 將堆疊移到左下、置中或右側邊緣，預設為 `BottomRight`。無論位置如何設定，寬度小於 480 px 的視窗始終讓堆疊置中。`VisibleToasts` 設定同時顯示的輕提示數量，`Limit` 會捨棄超出數量的最舊輕提示。`Limit` 的值不會低於 1。

<Demo name="Position" />

```xml
<arc:ToastStack Position="BottomCenter" VisibleToasts="3" Limit="12" />
```

`IsContained` 會把堆疊固定在其自身邊界內 16 px 處，而不是距視窗邊緣 24 px。在預覽、面板或任何位於視窗較小區域內而非覆蓋整個視窗的堆疊中啟用它。

### 由程式碼驅動

`ToastStack` 是一種服務，而不是帶有可繫結值的控制項，因此檢視模型接收的是介面，而不是控制項。在檢視中取得視窗的服務一次，然後將其傳遞下去：

```csharp
public partial class ExportViewModel : ObservableObject
{
    private readonly IToastService _toasts;

    public ExportViewModel(IToastService toasts) => _toasts = toasts;

    [RelayCommand]
    private async Task ExportAsync()
    {
        var id = _toasts.Show(new ToastOptions
        {
            Kind = ToastKind.Loading,
            Title = "正在匯出發票",
            Duration = Timeout.InfiniteTimeSpan
        });

        var result = await _exports.CreateCsvAsync(Range);
        _toasts.Update(id, result.IsSuccess
            ? new ToastUpdate { Kind = ToastKind.Success, Title = "匯出已就緒", Description = result.FileName }
            : new ToastUpdate { Kind = ToastKind.Error, Title = "匯出失敗", Description = result.Error });
    }
}
```

```csharp
// 在檢視的程式碼後置中，檢視附加到視窗後：
var toasts = ToastHost.GetService(this);
DataContext = new ExportViewModel(toasts);
```

檢視模型與匯出服務屬於應用程式自身。檢視中無需放置 `ToastStack`：`ToastHost` 會在第一次呼叫時於視窗的浮層上建立一個。只有當位置、數量限制或 `IsContained` 需要與預設值不同時，才自行放置 `ToastStack`。

### 範例：帳單後台中的發票匯出

一個帳單後台允許財務操作人員將一個月的發票匯出為 CSV。匯出需要幾秒鐘，而操作人員應繼續工作，因此請求開始時顯示一則沒有計時器的載入輕提示。檔案就緒後，輕提示變為附「下載」按鈕的「匯出已就緒」；失敗時，輕提示變為附原因與「重試」按鈕的錯誤。錯誤的停留時間比預設值更長，而且只有堆疊的清單矩形接收指標輸入，因此周圍的發票表格仍可正常使用。

堆疊放置在頁面內容之上，數量限制為八則，使長時間的匯出工作階段不會讓堆疊無限增長：

```xml
<Grid>
    <!-- 發票表格和篩選器放在這裡。 -->
    <arc:ToastStack Label="帳單通知"
                    Position="BottomRight"
                    VisibleToasts="3"
                    Limit="8" />
</Grid>
```

```csharp
var id = _toasts.Show(new ToastOptions
{
    Kind = ToastKind.Loading,
    Title = "正在匯出 3 月的發票",
    Duration = Timeout.InfiniteTimeSpan
});

var result = await _exports.CreateCsvAsync(March2026);

_toasts.Update(id, result.IsSuccess
    ? new ToastUpdate
    {
        Kind = ToastKind.Success,
        Title = "匯出已就緒",
        Description = "412 張發票，1.8 MB。",
        Action = new ToastAction("下載", () => _files.Open(result.FileName))
    }
    : new ToastUpdate
    {
        Kind = ToastKind.Error,
        Title = "匯出失敗",
        Description = result.Error,
        Action = new ToastAction("重試", () => _ = ExportAgainAsync())
    });
```

### 文案規範

- **標題**：用過去式簡述結果，或用一個狀態描述載入中：「匯出已就緒」「邀請已寄出」「正在上傳 report.pdf」。類型本身已經說明是否成功，因此不要在標題中重複。
- **描述**：一句話，包含數字、名稱或下一步：「412 張發票，1.8 MB。」「請檢查地址後再試一次。」數字使用阿拉伯數字。
- **操作標籤**：一個動詞，一到兩個字：「復原」「重試」「下載」「檢視」。不要用「確定」或「關閉」，因為關閉按鈕已經存在。
- **錯誤**：說明失敗的內容以及使用者能做什麼：原因與下一步的辦法。不要指責使用者。
- **句子式大小寫，不用驚嘆號**：通知報告的是事實。

| 不要寫 | 改為 |
|---|---|
| 成功！！！檔案已上傳 | 報告已上傳 |
| 錯誤 | 匯出失敗 |
| 發生了一些問題，請稍後再試。 | 無法寄出邀請，請檢查地址後再試一次。 |
| 點選「確定」繼續 | 專案已還原 |

### 最佳做法

**建議**

- 每項工作使用一則輕提示並更新它，使載入狀態變為其結果，而不是留下兩則訊息。
- 為每則載入中的輕提示提供結束路徑：成功時更新、失敗時更新，或由程式碼刻意設定的逾時。沒有任何更新的載入輕提示會一直停留在畫面上。
- 將 `Limit` 設為操作人員能夠掃視的數量，例如八則，使舊結果不會堆積在新結果後面。
- 對使用者必須處理的事項使用 `Warning` 和 `Error`，使其較長的生命週期與使用者需要完成的工作相符。

**避免**

- 用堆疊顯示必須保留到解決為止的訊息，例如方案限額。堆疊中的輕提示會自行關閉；這類訊息應使用 `Alert`。
- 過長的描述。輕提示是一眼讀完的，而卡片最寬只有 360 px。
- 悄悄執行主要任務的操作，例如沒有復原的「全部刪除」。具破壞性的工作應在執行前使用 `Dialog` 確認，執行後提供復原。
- 對每次按鍵或每個輪詢週期都顯示輕提示。每次 `Show` 和 `Update` 都會向輔助技術播報，頻繁的訊息會變成雜訊。應將它們合併為一則結果。

### 常見問題

**為什麼輕提示在我讀完之前就消失了？** 計時器在堆疊收起時執行，只在堆疊展開、被拖曳或視窗最小化時才會停止。可以為該輕提示設定更長的 `Duration`，或在閱讀時將指標懸停在堆疊上以暫停計時器。

**如何讓輕提示一直保留到使用者將其關閉？** 在 `ToastOptions` 或 `ToastUpdate` 中把 `Duration` 設為 `Timeout.InfiniteTimeSpan`。載入中的輕提示在被更新之前已經是這種方式。

**視窗沒有 `ToastStack` 時會發生什麼？** 第一次呼叫 `Toast.Show` 或 `ToastHost.GetService` 時，會在視窗的浮層上建立一個。如果視窗沒有浮層，`ToastHost` 會引發 `InvalidOperationException`，因此在這種情況下請在視窗中放置一個 `ToastStack`。從未附加到視窗的控制項呼叫也會引發例外。

**如何阻止堆疊占用 Alt+T 快速鍵？** 將 `IsHotkeyEnabled` 設為 `False`。在 Windows 上，Alt+T 可能與功能表的存取鍵衝突，因此擁有自身功能表的應用程式可能會將其關閉。你仍可以用 `FocusInto()` 自行把焦點移入堆疊，例如從「通知」功能表項目中呼叫。

**能否統計或讀取排隊中的輕提示？** 讀取 `Count` 可取得排隊中的輕提示數量（包括顯示中的和等待中的），讀取 `IsExpanded` 可判斷清單是否已展開。訂閱 `ToastsChanged` 可使徽章與堆疊保持同步。

## API 參考

<!-- api: ToastStack -->

<!-- api: ToastOptions -->

配套型別，摘自原始碼：

`ToastUpdate` 用於更改已顯示的輕提示。為 null 的成員保持不變。

| 成員 | 含義 |
|---|---|
| `Kind` | 新的類型；圖示會變形 |
| `Title` | 新訊息 |
| `Description`、`RemoveDescription` | 新的第二行文字，或將其移除 |
| `Action`、`RemoveAction` | 新的操作，或收起操作按鈕 |
| `Duration` | 新的生命週期；為 null 時以該類型的預設值重新開始 |

`ToastKind` 是輕提示的類型：`Success`、`Info`、`Warning`、`Error` 和 `Loading`。`ToastStackPosition` 是堆疊所在的邊緣：`BottomRight`（預設）、`BottomCenter` 和 `BottomLeft`。

`ToastHost` 用於尋找視窗的堆疊，是應用程式層級 API 的入口：

| 成員 | 含義 |
|---|---|
| `GetService(owner)` | 視窗的 `IToastService`；沒有堆疊時在浮層上建立一個 |
| `GetStack(owner)` | 視窗的 `ToastStack`，建立規則相同 |
| `Find(topLevel)` | 視窗已註冊的堆疊，或 null；放置的堆疊優先於自動建立的堆疊 |

## 無障礙

- 通知區域是一個由 `Label` 命名的區域地標。啟用 `IsHotkeyEnabled` 時，名稱末尾為「(Alt+T)」。Alt+T 會將焦點移入堆疊並將其展開。
- Tab 在顯示中的輕提示的操作按鈕與關閉按鈕之間移動。Escape 關閉目前聚焦的輕提示。
- 新增與更新的輕提示以「類型：標題。描述」的形式播報。錯誤以強提示方式播報，其他類型以禮貌方式播報。
- 每張卡片的名稱為「類型：標題」，螢幕閱讀器可藉此區分不同類型，其描述即為說明文字。關閉按鈕的名稱為 "Dismiss notification"。
- 堆疊展開、輕提示被拖曳以及視窗最小化時，計時器會暫停。堆疊被其他視窗遮住時，計時器仍會繼續執行。
- `VisibleToasts` 之外的輕提示以及正在離開的輕提示處於非作用狀態：無法透過指標或 Tab 到達。
- 聚焦的輕提示關閉時，鍵盤焦點會移交出去：移到下一則輕提示的第一個按鈕；如果沒有剩餘的輕提示，則回到進入堆疊之前擁有焦點的元素。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| `Alt+T` | 將焦點移到最前面的輕提示（有操作時為其操作按鈕，否則為關閉按鈕）並展開堆疊。將 `IsHotkeyEnabled` 設為 `False` 可關閉該快速鍵 |
| `Tab` / `Shift+Tab` | 在顯示中的輕提示的操作按鈕與關閉按鈕之間移動 |
| `Enter` 或 `Space` | 按下聚焦的按鈕：執行操作，或關閉輕提示 |
| `Escape` | 關閉聚焦的輕提示，不執行任何操作 |

鍵盤焦點會展開堆疊，離開堆疊則將其收合。滑鼠點選按鈕不會使堆疊保持展開。

## 動態效果

- 新輕提示從其自身高度的下方升起並淡入。較早的輕提示向後收起、縮小，並失去內容的色調。懸停或聚焦堆疊時，輕提示展開為清單；離開後再收合回去。
- 向右滑動輕提示時，它會跟隨指標移動。較長或較快的滑動會關閉它，卡片在移開時淡出。
- 更新時，圖示、文案與卡片高度會就地變形，操作按鈕的寬度會展開或收起。工作進行時，載入圓環持續轉動。
- 減少動態效果時，位置直接跳轉，輕提示以快速淡入淡出的方式出現和消失。關閉動態效果時，每次變化立即顯示。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 新輕提示到達 | 從下方升起，並帶有標準的進入淡入效果 | 原位出現，快速淡入 | 立即完成 |
| 較早的輕提示向後收起 | 每一層在變形彈簧上後退 14 px 並縮小 5%；內容淡出 | 位置直接跳轉；快速淡入 | 立即完成 |
| 堆疊展開或收合 | 在變形彈簧上展開為清單；其他變化使用平滑彈簧 | 位置直接跳轉 | 立即完成 |
| 輕提示被關閉 | 下沉 10 px，縮小到 96%，同時淡出 | 快速淡出 | 立即完成 |
| 輕提示被滑走 | 跟隨指標；滑動 280 px 時淡出至 25%；快速放開時在 0.22 秒內淡出 | 滑動仍會關閉輕提示；淡出很快，且不會甩出 | 立即完成 |
| 輕提示被更新 | 圖示、文案與卡片高度變形；操作槽展開或收起；載入圓環轉動 | 載入圓環停止；新文案與高度直接生效，不使用彈簧 | 立即完成 |

## 相關元件

- `Toast`：由頁面透過 `IsOpen` 控制的一則本機確認。
- `Alert`：頁面內持續存在的訊息。
- `AnnouncementBar`：帶倒數計時的輪播橫幅。
- `Dialog`：頁面繼續前必須做出的決定。

| 元件 | 適用情境 |
|---|---|
| `Toast` | 頁面掌控一則確認，並設定其文字與開啟狀態 |
| `Alert` | 訊息保留在頁面上，直到使用者或資料將其解決 |
| `AnnouncementBar` | 網站級通知輪流展示多則訊息 |
| `Dialog` | 使用者必須先做出回應，頁面才能繼續 |
| `ToastStack` + `Toast.Show` | 背景工作的結果可能來自任何控制項，並且可能相互重疊 |
