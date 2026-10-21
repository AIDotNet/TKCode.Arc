---
title: 多選
summary: 選取多個值，同時保持欄位清晰易讀。
source-hash: 4bd44541bd8a
---

<Demo name="Chips" />

## 何時使用

- 從固定清單中選取多個值的精簡場景，例如標籤或負責人。
- 在欄位內直接顯示已選值的篩選器。
- 選項可能需要簡短搜尋的清單，可透過 `IsSearchable` 開啟。

## 何時不用

- 所有選項都需要保持可見，並能同時切換：用 `ChipGroup`。
- 使用者自行輸入並建立的自由值：用 `TagInput`。
- 只需選取一個值：用 `Select`；清單較長時用 `Combobox`。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:MultiSelect Label="標籤"
                     Description="顯示在議題卡片上。"
                     Width="320">
        <arc:SelectItem Content="錯誤" Value="bug" />
        <arc:SelectItem Content="功能" Value="feature" />
        <arc:SelectItem Content="文件" Value="docs" />
        <arc:SelectItem Content="基礎架構" Value="infra" IsEnabled="False" />
    </arc:MultiSelect>
</UserControl>
```

### 繫結選項與值

將 `ItemsSource` 設為 `SelectOption` 記錄。`SelectedValues` 會依選取的先後順序列出值。它是唯讀的，因此請在事件處理常式或程式碼中讀取。

```csharp
var labels = new MultiSelect
{
    Label = "標籤",
    Width = 320,
    ItemsSource = new[]
    {
        new SelectOption("bug", "錯誤"),
        new SelectOption("feature", "功能"),
        new SelectOption("docs", "文件"),
        new SelectOption("infra", "基礎架構", isDisabled: true),
    }
};
labels.SelectionChanged += (_, _) => Console.WriteLine(string.Join(", ", labels.SelectedValues));
```

### 標籤片與溢出

已選的值會以標籤片的形式顯示在欄位中。`MaxVisibleChips` 設定直接顯示的數量，其餘項目會摺疊為一個 `+N` 標籤片。預設值為 2。

<Demo name="OneVisibleChip" />

```xml
<arc:MultiSelect Label="負責人"
                 MaxVisibleChips="1"
                 Width="280"
                 ItemsSource="{Binding Owners}" />
```

### 可搜尋的選單

`IsSearchable` 會在選單頂端加入搜尋欄位。`SearchPlaceholder` 設定其提示文字。這是 Arc 的額外功能，Arc 原本的選單沒有搜尋欄位。

<Demo name="Searchable" />

```xml
<arc:MultiSelect Label="城市"
                 Placeholder="選擇城市"
                 SearchPlaceholder="輸入城市"
                 IsSearchable="True"
                 Width="320"
                 ItemsSource="{Binding Cities}" />
```

### 以程式碼選取

`Selection` 是選取模型。`SelectAll()` 與 `UnselectAll()` 僅供程式碼使用，因為 Arc 沒有全選控制項。

```csharp
labels.Selection.Select(0);
labels.Selection.Select(1);
labels.UnselectAll();
```

### 預留位置文字與停用狀態

尚未選取任何項目時會顯示 `Placeholder`。停用的欄位會忽略輸入，並隱藏清除按鈕。

<Demo name="Disabled" />

```xml
<arc:MultiSelect Label="審閱者" Placeholder="選擇選項" IsEnabled="False" />
```

## API 參考

<!-- api: MultiSelect -->

<!-- api: SelectOption -->

## 無障礙

- `Label` 是欄位的名稱。名稱中同時會列出已選的標籤，讓螢幕閱讀器能讀出目前的值。
- 欄位會說明它是多選清單。選單開啟時，展開狀態會對外提供。
- Enter 或 Space 可開啟與關閉選單。方向鍵可開啟選單並移動反白，反白會循環並略過停用的選項。Enter 切換反白選項的選取狀態，選單保持開啟。Escape 關閉選單。
- 標籤片對輔助技術隱藏，改由一段朗讀摘要取代。清除按鈕的名稱為「清除選取項目」。
- 焦點框繪製在觸發器上。

## 動態效果

- 標籤片的位置會以平滑的彈簧效果展開到新的寬度。每個標籤片都從模糊中逐漸放大出現。
- `+N` 計數會隨數字變化向上或向下滾動。
- 選單中的每個勾選標記都會自行繪製，選單從上方落下。
- 減少動態效果時，尺寸與縮放的變化改為短暫的交叉淡入淡出。關閉動態效果時，變化直接完成。

## 相關元件

- 所有選項都需要可見，且要切換幾個面向時用 `ChipGroup`。
- 使用者需要建立自由值時用 `TagInput`。
- 從簡短清單中選取一個值用 `Select`。
- 從使用者需要搜尋的長清單中選取一個值用 `Combobox`。
