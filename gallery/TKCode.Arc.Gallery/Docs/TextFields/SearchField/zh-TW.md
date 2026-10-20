---
title: 搜尋欄位
summary: 易於辨識的搜尋入口，並帶有清楚的操作提示。
source-hash: 88552bbca4f4
---

## 何時使用

- 篩選頁面上已有的清單、表格或工具列。
- 帶清除按鈕、可用鍵盤操作的搜尋入口。
- 搜尋結果與搜尋欄位顯示在同一個檢視中的查詢。

## 何時不用

- 點選後開啟結果面板的精簡型頁首搜尋：使用 `ExpandingSearch`。
- 搜尋值需要儲存在表單中：使用 `Combobox`。
- 並排放置的多個篩選條件與排序選項：將搜尋欄位與其他控制項一起放在工具列中。

## 用法

搜尋欄位開頭有一個搜尋圖示。欄位中有文字時，結尾會出現清除按鈕。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:SearchField Label="搜尋人員"
                     Placeholder="依姓名篩選"
                     Width="360"
                     HorizontalAlignment="Left" />
</UserControl>
```

### 邊輸入邊篩選

每次按鍵時 `Text` 都會變動。在 `TextChanged` 處理常式中篩選清單。如果篩選成本較高，請在程式碼中自行做防彈跳處理。

```csharp
search.TextChanged += (_, _) =>
{
    var query = search.Text ?? string.Empty;
    list.Children.Clear();
    foreach (var name in names.Where(n => n.Contains(query, StringComparison.CurrentCultureIgnoreCase)))
    {
        list.Children.Add(new TextBlock { Text = name });
    }
};
```

### 繫結查詢字詞

`Text` 是雙向屬性，因此檢視模型可以讀取或重設查詢字詞。

```xml
<arc:SearchField Label="搜尋"
                 Placeholder="依姓名篩選"
                 Text="{Binding Query}" />
```

### 提交

按 Enter 會觸發 `SearchSubmitted`，並帶出欄位中的文字。按 Escape 會清除欄位。

```csharp
search.SearchSubmitted += (_, e) => submitted.Text = $"已提交“{e.Query}”";
```

在程式碼中呼叫 `Clear` 可清除欄位。除非傳入 `false`，焦點會保留在欄位中。

```csharp
search.Clear();
```

### 尺寸

`Size` 設定外框高度：`Small` 為 36，`Medium`（預設）為 44，`Large` 為 50。

```xml
<arc:SearchField Label="小" Size="Small" Placeholder="搜尋" />
<arc:SearchField Label="大" Size="Large" Text="路線圖" />
```

## API 參考

<!-- api: SearchField -->

## 無障礙

- `Label` 為搜尋欄位命名。搜尋圖示對輔助技術隱藏。
- 清除按鈕預設名稱為「清除搜尋」。設定 `ClearText` 可將其翻譯。
- 按 Enter 提交查詢字詞，按 Escape 從鍵盤清除欄位。
- 清除按鈕可用鍵盤操作，取得焦點時顯示焦點框。

## 動態效果

- 清除按鈕從略小的尺寸放大出現，並伴隨輕微的模糊。
- 其位置一律保留，因此無論按鈕是否可見，欄位寬度都保持不變。
- 按下清除按鈕時，按鈕會略微縮小。
- 減少動態效果時，清除按鈕只做淡入淡出，不縮放也不模糊。關閉動態效果時立即顯示或隱藏。

## 相關元件

- `ExpandingSearch`：頁首中的精簡搜尋，並在其下方顯示結果。
- `Combobox`：從清單中選擇一個值。
- `Input`：一般用途的單行輸入框。
