---
title: 移動端下拉重新整理
summary: 在可捲動內容的頂部下拉即可重新整理，帶橡皮筋阻尼、載入圈和彈簧回彈。
source-hash: 6f5a4c7796c7
---

<Demo name="Basic" />

## 何時使用

`PullToRefresh` 包住捲動的清單或頁面，讓使用者用早已熟悉的手勢要求最新資料。

- **資訊流和收件匣**：訊息、時間軸和訂單清單，內容在伺服器上會變。
- **任何有重新載入操作的清單**：用按鈕會顯得雜亂的時候。

即時範例在手機外框裡執行，那是用滑鼠驅動的桌面模擬；真機上的觸控表現，以及它與平台自帶過度捲動的配合，這裡沒有驗證。範例用計時器結束重新整理，就像一次請求那樣。

## 何時不用

- **不能捲動、也不需要重新載入的內容**：沒有可拉的東西。
- **應該自動到達的更新**：在背景重新整理並展示結果。
- **橫向清單**：下拉只在縱向。

## 用法

把捲動內容放進去。內容在頂部時向下拉，內容會帶著阻尼跟手，箭頭隨下拉接近 `PullThreshold`（預設 64 px）而翻轉。越過閾值放開會觸發 `RefreshRequested`；提前放開，內容彈回。

```xml
<arcm:PullToRefresh Command="{Binding ReloadCommand}">
    <ScrollViewer>
        <!-- the list -->
    </ScrollViewer>
</arcm:PullToRefresh>
```

重新整理會帶著載入圈一直進行，直到 `IsRefreshing` 再次為 false。可以繫結 `IsRefreshing`（它是雙向的），也可以從事件資料裡取一個延遲（deferral），工作完成時釋放它。

```csharp
refresher.RefreshRequested += async (_, e) =>
{
    using var deferral = e.GetDeferral();
    await LoadAsync();
};
```

也可以在程式碼裡設定 `IsRefreshing`，不經下拉直接顯示載入圈，此時不會觸發 `RefreshRequested`。

### 穿透

手勢開始時內部的 `ScrollViewer` 已經捲動過、向上，或主要是橫向，就交給內容處理，所以捲動長清單不會和下拉打架。重新整理進行時不能再下拉。

### Token

`PullToRefreshToken` 保存幾何：`IndicatorExtent`（56，也是重新整理時內容停靠的位置）、`ArrowSize`（20）、`SpinnerSize`（22）和 `SpinnerThickness`（2）。顏色來自 ArcDesign token。

## API 參考

<!-- api: MobilePullToRefresh -->

## 無障礙

重新整理是一種手勢，所以在要緊的地方要給出另一種重新載入的辦法（選單項目或按鈕）。`RefreshRequested` 和 `Command` 在放開時執行，按住時不執行。

## 動態效果

內容按橡皮筋曲線跟手：起初跟得上，越往下阻力越大。放開時，手指的速度交給彈簧，內容平滑地停在指示器上或回到頂部。新的下拉能在內容飛行中接住它。下拉達到閾值時箭頭翻轉，重新整理時由載入圈取代。減弱動態效果時，內容不隨手指拉伸；重新整理照常進行，內容直接跳轉。

## 相關元件

- `MobileListGroup`：通常的內容。
- `MobilePage`：承載被重新整理的畫面。
- `MobileNavbar`：它上方的列。
