---
title: 輕提示堆疊
summary: 將簡短結果堆疊在邊緣，直到你需要查看時再展開。
source-hash: 60b234c3a4b4
---

## 何時使用

- 應用程式任何位置非同步工作的結果，例如上傳失敗或邀請已寄出。
- 需要類型（成功、資訊、警告、錯誤）或操作（如復原或檢視）的通知。
- 載入訊息在工作完成後變為結果。

## 何時不用

- 頁面控制的一則本機確認：用 `Toast`。
- 需要保留在頁面上、直到使用者處理的訊息：用 `Alert`。
- 網站頂部輪播訊息的橫幅：用 `AnnouncementBar`。
- 必須先回應才能繼續的訊息：用 `Dialog`。

## 用法

將 `ToastStack` 放在內容之上，例如作為視窗根面板的最後一個子元素。在視窗中的任何控制項裡呼叫 `Toast.Show` 即可加入輕提示。如果視窗沒有 `ToastStack`，`ToastHost` 會在浮層上建立一個。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <Grid>
        <!-- 頁面內容放在這裡。 -->
        <arc:ToastStack Label="通知" />
    </Grid>
</UserControl>
```

### 顯示輕提示

`Show` 接收一個 `ToastOptions` 並傳回輕提示的 id。`Kind` 選擇圖示與顏色。`Toast.Show` 以參數形式接收相同的值，並將其送到擁有 `button` 的視窗之堆疊。

```csharp
stack.Show(new ToastOptions
{
    Kind = ToastKind.Warning,
    Title = "儲存空間即將用盡",
    Description = "已使用 10 GB 中的 92%。"
});

Toast.Show(button, "邀請已寄出", ToastKind.Success, "Maya 很快會收到一封電子郵件。");
```

### 載入與更新

在 `ToastOptions` 中傳入 id，再以相同的 id 呼叫 `Update`，即可原地修改輕提示。將 `Duration` 設為 `Timeout.InfiniteTimeSpan`，輕提示會一直保留，直到被更新或關閉。輕提示的圖示、文字與高度會變形為新狀態。

```csharp
var id = stack.Show(new ToastOptions
{
    Kind = ToastKind.Loading,
    Title = "正在上傳 report.pdf",
    Duration = Timeout.InfiniteTimeSpan
});

await UploadAsync();

stack.Update(id, new ToastUpdate
{
    Kind = ToastKind.Success,
    Title = "報告已上傳",
    Description = "已與 4 位使用者共用。"
});
```

### 操作

`ToastAction` 會為輕提示加入一個按鈕。操作執行後，除非回呼更新了輕提示，否則輕提示會關閉。復原操作可以將輕提示更新為結果，並透過 `RemoveAction` 移除按鈕。

```csharp
stack.Show(new ToastOptions
{
    Title = "專案已封存",
    Action = new ToastAction("復原", id => stack.Update(id, new ToastUpdate
    {
        Kind = ToastKind.Success,
        Title = "專案已還原",
        RemoveAction = true
    }))
});
```

### 位置與數量限制

`Position` 將堆疊移到左下、置中或右側邊緣。`VisibleToasts` 設定同時顯示的輕提示數量，`Limit` 則在超過數量時捨棄最舊的輕提示。

```xml
<arc:ToastStack Position="BottomCenter" VisibleToasts="3" Limit="12" />
```

## API 參考

<!-- api: ToastStack -->

<!-- api: ToastOptions -->

## 無障礙

- 通知區域是一個由 `Label` 命名的區域地標。鍵盤快速鍵 Alt+T 會將焦點移入堆疊並將其展開。
- Tab 在各輕提示的操作按鈕與關閉按鈕之間移動。Escape 關閉目前聚焦的輕提示。
- 新增與更新的輕提示以 `類型：標題。描述` 的形式播報。錯誤以強提示方式播報，其他類型以禮貌方式播報。
- 每個輕提示都有名稱，例如 `Error: Couldn't send invite`，螢幕閱讀器可藉此區分類型。
- 堆疊被懸停或聚焦、輕提示被拖曳以及視窗最小化時，計時器會暫停。

## 動態效果

- 新輕提示從下方升起並淡入。較早的輕提示收在最前面的輕提示後方，並略微縮小。
- 懸停或聚焦堆疊時，輕提示展開為清單；離開後再收合回去。
- 向右滑動輕提示時，它會跟隨指標移動。快速或較長的滑動會以淡出方式關閉它。
- 更新時，圖示、文字與高度會原地變形。工作進行時，載入圓環持續轉動。
- 減少動態效果時，位置直接跳轉，輕提示只做淡入淡出。關閉動態效果時，每次變化立即顯示。

## 相關元件

- `Toast`：由頁面透過 `IsOpen` 控制的一則本機確認。
- `Alert`：頁面內持續存在的訊息。
- `AnnouncementBar`：帶倒數計時的輪播橫幅。
