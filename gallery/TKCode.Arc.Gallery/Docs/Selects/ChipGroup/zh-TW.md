---
title: 標籤群組
summary: 以幾組會隨選取而變形的標籤進行篩選。
source-hash: 3111842585f6
---

## 何時使用

- 使用者經常切換的少數幾個篩選面向，例如主題或類別。
- 允許清除的單選情境，使用 `IsMultiple="False"`。
- 所有選項都應保持可見，讓使用者能一眼比較各個選擇。

## 何時不用

- 空間緊湊，或欄位值必須顯示在觸發器內：用 `MultiSelect`。
- 表單中每個選項都是「是」或「否」的回答：用 `Checkbox`。
- 在幾個檢視中選擇一個：用 `SegmentedControl`。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:ChipGroup Label="主題" MaxWidth="560">
        <arc:SelectItem Content="設計" />
        <arc:SelectItem Content="工程" />
        <arc:SelectItem Content="研究" />
        <arc:SelectItem Content="行銷" />
        <arc:SelectItem Content="業務" />
    </arc:ChipGroup>
</UserControl>
```

### 繫結選項與值

將 `ItemsSource` 設為任意物件，或設為 `SelectOption` 記錄。`SelectedValues` 會依選項順序列出所選標籤的值。它是唯讀的，因此請在處理常式中讀取。

```xml
<arc:ChipGroup Label="主題"
               MaxWidth="560"
               ItemsSource="{Binding Topics}"
               SelectedItems="{Binding ChosenTopics}" />
```

```csharp
var topics = new ChipGroup
{
    Label = "主題",
    MaxWidth = 560,
    ItemsSource = new[] { "設計", "工程", "研究", "行銷" }
};
topics.Selection.Select(1);
topics.SelectionChanged += (_, _) => Console.WriteLine(string.Join(", ", topics.SelectedValues));
```

### 單選

`IsMultiple="False"` 一次只允許一個標籤。已選取的標籤仍可再次點擊以清除選擇。

```xml
<arc:ChipGroup Label="狀態" IsMultiple="False">
    <arc:SelectItem Content="待處理" Value="open" />
    <arc:SelectItem Content="處理中" Value="progress" />
    <arc:SelectItem Content="已關閉" Value="closed" />
</arc:ChipGroup>
```

### 摺疊長清單

`MaxVisibleItems` 會顯示指定數量的標籤，其餘標籤摺疊在「+N more」標籤之後。已選取的標籤即使超過上限也會保持可見。`IsExpanded` 為雙向繫結，用來顯示或隱藏摺疊的標籤。

```xml
<arc:ChipGroup Label="主題"
               MaxVisibleItems="5"
               MaxWidth="560"
               IsExpanded="{Binding ShowAllTopics, Mode=TwoWay}"
               ItemsSource="{Binding Topics}" />
```

### 在程式碼中切換

`Toggle(index)` 會切換指定索引處的標籤，效果與點擊相同。

```csharp
topics.Toggle(0);
```

## API 參考

<!-- api: ChipGroup -->

<!-- api: SelectItem -->

## 無障礙

- `Label` 是該群組的無障礙名稱。
- 每個標籤都是帶按下狀態的按鈕。螢幕閱讀器會將其播報為切換按鈕。
- 該群組採用漫遊停駐點：Tab 順序中只有一個標籤。方向鍵在標籤之間移動並循環，Home 與 End 分別前往第一個與最後一個標籤。
- Space 或 Enter 會切換取得焦點的標籤。在「+N more」標籤上，則展開或收合摺疊的標籤。
- 「+N more」標籤會回報摺疊的標籤是否已展開。焦點框會繪製在取得焦點的標籤上。

## 動態效果

- 選取的標籤會從左邊緣長出一個勾號，標籤文字向外滑開至新的寬度。
- 相鄰的標籤會滑動到新位置，包括跨行時。
- 清單展開時，摺疊的標籤會以短暫的錯開時間出現，群組高度平順地變化。
- 按下的標籤會稍微縮小，放開時以彈簧效果彈回。
- 減少動態效果時，變形改為短暫淡入淡出。關閉動態效果時，標籤直接切換。

## 相關元件

- `MultiSelect`：值必須放進精簡欄位時使用。
- `Checkbox`：表單中各自獨立的選項。
- `SegmentedControl`：從兩到四個檢視中選取一個。
- `Select`：表單中從短清單選取單一值。
