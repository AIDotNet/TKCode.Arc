---
title: 篩選工具列
summary: 讓集合篩選條件保持在近處，並方便重設。
source-hash: 2797b67dcec3
---

<Demo name="Filters" />

## 何時使用

- 位於表格或清單上方的分面篩選，例如狀態、負責人或優先順序。
- 篩選、搜尋與表格協同運作的清單頁面。
- 使用者可逐一移除的篩選條件，並在設定了任一篩選時提供「全部清除」操作。

## 何時不用

- 以切換按鈕呈現的一組固定選項：用 `ChipGroup`。
- 僅有自由文字搜尋：用 `SearchField`。
- 需要接受多個值的表單欄位：用 `MultiSelect`。

## 用法

每個篩選條件都是一個 `FilterChip`，包含 `Id`、`Label` 與 `Value`。「新增篩選」選單中的欄位放入 `Fields`。每個欄位都有選項，使用者在第二步中進行選擇。

```xml
<arc:FilterToolbar Label="目前篩選"
                   ActiveFilters="{Binding Filters}"
                   MenuAlignment="Start">
    <arc:FilterToolbar.Fields>
        <arc:FilterField Id="status" Label="狀態">
            <arc:FilterField.Options>
                <arc:FilterOption Value="待處理" />
                <arc:FilterOption Value="處理中" />
                <arc:FilterOption Value="已關閉" />
            </arc:FilterField.Options>
        </arc:FilterField>
        <arc:FilterField Id="owner" Label="負責人">
            <arc:FilterField.Options>
                <arc:FilterOption Value="Mina" />
                <arc:FilterOption Value="Theo" />
            </arc:FilterField.Options>
        </arc:FilterField>
    </arc:FilterToolbar.Fields>
</arc:FilterToolbar>
```

### 以程式碼設定篩選

將目前生效的篩選建立為 `FilterChip` 清單。每個欄位只保留一個條件：新增與既有條件 `Id` 相同的條件時，會取代原有的條件。

```csharp
var toolbar = new FilterToolbar
{
    ActiveFilters = new ObservableCollection<FilterChip>
    {
        new("status", "狀態", "待處理"),
        new("owner", "負責人", "Mina")
    }
};
toolbar.AddFilter(new FilterChip("priority", "優先順序", "高"));
toolbar.RemoveFilter("owner");
toolbar.ClearFilters();
```

### 回應變更

`FiltersChanged` 會回報變更的內容（`Kind`）、涉及的條件，以及全部目前生效的篩選。用它重新載入工具列所篩選的資料。

```csharp
toolbar.FiltersChanged += (_, e) => Refilter(e.ActiveFilters);
```

### 搜尋

`IsSearchVisible` 會在工具列中新增搜尋方塊。`SearchText` 為雙向繫結，因此表格可以對其作出回應。

<Demo name="SearchAndTable" />

```xml
<arc:FilterToolbar IsSearchVisible="True"
                   SearchPlaceholder="搜尋問題"
                   SearchText="{Binding Query, Mode=TwoWay}" />
```

### 額外操作

`Actions` 可將其他控制項（例如匯出按鈕）放在「新增篩選」觸發器旁邊。

```xml
<arc:FilterToolbar>
    <arc:FilterToolbar.Actions>
        <Button Content="匯出" />
    </arc:FilterToolbar.Actions>
</arc:FilterToolbar>
```

### 沒有篩選

沒有套用中的篩選時，空狀態提示靠起始側顯示。將 `MenuAlignment` 設為 `Start` 後，選單會與觸發器的起始側對齊。

<Demo name="NoFilters" />

### 窄寬度

寬度低於 520px 時，工具列會堆疊顯示：先是條件，然後是靠末端對齊的操作。

<Demo name="Narrow" />

### 單獨使用篩選選單

`FilterMenu` 就是「新增篩選」觸發器及其兩步驟面板。在觸發器上按 Down 會開啟並停在第一個欄位，Right 進入欄位，Left 返回。

<Demo name="FilterMenu" />

## API 參考

<!-- api: FilterToolbar -->

<!-- api: FilterChip -->

<!-- api: FilterField -->

## 無障礙

- 工具列預設是一個名為「目前篩選」的群組。設定 `Label` 可為其指定適合頁面的名稱。
- 每個條件都有一個移除按鈕，名稱形如 "移除 狀態：待處理"。移除後，焦點會移到相鄰的條件，或「新增篩選」觸發器。
- 新增與移除篩選會在狀態區域中播報。
- 「新增篩選」選單是一個帶名稱的對話方塊，其選項為可勾選的選單項目。
- 按 Down 或 Up 可從觸發器開啟選單。方向鍵在清單中移動，Right 進入欄位的值，Left 返回，輸入字元可跳到對應選項。
- Escape 關閉選單，並將焦點返回到觸發器。

## 動態效果

- 條件會在自身的位置展開與收合，相鄰的條件滑動讓出空間。
- 值變更時在原位變形。設定了任一篩選時，「全部清除」即會顯示。
- 「新增篩選」觸發器會展開為選單面板，欄位與值的步驟則朝各自的方向滑入。
- 減少動態效果時，變更立即切換，並伴隨約 160 ms 的短暫淡入淡出。關閉動態效果時直接到最終狀態。

## 相關元件

- `ChipGroup`：一小組固定的切換項目。
- `SearchField`：不帶篩選的自由文字搜尋。
- `SortableDataTable`：被篩選條件縮小範圍的表格。
- `MultiSelect`：需要接受多個值的表單欄位。
