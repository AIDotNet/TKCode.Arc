---
title: 麵包屑
summary: 顯示頁面在階層結構中的位置。
source-hash: 68204e5bf393
---

## 何時使用

- 顯示從某個區域根部到目前頁面的路徑，例如工作區、設定、帳務。
- 桌面應用程式中的用戶端路徑，例如檔案瀏覽器的資料夾路徑。選擇某一層時在應用程式內導覽，而不是前往網址。
- 讓使用者一鍵返回上一層。

## 何時不用

- 瀏覽整棵項目樹：用 `TreeView`。
- 在同一物件的並列檢視之間切換：用 `Tabs`。
- 在同一集合的多頁之間翻頁：用 `Pagination`。

## 用法

每一層都是一個 `BreadcrumbItem`。最後一層是目前頁面：它是純文字，從不作為連結。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Breadcrumb>
        <arc:BreadcrumbItem Content="工作區" />
        <arc:BreadcrumbItem Content="設定" />
        <arc:BreadcrumbItem Content="帳務" />
    </arc:Breadcrumb>
</UserControl>
```

### 透過點按導覽

目前頁面之前的每一層都可以點按。處理 `ItemClick` 以回應所選的層級；`Index` 是它在路徑中的位置。

```csharp
breadcrumb.ItemClick += (_, e) =>
{
    status.Text = $"前往 {((BreadcrumbItem)e.Item!).Content}（索引 {e.Index}）";
};
```

### 繫結路徑

將 `ItemsSource` 設為路徑的集合。使用 `ObservableCollection<T>`，讓路徑變長或變短時層級隨之變化。

```csharp
var path = new ObservableCollection<string> { "首頁", "專案" };
breadcrumb.ItemsSource = path;
breadcrumb.ItemClick += (_, e) =>
{
    while (path.Count > e.Index + 1)
    {
        path.RemoveAt(path.Count - 1);
    }
};
```

開啟資料夾時，用 `path.Add(name)` 新增一層。選擇某一層時，其後的所有層級會被移除，被移除的層級會以動畫退出。

### 命令與導覽 URI

帶有 `Command` 的層級會以 `CommandParameter` 執行該命令，並以按鈕形式顯示。帶有 `NavigateUri` 的層級則表現為連結。

```xml
<arc:BreadcrumbItem Content="帳務"
                    Command="{Binding OpenBillingCommand}"
                    CommandParameter="{Binding BillingId}" />
```

### 標籤

`Label` 為輔助技術提供地標名稱。當麵包屑不是頁面上唯一的導覽地標時設定它。

```xml
<arc:Breadcrumb Label="資料夾路徑" />
```

## API 參考

<!-- api: Breadcrumb -->

## 無障礙

- 根節點是一個導覽地標，由 `Label` 命名。其層級以清單形式呈現，並帶有它們在路徑中的位置。
- 最後一層是帶有目前頁面狀態的文字。它從不作為連結，也無法取得焦點。
- Tab 鍵在作為連結或按鈕的層級之間移動。Enter 會跟隨目前焦點所在的層級。
- 箭頭形分隔符號對輔助技術隱藏。

## 動態效果

- 新層級從左側滑入並變清晰，同時相鄰層級以平滑彈簧讓出空間。
- 被移除的層級向左移動 4px 並快速淡出。其餘層級隨之讓出空間。
- 路徑未變化時，版面配置不做動畫。
- 減少動態效果時，新增與移除的層級僅以淡入淡出顯示，不做位移。關閉動態效果時立即套用變化。

## 相關元件

- `Tabs`：同一物件的並列檢視。
- `TreeView`：瀏覽整個階層結構。
- `Pagination`：在結果的多個頁面之間翻頁。
