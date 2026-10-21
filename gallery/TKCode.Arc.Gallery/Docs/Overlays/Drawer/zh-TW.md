---
title: 抽屜
summary: 用於專注工作的暫時側邊面板。
source-hash: b42421ed7675
---

<Demo name="Sides" />

## 何時使用

`Drawer` 是一種模態面板，從視窗的某一側邊緣滑入，並在邊緣處保留後方頁面的可見部分。它適合依賴目前檢視的專注工作：使用者篩選清單並看到結果隨之變化，或檢視某一筆紀錄而不遺失其在表格中的位置。它可以透過按鈕、Escape、按一下遮罩或朝邊緣拖曳來關閉，因此很容易離開，並回到頁面上的同一位置。

- **管理後台清單的篩選**：應收帳款清單在使用者於右側抽屜中設定狀態、日期範圍和負責人時保持列可見，然後套用或重設篩選條件。
- **發票或訂單明細**：客服人員選取一張發票，在抽屜中檢視其明細項目、付款紀錄和備註，然後關閉抽屜並選取下一列，頁面無需切換。
- **所選項目的設定**：產品編輯器開啟左側抽屜，顯示正在編輯的項目的規格與定價設定，目錄始終保持可見。
- **對話方塊容納不下的長表單**：包含銀行資料與稅務欄位的供應商建檔表單，超出短面板的容量，但使用者仍需要看到背後的供應商清單。
- **行動裝置與窄視窗**：在窄視窗中，抽屜幾乎占滿整個寬度，並把頁首保留為拖曳把手，因此使用者可以透過快速滑動將其關閉。

## 何時不用

- **簡短的決定與確認**，例如刪除專案：用 `Dialog`，它置中顯示，並把後果和按鈕放在同一個面板中。
- **需要停靠在多個高度的行動裝置優先面板**，例如配送選項或分享面板：用 `BottomSheet`，它停在預覽高度，並分段展開。
- **錨定在控制項上、沒有遮罩的小型內容**，例如欄位選擇器：用 `Popover`。
- **針對目前選取項目執行的命令清單**：用 `DropdownMenu` 或 `ContextMenu`。
- **應用程式各部分之間的導覽**：頁面內的分區用 `Tabs`，應用程式的主要區域用側邊欄。承載導覽的抽屜會把目的地藏在一個可關閉的面板後面。

## 用法

`Drawer` 衍生自 `ModalOverlay`，而 `ModalOverlay` 本身是 `ContentControl`。它的 `Content` 是可捲動的內文，`Title` 和 `Description` 組成頁首，`Actions` 是頁尾，`Side` 選擇邊緣，`IsOpen` 顯示或隱藏面板。此控制項在宣告的位置不佔用空間；當 `IsOpen` 為 true 時，面板會在視窗的覆蓋層上滑入，並位於輕微模糊的遮罩之上。可以在任何位置宣告它，並繫結 `IsOpen`。

### 邊緣

`Side` 設定面板滑入的邊緣：`Left`、`Right`、`Top` 或 `Bottom`。預設值為 `Right`。

```xml
<arc:Drawer Title="篩選"
            Description="不離開清單即可縮小範圍。"
            Side="Left"
            IsOpen="{Binding ShowFilters}">
    <StackPanel Spacing="16">
        <CheckBox Content="僅未結發票" />
        <CheckBox Content="包含草稿" />
    </StackPanel>
</arc:Drawer>
```

在寬視窗中，若清單位於一側，篩選與明細抽屜適合使用 `Right`，因為清單在左側保持可見。`Left` 適合使用者在清單之前瀏覽的內容，例如分類樹。`Top` 和 `Bottom` 橫跨整個寬度；底部抽屜會顯示一個把手，提示它可以被拖走。每類工作使用一個邊緣，讓使用者熟悉篩選或明細面板的開啟位置。

### 結構

1. **頁首**（`Title`、`Description`）：頁首就是拖曳把手。它的內距為 24 px（窄視窗時為 20 px），下方有 1 px 邊框。標題使用大字級與中等字重；描述使用小字級與次要文字顏色，位於標題下方 8 px，最寬 512 px。
2. **關閉按鈕**（`ShowCloseButton`、`CloseButtonName`）：32 px 的按鈕，使用控制項圓角與 1 px 邊框，與標題的第一行對齊。它的填色為淡化的表面色，不透明度 65%。它始終存在，預設名稱為「關閉抽屜」。
3. **把手**：僅在 `Bottom` 抽屜上顯示。它是 36 × 4 px 的膠囊形，使用強邊框色，不透明度 75%，位於頁首頂部下方 9 px。
4. **內文**（`Content`）：24 px 內距（窄視窗時為 20 px）。頁首保持不動，內文捲動。
5. **操作區**（`Actions`）：靠右對齊，位於內文下方 24 px。當 `Actions` 為 null 時隱藏。
6. **面板**：位於 `Left` 或 `Right` 邊緣時，寬度最多 480 px（30 rem），且不超過視窗減去 16 px 邊距（窄視窗時為 12 px）。位於 `Top` 或 `Bottom` 邊緣時，面板橫跨整個寬度，高度最多 512 px；若視窗高度較小，則取視窗高度。位於內側的圓角半徑為 22 px（窄視窗時為 18 px），細邊框只繪製在內側邊緣。
7. **遮罩**：接近黑色的暗化層，不透明度 38%，比 `Dialog` 下方的遮罩更淺，並帶有 4 px 的背景模糊。開啟「減少透明度」設定時，模糊關閉。

視窗寬度小於 40 rem（640 px）時，面板會變窄：幾乎占滿整個寬度，圓角與內距隨之收緊。

### 長內容

內文會捲動，而頁首保持不動。把完整內容放進抽屜即可，無須計算其尺寸。當抽屜比視窗更高時，內文會捲動，因此使用者在瀏覽明細項目清單時，標題、描述和關閉按鈕始終可見。

<Demo name="LongContent" />

```xml
<arc:Drawer Title="INV-2041" Description="Ada Lovelace 於 3 月 12 日付款。" IsOpen="{Binding ShowRecord}">
    <ItemsControl ItemsSource="{Binding LineItems}" />
</arc:Drawer>
```

對於包含數百列的清單，請對資料分頁，或在內文中使用會虛擬化其項目的控制項。內文是一般的捲動區域，因此放入其中的每個項目都會被量測與排列。

### 操作

把按鈕放在 `Actions` 中。帶有 `arc:Dialog.Result` 的按鈕會以該值關閉抽屜，與 `Dialog` 中的行為相同。篩選抽屜通常提供一個次要的「重設」（回傳 `false`）和一個主要的「套用」（回傳 `true`），因此呼叫端可以在抽屜關閉後讀取一個結果。

```xml
<arc:Drawer Title="篩選" IsOpen="{Binding ShowFilters}">
    <arc:Drawer.Actions>
        <StackPanel Orientation="Horizontal" Spacing="8">
            <arc:Button Variant="Secondary" Content="重設" arc:Dialog.Result="{x:False}" />
            <arc:Button Content="套用" arc:Dialog.Result="{x:True}" />
        </StackPanel>
    </arc:Drawer.Actions>
</arc:Drawer>
```

### 從程式碼開啟

當同一個抽屜服務多個邊緣時，請同時設定 `Side` 和 `IsOpen`。`OpenCommand` 可以從按鈕開啟抽屜，而無須設定 `IsOpen`。

```csharp
drawer.Side = DrawerSide.Bottom;
drawer.IsOpen = true;

openDetails.Command = details.OpenCommand;
```

### 關閉原因

`Closed` 透過 `OverlayClosedEventArgs.Reason` 回報抽屜關閉的原因：`CloseButton`、`Escape`、`PressOutside`、`Gesture`、`Action` 或 `Programmatic`。`Closing` 會在使用者關閉之前引發，並且可以透過 `e.Cancel` 取消，例如當抽屜中的表單有未儲存的變更時。

```csharp
bool hasUnsavedChanges = true; // 表單欄位變更時由表單設定
drawer.Closing += (_, e) =>
{
    if (e.Reason is DismissReason.PressOutside or DismissReason.Gesture && hasUnsavedChanges)
    {
        e.Cancel = true;
    }
};
drawer.Closed += (_, e) => log.Text = $"已由 {e.Reason} 關閉";
```

### 拖曳關閉

頁首就是拖曳把手。朝邊緣拖曳時，面板跟隨指標。背離邊緣拖曳時會受到平方根曲線的阻力，因此面板只移動一點並彈回。放開時，若拖曳距離超過面板尺寸的三分之一，或朝邊緣的快速滑動速度超過 500 px/s，抽屜就會關閉；否則它會以俐落的彈簧彈回。在頁首中的按鈕或文字方塊上按下是點選而非拖曳，因此關閉按鈕仍然有效。在減少動態效果以及關閉動態效果時，拖曳會被停用，因此關閉操作永遠不依賴手勢。

### 繫結到檢視模型

`IsOpen` 預設為雙向繫結，因此透過 Escape、遮罩或拖曳關閉時，會把 `false` 寫回檢視模型。抽屜的 `Title` 和 `Description` 是單向繫結，在抽屜開啟期間變更時會帶著柔和模糊切換；當抽屜顯示清單剛選取的紀錄時，這一點很有用。

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

public partial class ReceivablesViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isDetailOpen;

    [ObservableProperty]
    private Invoice? _selectedInvoice;

    [RelayCommand]
    private void OpenInvoice(Invoice invoice)
    {
        SelectedInvoice = invoice;
        IsDetailOpen = true;
    }

    [RelayCommand]
    private async Task MarkPaidAsync()
    {
        await _invoices.MarkPaidAsync(SelectedInvoice!.Id);
        IsDetailOpen = false;
    }
}
```

```xml
<arc:Drawer Title="{Binding SelectedInvoice.Number}"
            Description="{Binding SelectedInvoice.Summary}"
            Side="Right"
            IsOpen="{Binding IsDetailOpen}">
    <ItemsControl ItemsSource="{Binding SelectedInvoice.LineItems}" />
    <arc:Drawer.Actions>
        <arc:Button Content="標記為已付款" Command="{Binding MarkPaidCommand}" />
    </arc:Drawer.Actions>
</arc:Drawer>
```

### 範例：應收帳款清單中的發票明細

財務團隊在寬視窗的表格中審查逾期發票。選取一列會開啟右側抽屜，它以發票編號作為標題，以客戶和到期日作為描述，並在內文中顯示明細項目。抽屜不會遮住表格：使用者可以關閉抽屜並選取另一列，從而將下一張發票與上一張比較。操作區中，「發送提醒」使用次要變體，「標記為已付款」使用主要變體；檢視模型在付款紀錄完成後關閉抽屜，因此呼叫失敗時抽屜保持開啟，發票仍然可見。

```xml
<arc:Drawer Title="INV-2041"
            Description="Northwind Traders，2026 年 9 月 30 日到期，已逾期 21 天。"
            Side="Right"
            IsOpen="{Binding IsDetailOpen}">
    <StackPanel Spacing="12">
        <TextBlock Text="設計審查，12 小時，每小時 180 EUR" />
        <TextBlock Text="原型建置，20 小時，每小時 150 EUR" />
        <TextBlock Text="差旅與住宿，1 項，640 EUR" />
        <TextBlock Text="應付總額：EUR 5,800.00"
                   FontWeight="SemiBold"
                   Foreground="{arc:ArcDesignTokenResource Foreground}" />
    </StackPanel>
    <arc:Drawer.Actions>
        <StackPanel Orientation="Horizontal" Spacing="8">
            <arc:Button Variant="Secondary" Content="發送提醒" />
            <arc:Button Content="標記為已付款" Command="{Binding MarkPaidCommand}" />
        </StackPanel>
    </arc:Drawer.Actions>
</arc:Drawer>
```

### 文案規範

頁首應命名對象或工作：篩選抽屜用「篩選」，紀錄抽屜用發票編號。描述用一句話說明範圍，其中的數字使用阿拉伯數字。操作按鈕使用動詞加對象，篩選抽屜使用「套用篩選」而不是「確定」。

| 不要寫 | 改為 |
|---|---|
| 選項 | 篩選 |
| INV2041 明細 | INV-2041 |
| 提交 | 發送提醒 |
| 確定 | 套用篩選 |
| 按一下 X | 關閉抽屜 |
| 明細（13 項） | 明細項目（13） |

抽屜開啟期間保持標題穩定。如果標題在每次互動時都變化，頁首就會透過模糊的文字不斷閃動，因此只在所選對象變更時才更改標題。

### 最佳做法

**建議**

- 當抽屜與清單相關時，讓清單在抽屜後方保持可見。上下文讓使用者能夠比較各列，並立即看到篩選的效果。
- 依工作選擇邊緣：寬視窗上的明細與篩選使用 `Right`；窄視窗上的短面板使用 `Bottom`，並配有可拖走的把手。
- 為抽屜設定 `Title`。它是面板的無障礙名稱，而頁首又是拖曳把手，空白的頁首難以辨認。
- 保持關閉按鈕和 Escape 鍵可用；拖曳只是離開的額外方式，而不是唯一方式。

**避免**

- 在抽屜中放置簡短的是或否決定。`Dialog` 會把問題置中，而抽屜會把後果藏在視窗邊緣。
- 在抽屜中巢狀放置抽屜。疊加的遮罩會讓頁面難以閱讀，而且只有最上層的面板會回應 Escape。
- 把使用者必須頻繁存取的導覽放在抽屜中。按一下遮罩即會關閉抽屜，因此常用目的地應放在常駐的側邊欄中。
- 頁首中的長文字。標題保持幾個詞，描述保持一句話；描述最寬 512 px。
- 在內文中放置水平捲動。內文只做垂直捲動；寬表格應當縮窄或重新安排，以適應面板。

### 常見問題

**為什麼我放開頁首後抽屜關閉了？** 放開時拖曳距離超過面板的三分之一，或朝邊緣的快速滑動速度超過 500 px/s。請以 `Gesture` 原因處理 `Closing`，並在表單有未儲存的變更時取消關閉。

**如何在沒有程式碼後置的情況下從按鈕開啟抽屜？** 透過名稱把按鈕的命令繫結到抽屜的 `OpenCommand`，例如 `Command="{Binding #RecordDrawer.OpenCommand}"`，或在檢視模型中繫結 `IsOpen`。

**為什麼抽屜的內文不捲動？** 當內容高於面板時，內文才會捲動。移除內文中巢狀的 `ScrollViewer`：內文本身已經捲動全部內容，第二個捲動區域會分散捲動。

**底部邊緣的抽屜會顯示把手嗎？** 會。把手只屬於底部面板；頂部、左側和右側抽屜不顯示它，因為它們的拖曳方向從邊緣就能看出。

**儲存失敗後如何讓抽屜保持開啟？** 在 `Closing` 中取消關閉，或在儲存成功之前不要把 `IsOpen` 設為 `false`。抽屜會帶著內容保持開啟，因此錯誤可以顯示在欄位旁邊。

## API 參考

<!-- api: Drawer -->

抽屜的邊緣由配套列舉 `DrawerSide` 表示，成員為 `Left`、`Top`、`Right` 和 `Bottom`。預設值為 `Right`。

## 無障礙

- 面板的名稱來自 `Title`，說明文字來自 `Description`。
- 抽屜開啟時焦點移入其中，Tab 與 Shift+Tab 停留在抽屜內，關閉時焦點回到開啟它的元素。
- 當 `CloseOnEscape` 為 true 時，Escape 關閉抽屜。關閉按鈕始終存在，預設名稱為「關閉抽屜」；可用 `CloseButtonName` 修改。
- 拖曳是可選的。在減少動態效果下它會被關閉，因此關閉操作從不依賴手勢。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 移到抽屜內的下一個或上一個控制項，到達兩端時循環 |
| 按鈕上的 `Enter` 或 `Space` | 按下取得焦點的按鈕；帶有 `arc:Dialog.Result` 的按鈕會以該結果關閉抽屜 |
| `Escape` | 以 `Escape` 原因關閉，除非 `CloseOnEscape` 為 false 或 `Closing` 將其取消 |

抽屜不能用鍵盤移動。拖曳頁首只是指標或觸控操作，因此關閉按鈕和 Escape 是鍵盤離開的途徑。

## 動態效果

- 面板以平滑彈簧從其邊緣滑入，同時遮罩淡入。
- 朝邊緣拖曳頁首即可讓面板跟隨指標。拖曳超過面板的三分之一，或快速滑動速度超過 500px/s 時，抽屜關閉；否則它會彈回。
- 按一下、Escape 或按下遮罩會以短暫的補間動畫關閉抽屜，陰影在行程的最後一段淡出。快速滑動則以保留放開速度的彈簧關閉它。
- 減少動態效果會關閉拖曳，並將滑動替換為短暫的淡入。關閉動態效果時，抽屜立即顯示或消失。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 開啟 | 面板以平滑彈簧從其邊緣滑入；遮罩淡入 | 短暫淡入 | 立即完成 |
| 透過按鈕、Escape 或遮罩關閉 | 標準補間滑出；陰影在行程的最後一段淡入 | 短暫淡入 | 立即完成 |
| 透過快速滑動關閉 | 保留放開速度的彈簧 | 短暫淡入 | 立即完成 |
| 頁首拖曳 | 朝邊緣跟隨指標；反方向受阻 | 停用拖曳 | 停用拖曳 |
| 標題或描述變化 | 新文案帶著柔和模糊升起 | 瞬間淡入 | 立即完成 |

## 相關元件

- `Dialog`：必須打斷頁面、先行作出的決定。
- `BottomSheet`：行動裝置優先的面板，停在預覽高度，並可展開。
- `Popover`：錨定在某個控制項上的小型互動內容。

| 元件 | 適用情境 |
|---|---|
| `Dialog` | 問題簡短，且必須先於其他操作作出回答 |
| `BottomSheet` | 工作以行動裝置為主，先停在預覽高度，之後再展開 |
| `Popover` | 內容很小，錨定在一個控制項上，且不需要遮罩 |
| `Tabs` | 同一頁面的分區原地切換，且不應有面板遮住頁面 |
| `ResizablePanels` | 清單與明細都是頁面的固定部分，且使用者需要調整它們的大小 |
| `ExpandableCard` | 紀錄在頁面內展開，周圍的列需要隨之移動 |
