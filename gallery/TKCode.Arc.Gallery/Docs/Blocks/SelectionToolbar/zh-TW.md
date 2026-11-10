---
title: 選取工具列
summary: 選取清單或表格中的項目時從底部升起的浮動列：顯示數量、全選、批次操作和清除選取。
source-hash: ad3c171efa01
---

<Demo name="Preview" />

## 何時使用

`SelectionToolbar` 包住一個清單或表格，並為其中選取的內容加上一條浮動列。把清單放進 `Content`，並讓 `SelectedCount` 與它的選取保持一致。只要有內容被選取，就會有一條膠囊形的列從底部邊緣升起，顯示數量、用於選取其餘項目的「Select all」、批次操作和清除按鈕；選取清空時它會落下消失。具有破壞性的操作使用危險色並放在最後。

- **檔案清單和表格**：人們勾選多列後一次封存、移動或刪除。
- **收件匣和工單佇列**：許多項目要設定相同的標籤、負責人或狀態。
- **管理後台的表格**：選取之後進行匯出或批次修改。

## 何時不用

- **對單個項目的單個操作**：把操作放在列上，或放進 `ContextMenu`。
- **與選取無關的操作**：使用頁面標題中的工具列（`PageHeader`）。
- **很長的操作清單**：列上保持四五個操作，其餘放進頁面上的選單。

## 用法

```csharp
toolbar.TotalCount = files.Count;
list.SelectionChanged += (_, _) => toolbar.SelectedCount = list.SelectedItems.Count;
toolbar.SelectAllRequested += (_, _) => list.SelectAll();
toolbar.Cleared += (_, _) => list.UnselectAll();
toolbar.ActionInvoked += (_, e) => Run(e.Action.Key, e.SelectedCount);
```

清除按鈕和 Esc 會把 `SelectedCount` 設為 0 並引發 `Cleared`，在那裡取消列的選取。每個操作也可以帶一個 `Command`，它會收到選取的數量。沒有 `Actions` 時，列上提供 Archive、Move、Share 和 Delete。

<Demo name="CustomActions" />

## API 參考

<!-- api: SelectionToolbar -->

## 無障礙

- 這條列是名為「Bulk actions」的工具列。Tab 可以到達它；左右方向鍵在按鈕之間移動，Home 和 End 跳到兩端。
- 在控制項內任何位置按 Esc 都會清除選取；如果焦點原本在列上，會回到清單。
- 數量每次變化都會被禮貌地讀出。清除按鈕名為「Clear selection」。

## 動效

- 列在淡入時升起 16 px，淡出時落下。數量增加時數字向上捲動，減少時向下捲動。
- 減少動效時只保留淡入淡出；關閉動效時列立即出現和消失。

## 響應式

- 列在底部置中，兩側各留 16 px，下方留 18 px。在最後一列下方留出空間（清單底部的內距），免得列把它擋住。
- 寬度小於 `CompactWidth`（560 px）時，操作只保留圖示，並以名稱作為提示。

## 相關元件

- `FloatingButtonGroup`：與選取無關的浮動工具組。
- `Checkbox` 用於列的選取，`DataGrid` 用於大型表格。
- `InboxSidebar`：它的郵件清單常與這條列一起使用。
