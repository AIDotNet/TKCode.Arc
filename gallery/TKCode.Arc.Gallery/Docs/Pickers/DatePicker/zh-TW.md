---
title: 日期選擇器
summary: 選取日期，同時不遺失上下文。
source-hash: 115d5de1a22e
---

<Demo name="DateField" />

## 何時使用

`DatePicker` 是用於選取一個行事曆日期的表單欄位。它的外觀與行為都像文字方塊：使用者可以直接輸入日期，也可以開啟行事曆彈出視窗來選取。它與其他欄位一樣擁有相同的標籤、說明與錯誤列，因此能與周圍的表單保持一致。凡是表單需要選取某一天、而且在選取期間頁面需要保留上下文的地方，都可以使用它。

- **專案與訂單表單中的開始日期與截止日期**：專案經理為某個里程碑設定開始日期與截止日期。兩個欄位位於同一欄，截止日期透過 `MinDate` 受開始日期限制。
- **財務主控台中的發票與帳單日期**：會計輸入發票的開立日期。對於過去的某一天，輸入 `Oct 21, 2026` 比在行事曆中翻頁更快；彈出視窗則處理其餘情況。
- **帶限制的預約與預訂表單**：診所或租借櫃檯只接受營業日期。`MinDate`、`MaxDate` 與 `DisabledDates` 同時作用於彈出視窗與輸入的值，因此不可能的日期永遠不會送到伺服器。
- **面向多個市場的在地化表單**：多語言人資系統以德文、中文或 ISO 格式顯示同一個欄位。地區設定決定月份名稱、解析方式與預設顯示；當業務要求時，`DisplayFormat` 可以固定一種格式。
- **只需要一個日期的篩選器**：營運儀表板在載入資料前需要一個報表日期。若需要開始與結束日期的組合，請使用 `DateRangePicker`。

## 何時不用

- **需要持續顯示在頁面上的行事曆**：當使用者在比較各天時需要閱讀的預訂檢視或可用性格線，用 `Calendar`。`DatePicker` 在展開前會隱藏格線。
- **帶預設的開始與結束日期**：用 `DateRangePicker`。它保存兩個日期，提供「最近 7 天」這類預設，並且只在使用者確認後才套用該範圍。
- **一天中的時間，或同時包含日期與時間**：在 `DatePicker` 旁邊使用 `TimePicker`。`DatePicker` 的值沒有時間部分，因此帶有小時的截止時間必須由兩個欄位組成。
- **屬於少數命名週期之一的日期**：「本季」「上一季」或「自訂」這類選擇，用 `Select` 或 `SegmentedControl`。若使用 `DatePicker`，使用者就得自行計算週期名稱已經暗示的日期。
- **訊息或備註中的自由文字日期**：當文字是給人閱讀的文字，而不是要儲存為日期的值時，用 `Input`。對無法解析的文字予以拒絕的日期欄位，並不適合用於句子。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:DatePicker Label="開始日期"
                    Description="輸入日期，或按 Alt+Down 開啟行事曆。"
                    SelectedDate="{Binding StartDate, Mode=TwoWay}" />
</UserControl>
```

`DatePicker` 衍生自 `FieldTextBox`，後者是 Arc 的文字欄位基底類別，因此本質上是一個 Avalonia `TextBox`。欄位保留文字方塊的行為：游標、選取、復原、輸入法組字與剪貼簿。標籤、說明、錯誤列、尺寸與預留位置文字都來自共用的欄位基礎，因此 `DatePicker` 與 `Input` 在表單中的間距一致。行事曆彈出視窗是在此之上加入的，彈出視窗自身的按鍵與指標事件不會傳遞給文字。

### 結構

1. **標籤**（`Label`）：欄位名稱，樣式與 `Input` 相同，位於外框上方。它為輔助技術提供欄位名稱，並與觸發器相關聯。
2. **外框**：1 px 邊框、控制項圓角、表面色背景。懸停與焦點只改變邊框，外框尺寸不變。
3. **值**：顯示的日期，變化時逐部分滾動（見動態效果）。沒有值時，以淡化的文字顏色顯示預留位置文字；預設的預留位置文字為「Select a date」。
4. **行事曆按鈕**（下拉按鈕）：外框右側 28 px 的按鈕。彈出視窗開啟時，箭頭旋轉 180 度。點擊它會開啟或關閉彈出視窗。
5. **說明**（`Description`）：外框下方的輔助文案。彈出視窗開啟時它會淡出，因此文字不會超出彈出視窗的邊角。
6. **錯誤**（`ErrorMessage`）：外框下方的危險色文案。內容不為空時，欄位會被標示為無效。
7. **彈出視窗**：行事曆與頁尾，位於欄位下方 8 px。寬度為 `min(320px, 視窗寬度減 32px)`，內距為 16 px。在寬 360 px 及以下的視窗中，彈出視窗與欄位同寬，內距為 12 px。
8. **頁尾**：左側是「Clear」按鈕，沒有值時停用；右側是狀態文字，顯示「Selected Oct 20, 2026」或「Choose a day」。

彈出視窗使用與程式庫其他地方相同的 `Calendar`，因此鍵盤模型、限制與月份導覽都與 `Calendar` 頁面所記錄的一致。

### 值與輸入文字

`SelectedDate` 是 `DateOnly?`，且為雙向繫結。值為 `null` 時顯示預留位置文字。值會以三種方式變化：使用者在彈出視窗中選取一天、使用者輸入日期並確認，或程式碼設定該屬性。輸入的日期在使用者按下 Enter 或離開欄位時確認。空白欄位確認的是 `null`，即清除該值。

無法解析的文字，或超出限制的日期，不會被接受。欄位會回到最後一個有效值；對於超出限制的日期，欄位會先抖動一下，以表明該輸入已被拒絕。

每次確認變更後都會觸發 `SelectedDateChanged`，引數包含變更前後的日期：

| 成員 | 類型 | 含義 |
|---|---|---|
| `OldDate` | `DateOnly?` | 先前的日期；沒有時為 `null`。 |
| `NewDate` | `DateOnly?` | 新的日期；按下 Clear 或欄位被清空後為 `null`。 |

```csharp
picker.SelectedDateChanged += (_, e) =>
{
    status.Text = e.NewDate is { } d ? $"開始：{d:yyyy-MM-dd}" : "無開始日期";
};
```

直接在程式碼中設定 `SelectedDate` 不會經過輸入文字的規則：程式碼設定的值會原樣顯示，因此檢視模型必須確保它位於 `MinDate` 與 `MaxDate` 之內。`FormattedValue` 會回傳目前顯示的文字；沒有值時為空白。

### 限制與停用的日期

`MinDate` 與 `MaxDate` 限定可選的日期範圍。`DisabledDates` 是一個 `Func<DateOnly, bool>`，對需要阻擋的某一天回傳 `true`。這些限制同時作用於行事曆與輸入的日期。`IsDateAllowed(DateOnly date)` 在程式碼中給出相同的結果，因此表單可以在設定值之前先檢查日期。

輸入的日期若超出範圍或已停用，會被拒絕：欄位抖動並回到最後一個有效值。當方向鍵移動焦點時，行事曆也會略過這些日期。

<Demo name="LimitsAndDisabledDays" />

用限制來表達業務規則，而不是用來掩蓋錯誤。必須晚於開始日期的截止日期，寫作 `MinDate="{Binding StartDate}"`；只能在平日送達，則使用 `DisabledDates`。若規則依賴選擇器無法看到的資料，例如某個已關閉的週期，應放在檢視模型的驗證中，並透過 `ErrorMessage` 顯示。

### 尺寸

`Size` 設定欄位高度：`Small` 為 36，`Medium` 為 44（Arc 的外觀），`Large` 為 50。一個表單的所有欄位使用同一尺寸，因此 `DatePicker` 能與同一列中的 `Input` 或 `Select` 對齊。

<Demo name="Sizes" />

### 地區設定與顯示格式

`Culture` 控制解析、行事曆的月份與星期名稱，以及預設的顯示方式。未設定時，使用目前的 UI 地區設定。預設顯示為該地區設定下的長日期（不含星期），月份使用縮寫。在 `en-US` 中，日期顯示為 `Oct 20, 2026`；在 `zh-CN` 中，顯示為 `2026年10月20日`。

`DisplayFormat` 接受 .NET 日期格式字串，並以它取代顯示值的預設格式。當業務要求所有使用者都使用固定格式時使用它，例如匯出頁面中的 ISO 格式。該格式不會改變行事曆中的名稱，輸入的文字會先以同一格式解析。

<Demo name="CulturesAndFormats" />

解析會依序嘗試三種方式：顯示格式、地區設定自身的日期格式，最後是對文字的寬鬆解析。因此使用者可以在 `en-US` 中輸入 `10/21/2026`，也可以在 ISO 欄位中輸入 `2026-10-21`。若要接受額外的表示法，可在子類別中覆寫 `TryParseDate`；預設行為即為上述描述的內容。

`FirstDayOfWeek` 設定彈出視窗行事曆的第一欄。預設為 `Sunday`；上面的德文與中文範例使用的是 `Monday`。

### 錯誤與停用狀態

`ErrorMessage` 不為空時，欄位被標示為無效。停用的欄位會淡化到一半的不透明度，既不能開啟，也不能輸入。

<Demo name="InvalidAndDisabled" />

在值變化之前，保留錯誤訊息。文案應對應失敗的規則，並寫成修正方法：「請選取早於截止日期的日期。」停用的欄位應在說明中寫明被鎖定的原因，因為只靠淡化的外框無法解釋此限制。

### 驗證

`DatePicker` 自身檢查兩件事：輸入的文字能否解析為日期，以及日期是否在限制範圍之內。其他都是業務規則，由表單決定何時顯示。

| 規則 | 檢查時機 | 顯示位置 |
|---|---|---|
| 文字可以解析為日期 | 使用者按下 Enter 或離開欄位時 | 欄位回到最後一個有效值；文字不會保留 |
| 日期位於 `MinDate` 與 `MaxDate` 之間 | 使用者按下 Enter 或離開欄位時 | 欄位抖動並回到最後一個有效值 |
| 日期未被停用 | 使用者按下 Enter 或離開欄位時 | 欄位抖動並回到最後一個有效值 |
| 必填，或晚於另一個日期 | 送出時，或相關欄位變化時 | 規則所針對的欄位上的 `ErrorMessage` |
| 由伺服器檢查的規則，例如已關閉的週期 | 請求結束後 | `ErrorMessage`；值變化時清除 |

選擇器自行檢查的規則失敗時是靜默的，因為欄位只是回到最後一個有效值。對於使用者必須處理的每條規則都應顯示訊息，不要讓靜默還原成為唯一的回饋。

```csharp
public partial class InvoiceViewModel : ObservableValidator
{
    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "請選取開立日期。")]
    private DateOnly? _issueDate;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "請選取截止日期。")]
    private DateOnly? _dueDate;

    [RelayCommand]
    private async Task SaveAsync()
    {
        ValidateAllProperties();
        if (HasErrors)
        {
            return;
        }
        await _invoices.SaveAsync(IssueDate!.Value, DueDate!.Value);
    }
}
```

### 繫結到檢視模型

檢視模型負責日期與規則，欄位負責反映它們。`SelectedDate` 為雙向繫結，因此使用者一確認日期，檢視模型就會收到它；`MinDate` 為單向繫結，因此截止日期會跟隨開立日期。

```xml
<StackPanel Width="280" Spacing="16">
    <arc:DatePicker Label="開立日期"
                    SelectedDate="{Binding IssueDate, Mode=TwoWay}" />
    <arc:DatePicker Label="截止日期"
                    SelectedDate="{Binding DueDate, Mode=TwoWay}"
                    MinDate="{Binding IssueDate}"
                    ErrorMessage="{Binding DueDateError}" />
    <arc:Button Content="儲存發票" Command="{Binding SaveCommand}" IsDefault="True" />
</StackPanel>
```

當錯誤來自選擇器無法得知的規則時，將 `ErrorMessage` 繫結到檢視模型的字串，並在值變化時清除該字串。繫結產生的驗證錯誤也會顯示在欄位中，因此每條規則只使用一個來源。選擇器的 `IsDropDownOpen` 為雙向繫結；在檢視模型中將其設為 `False`，即可在頁面切換時關閉彈出視窗。

### 範例：發票日期

財務主控台中的一個帳單表單。開立日期已設定，截止日期僅限於開立日當天或之後的日期；由於它早於該帳戶要求的 net 30 帳期，因此截止日期會顯示一則訊息。標籤是名詞，說明寫明規則，錯誤訊息指出修正方法。截圖中彈出視窗是收合的，因此該場景展示的是使用者開啟行事曆之前所看到的欄位。

![帶有開立日期、截止日期錯誤與儲存按鈕的發票日期表單](images/InvoiceDates.png)

```xml
<Border Width="560"
        Padding="24"
        Background="{arc:ArcDesignTokenResource Surface}"
        BorderBrush="{arc:ArcDesignTokenResource BorderSubtle}"
        BorderThickness="1"
        CornerRadius="{arc:ArcDesignTokenResource RadiusPanel}">
    <StackPanel Spacing="20">
        <StackPanel Spacing="4">
            <TextBlock Text="Invoice INV-2026-0412"
                       FontSize="{arc:ArcDesignTokenResource TypeLG}"
                       FontWeight="{arc:ArcDesignTokenResource WeightMedium}"
                       Foreground="{arc:ArcDesignTokenResource Foreground}" />
            <TextBlock Text="Northwind Logistics, net 30 terms"
                       FontSize="{arc:ArcDesignTokenResource TypeSM}"
                       Foreground="{arc:ArcDesignTokenResource TextMuted}" />
        </StackPanel>
        <StackPanel Orientation="Horizontal" Spacing="16">
            <arc:DatePicker Label="Issue date"
                            Width="256"
                            SelectedDate="2026-10-21"
                            Description="The date the invoice is sent." />
            <arc:DatePicker Label="Due date"
                            Width="256"
                            SelectedDate="2026-10-30"
                            MinDate="2026-10-21"
                            ErrorMessage="Net 30 terms need a due date on Nov 20, 2026." />
        </StackPanel>
        <arc:Input Label="Customer reference"
                   Text="PO-88412"
                   Description="Printed on the invoice." />
        <StackPanel Orientation="Horizontal"
                    Spacing="8"
                    HorizontalAlignment="Right">
            <arc:Button Content="Cancel" Variant="Secondary" />
            <arc:Button Content="Save invoice" />
        </StackPanel>
    </StackPanel>
</Border>
```

截止日期的錯誤直接寫出帳期條款，而不是籠統的「日期無效」，因此使用者知道應該修改哪個日期。兩個欄位寬度相同並位於同一列，因此標籤與說明對齊。客戶參考號是 `Input`，因為它是識別碼，而不是日期。

### 文案規範

- **標籤**：簡短的名詞片語，句首大寫，不加冒號，例如「開立日期」「截止日期」「開始日期」。
- **預留位置文字**：如果表單沒有其他說明，保留預設的「Select a date」。只有當欄位屬於某個群組、並且在上下文中措辭更清楚時，才修改它。
- **說明**：用一句話說明日期控制什麼，或在不明顯時說明如何輸入，例如「輸入日期，或按 Alt+Down 開啟行事曆。」
- **錯誤**：說明規則與修正方法，例如「Net 30 帳期需要將截止日期設為 2026 年 11 月 20 日。」不要重複標籤。
- **頁尾文案**：彈出視窗頁尾使用 `ClearText`、`EmptyStatusText` 與 `SelectedStatusFormat`。「Clear」保持為動詞，狀態文字應說明結果，例如「已選取 {0}」。

| 不要寫 | 改為 |
|---|---|
| 日期 | 開立日期 |
| 日期無效 | 請輸入日期，例如 Oct 21, 2026。 |
| 請輸入截止日期： | 請選取截止日期。 |
| 格式錯誤 | 請使用 Oct 21, 2026 當天或之後的日期。 |
| 清除日期 | 清除 |

### 最佳做法

**建議**

- 將 `Culture` 設為使用者的地區設定；對於後台畫面，設為公司的地區設定，使月份名稱與解析結果符合使用者的預期。
- 每個表單只使用一種 `Size`，並讓 `DatePicker` 與同一列中的相鄰欄位對齊。
- 用繫結到較早日期的 `MinDate` 限制相依日期，使行事曆永遠不會提供表單會拒絕的日期。
- 對使用者必須處理的每條規則都顯示訊息。被拒絕的輸入日期會在抖動後靜默還原；訊息則說明原因。
- 只有當格式屬於約定的一部分時（例如匯出或法律文件）才使用 `DisplayFormat`，其他地方保留地區設定的預設格式。

**避免**

- 用 `DatePicker` 表示一天中的時間，或帶有小時的截止時間。該值沒有時間部分，因此小時會遺失。
- 在程式碼中把 `SelectedDate` 設為超出 `MinDate`、`MaxDate` 或 `DisabledDates` 的日期。輸入的值會被檢查，而程式碼設定的值不會被檢查，因此欄位會顯示規則所禁止的日期。
- 在同一頁面上同時放置行事曆彈出視窗和另一個可見的 `Calendar` 來表示同一個值。同一個日期的兩個檢視會讓使用者編輯錯誤的那一個。
- 只依賴 Alt+Down 快速鍵。說明中應寫明行事曆會開啟，因為不知道該按鍵的使用者永遠找不到彈出視窗。
- 每次按鍵都替換錯誤訊息。錯誤是在值確認時檢查的，輸入過程中不斷變化的訊息只會造成干擾。

### 常見問題

**輸入無效的日期時，為什麼欄位會回到原來的日期？** 輸入的值會在按下 Enter 或欄位失去焦點時確認。若無法解析、超出限制或已停用，該值會被拒絕，欄位顯示最後一個有效日期。如果使用者需要知道原因，請新增 `ErrorMessage`。

**如何在程式碼中清除該值？** 將 `SelectedDate` 設為 `null`。欄位會顯示預留位置文字，並透過 `SelectedDateChanged` 觸發變更，新日期為 `null`。彈出視窗中的 Clear 按鈕對使用者而言效果相同。

**選取日期後彈出視窗會關閉嗎？** 會。它會在選取後約 300 ms 關閉，讓值與頁尾有時間進行確認。在減少或關閉動態效果時，它會立即關閉。按下 Clear 也會立即關閉彈出視窗。

**如何在程式碼中開啟彈出視窗，或在頁面切換時關閉它？** 將 `IsDropDownOpen` 設為 `True` 或 `False`。Escape 與 Alt+Up 可以透過鍵盤關閉彈出視窗；按 Tab 離開欄位會關閉它，並確認輸入的文字。

**如何以與輸入格式不同的格式顯示日期？** 為顯示的值設定 `DisplayFormat`。輸入時會先接受顯示格式，然後才是地區設定的格式，因此使用者可以輸入他們已經熟悉的格式。

## API 參考

<!-- api: DatePicker -->

## 無障礙

- `Label` 為欄位命名，並與觸發器相關聯。`Description` 是說明文字；觸發器的說明文字包含格式化後的值，然後是說明。
- 觸發器會公開其展開狀態，因此螢幕閱讀器會播報行事曆是否已開啟。彈出視窗以標籤命名，例如「開始日期行事曆」。
- Enter、Space 與 ArrowDown 會開啟行事曆，並把焦點移到選取的日期；若沒有選取日期，則移到今天。在行事曆內部，按鍵的行為與 `Calendar` 中相同。
- Escape 關閉行事曆並把焦點返回欄位。從行事曆按 Tab 離開，或在行事曆外按下指標，也會關閉它。
- 焦點環只在鍵盤聚焦時顯示。透過指標按下開啟行事曆時不會顯示焦點環，游標保留在文字中。
- 無效或被拒絕的輸入不會被保留：欄位回到最後一個有效值；如果表單設定了錯誤訊息，該訊息會留在說明列中供使用者閱讀。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| 輸入後按 `Enter` | 確認輸入的日期；無效或已停用的日期會被拒絕，欄位回到最後一個有效值 |
| 沒有輸入變動時按 `Enter` | 開啟行事曆；若已開啟則關閉 |
| `Alt` + `Down`，或 `Down` | 開啟行事曆，並把焦點移到其 Tab 停駐日（若已開啟，則進入行事曆） |
| 彈出視窗開啟時從欄位按 `Tab` | 確認輸入的文字，並把焦點移入行事曆 |
| 有待確認的輸入變動時按 `Escape` | 捨棄輸入的變動，並顯示最後一個有效日期 |
| 行事曆開啟時按 `Escape` | 關閉行事曆，並把焦點返回欄位 |
| 行事曆中的 `Alt` + `Up` | 關閉行事曆，並把焦點返回欄位 |
| 從行事曆按 `Tab` 離開 | 關閉行事曆，並確認任何輸入的文字 |

在行事曆內按下的按鍵從不編輯文字，因此在選取的日期上按方向鍵或 Backspace 不會改變輸入的值。在行事曆內部，格線按鍵的行為與 `Calendar` 頁面中的說明一致。

## 動態效果

- 日期中變化的部分會沿時間方向滾動：較晚的日期向上升起，較早的日期向下落下。只有變化的部分會移動。
- 選取日期後，選取的圓形醒目標示滑入位置，彈出視窗隨即稍後關閉。
- 彈出視窗從行事曆圖示處以短促的淡入開啟，並以更短的淡出關閉。
- 減少動態效果時，值以淡入方式切換，並在選取後立即關閉彈出視窗。關閉動態效果時直接到達最終狀態。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 日期的某一部分變化 | 只有變化的部分滾動；較晚的日期上升，較早的日期下降 | 值淡入為新的文字 | 立即完成 |
| 某一部分的寬度變化 | 之後的部分沿同一曲線滑動，為其騰出空間 | 值淡入為新的文字 | 立即完成 |
| 彈出視窗開啟 | 從上方以快速彈簧落定 8 px，同時從 95% 縮放放大並淡入 | 淡入 | 立即完成 |
| 彈出視窗關閉 | 上移 6 px，縮放到 97% 並以短促的補間淡出 | 淡出 | 立即完成 |
| 彈出視窗開啟時的說明 | 快速淡出，彈出視窗關閉時恢復 | 快速淡出 | 立即完成 |
| 選取後頁尾的狀態 | 帶著柔和的模糊升起到位 | 淡入 | 立即完成 |
| 行事曆按鈕上的箭頭 | 以彈簧旋轉 180 度 | 立即轉向 | 立即完成 |

## 相關元件

- 需要保留在頁面上的月檢視，用 `Calendar`。
- 帶預設的開始與結束日期，用 `DateRangePicker`。
- 一天中的時間（與日期配對），用 `TimePicker`。
- `Field` 與 `FieldFrame` 為任何編輯器或觸發器提供相同的標籤、外框與訊息列。

| 元件 | 適用情境 |
|---|---|
| `Calendar` | 月檢視是主要內容，使用者需要在頁面上比較多個日期 |
| `DateRangePicker` | 使用者需要選取開始與結束日期，且常使用「最近 30 天」這類預設 |
| `TimePicker` | 值是一天中的時間，或日期還需要包含時間 |
| `Select` / `SegmentedControl` | 選擇是幾個命名週期之一，而不是一個日期 |
| `Input` | 文字是識別碼或句子，而不是需要解析的日期 |
