---
title: 單選卡片
summary: 可選取的選項卡片，帶有滑動的選取光環、價格與說明插槽，並具有單選按鈕的鍵盤行為。
source-hash: a439dd2259b6
---

<Demo name="Grid" />

## 何時使用

`RadioCards` 用於單選，並且每個選項在使用者做決定前需要的資訊不止一個標籤：價格、配送時間、說明，或無法選取的原因。它出現在表單或流程的某一步中，例如註冊時選擇方案、結帳時選擇配送方式，使用者選取的卡片就是流程繼續的依據。

- **註冊或帳單步驟中的方案選擇**：一個 SaaS 產品提供入門版、專業版和團隊版。每張卡片在中繼資訊插槽中顯示方案價格，並用一行文字說明包含的內容。
- **結帳時的配送方式選擇**：標準、加急和隔夜達，每項都附有配送時段和費用。清單版面把時段和價格放在同一列，方便比較費用。
- **管理主控台中的地區或資料駐留選擇**：基於合規要求選擇工作區地區，每張卡片說明地區，並附一段關於延遲或保留期限的簡短備註。
- **無法選取的選項及其原因**：需要聯絡業務的企業版，或目前帳號方案不包含的儲存層級。`DisabledReason` 會在使用者正在查看的位置說明限制。
- **簡短清單中必選的一項**：儀表板對話方塊中的匯出格式或報表期間，使用者必須在三到四個選項中選一個，之後「執行」才有意義。

## 何時不用

- **僅含簡短文字的選項**，例如「是、否、也許」或某種狀態：用 `RadioGroup`，它更精簡，沒有價格或說明插槽。
- **同時切換內容的兩到四個視圖**，例如面板上方的概覽、活動和設定：用 `SegmentedControl`。選取的是視圖，而不是表單要儲存的值。
- **很長的選項清單**，例如國家清單或上百個專案：用 `Select` 或 `Combobox`。卡片每個選項佔用大量空間，也不支援篩選。
- **按月或按年切換計費週期並附帶節省說明**：用 `BillingToggle`，它是一列附徽章的簡短標籤。
- **可以同時開啟的多個選項**，例如方案的附加功能：用 `Checkbox`。單選卡片只允許一個選擇。

## 用法

卡片就是此控制項的 `Items`。每個 `RadioCardOption` 接受一個值、一個標籤、一段說明，以及一段中繼資訊文字，中繼資訊會顯示在卡片末端。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:RadioCards SelectedValue="pro">
        <arc:RadioCardOption Value="starter" Label="入門版" Description="適合剛開始使用的個人。" Meta="$0" />
        <arc:RadioCardOption Value="pro" Label="專業版" Description="無限專案與優先支援。" Meta="$12" />
        <arc:RadioCardOption Value="team" Label="團隊版" Description="為小型團隊提供共享工作區與角色。" Meta="$32" />
    </arc:RadioCards>
</UserControl>
```

`RadioCards` 衍生自 Avalonia 的 `SelectingItemsControl`。它具有常見的項目模型（`ItemsSource`、`Items`、`SelectedIndex`、`SelectedItem`）以及該基底類別的選取事件。應用程式繫結的是 `SelectedValue`，因為它攜帶的是選項的值，而不是選項物件。卡片在這個選取控制項之上加入選取光環、指示點、說明與中繼資訊插槽、不可用原因以及兩種版面，因此表單無需自訂範本即可獲得完整的單選群組。

### 結構

1. **卡片**：20 px 圓角、表面色背景與 1 px 邊框。懸停和鍵盤焦點會加深邊框；停用的卡片使用淡化的表面色和淡化的文字顏色。
2. **選取光環**：一個 1.5 px 的光環，顏色為開啟態控制項色，繪製在卡片邊框之上。它屬於整個群組，而不屬於某一張卡片，因此會從一張卡片滑向下一張。
3. **指示器**：一個 18 px 的圓形，帶 1.5 px 邊框，網格卡片位於頂端末側，清單列位於起始位置。卡片被選取時，它以開啟態控制項色填滿，並在字形顏色中顯示一個 6 px 的圓點。
4. **圖示**（`Icon`）：標籤旁邊 18 px 的字形，使用次要文字顏色繪製；卡片被選取時使用前景色。`Geometry` 會自動以 18 px 包裝；其他內容則依原樣放置。
5. **標籤**（`Label`）：中等字重，放不下時以省略號截斷。
6. **說明**（`Description`）：次要顏色的超小文字，適合一到兩行短句，位於標籤下方 2 px。停用的卡片改在這裡顯示 `DisabledReason`。
7. **中繼資訊**（`Meta`）：價格或預估資訊。網格卡片中它位於底部，使用大號文字，上方留 14 px；清單列中它位於末端，使用小號中等字重，前方留 12 px。
8. **焦點環**：圍繞卡片的 2 px 環，供鍵盤焦點使用，指標焦點時隱藏。

同一列中的卡片共用最高卡片的高度，因此價格在整列中對齊。網格以至少 `MinColumnWidth`（預設為 180）的欄寬排列卡片，欄間距為 10 px。清單每列放置一張全寬卡片，列間距為 8 px。

### 繫結到檢視模型

在 MVVM 應用程式中，檢視模型負責選項與所選的值。將 `ItemsSource` 繫結到 `ObservableCollection<RadioCardOption>`，將 `SelectedValue` 繫結到所選選項的 id。選取是雙向的，選項清單則是單向的。使用值而不是選項物件，這樣儲存的選擇就是一般字串或 id。

```csharp
public partial class PlanViewModel : ObservableObject
{
    public ObservableCollection<RadioCardOption> Plans { get; } = new()
    {
        new RadioCardOption("starter", "入門版", "適合剛開始使用的個人。", "$0"),
        new RadioCardOption("pro", "專業版", "無限專案與優先支援。", "$12"),
        new RadioCardOption("team", "團隊版", "為小型團隊提供共享工作區與角色。", "$32"),
        new RadioCardOption("enterprise", "企業版", "單一登入、稽核紀錄與專屬聯絡人。", "客製化")
        {
            IsDisabled = true,
            DisabledReason = "請洽業務以開通企業版。"
        }
    };

    [ObservableProperty]
    private string? _planId = "pro";

    [RelayCommand]
    private async Task ContinueAsync()
    {
        await _subscriptions.StartAsync(PlanId);
    }
}
```

```xml
<StackPanel Spacing="16">
    <arc:RadioCards ItemsSource="{Binding Plans}"
                    SelectedValue="{Binding PlanId}"
                    AutomationProperties.Name="方案" />
    <arc:Button Content="繼續" Command="{Binding ContinueCommand}" IsDefault="True" />
</StackPanel>
```

- `SelectedValue` 是雙向的：使用者做出的選擇會把 id 寫入 `PlanId`，程式碼中設定的值會選取對應的卡片。與任何卡片都不相符的值會讓選取保持為空。
- 當可用性取決於帳號時，請在檢視模型中對選項設定 `IsDisabled`，而不是在 XAML 中設定。這樣卡片無需額外繫結即可顯示原因。
- 編譯繫結需要在檢視上設定 `x:DataType`（這裡的範例為求簡潔而省略了它）。

### 在程式碼中設定選項

同樣的選項可以在程式碼後置或服務中建立，然後指派給 `ItemsSource`，也可以直接在 XAML 中內嵌。在設定好選項後再指派 `SelectedValue`，會保留該選擇。

```csharp
var plans = new RadioCards
{
    ItemsSource = new[]
    {
        new RadioCardOption("pro", "專業版", "無限專案與優先支援。", "$12") { Icon = ArcIcons.Zap },
        new RadioCardOption("team", "團隊版", "為小型團隊提供共享工作區與角色。", "$32") { Icon = ArcIcons.Users }
    },
    SelectedValue = "pro"
};
plans.SelectionChanged += (_, _) => Console.WriteLine(plans.SelectedValue);
```

`Icon` 接受一個 `Geometry`。它會以 18px 的圖示繪製在卡片的起始位置。

### 選擇版面

`Layout="List"` 會將卡片堆疊為全寬的列，中繼資訊位於末端。較長的說明會自動換行。預設的 `Grid` 版面會填滿至少 `MinColumnWidth`（預設為 180）寬的欄。

網格適合方案和地區，使用者需要並排比較卡片，而說明正是選擇的理由。清單適合配送、付款或交付方式，每個選項是一句附有時間和價格的話，使用者需要由上往下掃視。當容器寬度小於約 370 px（兩欄 180 加上間距）時，網格會變為單欄，因此窄對話方塊仍能完整顯示每張卡片。

<Demo name="List" />

### 無法使用的選項

在選項上設定 `IsDisabled`，可讓它保持可見但無法使用。設定 `DisabledReason` 說明原因。選項停用時，該原因會取代說明。方向鍵會略過停用的選項。

```xml
<arc:RadioCards SelectedValue="pro">
    <arc:RadioCardOption Value="pro" Label="專業版" Description="無限專案與優先支援。" Meta="$12" />
    <arc:RadioCardOption Value="enterprise"
                         Label="企業版"
                         Description="單一登入、稽核紀錄與專屬聯絡窗口。"
                         Meta="客製化"
                         IsDisabled="True"
                         DisabledReason="請洽業務以開通企業版。" />
</arc:RadioCards>
```

### 必填與停用

`IsRequired` 會在表單中將該群組標示為必填。`IsEnabled="False"` 會停用所有卡片。

```xml
<arc:RadioCards IsRequired="True" IsEnabled="False" />
```

### 範例：註冊時選擇方案

一個專案管理產品 Northwind Cloud 的註冊步驟。四個方案在網格中並排比較。被選取的專業版卡片帶有光環和填滿的指示器，企業版卡片說明為何不能線上選取，主要按鈕則以所選方案繼續。每張卡片高度相同，因為該列取最高卡片的高度，所以價格在整列中對齊。

![包含四張單選卡片的方案選擇步驟，其中一張已選取，一張停用並附有原因](images/PlanCards.png)

```xml
<StackPanel Width="760" Spacing="20">
    <StackPanel Spacing="4">
        <TextBlock Text="Choose a plan for Northwind Cloud"
                   FontSize="18"
                   FontWeight="SemiBold"
                   Foreground="{arc:ArcDesignTokenResource Foreground}" />
        <TextBlock Text="You can change plans at any time from billing settings."
                   FontSize="13"
                   Foreground="{arc:ArcDesignTokenResource TextMuted}" />
    </StackPanel>
    <arc:RadioCards SelectedValue="pro" AutomationProperties.Name="Plan">
        <arc:RadioCardOption Value="starter"
                             Label="Starter"
                             Description="For individuals getting started."
                             Meta="$0"
                             Icon="{x:Static arc:ArcIcons.User}" />
        <arc:RadioCardOption Value="pro"
                             Label="Pro"
                             Description="Unlimited projects and priority support."
                             Meta="$12"
                             Icon="{x:Static arc:ArcIcons.Zap}" />
        <arc:RadioCardOption Value="team"
                             Label="Team"
                             Description="Shared workspaces and roles for small teams."
                             Meta="$32"
                             Icon="{x:Static arc:ArcIcons.Users}" />
        <arc:RadioCardOption Value="enterprise"
                             Label="Enterprise"
                             Description="SSO, audit logs and a dedicated contact."
                             Meta="Custom"
                             Icon="{x:Static arc:ArcIcons.Shield}"
                             IsDisabled="True"
                             DisabledReason="Talk to sales to enable Enterprise." />
    </arc:RadioCards>
    <StackPanel Orientation="Horizontal" Spacing="8" HorizontalAlignment="Right">
        <arc:Button Content="Back" Variant="Secondary" />
        <arc:Button Content="Continue with Pro" />
    </StackPanel>
</StackPanel>
```

### 文案規範

- **標籤**：方案、速度或地區的名稱，使用單一詞或短語，例如「Pro」「Express」「EU (Frankfurt)」。多詞標籤使用句首大寫。
- **說明**：一行文字，用平實的語言說明這個選擇給使用者帶來什麼，例如「無限專案與優先支援」「2 到 3 個工作天」。盡量控制在約 60 個字元以內，使其在網格卡片中能容納兩行。
- **中繼資訊**：價格或時間。當單位不明顯時，在說明中寫明貨幣符號和單位，例如「$12」「免費」「客製化」。數字用阿拉伯數字書寫。
- **不可用原因**：說明使用者可以採取的下一步，而不只是限制本身。「請洽業務以開通企業版。」比「無法使用」更好。

| 不要寫 | 改為 |
|---|---|
| 專業版方案（推薦） | 專業版 |
| 出貨很快 | 2 到 3 個工作天 |
| 專業版每月 12 美元 | $12 |
| 無法使用 | 請洽業務以開通企業版。 |

### 最佳做法

**建議**

- 把價格、時間或其他比較資訊放在 `Meta` 中，說明則只描述選項包含什麼。使用者會先比較中繼資訊這一欄。
- 當沒有可見的標題為群組命名時，使用 `AutomationProperties.Name` 為它命名，這樣選取時朗讀的是「方案」，而不是一串沒有名稱的卡片。
- 當某個選項是常見的選擇時，用 `SelectedValue` 設定一個預設值，並在說明中註明。
- 每張停用的卡片都使用 `DisabledReason`，讓使用者知道選項為何不能選取以及該怎麼做。

**避免**

- 在三四張卡片的網格中使用過長的說明。兩行是設計的預期；一段長文字會讓該列變高，並拖慢比較。
- 對沒有說明或價格的選項使用網格。對於簡短清單，`RadioGroup` 更容易閱讀。
- 當使用者需要知道某個選項存在時，把停用的選項從集合中移除。應保持其可見並附上原因。
- 設定 `IsRequired` 卻沒有可見的提示。該屬性會回報給輔助技術，但表單仍需要一則訊息說明必須選擇一個方案。

### 常見問題

**沒有設定 `SelectedValue` 時，選取的是什麼？** 什麼都沒有。在使用者選取卡片或你的程式碼設定 `SelectedValue` 之前，該群組沒有選取項目；此時按 Tab 會聚焦第一張可用的卡片。

**鍵盤如何在卡片之間移動？** 該群組只佔一個 Tab 停駐點。方向鍵選取並切換到下一張或上一張可用的卡片，Enter 或空白鍵選取取得焦點的卡片。方向鍵會略過停用的卡片，Home 與 End 分別選取第一張和最後一張可用的卡片。鍵盤對應表見無障礙段落。

**為什麼光環是滑動的，而不是直接跳過去？** 光環是一個形狀，它以彈簧效果在被選取卡片的位置之間滑動，視線能跟隨變化。當版面尺寸變化時，光環會立即移到新位置。

**如何顯示一張既停用又被選取的卡片？** 在同一個選項上同時設定 `IsDisabled` 和 `SelectedValue`。此時卡片會用其原因取代說明，光環也保持在該卡片上。通常更好的做法是在停用某個選項之前，先在檢視模型中修改選擇。

**卡片能容納控制項嗎，例如自訂圖示或徽章？** `Icon` 接受 `Geometry` 或任意內容，`Meta` 接受任意物件，因此徽章可以作為中繼資訊內容。內容應保持簡短，因為卡片的高度與其所在列共用。

## API 參考

<!-- api: RadioCards -->
<!-- api: RadioCardOption -->

## 無障礙

- 對輔助技術而言，每張卡片都是一個單選按鈕。當沒有可見的標題為此群組命名時，請在群組上設定 `AutomationProperties.Name`。
- 卡片標籤即卡片的名稱。說明是其說明文字。停用的卡片會改為回報其 `DisabledReason`。
- 該群組只佔一個 Tab 停駐點。`IsRequired` 會作為表單的必填項目回報給輔助技術。
- 從右至左的版面會鏡像方向鍵：Right 移到上一張卡片，Left 移到下一張。
- 鍵盤焦點會繪製一個偏移 2px 的 2px 外框。指標焦點不會繪製。
- 選取狀態以光環和圓點顯示，因此不只仰賴顏色來傳達。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 進入該群組時聚焦在選取的卡片上；若未選取任何卡片，則聚焦在第一張可用的卡片上；離開時同理。 |
| `Right` / `Down` | 選取並聚焦下一張可用的卡片。從最後一張卡片循環回到第一張。在從右至左的版面中，`Right` 向後移動。 |
| `Left` / `Up` | 選取並聚焦上一張可用的卡片。在從右至左的版面中，`Left` 向前移動。 |
| `Home` / `End` | 選取並聚焦第一張或最後一張可用的卡片。 |
| `Enter` / `Space` | 選取取得焦點的卡片。 |

## 動態效果

- 選取光環以彈簧效果從一張卡片滑向另一張，首次放置時淡入。版面尺寸變化時，它會立即移到新位置。
- 選取卡片時，圓點會從無到有地放大出現。卡片背景與邊框顏色快速變化。
- 減少動態效果時，光環與圓點立即進入最終狀態，並取消顏色過渡。關閉動態效果時效果相同。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 選取移到另一張卡片 | 光環以彈簧效果滑到該卡片 | 光環立即位於該卡片 | 光環立即位於該卡片 |
| 首次放置選取 | 光環快速淡入 | 光環立即出現 | 光環立即出現 |
| 選取一張卡片 | 指示點以靈敏的彈簧彈出 | 圓點立即出現 | 圓點立即出現 |
| 版面尺寸變化 | 光環立即移到新位置 | 光環立即移動 | 光環立即移動 |
| 懸停、按下或聚焦卡片 | 卡片背景與邊框顏色快速變化 | 立即完成 | 立即完成 |

## 相關元件

- `RadioGroup`：垂直清單中的簡短純文字選項。
- `SegmentedControl`：兩到四個需要同時切換內容的視圖。
- `Checkbox`：使用者可以選取多個選項的情境。
- `BillingToggle`：按月或按年的計費週期。

| 元件 | 適用情境 |
|---|---|
| `RadioGroup` | 選項是簡短文字，沒有價格或說明 |
| `SegmentedControl` | 選取用於切換視圖，而不是儲存為一個值 |
| `Select` / `Combobox` | 清單很長或需要篩選 |
| `BillingToggle` | 選取的是帶節省說明的計費週期 |
| `Checkbox` | 一次可以開啟多個選項 |
