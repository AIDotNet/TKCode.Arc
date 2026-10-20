---
title: 展開式搜尋
summary: 圖示展開為搜尋欄位，並在下方顯示結果。
source-hash: 9ba7b7424543
---

## 何時使用

- 頁首或工具列空間有限，放不下完整搜尋欄位的情境。
- 從結果清單中跳轉到文件、專案或頁面。
- 使用者尚未輸入任何內容時，顯示最近的搜尋。

## 何時不用

- 篩選頁面上已有的清單：用 `SearchField`。
- 為表單選擇一個值：用 `Combobox`。
- 涵蓋整個應用程式的全域指令搜尋：用指令面板，本函式庫不提供。

## 用法

此控制項初始為圓形圖示按鈕。啟動後會展開搜尋欄位，並顯示結果或建議。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:ExpandingSearch Label="搜尋專案與文件"
                         MaxResults="6"
                         ExpandedWidth="360" />
</UserControl>
```

### 項目與建議

將 `ItemsSource` 填入 `ExpandingSearchItem` 物件。每個項目都有 `Id` 和 `Title`，另可設定 `Meta`、`Group` 與 `Keywords`。結果會與標題和關鍵字比對，並依群組列出。`Suggestions` 會在使用者輸入任何內容之前顯示。

```csharp
var items = new List<ExpandingSearchItem>
{
    new() { Id = "q3", Title = "季度報告", Meta = "文件 · 2 小時前編輯", Group = "文件" },
    new() { Id = "atlas", Title = "Atlas重新設計", Meta = "專案 · 12 個待辦事項", Group = "專案" },
    new() { Id = "billing", Title = "帳單遷移", Group = "專案", Keywords = ["invoices", "payments"] }
};

header.ItemsSource = items;
header.Suggestions = new[] { items[0], items[1] };
header.SuggestionsLabel = "最近";
```

### 選取

選取結果或建議後，搜尋欄位會先收合，然後觸發 `ItemSelected`。若希望改為以選取的項目執行指令，請繫結 `SelectCommand`。

```csharp
header.ItemSelected += (_, e) => Open(e.Item.Id);
```

```xml
<arc:ExpandingSearch Label="搜尋"
                     ItemsSource="{Binding Items}"
                     SelectCommand="{Binding OpenCommand}" />
```

### 錨點與寬度

`Anchor` 設定按鈕所在的邊緣。欄位會從該邊緣向外展開，最寬至 `ExpandedWidth`。

```xml
<arc:ExpandingSearch Label="尋找儀表板"
                     Anchor="Start"
                     ExpandedWidth="300" />
```

### 在程式碼中開啟與關閉

`Expand` 會開啟搜尋欄位並將焦點移入其中。`Collapse` 會將其收回為圖示，並清除查詢內容。

```csharp
search.Expand();
search.Collapse();
```

`IsExpanded` 是雙向的，因此也可以將它繫結到檢視模型。

## API 參考

<!-- api: ExpandingSearch -->

<!-- api: ExpandingSearchItem -->

## 無障礙

- `Label` 為按鈕、搜尋欄位與結果清單命名。
- 結果以依標題分組的清單呈現。結果數量與空白狀態會以禮貌的方式通知。
- 在按鈕上按 Enter 或 Space 可開啟搜尋欄位。ArrowDown 與 ArrowUp 在結果間移動，並會循環。
- Enter 選取反白的結果，Escape 將搜尋欄位收回為圖示。
- 收合的搜尋欄位不接收焦點，也不參與點擊測試，因此不會擋住下方的內容。

## 動態效果

- 展開時寬度變形，圓形圖示在同一彈簧上變為圓角欄位。收合較平滑，時間也較短。
- 結果面板跟隨搜尋欄位的寬度，高度平滑展開並淡入。
- 反白在結果之間滑動，並保持上一次移動的速度。
- 減少動態效果時，狀態切換改為淡入淡出，不做寬度或位置移動。關閉動態效果時立即切換。

## 相關元件

- `SearchField`：用於篩選頁面上已有內容的欄位。
- `Combobox`：用於選擇表單中儲存的值。
- `Input`：一般用途的單行輸入框。
