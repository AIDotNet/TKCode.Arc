---
title: 按鈕
summary: 清楚、反應迅速的操作按鈕，次要狀態保持低調。
source-hash: 2e7052f3cb01
---

<Demo name="Variants" />

## 何時使用

`Button` 是頁面、表單、對話方塊或工具列上單一操作的預設控制項。它提供產品所需的四種視覺層級（主要、次要、幽靈與危險）、三種高度、一個在載入時保持標籤寬度的狀態，以及一個可以原地變化的標籤。應用程式中的大多數操作都是這個控制項，其他 Arc 操作控制項則是同一理念下的特定情境。

- **在 CRM 中儲存客戶紀錄**：聯絡人表單底部放一個「儲存變更」主要按鈕，旁邊是「取消」次要按鈕。每個表單只有一個主要操作，能告訴使用者下一步是什麼。
- **在帳務主控台中建立發票**：「建立發票」作為對話方塊底部的主要操作，「儲存為草稿」作為次要搭檔。兩者使用相同的中等高度，讓底部區域保持對齊。
- **在結帳設定頁中新增卡片**：「新增卡片」為主要按鈕，旁邊的「檢視紀錄」為幽靈按鈕，它不會與主要操作爭奪注意力。
- **在分析儀表板中匯出報表**：產生檔案時「匯出報表」使用 `IsLoading`。標籤保持寬度，載入指示顯示在標籤原來的位置，鍵盤焦點仍停留在按鈕上。
- **在設定頁中刪除專案**：「刪除專案」使用危險按鈕，讓顏色在按下之前就發出警告。若刪除無法復原，應再搭配一個確認步驟。

## 何時不用

- **送出後必須在按鈕上顯示結果，例如「已儲存」或「已送出」**：使用 `ActionButton`，它保存待處理和成功兩組文字，並會自動回到閒置狀態。
- **同一操作有兩到五個相近的替代方式**：使用 `SplitButton`，把替代方式放在它的選單中。
- **需要刻意按住才能執行的破壞性操作**：使用 Gestures 中的 `HoldToConfirm`，避免一次誤點就刪除資料。
- **需要保持選取的值，例如檢視模式或時間範圍**：使用 Toggles 中的 `SegmentedControl`。按鈕只執行一次操作，不會記住選擇。
- **同一物件上的多個相關操作，例如分享、複製和封存**：使用 `ButtonGroup`，它把它們合併為一個帶分隔線的整體，而不是一排獨立按鈕。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:Button Content="儲存變更" Variant="Primary" />
</UserControl>
```

`Button` 衍生自 Avalonia 的 `Button`，因此 `Content`、`Click`、`Command`、`CommandParameter`、`IsEnabled`、`IsDefault`
以及 Enter 與空白鍵的啟動方式，都與任何按鈕一樣運作。Arc 層在此之上加入變體、尺寸、載入狀態，以及 Arc 外框、焦點環和動態效果。
由於 `Content` 承載標籤，按鈕可以顯示任何內容，而不只是文字，但應保持標籤簡短，使寬度可預期。

### 結構

1. **外框**：1 px 邊框，圓角為控制項圓角（`RadiusControl`）。填滿色與邊框顏色由變體決定。
2. **標籤**（`Content`）：小號內文字級（`TypeSM`）、中等字重，置中顯示。內容變動時，新文字會在原位升起。
3. **載入指示**（`IsLoading`）：16 px 的圓環，載入時在外框中央旋轉。標籤留在原位並淡出，因此寬度不變。
4. **內距**（隨尺寸）：`Small` 為 12 px，`Medium` 為 16 px，`Large` 為 20 px，兩側各一份。
5. **高度**（`MinHeight`，隨尺寸）：`Small` 為 36 px，`Medium` 為 44 px，`Large` 為 50 px。
6. **焦點環**：2 px 的焦點色環，繪製在外框外側 2 px 處，圓角為 22 px。僅在鍵盤焦點時出現。

### 變體

`Variant` 設定視覺層級。每個表面只使用一個 `Primary` 操作。其他變體回答的問題是：「這個操作相對於旁邊的那個有多重要？」

| 變體 | 填滿與邊框 | 文字 | 適用情境 |
|---|---|---|---|
| `Primary` | 前景色填滿 | 背景色 | 表面上的主要操作，例如「儲存變更」或「建立發票」 |
| `Secondary` | 帶邊框的表面 | 前景色 | 主要按鈕旁邊的替代操作，例如「取消」或「儲存為草稿」 |
| `Ghost` | 透明，無邊框 | 次要文字，懸停時為前景色 | 卡片與工具列中的低強調操作，例如「檢視紀錄」 |
| `Danger` | 帶邊框的表面 | 危險色 | 破壞性操作，顏色在按下之前發出警告 |

```xml
<arc:Button Content="儲存變更" Variant="Primary" />
<arc:Button Content="取消" Variant="Secondary" />
<arc:Button Content="瞭解更多" Variant="Ghost" />
<arc:Button Content="刪除專案" Variant="Danger" />
```

每種變體都有自己的狀態。`Primary` 在懸停和按下時降低不透明度。`Secondary` 與 `Ghost` 在懸停和按下時以淡化的表面色填滿。
放在凸起表面（對話框、抽屜、底部面板、彈出框或選單）上時，`Ghost` 改用更明顯的填色，因為淡化的表面色與凸起表面幾乎同色。
`Danger` 在懸停和按下時把邊框變為危險色。停用按鈕保持其形狀：`Primary` 淡化到 52% 的不透明度，而描邊變體切換為淡化文字與淡化表面。
停用的危險標籤會向表面色偏移，因此仍能辨認為危險操作。

將 `Danger` 按鈕與 `Primary` 操作分開放置，中間隔一個次要按鈕或留出明顯的間距，避免兩者被誤認。

### 尺寸

`Size` 設定最小高度：`Small` 為 36 px，`Medium` 為 44 px（預設），`Large` 為 50 px。尺寸不會隨視窗寬度變化，因此視窗變窄時，一排按鈕的節奏保持不變。

<Demo name="Sizes" />

每個表單、對話方塊或工具列使用一種尺寸。`Small` 適合表格、工具列和行內操作，`Medium` 是表單、對話方塊和設定頁的預設值，`Large` 適合登入頁或空白狀態頁上單一的醒目操作。內距隨尺寸變化，使標籤的邊距與高度保持比例。

### 載入與標籤變形

將 `IsLoading` 設為 `True` 以顯示載入指示。標籤按其寬度留在原位，因此請求執行時對話方塊的底部區域不會移動。按鈕保持鍵盤焦點，但在工作結束前會忽略點擊與按鍵。更改 `Content` 會替換標籤：新文字從柔和模糊中升起 4 px，舊文字上移 3 px 並淡出，寬度彈性地調整為新標籤的寬度。

<Demo name="LoadingMorph" />

```xml
<arc:Button Content="{Binding SaveLabel}"
            IsLoading="{Binding IsSaving}"
            Variant="Primary" />
```

`IsLoading` 適用於幾秒內完成的工作。對於耗時更長的任務，例如匯出數千列資料，應在頁面中顯示進度（例如進度指示器或狀態列），讓按鈕只負責發起任務的請求。轉了一分鐘的載入圓環無法告訴人們工作進展到了哪裡。

### 純圖示按鈕

純圖示按鈕沒有可見文字，因此要為它提供無障礙名稱。寬度為 48 px 或更窄的按鈕被視為圖示按鈕：按下時會比文字按鈕更深，壓至其尺寸的 96%。

```xml
<arc:Button Size="Small" Variant="Ghost"
            AutomationProperties.Name="關閉">
    <TextBlock Text="×" />
</arc:Button>
```

為每個純圖示按鈕提供一個描述其操作的名稱，例如「關閉」或「移除篩選」；當圖示含義不夠直觀時，還應加上工具提示。螢幕閱讀器朗讀的就是這個名稱，對於看不見圖示的人來說，它是辨識該操作的唯一途徑。

### 停用

將 `IsEnabled` 設為 `False`。描邊變體保持其形狀，因此無法使用的操作仍像按鈕。停用的按鈕和其他停用控制項一樣移出 Tab 順序。

<Demo name="Disabled" />

只在操作確實無法執行時才停用，並在附近說明原因，例如在按鈕下方寫「連接付款方式後才能發布發票」。沒有原因的停用按鈕只會讓人去猜問題出在哪裡。

### 繫結到檢視模型

按鈕有命令、標籤和忙碌狀態。工作由檢視模型負責，按鈕負責顯示其進度。下例使用 CommunityToolkit.Mvvm 以及一個儲存草稿的應用程式服務。

```csharp
public interface IInvoiceService
{
    Task SaveDraftAsync(string invoiceId);
}

public partial class InvoiceViewModel : ObservableObject
{
    private readonly IInvoiceService _invoices;

    public InvoiceViewModel(IInvoiceService invoices)
    {
        _invoices = invoices;
    }

    [ObservableProperty]
    private string _invoiceId = "inv_2026_0412";

    [ObservableProperty]
    private bool _isSaving;

    [ObservableProperty]
    private string _saveLabel = "儲存變更";

    [RelayCommand]
    private async Task SaveAsync()
    {
        IsSaving = true;
        try
        {
            await _invoices.SaveDraftAsync(InvoiceId);
            SaveLabel = "已儲存";
        }
        finally
        {
            IsSaving = false;
        }
    }
}
```

```xml
<StackPanel Width="420" Spacing="16">
    <arc:Input Label="發票編號" Text="{Binding InvoiceId}" />
    <arc:Button Content="{Binding SaveLabel}"
                Command="{Binding SaveCommand}"
                IsLoading="{Binding IsSaving}"
                Variant="Primary"
                HorizontalAlignment="Right" />
</StackPanel>
```

所有繫結都是單向的。`Content`、`IsLoading` 和 `Variant` 來自檢視模型，按鈕不會寫回任何值。按鈕會觸發 `Click` 並執行 `Command`，因此工作應放在 `Command` 中。`IsLoading` 為 `True` 時，按鈕會忽略啟動，因此使用者再次按下時，`Command` 不會第二次執行。編譯繫結需要在檢視上設定 `x:DataType`；為求簡潔，範例中省略了它。

### 範例：帳務設定頁底部

SaaS 帳務主控台中的付款設定面板。主要操作「儲存變更」位於最右側，這是讀完表單後視線最終落點的位置。「取消」在其左側，作為次要選項。「移除卡片」是左側的危險按鈕，與儲存組分開放置，使破壞性操作不會離儲存只有一次誤觸的距離。「檢視紀錄」是幽靈按鈕，因為它只是跳轉，不會更改任何內容。

![帳務設定頁底部，包含幽靈、危險、次要和主要按鈕](images/BillingFooter.png)

```xml
<StackPanel Width="600" Spacing="20">
    <StackPanel Spacing="6">
        <TextBlock Text="Payment method"
                   FontSize="20"
                   FontWeight="SemiBold"
                   Foreground="{arc:ArcDesignTokenResource Foreground}" />
        <TextBlock Text="Visa ending 4242, used for the Growth plan. Next invoice on 1 November 2026."
                   TextWrapping="Wrap"
                   Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
    </StackPanel>
    <Grid ColumnDefinitions="Auto,*,Auto">
        <arc:Button Grid.Column="0" Content="Remove card" Variant="Danger" />
        <StackPanel Grid.Column="2" Orientation="Horizontal" Spacing="8">
            <arc:Button Content="View history" Variant="Ghost" />
            <arc:Button Content="Cancel" Variant="Secondary" />
            <arc:Button Content="Save changes" Variant="Primary" />
        </StackPanel>
    </Grid>
</StackPanel>
```

### 文案規範

- **標籤**：動詞加受詞，句首大寫，例如「儲存變更」「建立發票」「刪除專案」。當畫面上有兩個項目時，只寫「刪除」會含糊不清。
- **長度**：標籤控制在兩到三個詞。過長的標籤會把頁尾中的其他按鈕擠出一列。
- **關閉**：對於會捨棄變更的對話方塊使用「取消」；只有在未編輯任何內容時才使用「關閉」。
- **不加標點**：結尾不加句號，也不加驚嘆號。按鈕是一條指令，不是句子。
- **避免含糊的確認**：「確定」沒有告訴使用者將發生什麼。應說明具體的變更。

| 不要寫 | 改為 |
|---|---|
| 確定 | 儲存變更 |
| 送出 | 建立發票 |
| 刪除 | 刪除專案 |
| 點這裡 | 檢視發票 |
| 完成！ | 已儲存 |

### 最佳做法

**建議**

- 每個表面只使用一個 `Primary` 按鈕。兩個實心按鈕會爭奪同一注意力，使用者必須讀完兩個標籤才能找到下一步。
- 把主要操作放在最後，位於列的末端，次要操作在其左側。人們從左到右閱讀這一列，會期望送出操作位於閱讀的終點。
- 表單、對話方塊或工具列中的所有按鈕使用同一個 `Size`。高度不一致會讓頁尾看起來破碎，即使每個按鈕本身都是正確的。
- 為純圖示按鈕提供無障礙名稱；當圖示含義不夠直觀時，再加上工具提示。
- 對幾秒內完成的請求使用 `IsLoading`。寬度會被保留，因此載入指示出現時版面不會移動。

**避免**

- 把 `Danger` 用於非破壞性的操作。紅色按鈕會讓人習慣忽略顏色，真正需要警告時反而失去作用。
- 為了隱藏驗證問題而停用主要按鈕。應在使用者按下時驗證，並指向第一個問題，讓原因可見。
- 用計時器反覆切換標籤。按鈕最終會進入使用者無法預測的狀態；當結果文字需要出現在按鈕上時，應使用 `ActionButton`。
- 把 `Ghost` 用於表面上唯一的操作。它的低對比適合次要導覽連結，但頁面上最需要的操作應一眼可見。
- 在同一列中放入多個很長的標籤。這一列會換行或捲動，操作的順序也會亂掉。

### 常見問題

**為什麼載入中的按鈕保持焦點卻忽略我的點擊？** 這是設計如此。`IsLoading` 會吞掉點擊和按鍵，避免請求被重複送出；同時它保持焦點，讓使用者和輔助技術留在原處。按鈕會向外回報自己處於忙碌且無法使用的狀態。

**按鈕載入時 `Command` 會執行嗎？** 不會。`IsLoading` 為 `True` 時，按鈕會在點擊處理中提前返回，因此 `Click` 和 `Command` 都不會執行。

**為什麼我的選單錨點按鈕按下時不縮放？** 對於會開啟浮層或選單的按鈕，將 `IsPressScaleEnabled` 設為 `False`。浮層會測量錨點按下時的尺寸，因此錨點只透過顏色做出回應。該屬性預設為 `True`。

**標籤變化時按鈕寬度跳動，如何保持穩定？** 寬度會彈性地適應每個標籤。若要固定寬度，把 `MinWidth` 設為你預期的最長標籤的寬度，這樣按鈕就不會窄於它。

**為什麼窄的圖示按鈕比文字按鈕按下得更深？** 按下深度跟隨寬度。寬度為 48 px 或更窄時，按鈕壓至 96%；超過 220 px 時壓至 98.5%，使按壓深度與目標大小相符。

## API 參考

<!-- api: Button -->

本頁還用到的配套型別：

| `ButtonVariant` 值 | 意義 |
|---|---|
| `Primary` | 預設值。表面主要操作的前景色填滿。 |
| `Secondary` | 帶邊框的表面，用於主要操作旁邊的替代操作。 |
| `Ghost` | 透明按鈕，用於低強調的操作。 |
| `Danger` | 帶危險色標籤的表面，用於破壞性操作。 |

`Size` 使用 `ControlSize` 的值 `Small`、`Medium`（預設）和 `Large`。

## 無障礙

- 按鈕具有 Button 角色。純圖示按鈕需要 `AutomationProperties.Name`，因為它沒有可供命名的文字。
- 設定 `IsLoading` 時，按鈕仍可取得焦點，並向外回報自己處於忙碌且無法使用的狀態。
- Enter 與空白鍵啟動按鈕。載入期間，點擊和按鍵會被吞掉。
- 鍵盤焦點顯示 2 px 的焦點環，並偏移 2 px。指標焦點不繪製焦點環，因此點擊後不會留下高亮。
- 停用的按鈕會被移出 Tab 順序，並向外回報自己無法使用。它會被淡化而不是隱藏，因此使用者仍能看到該操作的存在。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 將焦點移到下一個或上一個按鈕。停用的按鈕會被略過。 |
| `Enter` / `Space` | 啟動按鈕，觸發 `Click` 並執行 `Command`。`IsLoading` 為 `True` 時忽略。 |
| 文字方塊中的 `Enter` | 視窗中有標記為 `IsDefault="True"` 的預設按鈕時，按下該按鈕。 |

## 動態效果

- 按下時內容會輕微縮小，鬆開後彈回。窄的圖示按鈕縮放幅度稍大。
- 標籤變化時，新文字從柔和模糊中升起，寬度彈性地調整到新標籤。載入時，載入指示持續旋轉。
- 減少動態效果會去掉按下縮放和寬度彈簧。標籤與載入指示以短暫的交叉淡化切換，載入指示停止旋轉。關閉動態效果則立即替換標籤與狀態。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 按下 | 內容縮放到 97%（圖示按鈕為 96%，寬按鈕為 98.5%），然後彈回 | 無按下縮放 | 無按下縮放 |
| 標籤變化 | 新文字從柔和模糊中升起，舊文字向上淡出，寬度彈性適配 | 瞬間交叉淡化 | 立即完成 |
| 開始或結束載入 | 標籤在載入指示下方淡出，載入指示每 0.7 秒轉一圈 | 交叉淡化，載入指示停止旋轉 | 立即完成 |
| 懸停、按下、停用顏色 | 顏色以快速曲線交叉漸變 | 瞬間完成 | 立即完成 |

## 相關元件

- `ActionButton`：用於非同步送出，在按鈕上顯示待處理與成功狀態。
- `SplitButton`：用於主要操作加上一個包含相近替代操作的選單。
- `ButtonGroup`：用於把幾個相關操作合併到一個表面上。
- `CopyButton`：用於複製一個值並給出確認。

| 元件 | 適用情境 |
|---|---|
| `ActionButton` | 送出時應在按鈕上顯示待處理詞與結果詞 |
| `SplitButton` | 一個預設操作旁邊有兩到五個相近的替代操作 |
| `ButtonGroup` | 多個操作作用於同一物件，並共用一個表面 |
| `ConfirmMorph` | 破壞性操作應在原位要求確認 |
| `HoldToConfirm` | 破壞性操作需要刻意按住，不能被誤觸發 |
| `SegmentedControl` | 控制項顯示一個會保持選取的選擇，例如檢視模式 |
