---
title: 單選卡片
summary: 可選取的選項卡片，帶有滑動的選取光環、價格與說明插槽，並具有單選按鈕的鍵盤行為。
source-hash: 39ff78d5f30d
---

## 何時使用

- 使用者做決定前需要說明、價格或預估資訊的選項，例如方案、配送速度或地區。
- 無法選取時，需要透過 `DisabledReason` 顯示原因的選項。
- 從簡短清單中必須做出的一個選擇。

## 何時不用

- 簡短的純文字選項：使用 `RadioGroup`。
- 兩到四個視圖，需要同時切換內容：使用 `SegmentedControl`。
- 長清單：使用 `Select`。

## 用法

卡片即此控制項的 `Items`。每個 `RadioCardOption` 接受一個值、一個標籤、一段說明，以及一段中繼資訊文字，中繼資訊會顯示在卡片末端。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:RadioCards SelectedValue="pro">
        <arc:RadioCardOption Value="starter" Label="入門版" Description="適合剛開始使用的個人。" Meta="$0" />
        <arc:RadioCardOption Value="pro" Label="專業版" Description="無限專案與優先支援。" Meta="$12" />
        <arc:RadioCardOption Value="team" Label="團隊版" Description="為小型團隊提供共享工作區與角色。" Meta="$32" />
    </arc:RadioCards>
</UserControl>
```

### 繫結選項

將 `ItemsSource` 繫結到選項清單，並將 `SelectedValue` 繫結到所選的值。兩者皆為雙向。

```xml
<arc:RadioCards ItemsSource="{Binding Plans}"
                SelectedValue="{Binding PlanId}"
                AutomationProperties.Name="方案" />
```

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

`Icon` 接受一個 `Geometry`。它會以 18px 的圖示繪製在卡片開頭。

### 清單版面

`Layout="List"` 會將卡片堆疊為全寬的列，中繼資訊位於末端。較長的說明會自動換行。預設的 `Grid` 版面會填滿至少 `MinColumnWidth` 寬的欄（預設為 180）。

```xml
<arc:RadioCards Layout="List" SelectedValue="standard">
    <arc:RadioCardOption Value="standard" Label="標準配送" Description="4 至 6 個工作天" Meta="免費" />
    <arc:RadioCardOption Value="express" Label="快速配送" Description="2 至 3 個工作天" Meta="$9.00" />
    <arc:RadioCardOption Value="overnight" Label="隔日達" Description="下一個工作天中午前送達" Meta="$24.00" />
</arc:RadioCards>
```

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

## API 參考

<!-- api: RadioCards -->
<!-- api: RadioCardOption -->

## 無障礙

- 對輔助技術而言，每張卡片都是一個單選按鈕。當沒有可見的標題為此群組命名時，請在群組上設定 `AutomationProperties.Name`。
- 卡片標籤即卡片的名稱。說明是其說明文字。停用的卡片會改為回報其 `DisabledReason`。
- Tab 會將焦點移至選取的卡片，若沒有選取項目則移至第一張可用的卡片。方向鍵移動選取並會循環，Home 與 End 分別選取第一張與最後一張卡片。Enter 與空白鍵選取取得焦點的卡片。從右至左的版面會鏡像方向鍵。
- 鍵盤焦點會繪製 2px 的外框，並與控制項保持 2px 的間距。指標焦點則不顯示。
- 選取狀態以光環與圓點顯示，因此不只仰賴顏色來傳達。

## 動態效果

- 選取光環以彈簧效果從一張卡片滑到另一張。第一次放置時淡入。版面尺寸變化時，光環會立即移到新位置。
- 選取卡片時，圓點會從無到有地放大出現。卡片背景與邊框顏色快速變化。
- 減少動態效果時，光環與圓點立即進入最終狀態，並取消顏色過渡。關閉動態效果時效果相同。

## 相關元件

- `RadioGroup`：垂直清單中的簡短純文字選項。
- `SegmentedControl`：兩到四個需要同時切換內容的視圖。
- `Checkbox`：使用者可以選取多個選項的情境。
- `BillingToggle`：按月或按年的計費週期。
