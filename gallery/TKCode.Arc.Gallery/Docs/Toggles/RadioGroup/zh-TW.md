---
title: 單選群組
summary: 從可見的一組選項中選取一項。
source-hash: 545fc82438a2
---

<Demo name="Plans" />

## 何時使用

- 兩到六個互斥的選項，且每個選項都需要一段說明，例如方案。
- 表單中隨表單一併送出的選擇。
- 垂直排列的選項，每個選項都保持可見。

## 何時不用

- 切換內容的簡短行內檢視：用 `SegmentedControl`。
- 很長的選項清單：用 `Select`。
- 需要同時選取多個選項：用 `Checkbox`。
- 選項需要在卡片上顯示價格、中繼文字或圖示：用 `RadioCards`。

## 用法

`Header` 是群組的標籤。選項即控制項的 `Items`；每個 `RadioOption` 接受一個值、一個標籤以及選用的說明。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:RadioGroup Header="方案" Width="420" SelectedValue="pro">
        <arc:RadioOption Value="hobby" Label="愛好版" Description="適合個人專案和嘗試。" />
        <arc:RadioOption Value="pro" Label="專業版" Description="適合每週都要交付的專業人士。" />
        <arc:RadioOption Value="team" Label="團隊版" Description="為持續成長的團隊提供共用工作區、角色與稽核紀錄。" />
    </arc:RadioGroup>
</UserControl>
```

### 繫結選項

將 `ItemsSource` 繫結到選項清單，將 `SelectedValue` 繫結到目前選取的值。兩者都是雙向繫結。

```xml
<arc:RadioGroup Header="方案"
                ItemsSource="{Binding Plans}"
                SelectedValue="{Binding PlanId}" />
```

```csharp
var plans = new RadioGroup
{
    Header = "方案",
    ItemsSource = new[]
    {
        new RadioOption("hobby", "愛好版", "適合個人專案和嘗試。"),
        new RadioOption("pro", "專業版", "適合每週都要交付的專業人士。")
    },
    SelectedValue = "pro"
};
plans.SelectionChanged += (_, _) => status.Text = $"已選取：{plans.SelectedValue}";
```

### 停用選項

`IsDisabled` 讓選項保持可見但無法使用。方向鍵會跳過停用的選項。

<Demo name="InlineOptions" />

### 選取事件

選取項目變更時（無論透過滑鼠還是鍵盤）會引發 `SelectionChanged`。`SelectedItem` 會傳回 `RadioOption` 本身。

```csharp
group.SelectionChanged += (_, _) =>
{
    if (group.SelectedItem is RadioOption option)
    {
        Console.WriteLine(option.Value);
    }
};
```

## API 參考

<!-- api: RadioGroup -->
<!-- api: RadioOption -->

## 無障礙

- `Header` 是群組的名稱。每個選項都是單選按鈕，其說明即為輔助文字。
- 方向鍵將選取移到下一個或上一個選項，並循環切換。停用的選項會被略過。
- Home 和 End 選取第一個和最後一個選項。空白鍵和 Enter 選取目前焦點所在的選項。
- Tab 鍵從選取的選項進入群組，並從該處離開，因此整個群組只佔一個 Tab 停駐點。
- 鍵盤焦點會繪製一個偏移 2px 的 2px 外框線。滑鼠焦點不會繪製。

## 動態效果

- 一個醒目提示區塊以形變彈簧滑動到選取的列。新的圓點以彈簧彈入，舊的圓點同時縮小消失。
- 按下單選圓圈時會輕微縮小。按下未選取的列會預覽其圓點。
- 群組尺寸變動時，醒目提示區塊立即移到新的列。
- 減少動態效果時，醒目提示區塊與圓點立即就位，並取消按下時的縮放。關閉動態效果時效果相同。

## 相關元件

- `RadioCards`：選項需要在卡片上顯示價格、中繼文字或圖示時使用。
- `SegmentedControl`：兩到五個簡短檢視，需要立即切換內容時使用。
- `Checkbox`：使用者可能選取多個選項時使用。
- `Select`：很長的選項清單。
