---
title: 分段控制項
summary: 在一組相關的檢視之間切換。
source-hash: 32fa7950664f
---

<Demo name="ViewSwitcher" />

## 何時使用

- 同一內容的兩到五個簡短檢視，例如日、週、月。
- 立即生效的版面配置或模式切換。
- 標籤夠簡短、一眼即可讀懂的一排選項。

## 何時不用

- 選項需要說明或垂直版面配置：用 `RadioGroup`。
- 選項需要價格、中繼文字或圖示：用 `RadioCards`。
- 單一的開關設定：用 `Switch`。
- 選項很多的選擇：用 `Select`。

## 用法

每個 `SegmentedOption` 接受一個值和一個標籤。控制項透過值 `SelectedValue` 選取對應的分段。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:SegmentedControl AutomationProperties.Name="範圍" SelectedValue="week">
        <arc:SegmentedOption Value="day" Label="日" />
        <arc:SegmentedOption Value="week" Label="週" />
        <arc:SegmentedOption Value="month" Label="月" />
    </arc:SegmentedControl>
</UserControl>
```

### 從程式碼設定項目

將 `ItemsSource` 繫結到選項清單。控制項也接受純字串，`SelectedIndex` 可依位置選取分段。

```csharp
var sections = new SegmentedControl
{
    MaxWidth = 300,
    ItemsSource = new[] { "總覽", "活動", "部署", "分析", "記錄", "設定" },
    SelectedIndex = 0
};
```

### 附加內容

`Accessory` 是顯示在標籤之後的內容，例如數量。

<Demo name="AccessoryAndIntent" />

```xml
<arc:SegmentedControl SelectedValue="all">
    <arc:SegmentedOption Value="all" Label="全部" />
    <arc:SegmentedOption Value="unread" Label="未讀">
        <arc:SegmentedOption.Accessory>
            <TextBlock Text="3" />
        </arc:SegmentedOption.Accessory>
    </arc:SegmentedOption>
    <arc:SegmentedOption Value="archived" Label="已封存" IsDisabled="True" />
</arc:SegmentedControl>
```

### 繫結

`SelectedValue` 是雙向的。將它繫結到保存目前檢視的檢視模型屬性。

```xml
<arc:SegmentedControl ItemsSource="{Binding Ranges}"
                      SelectedValue="{Binding SelectedRange}"
                      AutomationProperties.Name="範圍" />
```

### 意圖事件

當滑鼠指標或焦點到達某個分段時，會在該分段被選取之前引發 `ItemIntent`。可用來做預覽，例如顯示提示。不要用它來切換檢視。

```csharp
segmented.ItemIntent += (_, e) => hint.Text = $"預覽：{e.Value}";
segmented.SelectionChanged += (_, _) => ShowView(segmented.SelectedValue);
```

### 溢出

當選項比控制項更寬時，選項列會在控制項內部捲動，有更多內容的一側會顯示淡出的邊緣。

<Demo name="Overflow" />

## API 參考

<!-- api: SegmentedControl -->
<!-- api: SegmentedOption -->

## 無障礙

- `AutomationProperties.Name` 為群組命名。對於沒有可見標題的控制項，每個控制項都應設定它。
- 每個分段都是一個按鈕，會回報其按下狀態。滑動的藥丸形醒目提示對輔助技術隱藏。
- Tab 鍵從選取的分段進入控制項；若未選取任何分段，則從第一個分段進入。再按一次 Tab 鍵即離開。
- 方向鍵選取並聚焦下一個或上一個分段，並循環切換。Home 和 End 選取第一個和最後一個分段。
- 鍵盤焦點會在分段內部繪製一個 2px 的外框線，因此捲動區域不會裁切它。滑鼠焦點不會繪製。

## 動態效果

- 一個藥丸形醒目提示以形變彈簧滑動到選取的分段下方。醒目提示位於標籤之下，並隨標籤一起捲動。
- 分段本身不會縮放或改變字重，只有醒目提示移動，標籤顏色快速變化。
- 分段溢出時，該列會以短促的緩動捲動，以保持選取的分段可見。
- 減少動態效果時，醒目提示立即移動，並取消顏色過渡。關閉動態效果時效果相同。

## 相關元件

- `RadioGroup`：選項需要說明或垂直版面配置時使用。
- `BillingToggle`：用於按月或按年計費週期切換，並附帶節省說明。
- `Switch`：立即生效的單一設定。
