---
title: 變形選擇
summary: 觸發器展開為清單的選取器，帶有滑動反白與鍵入搜尋。
source-hash: 10da2b25f10e
---

<Demo name="GroupedWithSearch" />

## 何時使用

- 表單與屬性面板中的固定清單，例如時區、負責人或專案。
- 帶圖示或簡短中繼資料的分組選項，使用 `SelectItem.Group`、`Icon` 與 `Meta` 呈現。
- 工具列中的精簡膠囊形觸發器，點選後展開為完整清單。

## 何時不用

- 自由文字，或來自遠端資料來源的長清單：用 `Combobox`。
- 需要選取多個值：用 `MultiSelect`。
- 執行動作而非選取值：用 `DropdownMenu`。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:MorphSelect Label="狀態" SelectedIndex="1">
        <arc:SelectItem Content="待辦" />
        <arc:SelectItem Content="進行中" />
        <arc:SelectItem Content="待審核" />
        <arc:SelectItem Content="已完成" />
        <arc:SelectItem Content="已取消" IsEnabled="False" />
    </arc:MorphSelect>
</UserControl>
```

### 分組、圖示與中繼資料

為每個選項設定 `Group`、`Icon` 與 `Meta`。每一組選項的上方會插入一個分組標題。`Icon` 接受 Lucide 幾何圖形或圖片。`Meta` 以等寬數字樣式顯示。

```csharp
var zone = new MorphSelect
{
    Label = "時區",
    SelectedValue = "London",
    ItemsSource = cities.Select(c => new SelectOption(c.City, c.City)
    {
        Group = c.Region,
        Meta = c.Utc,
        Keywords = c.Zone,
        Icon = ArcIcons.Globe
    }).ToArray()
};
zone.SelectionChanged += (_, _) => Console.WriteLine(zone.SelectedValue);
```

### 精簡工具列

使用 `IsLabelVisible="False"` 隱藏標籤。觸發器仍以 `Label` 作為無障礙名稱，因此膠囊形狀保持精簡。

<Demo name="CompactToolbar" />

```xml
<arc:MorphSelect Label="狀態" IsLabelVisible="False" />
<arc:MorphSelect Label="優先順序" PanelAlignment="End" PanelWidth="220"
                 ItemsSource="{Binding Priorities}" />
```

### 搜尋

`SearchMode` 決定選單是否帶有搜尋欄位。預設值 `Auto` 在選項超過八個時加入搜尋欄位。`On` 則一律加入，`SearchPlaceholder` 設定其提示文字。在關閉狀態的觸發器上鍵入字母，會跳到相符的選項；選單展開後由搜尋欄位接手輸入。

```xml
<arc:MorphSelect Label="負責人"
                 SearchMode="On"
                 SearchPlaceholder="尋找人員"
                 ItemsSource="{Binding Owners}" />
```

### 面板尺寸

`PanelWidth` 設定選單寬度（預設 272）。`MaxListHeight` 限制其高度，超出時清單可捲動。

## API 參考

<!-- api: MorphSelect -->

<!-- api: SelectItem -->

## 無障礙

- `Label` 為觸發器命名，即使 `IsLabelVisible` 為 `False` 也是如此。
- 觸發器是帶有清單方塊彈出層的組合方塊。選單展開時會公開展開狀態，並播報目前反白的選項。
- 方向鍵移動反白並略過停用的選項。Page Up 與 Page Down 每次移動八個選項。Home 與 End 分別移到第一個與最後一個選項。
- Enter 或 Space 選定，Alt 加 Up 選定並關閉，Escape 關閉選單並將焦點返回觸發器。
- 選項分組以其標題命名。搜尋結果的數量會以禮貌方式播報。

## 動態效果

- 觸發器展開為清單，其圓角從膠囊形狀變形為面板形狀。
- 反白在選項之間滑動。
- 選定的標籤從清單飛回觸發器，觸發器寬度隨之調整為新標籤的寬度。
- 減少動態效果時，尺寸直接跳轉，並省去飛行與捲動，僅保留短暫淡入。關閉動態效果時直接切換。

## 相關元件

- `Select`：帶固定清單的一般標籤選取欄位。
- `Combobox`：使用者鍵入後可搜尋的單選。
- `MultiSelect`：需要選取多個值時使用。
- `DropdownMenu`：選單用於執行動作時使用。
