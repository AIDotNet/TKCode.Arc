---
title: 下拉選擇
summary: 精簡的選擇欄位，選單支援鍵盤操作。
source-hash: 8f3c194c2ee3
---

## 何時使用

- 清單簡短且固定，例如地區、排序方式或方案，使用者只需選擇一個值，無須輸入。
- 表單中有標籤的欄位，觸發器下方可選擇性顯示說明文字。
- 清單夠短，便於瀏覽。選單可以捲動，但較長的清單更適合搜尋。

## 何時不用

- 較長或需要搜尋的清單：用 `Combobox`。
- 需要一次選取多個值：用 `MultiSelect`。
- 只有兩到四個選項且需要持續可見：用 `SegmentedControl`。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Select Label="地區"
                Description="工作區資料的儲存位置。"
                Width="280">
        <arc:SelectItem Content="北美" Value="na" />
        <arc:SelectItem Content="歐洲" Value="eu" />
        <arc:SelectItem Content="南美（即將推出）" Value="sa" IsEnabled="False" />
        <arc:SelectItem Content="非洲" Value="af" />
    </arc:Select>
</UserControl>
```

### 繫結選項與值

將 `ItemsSource` 設為任意物件或 `SelectOption` 記錄。`SelectedValue` 為雙向繫結，所選的值會寫入繫結的屬性。

```xml
<arc:Select Label="排序方式"
            Width="220"
            ItemsSource="{Binding SortOrders}"
            SelectedValue="{Binding SortKey, Mode=TwoWay}" />
```

```csharp
var sort = new Select
{
    Label = "排序方式",
    Width = 220,
    ItemsSource = new[]
    {
        new SelectOption("newest", "最新優先"),
        new SelectOption("oldest", "最舊優先"),
        new SelectOption("az", "名稱，A 到 Z"),
    },
    SelectedValue = "newest"
};
sort.SelectionChanged += (_, _) => Console.WriteLine(sort.SelectedValue);
```

### 自訂標籤與值

對於不是 `SelectOption` 的資料項目，請設定 `TextSelector` 與 `ValueSelector`。與 `DisplayMemberBinding` 不同，它們對 AOT 友善。

```csharp
var zones = new Select
{
    Label = "時區",
    Placeholder = "選擇時區",
    Width = 300,
    ItemsSource = cities,
    TextSelector = item => item is City c ? $"{c.Name} ({c.Offset})" : null,
    ValueSelector = item => item is City c ? c.Id : null
};
```

### 預留位置文字與說明文字

尚未選取任何項目時會顯示 `Placeholder`。`Description` 是觸發器下方的淡色提示，同時也是無障礙的輔助文字。

```xml
<arc:Select Label="方案" Placeholder="選擇方案" Description="之後可以隨時變更。" />
```

### 長清單與停用狀態

選單的高度上限為 `MaxDropDownHeight`（預設 320），超出的部分可以捲動。只會繪製可見的列，因此包含數千個項目的清單仍能保持流暢回應。停用的 `Select` 會忽略輸入。

```xml
<arc:Select Label="建置編號" ItemsSource="{Binding Builds}" MaxDropDownHeight="240" />
<arc:Select Label="方案" IsEnabled="False" SelectedIndex="1">
    <arc:SelectItem Content="免費版" />
    <arc:SelectItem Content="專業版" />
</arc:Select>
```

## API 參考

<!-- api: Select -->

<!-- api: SelectItem -->

## 無障礙

- `Label` 是欄位的名稱，`Description` 是其說明文字。
- 觸發器會取得焦點。Enter、Space 與方向鍵可開啟選單。方向鍵、Home、End、PageUp 與 PageDown 用於移動反白，並略過停用的選項。Enter 或 Space 進行選取，Escape 關閉選單且不變更選取內容，Tab 則關閉選單。
- 輸入字母會跳到下一個以該字母開頭的選項。欄位處於關閉狀態時，會直接選取該選項。
- 所選的值以純文字形式提供給輔助技術。帶動畫的副本會被隱藏。
- 選單開啟時，焦點框仍保留在觸發器上。

## 動態效果

- 顯示的值會沿清單方向滾動：後面的選項從下方升起，前面的選項從上方落下。
- 選單以短暫的縮放與淡入淡出從觸發器展開。關閉時立即完成。
- 反白會從一個選項平滑滑到下一個。
- 減少動態效果時，值以瞬間交叉淡入淡出的方式切換，選單則淡入淡出。關閉動態效果時，值與選單直接切換，不含任何過場。

## 相關元件

- 清單較長或需要使用者搜尋時用 `Combobox`。
- 需要同時選取多個值時用 `MultiSelect`。
- 觸發器需要展開為清單，且選項需要圖示或分組時用 `MorphSelect`。
- 兩到四個選項需要持續可見時用 `SegmentedControl`。
