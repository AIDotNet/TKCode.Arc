---
title: 標籤輸入
summary: 將簡短文字值轉換為可移除的標籤。
source-hash: 49ac8f96a74d
---

<Demo name="Default" />

## 何時使用

`TagInput` 把使用者輸入的簡短值轉換為一個表單欄位內可移除的標籤。它適用於使用者需要逐一建立簡短字串清單、並且要同時看到每一項的表單：支援工單上的標籤、定時報表的收件者、允許登入工作區的網域。欄位自己繪製標籤、輔助文字和錯誤列，因此清單與周圍的輸入框一樣，是一個完整的表單控制項。

- **支援工單或 CRM 聯絡人上的標籤**：客服輸入 `billing`，按下 Enter，再輸入 `refund`。標籤是自由文字，因此標籤集合會隨團隊的用語增長，而不必等管理員修改清單。
- **定時報表的收件者**：財務主管逐一新增 `cfo@northwind.com` 等地址，或貼上從試算表複製的一欄。貼上的逗號、換行和定位字元都會拆分為標籤，每個地址在報表儲存前都清楚可見。
- **單一登入設定中允許的網域**：管理員輸入 `acme.com` 和 `acme.co.uk`。每個網域都以獨立標籤顯示在畫面上，因此缺漏或拼錯的網域在表單儲存前很容易發現。
- **知識庫文章或商品列表中的關鍵字**：作者新增多個搜尋詞。重複的詞會在不分大小寫的情況下被拒絕，因此 Refund 與 refund 不能同時儲存。
- **方案表單中有數量上限的清單**：包含五項整合的方案設定 `MaxTags="5"`。第六次新增會被拒絕，並伴隨抖動與提示，因此上限在使用者操作的地方就得到了說明。

## 何時不用

- **從現有目錄中選取的值**：角色、方案、倉庫或地區等已經作為紀錄存在的內容：用 `MultiSelect`（從清單中選擇），清單很長並需要搜尋時用 `Combobox`。
- **結果頁面上的分面篩選**：幾個用於縮小表格或動態列表的開關：用 `ChipGroup`，它專為在篩選分面中選擇而設計。
- **單一值**：一個電子郵件地址、一個名稱或一個 URL：用 `Input`。只有一個標籤的標籤欄位比單一輸入框更難閱讀和驗證。
- **金額與數量**：價格、重量或計數的清單需要解析和格式化：用 `MoneyInput` 或 `NumberField`。`TagInput` 儲存的是一般字串，不做任何解析。
- **連續文字中的提及**：留言或訊息中的 @人員和 #頻道參照：用 `MentionInput`，它會把每個提及保持為句子中的一個詞元。
- **多行文字**：備註、說明或貼上的段落：用 `Textarea`。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:TagInput Label="標籤"
                  Placeholder="新增一個標籤"
                  Description="按 Enter 或輸入逗號即可新增標籤。" />
</UserControl>
```

`TagInput` 衍生自 Avalonia 的 `TemplatedControl`。它的外框是其他 Arc 欄位共用的 `Field` 與 `FieldFrame`，因此標籤、訊息列、焦點光暈和無效邊框都與 `Input` 一致。外框內，標籤與無邊框的文字編輯器排列在可換行的列中。編輯器是一個文字方塊，因此游標、選取範圍、輸入法和剪貼簿的行為與 `Input` 相同；此控制項額外為標籤加入了 Enter、逗號、Backspace 和方向鍵的處理。

### 結構

1. **標籤**（`Label`）：欄位標籤，樣式與 `Input` 相同（14 px、中等字重，位於外框上方 8 px）。它同時是編輯器的無障礙名稱。只有當可見的標題已經為欄位命名時，才用 `IsLabelVisible="False"` 將其隱藏。
2. **外框**（`FieldFrame`）：表面色背景上 1 px 的邊框，顏色為邊框色。只有在欄位未取得焦點且沒有錯誤時，懸停才會加深邊框。取得焦點時顯示 3 px 的焦點光暈。出現錯誤（`ErrorMessage` 或 `IsInvalid`）時，邊框變為危險色。外框的最小高度為 44 px，可容納一列標籤。
3. **內容**：四周 7 px 內距，標籤、編輯器和換列之間的間距為 6 px。每多一列，高度增加 34 px。高度以平滑彈簧跟隨列數變化。
4. **標籤**（`Value` 的一項）：至少 28 px 高，標籤文字前 9 px、後 4 px 的內距，位於 1 px 的膠囊形邊框內，背景為淡化的表面色。標籤文字使用小字級的內文字型和前景色，與刪除按鈕之間留 3 px 間距。寬於欄位的標籤會以省略號截斷。
5. **刪除按鈕**：22 px 的圓形按鈕，中間是 14 px 的 X 圖形，線寬 2 px，顏色為次要文字色。懸停時背景變為表面色。鍵盤焦點會在圓圈內繪製 1.5 px 的焦點環。按下時縮小到 96%。
6. **編輯器**（草稿）：佔據該列剩餘的空間。最小寬度為 80 px，以 100 px 作為基準寬度，高度 28 px，水平內距 5 px。當一列剩餘不足 100 px 時，編輯器移到下一列。
7. **預留位置文字**（`Placeholder`）：預設為「新增一個標籤」，以淡化的文字顏色、12 px 的內縮顯示。僅當沒有標籤且草稿為空時才顯示。
8. **輔助文字**（`Description`）與**錯誤列**（`ErrorMessage`）：外框下方的輔助文案和錯誤文案，與 `Input` 的列相同。`ErrorMessage` 不為空時欄位即為無效。

`TagInput` 沒有 `Size` 屬性。它的高度跟隨列數變化，因此應讓標籤換行，而不是縮小文字。

### 初始標籤

`Value` 以 `IReadOnlyList<string>` 保存標籤。設定它會取代整個清單並觸發 `ValueChanged`；`null` 讀回時為空清單。

```csharp
var tags = new TagInput
{
    Label = "標籤",
    Value = ["design", "motion", "react"]
};
```

### 繫結到檢視模型

在 MVVM 應用程式中，檢視模型負責管理清單及其規則。控制項從不修改它收到的清單：每次新增、刪除或貼上都會指派一個新陣列。應持有一個 `IReadOnlyList<string>` 屬性並替換它；不要期望 `ObservableCollection<T>` 被原地修改。下面的範例檢查收件者的電子郵件格式。

```csharp
public partial class ReportViewModel : ObservableObject
{
    [ObservableProperty]
    private IReadOnlyList<string> _labels = Array.Empty<string>();

    [ObservableProperty]
    private IReadOnlyList<string> _recipients = Array.Empty<string>();

    [ObservableProperty]
    private string? _recipientsError;

    partial void OnRecipientsChanged(IReadOnlyList<string> value)
    {
        // 控制項會立即新增標籤，因此被規則拒絕的標籤需要移除，並說明原因。
        var valid = value.Where(IsEmailAddress).ToArray();
        if (valid.Length != value.Count)
        {
            Recipients = valid;
            RecipientsError = "請輸入完整的電子郵件地址，例如 ops@acme.com。";
            return;
        }
        RecipientsError = null;
    }

    [RelayCommand]
    private async Task SendAsync()
    {
        if (Recipients.Count == 0)
        {
            RecipientsError = "至少新增一位收件者。";
            return;
        }
        await _reports.ScheduleAsync(Labels, Recipients);
    }

    private static bool IsEmailAddress(string tag) =>
        Regex.IsMatch(tag, @"^[^@\s]+@[^@\s]+\.[^@\s]+$");
}
```

```xml
<StackPanel Spacing="16">
    <arc:TagInput Label="標籤"
                  Value="{Binding Labels, Mode=TwoWay}"
                  Description="按 Enter 或輸入逗號即可新增標籤。" />
    <arc:TagInput Label="收件者"
                  Value="{Binding Recipients, Mode=TwoWay}"
                  ErrorMessage="{Binding RecipientsError}"
                  Placeholder="name@company.com" />
    <arc:Button Content="排定報表" Command="{Binding SendCommand}" />
</StackPanel>
```

- `Value` 預設是雙向的，因此 `Mode=TwoWay` 只是明確表達意圖。檢視模型會收到每次變動，包括透過貼上新增的標籤。
- `ErrorMessage` 是單向的：訊息由檢視模型掌握，並在清單再次有效時清除。
- 編譯繫結需要在檢視上設定 `x:DataType`（為求簡潔，這裡的範例省略了它）。

### 繫結與事件

`ValueChanged` 會回報變動前後的清單，以及被 `Added` 和 `Removed` 的標籤。需要完整狀態時使用雙向的 `Value` 繫結；需要差異時使用事件，例如寫入稽核紀錄，或為每個新增的標籤呼叫一次伺服器。

<Demo name="TwoWayValue" />

```xml
<arc:TagInput Label="關鍵字" Value="{Binding Keywords, Mode=TwoWay}" />
```

```csharp
tags.ValueChanged += (_, e) =>
{
    Console.WriteLine($"Added: {string.Join(", ", e.Added)}; removed: {string.Join(", ", e.Removed)}");
};

tags.AddTag("release");
tags.FocusEditor();
```

`AddTag` 與 `RemoveTag` 會觸發 `ValueChanged`，但不會播報任何內容，因為使用者並沒有操作該欄位。

### 限制與錯誤

`MaxTags` 拒絕超過上限的新增。欄位會抖動，並播報上限資訊。`ErrorMessage` 會把欄位標記為無效並顯示錯誤列。

<Demo name="Limit" />

<Demo name="Error" />

`MaxTags` 作用於所有新增方式：輸入、Enter、逗號、貼上和 `AddTag`。`null`（預設值）表示沒有上限。貼上超出上限時，能放下的部分會被新增，被拒絕的部分保留在草稿中。

對於控制項無法得知的規則（例如伺服器錯誤），使用 `ErrorMessage`。將其設為 `null` 或空字串即可清除。

### 驗證

`TagInput` 沒有提交前的掛鉤：按下 Enter 時草稿會立即新增。控制項負責執行數量上限與重複檢查；格式與伺服器規則在變動之後由檢視模型執行。

| 規則 | 何時執行 | 顯示在哪裡 |
|---|---|---|
| 最大數量（`MaxTags`） | 每次新增時立即執行 | 欄位抖動，並播報上限資訊；沒有錯誤列。如果使用者需要看到數量，把計數放在 `Description` 中 |
| 重複 | 按下 Enter、輸入逗號或貼上時 | 既有的標籤閃爍，並播報「<tag> 已新增」；草稿保留 |
| 格式（電子郵件、網域、slug） | 標籤出現之後，在檢視模型中 | 由檢視模型繫結的 `ErrorMessage`，直到清單再次有效 |
| 至少一個標籤 | 送出時 | `ErrorMessage`；用 `FocusEditor()` 把焦點移到該欄位 |
| 伺服器或唯一性規則 | 請求返回之後 | 在錯誤所指的欄位上使用 `ErrorMessage`；清單變動時清除 |

重複檢查忽略大小寫，因此清單中已有 design 時，再輸入 Design 會被拒絕，並保留原有的寫法。

### 資料結構與效能

標籤就是一個字串，沒有 id、顏色或顯示文字。像每個標籤的顏色這類額外資料，應以標籤文字為鍵保存在檢視模型中。

每次新增或刪除都會複製清單，對於表單中幾十個標籤的規模來說成本可以忽略。批次載入大量標籤時，使用一次 `Value` 指派：它只觸發一次 `ValueChanged` 事件，而迴圈呼叫 `AddTag` 則會為每個標籤各觸發一次。

### 貼上的清單

貼上包含逗號、換行或定位字元的文字時，文字會被拆分為多個標籤。最後一段留在草稿中。

<Demo name="Paste" />

各段會被去除前後空白；空段和重複項會被略過。不含分隔符號的文字會貼到草稿中。

### 空狀態與預留位置文字

欄位沒有標籤且草稿為空時，預留位置文字會升起出現。

<Demo name="Empty" />

除非有更好的範例，例如收件者欄位使用 `name@company.com`，否則保留預設的「新增一個標籤」。

### 輔助文字

欄位下方的輔助文字也會為螢幕閱讀器描述編輯器。

<Demo name="Description" />

用一句話說明規則或清單的作用，例如「最多 5 項技能」。螢幕閱讀器會在編輯器名稱之後，把它作為編輯器的說明文字朗讀。

### 換列

標籤會換到新的一列，欄位隨之增高。刪除一個標籤，可以看到其餘標籤滑動到新位置。

<Demo name="Wrapping" />

很長的清單會換列，欄位向下增高；它從不橫向捲動。寬於欄位的標籤會以省略號截斷。

### 鍵盤

在列首按 Backspace 會選取最後一個標籤；再按 Backspace 或 Delete 會刪除選取的標籤；方向鍵移動選取項；Escape 清除選取。點選標籤也會選取它。

<Demo name="Keyboard" />

按鍵列在無障礙一節中。

### 停用

停用的欄位會以 0.5 的不透明度淡化，背景為 `SurfaceMuted`。

<Demo name="Disabled" />

清單暫時不能修改時使用 `IsEnabled="False"`，並在附近說明原因。`TagInput` 沒有唯讀狀態，因此停用欄位是鎖定清單的唯一方式；Tab 鍵會略過它。

### 範例：支援工單表單

一個用於工單的支援主控台表單。收件者欄位展示了一條拒絕地址的規則：錯誤的標籤已被移除，並且提示說明了如何修正。兩個欄位寬度相同，因此它們的訊息列對齊。

![工單表單，包含標籤清單，以及顯示無效地址的收件者欄位](images/TagInputForm.png)

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc"
             xmlns:generic="using:System.Collections.Generic"
             Width="640">
    <StackPanel Spacing="20">
        <arc:Input Label="Ticket title"
                   Text="Invoice export times out for large workspaces"
                   Description="Shown to the customer in the ticket list." />
        <arc:TagInput Label="Labels"
                      Description="Press Enter or a comma to add a label. Use lowercase words."
                      HorizontalAlignment="Stretch">
            <arc:TagInput.Value>
                <generic:List x:TypeArguments="x:String">
                    <x:String>billing</x:String>
                    <x:String>export</x:String>
                    <x:String>p2</x:String>
                </generic:List>
            </arc:TagInput.Value>
        </arc:TagInput>
        <arc:TagInput Label="Notify"
                      ErrorMessage="Enter a full email address, such as ops@acme.com."
                      HorizontalAlignment="Stretch">
            <arc:TagInput.Value>
                <generic:List x:TypeArguments="x:String">
                    <x:String>finance@northwind.com</x:String>
                    <x:String>ops@acme.com</x:String>
                    <x:String>on-call-team</x:String>
                </generic:List>
            </arc:TagInput.Value>
        </arc:TagInput>
        <StackPanel Orientation="Horizontal" Spacing="8" HorizontalAlignment="Right">
            <arc:Button Content="Cancel" Variant="Secondary" />
            <arc:Button Content="Save ticket" />
        </StackPanel>
    </StackPanel>
</UserControl>
```

### 文案規範

- **標籤**：句首大寫的複數名詞，不加冒號，例如「標籤」「收件者」「允許的網域」。不要寫「請輸入標籤：」。標籤在欄位有內容時仍然可見，因此由它承載含義。
- **預留位置文字**：一個標籤的範例（「name@company.com」「billing」），僅在欄位為空時顯示。
- **輔助文字**：一句話說明規則或作用，例如「最多 5 項技能。」「顯示在發票抬頭上。」。數字用阿拉伯數字書寫。
- **標籤內容**：保存使用者應看到的形式，並保持大小寫一致。
- **錯誤提示**：用與輔助文字相同的用詞說明規則和修正方法，例如「請輸入完整的電子郵件地址，例如 ops@acme.com。」。

| 不要寫 | 改為 |
|---|---|
| 在此輸入標籤 | 新增一個標籤 |
| 電子郵件無效！ | 請輸入完整的電子郵件地址，例如 ops@acme.com。 |
| 標籤太多 | 最多可新增 5 項技能。刪除一項後才能再新增。 |
| 標籤已存在 | Design 已新增。 |
| 請新增一些內容 | 至少新增一位收件者。 |

### 最佳做法

**建議**

- 為每個標籤欄位提供可見標籤；當規則不明顯時，還要提供輔助文字。標籤就是編輯器的無障礙名稱，沒有標籤的欄位就是沒有名稱的控制項。
- 把清單保存為 `IReadOnlyList<string>` 屬性，並像控制項那樣為每次變動指派新清單。
- 對表單已經聲明的上限使用 `MaxTags`，這樣新增會被立即拒絕並播報。
- 格式錯誤在清單有效之前一直保留在欄位上，讓規則在使用者操作的地方可見。
- 送出失敗時，用 `FocusEditor()` 把焦點移到該欄位。

**避免**

- 用標籤欄位來表示固定的目錄。使用者無法判斷哪些標籤是允許的，拼字錯誤也會變成新值。對於已經存在的清單，使用 `MultiSelect` 或 `Combobox`。
- 刪除被拒絕的標籤卻不給出提示。應在欄位上說明違反了哪條規則以及如何修正。
- 依賴預留位置文字來說明規則。欄位有了標籤或草稿後，預留位置文字就會消失。

### 常見問題

**為什麼按 Enter 不會送出我的表單？** 編輯器會用 Enter 把草稿新增為標籤，因此表單的預設按鈕（`IsDefault="True"`）不會回應該按鍵。使用者需要按儲存按鈕，或用 Tab 切換到它。

**如何顯示還剩多少個標籤？** `TagInput` 沒有計數器。把 `Description` 繫結到檢視模型根據 `Value.Count` 和 `MaxTags` 產生的字串，例如「已用 3 個，共 5 個」。這樣計數會隨每次變動更新。

**如何清空所有標籤？** 把 `Value` 設為空清單，例如 `tags.Value = [];`。草稿也為空時，預留位置文字會重新出現。


## API 參考

<!-- api: TagInput -->

### 事件參數

`ValueChanged` 傳遞的是 `TagInputValueChangedEventArgs`。

| 成員 | 類型 | 說明 |
|---|---|---|
| `OldValue` | `IReadOnlyList<string>` | 變動前的標籤 |
| `NewValue` | `IReadOnlyList<string>` | 變動後的標籤 |
| `Added` | `IReadOnlyList<string>` | `NewValue` 中存在而 `OldValue` 中不存在的標籤 |
| `Removed` | `IReadOnlyList<string>` | `OldValue` 中存在而 `NewValue` 中不存在的標籤 |

## 無障礙

- `Label` 為欄位命名。`IsLabelVisible="False"` 時標籤被隱藏，但仍為欄位命名。
- 編輯器的說明文字來自 `Description` 和 `ErrorMessage`，因此螢幕閱讀器會在名稱之後朗讀規則或錯誤。
- 每個標籤的刪除按鈕命名為「Remove <tag>」（即 `RemoveLabelFormat` 的格式，預設為 `Remove {0}`）。標籤本身作為一般文字朗讀；它的刪除按鈕是其中唯一的控制項。
- 新增、刪除、選取、重複和被拒絕的新增都會以禮貌的方式播報。新增或選取時焦點不移動；用按鈕或 Backspace 刪除標籤後，焦點會回到編輯器。重複的標籤不會被再次新增：既有的標籤會閃爍，使被忽略的 Enter 也有回饋。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| `Enter` 或 `,` | 把修剪後的草稿新增為標籤。重複項不會被新增：既有的標籤閃爍，並播報重複資訊。草稿為空時無任何動作 |
| 游標位於空草稿開頭時按 `Backspace` | 選取最後一個標籤 |
| 游標位於草稿開頭時按 `ArrowLeft`，或已選取標籤時 | 選取前一個標籤；在第一個標籤時選取項保持不變 |
| 已選取標籤時按 `ArrowRight` | 選取後一個標籤；超過最後一個標籤後清除選取，游標回到草稿 |
| 已選取標籤時按 `Backspace` 或 `Delete` | 刪除選取的標籤，並把焦點移回編輯器 |
| 已選取標籤時按 `Escape` | 清除選取 |
| 草稿內按 `Backspace` | 照常編輯文字 |
| `Tab` / `Shift+Tab` | 依值的順序依序經過刪除按鈕，然後到達編輯器 |
| 在取得焦點的刪除按鈕上按 `Enter` | 刪除該標籤 |
| 點選標籤 | 選取它；再次點選清除選取。焦點保留在編輯器中 |
| 點選欄位的空白部分 | 把焦點移到編輯器 |

輸入任何字元都會清除選取。離開欄位時，草稿會作為標籤送出，並清除選取。

## 動態效果

- 新標籤在輸入的位置以模糊和輕微縮放淡入。被刪除的標籤離開，其餘標籤滑動到新的位置。
- 選取光環在標籤之間滑動；標籤換列時，外框高度隨之變化。
- 重複的標籤閃爍一次。被拒絕的新增會使欄位抖動。
- 減少動態效果時，去掉版面移動、縮放和模糊，僅保留短暫淡入。關閉動態效果時直接到達最終狀態。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 新增標籤 | 帶模糊淡入，在輸入位置從 90% 放大 | 淡入 | 立即完成 |
| 刪除標籤 | 從原位置離開，其餘標籤滑入新位置 | 立即消失 | 立即完成 |
| 標籤重排或換列 | 透過變形彈簧滑動；外框高度以平滑彈簧跟隨 | 直接跳到新位置 | 立即完成 |
| 選取的標籤變化 | 選取光環滑動到新標籤 | 光環直接跳到該標籤 | 立即完成 |
| 輸入重複項 | 既有的標籤閃爍一次 | 不閃爍 | 不閃爍 |
| 被拒絕的新增（超出上限） | 內容向一側晃動，並以彈性彈簧回彈到原位 | 不抖動 | 不抖動 |
| 預留位置文字出現 | 以 0.3 em 升起，並帶輕微模糊 | 淡入 | 立即完成 |

## 相關元件

- 當值來自已存在的清單時，用 `MultiSelect` 或 `Combobox`。
- `ChipGroup` 用於縮小結果集的分面篩選。
- `Input` 用於單一值。
- `MentionInput` 用於連續文字中的 @人員和 #頻道參照。

| 元件 | 適用情境 |
|---|---|
| `Input` | 值是一行文字，而不是清單 |
| `MultiSelect` | 值來自固定清單，使用者需要選擇多個 |
| `Combobox` | 清單很長，使用者在選擇前需要搜尋 |
| `ChipGroup` | 標籤是結果集上的篩選分面 |
| `MentionInput` | 連續文字中包含 @人員或 #頻道參照 |
| `Textarea` | 文字跨多行，或使用者會貼上整段內容 |
| `MoneyInput` / `NumberField` | 值是需要解析的金額或數字 |
