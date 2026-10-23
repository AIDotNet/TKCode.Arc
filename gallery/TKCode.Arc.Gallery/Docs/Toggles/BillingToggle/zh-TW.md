---
title: 計費切換
summary: 按月與按年切換，帶有節省標章，價格會滾動至新金額。
source-hash: 7e0fd9ad8d5d
---

<Demo name="Pricing" />

## 何時使用

`BillingToggle` 是用於選擇計費週期的分段切換，通常位於定價頁頂端，或計費設定頁的方案區段。它與 `BillingPrice` 搭配使用：後者顯示所選週期對應的金額，週期變動時數字會滾動到新值，讓使用者不必離開頁面就能看到每種選擇的費用。

- **定價頁的計費週期**：銷售按月與按年方案的 SaaS 產品。按年選項上的節省標章（「省 20%」）在做決定的地方說明折扣。
- **計費設定中的方案變更**：管理員把工作區從按月計費改為按年計費的帳號頁面。在使用者確認之前，切換會顯示新金額與「按年計費」的週期文字。
- **訂閱之外的終身或一次性選項**：一款銷售按月、按年與終身授權的開發者工具。帶有「最划算」標章的三選項切換，讓所有選擇都維持在一列內。
- **帶週期切換的結帳摘要**：結帳步驟顯示按月或按年計費的總額，所選選項上的標章以金額說明省了多少（「你省 $48」）。

## 何時不用

- **切換的檢視不會改變價格**，例如同一批訂單的清單與看板：使用 `SegmentedControl`。它沒有標章，也沒有價格語意。
- **選項需要說明或垂直排版**，例如每個方案都有一句說明的清單：使用 `RadioGroup` 或 `RadioCards`。`BillingToggle` 是一列簡短標籤的水平排列。
- **單一開啟或關閉的設定**，例如「自動續訂」：使用 `Switch`，它表現為立即生效的設定。
- **每個方案都有價格、功能以及不可用原因**：使用 `RadioCards`。價格放在卡片中，切換只負責週期。
- **從很長的計費貨幣或地區清單中選擇**：使用 `Select` 或 `Combobox`。切換適合兩到三個能放在一列內的選項。

## 用法

沒有選項時，切換顯示「按月」與「按年」，其中「按年」帶有標章「省 20%」。將 `Value` 設為所選選項的值。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:BillingToggle Value="yearly" />
</UserControl>
```

`BillingToggle` 衍生自 Avalonia 的 `SelectingItemsControl`，因此具備一般的項目模型（`ItemsSource`、`Items`、`SelectedIndex`、`SelectedItem`、`SelectedValue`）以及該基底類別的選取事件。應用程式中應使用的是 `Value`：它以字串形式保存所選選項的值，為雙向繫結，並與 `SelectedValue` 保持同步。切換在選取控制項之上加入節省標章、滑動的滑塊與週期語意，因此計費頁面不需要自訂範本就能獲得完整的控制項。

### 結構

1. **軌道**（`BillingToggle` 本身）：一個膠囊形狀，使用淡化的表面色並帶有 1 px 的細邊框。四邊各內縮 3 px（`Large` 時為 4 px），選項水平排列其中。
2. **滑塊**：位於所選選項下方的凸起膠囊，使用凸起表面色並帶有柔和陰影。整個軌道共用一個形狀，它在選項之間移動，而不是在每個選項中各自出現。
3. **選項**（每個週期對應一個 `BillingToggleOption`）：`Medium` 時高 34 px，`Large` 時高 42 px。左右內距為 14 px（`Large` 時為 18 px），標籤使用中等字重。
4. **標籤**（`Label`；未設定標籤時使用 `Value`）：週期名稱，例如「按月」或「按年」。`Medium` 使用小字級，`Large` 使用基礎字級。
5. **標章**（`Badge`）：節省說明，`Medium` 時高 24 px，`Large` 時高 32 px，左右內距 8 px，位於膠囊中，背景為安靜的底色（前景色的 6%），文字使用次要色。它位於標籤之後 8 px。
6. **啟用標章**（`ActiveBadge`）：選項被選取時標章顯示的文字，例如「你省 $48」。未設定時，標章維持 `Badge`。選取時標章採用強調色（見「尺寸與強調色」）。
7. **焦點環**：所選選項周圍 2 px 的環，僅在鍵盤焦點時出現。

`BillingPrice` 是獨立的控制項，有自己的組成部分：貨幣符號、使用顯示字型的金額、位於週期文字左側帶刪除線的舊價格（`WasAmount`），以及週期文字（`Period`）。貨幣符號的字級約為金額的 0.55，並上提至與數字頂端齊平。

### 自訂選項

可以內嵌撰寫選項，也可以將 `ItemsSource` 繫結到 `BillingToggleOption` 物件的清單。內嵌加入的選項必須在切換加入邏輯樹之前就存在，因為切換只有在那一刻沒有任何選項時，才會安裝預設的「按月」與「按年」選項。`Badge` 顯示在選項上。`ActiveBadge` 在選項被選取時取代它，因此說明可以原地變化，而不會移動周圍的內容。選項可以顯示的每段標章文字都會預留相同的寬度，因此「省 20%」與「你省 $48」不會推動相鄰的選項。

標章適用於幫助使用者做出選擇的資訊：節省金額、「最划算」標記或方案限制。若每個選項的費用都相同，就不要加入標章，因為沒有額外資訊的標章只會為這一列增加雜訊。

<Demo name="ThreePeriods" />

### 價格

`BillingPrice` 顯示一個金額。`Currency` 預設為 `$`，`Decimals` 預設為 0；負的 `Decimals` 視為 0。`Amount` 是 `decimal`，數值以 en-US 文化格式化，因此應使用 `decimal`，而不是格式化後的字串。當 `WasAmount` 高於 `Amount` 時，它以刪除線繪製舊價格；較低或為 null 的值會隱藏舊價格。`Period` 是金額後方的文字，變動時會在原地切換。

```xml
<arc:BillingPrice Amount="24" WasAmount="30" Period="每月" />
```

切換週期時，應在接收切換值的同一個處理常式中更新價格。金額滾動到新數字，舊價格帶著它最後的數值淡出，週期文字切換。下面的範例一次設定三個值，因此卡片永遠不會出現新金額搭配舊週期的情況。

```csharp
var toggle = new BillingToggle { Value = "yearly" };
var price = new BillingPrice { Amount = 24, WasAmount = 30, Period = "每月，按年計費" };

toggle.ValueChanged += (_, e) =>
{
    var yearly = e.NewValue == "yearly";
    price.Amount = yearly ? 24 : 30;
    price.WasAmount = yearly ? 30 : null;
    price.Period = yearly ? "每月，按年計費" : "每月";
};
```

`ValueChanged` 事件攜帶 `OldValue` 與 `NewValue`。它會冒泡，因此父面板上的處理常式也會收到它。副作用（例如追蹤或重新載入報價）使用該事件；資料則使用繫結的檢視模型。

### 尺寸與強調色

`Size` 設定選項高度：`Medium` 為 34，`Large` 為 42。整個計費區域使用同一種尺寸。`Medium` 適合設定表單與定價列；`Large` 適合定價頁的主視覺區，因為訪客最先接觸的控制項就是切換。軌道的內縮與文字大小會隨尺寸一起增長，因此兩種尺寸並不只是等比例縮放。

`BadgeAccent` 為選取的標章上色，預設為 Arc 的成功色。標章背景在淺色主題下為強調色的 13% 不透明度，在深色主題下為 18%；標章文字在淺色主題下是強調色與少量前景色的混合。強調色是應用程式可以設定的筆刷，因此它在程式碼中推導，而不是從權杖讀取。請讓強調色維持在成功或資訊色系：紅色或琥珀色的標章會被理解為警告，而不是節省。

```xml
<arc:BillingToggle Size="Large" Value="yearly" />
```

### 繫結到檢視模型

`Value` 為雙向，承載所選選項的值。將它繫結到檢視模型中的字串屬性。價格是 `BillingPrice` 的單向輸入，因此金額由檢視模型持有。

```csharp
public partial class PricingViewModel : ObservableObject
{
    [ObservableProperty]
    private string? _billingPeriod = "yearly";

    [ObservableProperty]
    private decimal _proAmount = 24;

    [ObservableProperty]
    private decimal? _proWasAmount = 30;

    [ObservableProperty]
    private string _proPeriod = "每月，按年計費";

    partial void OnBillingPeriodChanged(string? value)
    {
        var yearly = value == "yearly";
        ProAmount = yearly ? 24 : 30;
        ProWasAmount = yearly ? 30 : null;
        ProPeriod = yearly ? "每月，按年計費" : "每月";
    }
}
```

```xml
<StackPanel Spacing="24">
    <arc:BillingToggle Value="{Binding BillingPeriod}" HorizontalAlignment="Center" />
    <arc:BillingPrice Amount="{Binding ProAmount}"
                      WasAmount="{Binding ProWasAmount}"
                      Period="{Binding ProPeriod}" />
</StackPanel>
```

- `Value` 為雙向。使用者做出的變更會把新值寫回 `BillingPeriod`，程式碼做出的變更則會選取相符的選項。
- 將 `BillingPeriod` 繫結為 `string`。選項的值是 "monthly" 與 "yearly" 這類字串；在檢視模型中再對應到列舉。
- 三個價格屬性應由同一個方法設定，就像 `OnBillingPeriodChanged` 那樣，這樣檢視模型永遠不會暴露只更新一半的價格。
- 編譯繫結需要在檢視上設定 `x:DataType`（為求簡潔，範例省略了它）。

### 範例：定價頁的方案區段

Northwind Cloud 專案管理產品的定價頁。切換位於三個方案之上。每個方案在按年計費時顯示其月價，舊的月價帶刪除線，週期文字緊接其後。按年選項帶有節省說明，專業版方案在與相鄰卡片等高的卡片中有一個行動按鈕。

![包含按年計費切換與三個方案價格（帶刪除線的月價）的定價區段](images/PricingPlans.png)

```xml
<StackPanel Width="680" Spacing="24">
    <TextBlock Text="Plans for Northwind Cloud"
               FontSize="22"
               FontWeight="SemiBold"
               HorizontalAlignment="Center"
               Foreground="{arc:ArcDesignTokenResource Foreground}" />
    <arc:BillingToggle Size="Large" Value="yearly" HorizontalAlignment="Center">
        <arc:BillingToggleOption Value="monthly" Label="Monthly" />
        <arc:BillingToggleOption Value="yearly"
                                 Label="Yearly"
                                 Badge="Save 20%"
                                 ActiveBadge="You save $48" />
    </arc:BillingToggle>
    <Grid ColumnDefinitions="*,*,*">
        <Border Grid.Column="0"
                Margin="0,0,16,0"
                Padding="20"
                CornerRadius="12"
                BorderThickness="1"
                BorderBrush="{arc:ArcDesignTokenResource BorderSubtle}"
                Background="{arc:ArcDesignTokenResource SurfaceRaised}">
            <StackPanel Spacing="12">
                <TextBlock Text="Starter" FontWeight="SemiBold"
                           Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
                <arc:BillingPrice Amount="10" WasAmount="12" Period="per month" />
                <TextBlock Text="Up to 5 members and shared boards."
                           TextWrapping="Wrap" FontSize="13"
                           Foreground="{arc:ArcDesignTokenResource TextMuted}" />
                <arc:Button Content="Choose Starter" Variant="Secondary" HorizontalAlignment="Stretch" />
            </StackPanel>
        </Border>
        <Border Grid.Column="1"
                Margin="0,0,16,0"
                Padding="20"
                CornerRadius="12"
                BorderThickness="1"
                BorderBrush="{arc:ArcDesignTokenResource BorderSubtle}"
                Background="{arc:ArcDesignTokenResource SurfaceRaised}">
            <StackPanel Spacing="12">
                <TextBlock Text="Pro" FontWeight="SemiBold"
                           Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
                <arc:BillingPrice Amount="24" WasAmount="30" Period="per month" />
                <TextBlock Text="Unlimited boards, audit logs and SSO."
                           TextWrapping="Wrap" FontSize="13"
                           Foreground="{arc:ArcDesignTokenResource TextMuted}" />
                <arc:Button Content="Choose Pro" HorizontalAlignment="Stretch" />
            </StackPanel>
        </Border>
        <Border Grid.Column="2"
                Padding="20"
                CornerRadius="12"
                BorderThickness="1"
                BorderBrush="{arc:ArcDesignTokenResource BorderSubtle}"
                Background="{arc:ArcDesignTokenResource SurfaceRaised}">
            <StackPanel Spacing="12">
                <TextBlock Text="Team" FontWeight="SemiBold"
                           Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
                <arc:BillingPrice Amount="48" WasAmount="60" Period="per month" />
                <TextBlock Text="Everything in Pro, plus admin controls."
                           TextWrapping="Wrap" FontSize="13"
                           Foreground="{arc:ArcDesignTokenResource TextMuted}" />
                <arc:Button Content="Choose Team" Variant="Secondary" HorizontalAlignment="Stretch" />
            </StackPanel>
        </Border>
    </Grid>
</StackPanel>
```

### 文案規範

- **選項標籤**：表示週期的單一詞：「按月」「按年」「終身」。避免「按年付費」或「年度計費」，因為切換表示的是週期的選擇，而不是一個動作。
- **標章**：簡短的節省說明或單一事實：「省 20%」「最划算」。百分比以數字加百分號書寫；金額為整數時使用貨幣符號且不帶小數。
- **啟用標章**：所選週期的金額節省，例如「你省 $48」。盡量與標章長度一致；啟用文字過長會使選項變寬，整列跳動。
- **價格週期**：說明價格的計算基礎，例如「每月，按年計費」，讓使用者知道將被收取的總額。沒有週期的價格含糊不清。
- **無障礙名稱**：當「計費週期」不是合適的名稱時，為該群組命名其所選的內容，例如續訂對話方塊中的「訂閱期限」。

| 不要寫 | 改為 |
|---|---|
| 按年付費 | 按年 |
| 節省 20 個百分點 | 省 20% |
| $24/月 | 每月 $24，按年計費 |
| 超值優惠！！！ | 最划算 |
| 年度方案計費週期 | 計費週期 |

### 最佳做法

**建議**

- 顯示年付價格時，同時保留月價，用 `WasAmount` 或旁邊的文字呈現，讓節省可以對照真實數字來核對。
- 定價頁的主切換使用 `Size="Large"`，設定頁使用 `Medium`。同一列混用尺寸會讓選項看起來彼此無關。
- 在同一個處理常式或同一個檢視模型方法中，同時更新金額、舊價格與週期。
- 將 `Value` 繫結到字串屬性，並讓選項的值保持穩定（"monthly"、"yearly"），因為你的後端與分析會儲存它們。

**避免**

- 用切換來切換清單或儀表板檢視。使用者預期週期切換會改變價格，因此切換檢視卻移動了標章與數字，會被理解為價格變動出錯。
- 在短選項中使用很長的標章文字。標章會為它可能顯示的所有文字預留寬度，因此過長的啟用文字會迫使整列變寬。
- 期望劃線而把 `WasAmount` 設為小於或等於 `Amount` 的值。它會隱藏舊價格，節省也隨之消失，且沒有任何說明。
- 用警告色為標章上色；強調色用於節省或收益，警告色會掩蓋訊息。
- 隱藏週期文字。它會告訴使用者這個金額是按月還是按年的總額。

### 常見問題

**為什麼我沒有加入選項，切換卻顯示「按月」與「按年」？** 切換自身沒有選項時，會在加入邏輯樹時安裝 `DefaultOptions`（「按月」，以及帶有「省 20%」的「按年」）。在切換附加到頁面之前加入你自己的選項或 `ItemsSource`，即可顯示不同的週期。

**沒有設定 `Value` 時選取哪一項？** `Value` 未設定時會選取第一個選項，並且該選擇會寫回 `Value`。當預設週期必須是特定的一個（例如按年）而不是第一個選項時，請明確設定 `Value`。

**舊價格沒有出現。** `WasAmount` 只有高於 `Amount` 時才會顯示。將它設為 `null` 可隱藏舊價格，或檢查月價是否確實是較大的數字。

**可以用數字或列舉作為值嗎？** `Value` 屬性與 `BillingToggleOption.Value` 都是字串。使用 "yearly" 這類字串鍵，並在檢視模型中對應到你的列舉。

**如何更改該群組的無障礙名稱？** 在切換上設定 `AutomationProperties.Name`。預設名稱是「計費週期」。每個選項會連同其標籤與目前的標章一起播報，例如「按年 省 20%」。

## API 參考

<!-- api: BillingToggle -->
<!-- api: BillingPrice -->
<!-- api: BillingToggleOption -->

### BillingToggleValueChangedEventArgs

| 屬性 | 類型 | 說明 |
|---|---|---|
| `OldValue` | `string?` | 變動前的值。 |
| `NewValue` | `string?` | 變動後的值。 |

### BillingToggleSize

| 成員 | 說明 |
|---|---|
| `Medium` | 34 px 的選項（預設值）。 |
| `Large` | 42 px 的選項，使用基礎文字大小。 |

## 無障礙

- 該群組預設名稱為「計費週期」。設定 `AutomationProperties.Name` 可改用其他名稱。
- 每個選項都是一個單選按鈕。其名稱為標籤與目前的標章，例如「按年 省 20%」。選取時顯示的標章文字為啟用標章（如已設定）。
- 滑塊對輔助技術隱藏。`BillingPrice` 會被朗讀為金額、舊價格與週期，例如「$24，原價 $30，每月」。
- Tab 會聚焦所選選項。方向鍵會選取並聚焦下一個或上一個選項，並且會循環。Home 與 End 分別選取第一個與最後一個選項。
- 鍵盤焦點會繪製 2 px 的外框，並偏移 2 px。指標焦點則不繪製。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 將焦點移入該群組的所選選項，並移出。只有所選選項是 Tab 停駐點。 |
| `Right` / `Down` | 選取並聚焦下一個選項；從最後一個選項循環到第一個。 |
| `Left` / `Up` | 選取並聚焦上一個選項；從第一個選項循環到最後一個。 |
| `Home` / `End` | 選取並聚焦第一個或最後一個選項。 |

## 動態效果

- 滑塊以約 0.34 秒的臨界阻尼彈簧滑動到所選選項之下，沒有回彈。
- 所選選項的標章採用強調色，其文字以模糊轉場切換。
- 價格會滾動到新金額。舊價格收合消失，週期文字在原地切換。
- 減少動態效果時，滑塊立即就位，未選取的選項不做按下縮放。關閉動態效果時效果相同。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 所選選項改變 | 滑塊以臨界阻尼彈簧滑動，約 0.34 s，沒有回彈 | 滑塊立即移動 | 滑塊立即移動 |
| 未選取的選項被按下 | 按下時選項縮放至 97% | 無按下縮放 | 無按下縮放 |
| 選項文字在懸停或選取時變色 | 顏色透過快速補間緩和變化 | 立即 | 立即 |
| 所選標章採用強調色 | 背景透過標準補間緩和變化；標章文字以模糊切換 | 立即 | 立即 |

## 相關元件

- `SegmentedControl`：用於不涉及價格、只切換內容的檢視。
- `RadioGroup`：用於需要說明或垂直排版的選項。
- `RadioCards`：用於需要價格、說明以及不可用原因的方案。
- `Switch`：用於立即生效的單一設定。

| 元件 | 適用情境 |
|---|---|
| `SegmentedControl` | 選項切換檢視、篩選或版面，且不帶價格或節省資訊 |
| `RadioGroup` | 選項需要說明或垂直排版 |
| `RadioCards` | 每個選項都是一個方案，帶有各自的價格、功能與不可用原因 |
| `Switch` | 單一設定立即生效 |
| `Select` / `Combobox` | 清單很長，或選項不是一組簡短的固定集合 |
| `Checkbox` | 使用者選擇的是多個獨立項目，而不是一個週期 |
