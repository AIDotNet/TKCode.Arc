---
title: 對話方塊
summary: 為需要使用者關注的決定提供專注的浮層面板。
source-hash: 1eb8d04fe87d
---

<Demo name="ShortForm" />

## 何時使用

`Dialog` 是一個置中的模態面板，用於必須在使用者返回頁面之前完成的決定或簡短任務。它把問題、說明與按鈕放在同一處，壓暗其後的頁面，並在使用者作答之前把焦點保持在面板內。在需要有意打斷使用者的地方使用它，並讓面板保持足夠小，使使用者無需捲動即可讀完整個面板。

- **管理主控台中的破壞性確認**：刪除專案、撤銷 API 金鑰或取消訂閱。對話方塊說明後果（「此操作將永久刪除 Atlas redesign 及其 14 個檔案」），並把破壞性操作放在 `Danger` 變體中，與「取消」並列。
- **適合一個面板的簡短表單**：重新命名工作區、透過電子郵件邀請成員並指定角色，或設定付款參考號。欄位很少，儲存或取消的決定是即時的。
- **在一個面板中完成的多步流程**：「邀請你的團隊」之類的入門步驟透過修改 `Title` 與 `Description` 切換步驟，使用者始終停留在同一個介面中，文案在原位淡入替換。
- **繼續前的確認**：使用者必須閱讀的計費或政策變更，例如 Team 方案的新價格，在使用者開啟帳單頁面之前顯示。
- **來自程式碼的提示**：表格上的批次操作，例如「封存 42 張發票」，透過 `Dialog.ConfirmAsync` 請求確認，僅在回傳 `true` 時繼續。

## 何時不用

- **長表單或紀錄詳細資料**：用 `Drawer`，它可以捲動內文，並讓頁面在邊緣保持可見。
- **以行動裝置為主、停駐在預覽高度的任務**：用 `BottomSheet`，它先部分開啟，並可透過拖曳展開。
- **不應阻擋頁面的輕量內容**：用 `Popover`，它錨定在觸發器上，不使用遮罩。
- **指令清單**：用 `DropdownMenu` 或 `ContextMenu`，它們把選項放在所作用的控制項旁邊。
- **需要留在頁面中直到問題解決的訊息**：用 `Alert`。應自行消失的簡短結果屬於 `Toast`。

## 用法

`Dialog` 衍生自 `ModalOverlay`，而 `ModalOverlay` 本身是 `ContentControl`。它的 `Content` 是內文，`Title` 與 `Description` 構成頁首，`Actions` 是頁尾，`IsOpen` 控制顯示或隱藏。控制項在宣告的位置不佔用空間。當 `IsOpen` 為 true 時，面板會顯示在視窗的覆蓋層上，位於模糊遮罩之上，因此對話方塊可以從 XAML 樹中的任何位置使用，且不會干擾周圍的版面。

在觸發器旁邊宣告 `Dialog`。它本身不轉譯任何內容，`IsOpen` 為 true 時顯示面板。

### 在 XAML 中宣告

把內文放入內容區，把按鈕放入 `Actions`。帶有 `arc:Dialog.Result` 的按鈕會以該值關閉對話方塊。

```xml
<arc:Dialog Title="重新命名專案"
            Description="請選擇一個團隊成員能快速辨識的簡短名稱。"
            IsOpen="{Binding IsRenaming}">
    <arc:Input Text="{Binding ProjectName}" />
    <arc:Dialog.Actions>
        <StackPanel Orientation="Horizontal" Spacing="8">
            <arc:Button Variant="Secondary" Content="取消" arc:Dialog.Result="{x:False}" />
            <arc:Button Content="儲存" arc:Dialog.Result="{x:True}" />
        </StackPanel>
    </arc:Dialog.Actions>
</arc:Dialog>
```

### 結構

1. **標題**（`Title`）：大號字級、中等字重，顏色為前景色。對話方塊開啟期間的修改會帶著柔和的模糊升起，舊文案隨之離開。
2. **說明**（`Description`）：小號字級、次要文字顏色，位於標題下方 8 px。為空時隱藏。
3. **關閉按鈕**（`ShowCloseButton`、`CloseButtonName`）：32 px 按鈕，帶 1 px 邊框與控制項圓角，與標題第一行對齊。填色為 50% 不透明度的弱化表面色。預設名稱為「關閉對話方塊」。
4. **頁首分隔線**：頁首下方一條邊框顏色的 1 px 線。
5. **內文**（`Content`）：四周各 24 px 內距。當面板高於視窗時，整個面板捲動。
6. **操作區**（`Actions`）：靠右對齊，位於內文下方 24 px。`Actions` 為 null 時隱藏。
7. **面板**：凸起表面顏色、1 px 邊框、表面圓角與浮動陰影。寬度由尺寸預設決定（見「尺寸」），且永遠不會超過視窗寬度減去兩側各 16 px 的邊距。
8. **遮罩**：接近黑色的壓暗層，兩種外觀下都為 46% 不透明度，並帶 7 px 的背景模糊。當「減少透明度」設定開啟時，模糊會被關閉。

### 從程式碼中確認

`Dialog.ConfirmAsync` 會建立對話方塊、等待選擇，並且只有按下確認按鈕時才回傳 `true`。按 Escape、點選遮罩或關閉按鈕時回傳 `false`。對於破壞性操作，將 `isDanger` 設為 `true`，確認按鈕就會使用 `Danger` 變體。

<Demo name="Confirmation" />

`Dialog.ShowAsync` 接受自訂內容與一組 `DialogAction`，並回傳被按下之操作的結果；對話方塊被關閉時回傳 `null`。當問題需要一個確認無法表達的內文時使用它，例如在三種匯出格式之間做選擇。

```csharp
var choice = await Dialog.ShowAsync(this, "匯出發票", exportForm,
    "請選擇格式。檔案由伺服器端產生。",
    new DialogAction("取消", false),
    new DialogAction("匯出", "csv"));
```

配套的紀錄型別 `DialogAction` 描述程式碼建立的對話方塊中的一個按鈕。

| 成員 | 型別 | 含義 |
|---|---|---|
| `Label` | `string` | 按鈕文字 |
| `Result` | `object?` | 按下此按鈕時 `ShowAsync` 回傳的值 |
| `Variant` | `ButtonVariant` | 用於取消類操作的 `Secondary`（預設）；用於主要操作的 `Primary` 或 `Danger` |

### 步驟標題

對話方塊開啟期間可以修改 `Title` 與 `Description`。新文案會在原位淡入替換，因此分步流程不需要第二個對話方塊。把步驟數量放在內文或一行小的狀態文字中，而不是標題裡，這樣標題改寫時長度不會變化太多。

<Demo name="Steps" />

```csharp
dialog.Title = "選擇角色";
dialog.Description = "編輯者可以修改內容；檢視者只能閱讀。";
```

### 尺寸

`Size` 決定面板寬度：`Small` 為 360 px，`Medium` 為 440 px（Arc 預設值），`Large` 為 560 px。面板永遠不會超過視窗寬度減去兩側各 16 px 的邊距，因此窄視窗會把每種尺寸都縮小以適應。

<Demo name="Sizes" />

```xml
<arc:Dialog Title="工作區限制" Size="Large" IsOpen="{Binding ShowLimits}">
    <TextBlock Text="限制依工作區分別套用。" />
</arc:Dialog>
```

對於單一問題且只有一兩個按鈕的場景，例如刪除確認，使用 `Small`。`Medium` 是表單與設定的預設值，適合大多數簡短任務。`Large` 適合包含多個欄位的表單，或使用者必須審閱的變更表格，例如降級前的方案限制。在整個應用程式中，每類決定使用一種尺寸，讓使用者知道按鈕總會出現在哪裡。

### 關閉與結果

`Closing` 在使用者關閉之前引發，並且可以取消。`Closed` 回報原因與結果。帶有 `arc:Dialog.Result` 的按鈕會設定結果，並以原因 `Action` 關閉對話方塊。帶有 `Command` 且沒有結果的按鈕本身不會關閉對話方塊；當工作成功時，由檢視模型把 `IsOpen` 設為 `false`，這樣在儲存進行期間對話方塊保持開啟，並回報原因 `Programmatic`。

```csharp
dialog.Closing += (_, e) =>
{
    if (e.Reason == DismissReason.PressOutside && isSaving)
    {
        e.Cancel = true;
    }
};
dialog.Closed += (_, e) => status.Text = e.Result is true ? "已儲存" : "已關閉，方式：" + e.Reason;
```

### 繫結到檢視模型

`IsOpen` 預設為雙向繫結，因此透過 Escape、遮罩或關閉按鈕關閉時，會把 `false` 寫回檢視模型。`Title` 與 `Description` 是單向的：文案由檢視模型持有，對話方塊負責顯示。對於可能失敗的工作，請在操作按鈕上使用 `Command`；結果屬性只用於不需要執行工作的回答。編譯繫結需要在檢視上設定 `x:DataType`；範例為求簡潔省略了它。

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

public interface IProjectService
{
    Task RenameAsync(string name);
}

public partial class ProjectSettingsViewModel : ObservableObject
{
    private readonly IProjectService _projects;

    public ProjectSettingsViewModel(IProjectService projects) => _projects = projects;

    [ObservableProperty]
    private bool _isRenaming;

    [ObservableProperty]
    private string _projectName = string.Empty;

    [ObservableProperty]
    private bool _isSaving;

    [RelayCommand]
    private async Task SaveNameAsync()
    {
        IsSaving = true;
        try
        {
            await _projects.RenameAsync(ProjectName);
            IsRenaming = false;
        }
        finally
        {
            IsSaving = false;
        }
    }

    [RelayCommand]
    private void CancelRename() => IsRenaming = false;
}
```

```xml
<arc:Dialog Title="重新命名專案"
            Description="請選擇一個團隊成員能快速辨識的簡短名稱。"
            IsOpen="{Binding IsRenaming}">
    <arc:Input Label="專案名稱" Text="{Binding ProjectName}" />
    <arc:Dialog.Actions>
        <StackPanel Orientation="Horizontal" Spacing="8">
            <arc:Button Variant="Secondary"
                        Content="取消"
                        Command="{Binding CancelRenameCommand}" />
            <arc:Button Content="儲存名稱"
                        Command="{Binding SaveNameCommand}"
                        IsEnabled="{Binding !IsSaving}" />
        </StackPanel>
    </arc:Dialog.Actions>
</arc:Dialog>
```

只有當檢視模型把 `IsRenaming` 設為 `false` 時，對話方塊才會關閉。如果重新命名失敗，對話方塊保持開啟並保留使用者輸入的名稱，檢視模型可以在輸入框的 `ErrorMessage` 中顯示錯誤。

### 範例：帳單主控台中的訂閱取消

帳單頁面為 Team 方案提供「取消訂閱」。對話方塊以淺白的語言說明下一個計費日會發生什麼，按鈕分別為「保留方案」與「取消訂閱」。「保留方案」是安全的預設選項，以次要變體放在左側；破壞性操作以危險變體放在右側。兩個按鈕都會設定結果，因此呼叫端只需讀取一個值，無需知道使用的是哪個按鈕。按 Escape 或關閉按鈕會在沒有結果的情況下關閉對話方塊，因此檢查 `true` 的處理程式會把它們當作「保留方案」。

```xml
<arc:Dialog Title="取消 Team 方案？"
            Description="Acme Analytics 將保留存取權限至 2026 年 10 月 31 日。目前計費週期不予退款。"
            Size="Small"
            IsOpen="{Binding IsCancelling}">
    <arc:Dialog.Actions>
        <StackPanel Orientation="Horizontal" Spacing="8">
            <arc:Button Variant="Secondary"
                        Content="保留方案"
                        arc:Dialog.Result="{x:False}" />
            <arc:Button Variant="Danger"
                        Content="取消訂閱"
                        arc:Dialog.Result="{x:True}" />
        </StackPanel>
    </arc:Dialog.Actions>
</arc:Dialog>
```

### 文案規範

對話方塊只提出一個問題，其標題就是這個問題或操作。標題依句子格式書寫。確認類標題以問號結尾；說明寫明會發生什麼變化，指明對象，數量用阿拉伯數字。按鈕文字是動詞加受詞，讓使用者在按下之前就知道結果。不要單獨使用「好」「是」「否」，它們迫使使用者回讀標題才知道自己同意了什麼。

| 不要寫 | 改為 |
|---|---|
| 你確定嗎？ | 刪除專案？ |
| 好 | 刪除專案 |
| 是 / 否 | 取消訂閱 / 保留方案 |
| 出錯了 | 無法儲存名稱，請再試一次。 |
| 點選此處繼續 | 繼續前往帳單 |
| 刪除 42 項？ | 封存 42 張發票？ |

### 最佳做法

**建議**

- 把破壞性操作放在右側，使用 `Danger` 變體；把安全操作放在左側，使用 `Secondary` 變體。人們從左到右閱讀，主要位置正是他們期待送出操作的地方。
- 在標題與按鈕中都寫明對象（「刪除專案？」與「刪除專案」）。只掃讀按鈕的讀者仍然知道自己同意了什麼。
- 對於不需要自訂內文的是或否問題，使用 `Dialog.ConfirmAsync`，結果就是一個 `bool`，呼叫處也更簡短。
- 保持關閉按鈕與 Escape 可用。它們是鍵盤與輔助技術的退出方式，對於不知道 Escape 的滑鼠使用者來說，關閉按鈕是唯一可見的退出途徑。

**避免**

- 對話方塊中的長表單。內文會捲動，但捲動的面板會把操作按鈕隱藏在首屏以下；`Drawer` 讓頁面保持可見，表單放在更長的面板中。
- 為防止遺失工作而用 `CloseOnEscape` 停用 Escape。應為 `Escape` 原因處理 `Closing`，並在儲存進行時取消它，這樣工作完成後鍵盤退出途徑仍然可用。
- 為一個後續的單一問題在對話方塊之上再疊加對話方塊。每層都會增加遮罩與焦點陷阱，且只有最上層回應 Escape，因此使用者需要關閉兩個面板才能返回。
- 在工作成功之前由命令關閉對話方塊。呼叫失敗時，使用者將既沒有對話方塊也沒有錯誤提示；只有在工作成功後才把 `IsOpen` 設為 `false`。

### 焦點與關閉

對話方塊開啟時焦點移入其中，第一個可取得焦點的元素會取得焦點，除非為該呈現設定了 `InitialFocus`。對話方塊為最上層的模態時，Tab 與 Shift+Tab 停留在其中。關閉時，焦點回到開啟對話方塊之前擁有焦點的控制項，也就是啟動該流程的按鈕。

當 `CloseOnEscape` 為 true（預設值）時，Escape 關閉對話方塊。當 `CloseOnOutsidePress` 為 true 時，按下遮罩會關閉對話方塊。兩種原因都會經過 `Closing`，因此應用程式可以在儲存進行時取消它們。只有在頁面必須在對話方塊旁邊保持可用時，才將 `IsModal` 設為 `false`；此時遮罩與焦點陷阱都會被移除。允許堆疊對話方塊：只有最上層的對話方塊回應 Escape，因此按下它會關閉內層對話方塊，外層對話方塊保持開啟。

### 常見問題

**為什麼儲存期間按 Escape 沒有反應？** 應用程式在儲存進行時會取消 `Escape` 原因的 `Closing` 事件。只有當對話方塊絕不能透過鍵盤關閉時，才將 `CloseOnEscape` 設為 `False`，因為這會完全移除鍵盤退出途徑。

**使用者關閉對話方塊時結果為什麼是 null？** 對於 Escape、遮罩與關閉按鈕，`ShowAsync` 與 `ConfirmAsync` 回傳 `null`；否則回傳被按下操作的 `Result`。請檢查你預期的值，而不是與 `null` 比較。

**帶有 `Command` 的按鈕會關閉對話方塊嗎？** 不會。只有帶有 `arc:Dialog.Result` 的按鈕才會關閉它。對於可能失敗的命令，請在工作成功後於檢視模型中把 `IsOpen` 設為 `false`。

**如何讓 Enter 觸發主要按鈕？** 在 `Actions` 中為主要按鈕設定 `IsDefault="True"`。之後在對話方塊中按 Enter 就會按下該按鈕，與任何 Avalonia 視窗一樣。

**能否從尚不在視窗中的控制項開啟對話方塊？** 使用位於視窗中的視覺元素呼叫 `ShowAsync(owner)`。對話方塊透過該擁有者找到視窗，並在開啟期間加入邏輯樹。

## API 參考

<!-- api: Dialog -->

## 無障礙

- 面板名稱取自 `Title`，說明文字取自 `Description`。
- 關閉按鈕預設名稱為「關閉對話方塊」，可透過 `CloseButtonName` 修改。
- 對話方塊開啟時焦點移入其中，Tab 與 Shift+Tab 停留在對話方塊內。關閉時焦點返回給開啟對話方塊的控制項。
- 當 `CloseOnEscape` 為 true 時，Escape 關閉對話方塊，並把焦點返回給開啟它的控制項。
- 對話方塊是模態介面：開啟期間，其後的頁面無法透過鍵盤到達。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 移到對話方塊內的下一個或上一個控制項，到達兩端時循環 |
| 按鈕上的 `Enter` 或 `Space` | 按下取得焦點的按鈕；帶有 `arc:Dialog.Result` 的按鈕會以該結果關閉對話方塊 |
| `Escape` | 以原因 `Escape` 關閉，除非 `CloseOnEscape` 為 false 或 `Closing` 取消了它 |

## 動態效果

- 遮罩淡入，面板以平滑彈簧從縮放 0.96 上升 8 px，同時淡入。關閉動畫較短：面板移動 4 px 並縮放到 0.98，兩者都快速淡出。關閉從進場的當前狀態開始，因此在進場期間關閉的對話方塊不會跳動。
- 標題或說明變化時，帶著柔和的模糊升起。
- 減少動態效果時只保留短暫的不透明度淡入。關閉動態效果時，對話方塊立即顯示或移除。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 開啟 | 遮罩淡入；面板以平滑彈簧從 0.96 上升 8 px，同時淡入 | 短暫的不透明度淡入 | 立即完成 |
| 關閉 | 面板移動 4 px 並縮放到 0.98，兩者快速淡出 | 短暫的不透明度淡入 | 立即完成 |
| 標題或說明變化 | 新文案帶著柔和的模糊升起 | 瞬時淡入 | 立即完成 |
| 按下關閉按鈕 | 按鈕縮放到 0.96，放開後彈回 | 不縮放 | 不縮放 |

## 相關元件

- `Drawer`：用於長表單及需保持頁面可見的詳細資料面板。
- `BottomSheet`：用於以行動裝置為主、停駐在預覽高度的任務。
- `Popover`：用於不阻擋頁面的互動內容。
- `Alert`：用於留在頁面內的訊息，`Toast` 用於簡短的操作結果。

| 元件 | 適用情境 |
|---|---|
| `Drawer` | 表單或詳細資料太長而無法放進一個面板，或頁面必須保持可見 |
| `BottomSheet` | 任務以行動裝置為主，並且在預覽高度下閱讀效果良好 |
| `Popover` | 內容很小，錨定在一個控制項上，且不需要遮罩 |
| `DropdownMenu` / `ContextMenu` | 內容是一個指令清單，而不是一個問題 |
| `Alert` | 訊息留在頁面中，直到問題被解決 |
| `Toast` | 簡短的結果，例如「專案已重新命名」，應自行消失 |
