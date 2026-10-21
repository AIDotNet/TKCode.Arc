---
title: 分頁
summary: 在有明確邊界的長集合中翻頁。
source-hash: bbd3f447c7fd
---

<Demo name="Default" />

## 何時使用

- 瀏覽伺服器端結果或長表格，頁碼保存在狀態或網址中。
- 顯示使用者在固定頁數中的位置，並在兩端停用上一頁與下一頁按鈕。
- 與資料表格或清單共用頁數，頁碼變化時重新載入。

## 何時不用

- 瀏覽項目的投影片：用 `Carousel`。
- 隨捲動閱讀長串資訊流：用有高度限制的 `Timeline`，而不是頁碼按鈕。
- 跳到階層結構中的某個位置：用 `Breadcrumb`。

## 用法

繫結 `CurrentPage` 與 `PageCount`。控制項會呈現一個包含五個頁碼按鈕的視窗，視窗隨目前頁移動。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Pagination PageCount="{Binding PageCount}"
                    CurrentPage="{Binding CurrentPage, Mode=TwoWay}" />
</UserControl>
```

### 回應頁碼變化

處理 `PageChanged` 以載入頁面。`NewPage` 是使用者要求的頁碼。

```csharp
pagination.PageChanged += async (_, e) =>
{
    status.Text = $"第 {e.NewPage} 頁，共 {pagination.PageCount} 頁";
    await LoadPageAsync(e.NewPage);
};
```

### 邊界頁

`ShowBoundaryPages` 讓首頁與末頁保持一次點按即可到達，並在它們與視窗之間顯示省略號。

<Demo name="BoundaryPages" />

### 頁數很少或沒有頁

頁數少於五頁時全部顯示；兩端的箭頭會被停用。`PageCount` 為 `0` 時呈現一個空的導覽地標。

<Demo name="FewPages" />

<Demo name="NoPages" />

### 標籤

`Label` 為導覽地標命名。`PreviousLabel`、`NextLabel` 與 `PageLabelFormat` 為輔助技術提供按鈕名稱。當預設文字不是使用者所用的語言時進行設定。

```xml
<arc:Pagination Label="結果分頁"
                PreviousLabel="上一頁"
                NextLabel="下一頁" />
```

## API 參考

<!-- api: Pagination -->

## 無障礙

- 控制項是一個導覽地標，由 `Label` 命名。
- 每個頁碼按鈕由 `PageLabelFormat` 命名，例如「第 3 頁」。目前頁具有目前頁狀態。
- 上一頁與下一頁按鈕在兩端會被停用，但保留其名稱。
- Tab 鍵在上一頁按鈕、頁碼按鈕與下一頁按鈕之間移動。Enter 或空白鍵會進入目前焦點所在的頁碼。左右方向鍵在按鈕之間移動焦點。

## 動態效果

- 單一目前頁標記以 morph 彈簧滑動到所選按鈕。
- 視窗移動時，數字依移動的格數像傳送帶一樣滑動，新數字淡入。
- 減少動態效果時，標記立即移動，數字原位替換。關閉動態效果時立即套用變化。

## 相關元件

- `Tabs`：用於切換檢視，而不是同一集合的頁面。
- `Breadcrumb`：用於顯示頁面在階層結構中的位置。
- `Carousel`：用於逐張瀏覽投影片。
