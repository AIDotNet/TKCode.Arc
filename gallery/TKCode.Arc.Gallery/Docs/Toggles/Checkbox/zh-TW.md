---
title: 核取方塊
summary: 二元選擇，狀態精確且清楚易讀。
source-hash: 352309eb57b1
---

<Demo name="LabelAndDescription" />

## 何時使用

`Checkbox` 用於表單、設定面板或表格工具列中，使用者做出的獨立「是或否」選擇。它自帶標籤與說明文字，並提供部分選取狀態，供子項只部分被選取的父層列使用。凡是透過送出或儲存確認選擇、且方框的變動不會立即生效的地方，都適合使用它。

- **註冊與結帳中的同意條款**：「接受資料處理條款」，說明文字說明同意涵蓋什麼。在點選「建立帳號」繼續之前，必須勾選此項。
- **管理主控台中的通知管道**：父層列「所有管道」下有電子郵件、Slack 與簡訊子項。當部分管道開啟時，父層顯示部分選取狀態，讓摘要與明細一致。
- **資料表格中的列選取**：表頭的核取方塊會選取所有可見的發票；只選取部分列時顯示為部分選取。表格在使用者執行批次動作時才套用選取。
- **設定表單中的選用項目**：「將每張發票的副本寄送給帳務聯絡人」。此值隨表單一起儲存，說明文字以一行說明其後果。
- **變動在儲存時才生效的清單篩選**：匯出對話方塊中的一組狀態，使用者可以勾選多個後按下「匯出」。

## 何時不用

- **立即生效的設定**，例如開啟雙重驗證或暫停發票郵件：使用 `Switch`，它讓人感覺是立即的開或關。
- **從一組選項中只能選一個**，例如帳務國家或方案週期：使用 `RadioGroup`，選了一個就會清除其他項目。
- **以一列標籤呈現、經常切換的篩選條件**，例如表格上方的「未結」「逾期」「已付」：使用 `ChipGroup`。標籤精簡，看起來像篩選器，而不是表單欄位。
- **在兩種狀態之間切換、且會改變價格或內容的週期或檢視**，例如按月與按年：使用 `BillingToggle` 或 `SegmentedControl`。
- **多個各自需要長篇說明與價格的獨立選擇**，例如方案的附加項目：使用者只選一個方案時使用 `RadioCards`；選項彼此獨立時，則在設定表單中使用一組附說明的核取方塊。

## 用法

標籤即控制項的內容。`Description` 會在標籤下方加上一行說明文字。此控制項衍生自 Avalonia 的 `CheckBox`，因此 `IsChecked`（`bool?` 型別，其中 `null` 表示部分選取）與 `IsThreeState` 的行為與 Avalonia 中一致。Arc 新增的部分包括 `CheckedState` 列舉、說明、無效與必填狀態，以及 `CheckedStateChanged` 事件。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Checkbox Content="同意條款及條件"
                  Description="您同意我們的服務條款與隱私權政策。" />
</UserControl>
```

### 結構

1. **點選區域**：44 px 的方形（Arc 的控制項尺寸）。整個方形都會回應點擊或點按，它是該列的指標目標，即使繪製的方框較小。點選區域位於列的頂端，因此標籤的第一行與方框對齊。
2. **方框**：18 px 的方形，圓角 5 px，邊框 1 px。它在點選區域中置中。未勾選時，邊框使用強邊框色；懸停時加深為淡化文字色；無效（且未勾選）時使用危險色。
3. **填滿與標記**：勾選或部分選取的方框以控制項開啟色填滿。勾號或短橫線是 1.75 px 的筆畫，顏色為控制項圖形色，使用圓形端點與接合。
4. **標籤**（`Content`）：中等字重，可為一行或多行，行高 19.6 px。它位於點選區域之後，並為輔助技術提供控制項名稱。
5. **說明文字**（`Description`）：極小字級的淡化色文字，行高 16.8 px，位於標籤下方 2 px。說明為空時隱藏。
6. **焦點環**：繞點選區域繪製的 2 px 環，用於鍵盤焦點。指標焦點時隱藏。

Arc 核取方塊沒有尺寸屬性：方框維持 18 px。核取方塊只會隨標籤與說明文字變高，例如說明文字為兩行時。

### 必填與無效

`IsRequired` 會在表單中將該欄位標示為必填。它會向輔助技術回報為必填，但不繪製任何內容。`IsInvalid` 顯示錯誤狀態：方框未勾選時，其邊框使用危險色。已勾選的方框永遠不顯示錯誤，因此使用者一勾選就應清除 `IsInvalid`，或保留其設定，讓勾選後隱藏危險色邊框。

<Demo name="States" />

```xml
<arc:Checkbox Content="同意條款及條件"
              Description="勾選此項以繼續。"
              IsRequired="True"
              IsInvalid="True" />
```

### 驗證

在大多數表單中，核取方塊只有一條規則：儲存表單前必須勾選。控制項會回報必填要求與危險邊框，但何時顯示由應用程式決定。在使用者能夠處理問題的時刻顯示必填狀態，並把訊息放在說明文字中，讓版面不會移動。

| 規則 | 何時檢查 | 顯示在哪裡 |
|---|---|---|
| 必須同意 | 送出時，之後隨方框變動而檢查 | `IsInvalid`（危險邊框）以及「勾選此項以繼續。」之類的說明文字 |
| 父子項保持一致 | 任一子項變動時 | 父層的 `CheckedState`；這不是錯誤 |
| 伺服器拒絕該選擇 | 請求結束後 | 說明文字，方框變動時清除 |

```csharp
// 送出時：標記未勾選的方框並把焦點移到它上面。
acceptTerms.IsInvalid = acceptTerms.IsChecked != true;
if (acceptTerms.IsInvalid)
{
    acceptTerms.Focus();
    return;
}

// 使用者一勾選就清除標記。
acceptTerms.IsCheckedChanged += (_, _) => acceptTerms.IsInvalid = false;
```

### 部分選取的父項

`CheckedState` 是包含 `Unchecked`、`Checked` 與 `Indeterminate` 的列舉。`IsChecked` 是同一個值的可為 null 布林形式，其中 `null` 表示部分選取。當部分子項被勾選時，將父項設為部分選取。

<Demo name="IndeterminateParent" />

```xml
<arc:Checkbox Content="全部管道" CheckedState="Indeterminate" />
```

若要讓點選父項時同時設定所有子項，請在父項上處理 `CheckedStateChanged`，並將每個子項的 `IsChecked` 設為新狀態。在該處理常式中略過部分選取狀態，因為父項無法把子項設為「部分選取」。上面的範例還會根據子項更新父項，並以一個旗標保護兩個處理常式，使它們不會互相呼叫。

點選部分選取的父項會將其勾選，這與 Radix 一致；點選已勾選的父項則會清除它。因此，像範例中那樣設定子項的父項，下一次點選時會把每個子項都設定好。當父項是表格或清單的「全選」控制項時，應採用這種順序。

### 三態循環

`IsThreeState="True"` 讓點選在未勾選、勾選與部分選取之間循環，與 Avalonia 的 `CheckBox` 一致。只有當部分選取狀態本身就是一種選擇時才使用循環，例如清單中表示「兩者皆可」的篩選。對於只摘要子項的父層，保留預設的兩態行為，即點選部分選取時勾選該方框。

<Demo name="TriState" />

### 繫結到檢視模型

在 MVVM 應用程式中，將 `IsChecked` 繫結到 `bool?` 屬性；當檢視模型偏好三個具名值時，則將 `CheckedState` 繫結到該列舉。兩者都是雙向的。部分選取的父項是計算值，因此檢視模型應從子項推導它，而不是儲存它。

```csharp
public partial class NotificationsViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AllChannels))]
    private bool _email = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AllChannels))]
    private bool _slack = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AllChannels))]
    private bool _sms;

    [ObservableProperty]
    private bool _acceptTerms;

    public CheckboxState AllChannels => (Email, Slack, Sms) switch
    {
        (true, true, true) => CheckboxState.Checked,
        (false, false, false) => CheckboxState.Unchecked,
        _ => CheckboxState.Indeterminate
    };

    [RelayCommand]
    private void SetAllChannels(CheckboxState state)
    {
        if (state == CheckboxState.Indeterminate)
        {
            return;
        }
        Email = Slack = Sms = state == CheckboxState.Checked;
    }
}
```

```xml
<StackPanel Spacing="8">
    <arc:Checkbox Content="全部管道"
                  CheckedState="{Binding AllChannels, Mode=OneWay}" />
    <arc:Checkbox Content="電子郵件" IsChecked="{Binding Email}" Margin="28,0,0,0" />
    <arc:Checkbox Content="Slack" IsChecked="{Binding Slack}" Margin="28,0,0,0" />
    <arc:Checkbox Content="簡訊" IsChecked="{Binding Sms}" Margin="28,0,0,0" />
    <arc:Checkbox Content="接受條款及條件"
                  IsChecked="{Binding AcceptTerms}"
                  IsRequired="True" />
</StackPanel>
```

- 父項以單向方式讀取計算值 `AllChannels`。使用者點選父項只會改變控制項本身，因此需要在程式碼後置中處理 `CheckedStateChanged`，並把 `e.NewState` 傳給 `SetAllChannelsCommand`；接著子項會更新檢視模型。上面的範例在程式碼後置中完成了同樣的工作。
- `IsChecked` 是雙向的，因此子項的方框會直接修改檢視模型，`AllChannels` 則透過通知隨之更新。
- 編譯繫結需要在檢視上設定 `x:DataType`（為求精簡，範例省略了它）。

### 事件

`CheckedStateChanged` 會回報變動前後的狀態。只有狀態真正改變時才會引發，並且會冒泡，因此父面板上的處理常式也會收到它。`IsCheckedChanged`（繼承自 Avalonia 的核取方塊）會在每次 `IsChecked` 變動時引發，包括從 `true` 變為 `null` 的情況。

```csharp
checkbox.CheckedStateChanged += (_, e) =>
{
    Console.WriteLine($"{e.OldState} -> {e.NewState}");
};
```

### 範例：發票提醒的通知設定

SaaS 管理主控台中的帳務設定頁面。父層列「所有管道」為部分選取狀態，因為電子郵件與 Slack 已開啟而簡訊已關閉，因此摘要與子項一致。條款核取方塊為必填，使用者尚未接受，因此顯示危險邊框；稽核紀錄這一列已停用，因為產品一律會記錄它。每個標籤都說明勾選後會開啟什麼，每段說明文字以一行說明其後果。

![包含部分選取父項核取方塊、必填條款核取方塊與停用核取方塊的通知設定表單](images/InvoiceReminders.png)

```xml
<StackPanel Width="560" Spacing="14">
    <TextBlock Text="Invoice reminders"
               FontSize="16"
               FontWeight="SemiBold"
               Foreground="{arc:ArcDesignTokenResource Foreground}" />
    <arc:Checkbox Content="All channels"
                  CheckedState="Indeterminate"
                  Description="Email and Slack are on. SMS is off." />
    <StackPanel Margin="28,0,0,0" Spacing="6">
        <arc:Checkbox Content="Email" IsChecked="True" />
        <arc:Checkbox Content="Slack" IsChecked="True" />
        <arc:Checkbox Content="SMS" />
    </StackPanel>
    <arc:Checkbox Content="Send a copy to the billing contact"
                  Description="Copies go to billing@northwind.com."
                  IsChecked="True" />
    <arc:Checkbox Content="Accept the data processing terms"
                  Description="Check this box to turn on invoice reminders."
                  IsRequired="True"
                  IsInvalid="True" />
    <arc:Checkbox Content="Keep the audit log"
                  Description="Always on for this workspace."
                  IsChecked="True"
                  IsEnabled="False" />
</StackPanel>
```

### 文案規範

- **標籤**：說明勾選後會開啟或同意什麼，使用句首大寫的精簡片語：「透過電子郵件寄送發票提醒」「接受資料處理條款」。當方框做的事情更具體時，避免只用「電子郵件」這樣的單一名詞。
- **說明文字**：一句話說明後果或要求：「副本將寄送至 billing@northwind.com」「勾選此項以繼續」。數字以阿拉伯數字書寫。
- **無效提示**：說明該怎麼做，而不是說某件事失敗了。把它放在說明文字中，因為 `IsInvalid` 只繪製危險邊框。
- **父層標籤**：使用子項的範圍，例如「所有管道」或「本頁所有發票」，讓父層讀起來像是子項的摘要。

| 不要寫 | 改為 |
|---|---|
| 電子郵件 | 透過電子郵件寄送發票提醒 |
| 條款 | 接受資料處理條款 |
| 必填欄位！！！ | 勾選此項以繼續。 |
| 是 / 否 | 將副本寄送給帳務聯絡人 |

### 最佳做法

**建議**

- 對於隨表單一起儲存的獨立選擇使用核取方塊；對於立即生效的設定使用 `Switch`。使用者會預期核取方塊要等到儲存時才生效。
- 標籤與說明文字要夠短，維持在一到兩行內，讓點選區域與標籤對齊。
- 只要子項部分被選取，就在父層列上顯示部分選取狀態，讓摘要保持正確。
- 只在送出時標示必填的方框，並把焦點移到它上面，避免告訴使用者一個他們還沒走到的欄位。

**避免**

- 用核取方塊表示兩個互斥選項之一。一對不能同時開啟的核取方塊應改用 `RadioGroup`。
- 只用危險色說明錯誤。沒有文字的紅色邊框不會告訴使用者任何資訊。
- 無理由地停用核取方塊。應在說明文字中說明它為什麼不能變動，就像稽核紀錄那一列所做的。
- 在同一輪更新中，既從父項自己的處理常式改變父項狀態，又從子項改變父項狀態；兩次更新會互相呼叫。應像範例那樣用旗標保護其中一個方向。

### 常見問題

**為什麼點選部分選取的方框是勾選它而不是清除它？** 這遵循 Radix 與 WAI-ARIA 模式：部分選取的方框點選後會變為勾選。若父項應把子項設為與摘要相反的狀態，就必須處理 `CheckedStateChanged` 並自行決定下一個狀態。

**為什麼按 Enter 不會切換核取方塊？** WAI-ARIA 核取方塊模式使用空白鍵。核取方塊不處理 Enter，因此該按鍵會送到視窗，由表單的預設按鈕回應。

**`IsRequired` 與 `IsInvalid` 有什麼差別？** `IsRequired` 告訴輔助技術該欄位必須在表單送出前勾選，且不繪製任何內容。`IsInvalid` 在方框未勾選時繪製危險邊框。應根據你自己的送出邏輯設定 `IsInvalid`。

**如何顯示「是、否、任意」這樣的三態選項？** 設定 `IsThreeState="True"`，讓點選在未勾選、勾選與部分選取之間循環。用 `CheckedState` 儲存該值，讓檢視模型讀取這三個名稱。

**方框沒有標籤時，它會回報什麼名稱？** 當 `Content` 為空且未設定名稱時，核取方塊會回報「核取方塊」。對於沒有可見標籤的方框，例如表格中的列選取器，請設定 `AutomationProperties.Name` 為它命名。

## API 參考

<!-- api: Checkbox -->

### CheckboxState

| 成員 | 說明 |
|---|---|
| `Unchecked` | 方框為空。 |
| `Checked` | 方框顯示勾號。 |
| `Indeterminate` | 部分但非全部子項被勾選；勾號會變形為短橫線。 |

### CheckboxStateChangedEventArgs

| 屬性 | 類型 | 說明 |
|---|---|---|
| `OldState` | `CheckboxState` | 變動前的狀態。 |
| `NewState` | `CheckboxState` | 變動後的狀態。 |

## 無障礙

- 標籤即核取方塊的名稱。沒有標籤時，請設定 `AutomationProperties.Name`；名稱會回退為「核取方塊」。
- `Description` 是說明文字。輔助技術會在名稱之後朗讀它。
- `IsRequired` 會向輔助技術回報該表單欄位為必填。這不需要視覺標記。
- 部分選取狀態會回報為混合狀態。繪製的勾號與短橫線不會分別對外公開。
- 控制項公開核取方塊模式：`IsChecked` 為 null 時回報為部分選取，因此螢幕閱讀器會把父層列讀為部分選取。
- 空白鍵切換方框。與 WAI-ARIA 核取方塊模式一致，Enter 不會切換。
- 鍵盤焦點會繪製 2px 的外框，並偏移 2px。指標焦點則不繪製。
- 無效邊框只是視覺狀態。應搭配說明需要修正什麼的說明文字，使訊息不只依賴顏色。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 將焦點移到下一個或上一個控制項。停用的核取方塊會被略過。 |
| `Space` | 切換方框：未勾選變為勾選，勾選變為未勾選，部分選取變為勾選。使用 `IsThreeState` 時，循環順序為未勾選、勾選、部分選取。 |
| `Enter` | 核取方塊上沒有任何作用。此按鍵不會被處理，會送到視窗，表單的預設按鈕仍會執行。 |

## 動態效果

- 方框勾選時，填色從略小的尺寸放大出現；取消勾選時則快速淡出。
- 勾號會逐步繪製出來。切換為部分選取時，勾號會變形為短橫線，切回時則反向變形。
- 按下方框時會略微縮小，放開後彈回。
- 減少動態效果時，所有狀態變化立即套用，並取消按下縮放。關閉動態效果時效果相同。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 方框被勾選 | 填色以靈敏的彈簧放大出現，勾號逐步繪製 | 立即套用 | 立即套用 |
| 方框被取消勾選 | 填色快速淡出 | 立即套用 | 立即套用 |
| 勾選與部分選取之間切換 | 勾號變形為短橫線，或反向變形 | 立即套用 | 立即套用 |
| 方框被按下 | 方形立即縮放至 95%；放開後彈回 | 無按下縮放 | 無按下縮放 |
| 邊框顏色變化（懸停、無效） | 邊框顏色快速緩和變化 | 立即 | 立即 |

## 相關元件

- `Switch`：用於立即生效的設定。
- `RadioGroup`：用於從一組選項中只選一個。
- `ChipGroup`：用於使用者經常切換的一組篩選條件。

| 元件 | 適用情境 |
|---|---|
| `Switch` | 使用者改變單一設定時，該設定立即生效 |
| `RadioGroup` | 從一組選項中恰好選一個 |
| `RadioCards` | 恰好選一個方案，且每個選項都需要價格與說明 |
| `ChipGroup` | 一列篩選條件位於清單或表格上方，且經常切換 |
| `BillingToggle` | 以節省說明選擇計費週期 |
| `SegmentedControl` | 選項切換檢視，且沒有需要儲存的值選擇 |
