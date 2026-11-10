---
title: 回到頂部
summary: 長清單、日誌或動態消息捲動超過門檻後出現的圓形按鈕，按下後平滑捲回頂部。
source-hash: e9f4c68d5bfc
---

<Demo name="Preview" />

## 何時使用

`BackToTop` 監看一個捲動容器，讀者向下捲動超過 `Threshold`（預設 200 px）後顯示一個小圓形按鈕。按下它，內容平滑捲回頂部；
往上的途中按鈕會再次淡出。

- **長清單與動態消息**：讀者翻得很深，又想回到開頭，例如搜尋結果、貼文、通知。
- **日誌與訊息串流**：內容在閱讀時仍持續增加。
- **很長的設定頁或文件頁**：放在同一個捲動區域裡。

## 何時不用

- **內容很短**，一兩個畫面就能看完：按鈕沒有意義。（內容無法捲動時它本來也保持隱藏。）
- **跳到各個章節**：用目錄或錨點，而不是一個「回到頂部」按鈕。
- **聊天對話**：應該固定在底部最新的訊息，捲動方向相反。

## 用法

有兩種用法。一種是自己把 `BackToTop` 放在捲動容器上方，並把 `Target` 指向它；另一種是在容器上設定
`BackToTop.IsAttached`，由程式庫把按鈕放在檢視區的一角。

`Target` 可以是 `ScrollViewer`（`ScrollArea` 就是一種），也可以是範本裡含有 `ScrollViewer` 的控制項（`Timeline`、`Table`、
`TreeView`，或由應用程式佈景主題提供捲動範本的 `ListBox`）：控制項本身是 `ScrollViewer` 時直接使用它，否則在範本套用後使用其中
第一個 `ScrollViewer`。只看垂直位移：內容可以捲動且捲動超過 `Threshold` 時顯示按鈕；內容變短或檢視區變高時隱藏。

### 放在自己的容器上

把按鈕放在與容器相同的 `Grid` 儲存格裡、排在容器之後，用一般的對齊方式和 `Margin` 定位。按鈕不在內容裡面，所以不會隨內容捲動。

```xml
<Grid xmlns="https://github.com/avaloniaui"
      xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
      xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <ScrollViewer x:Name="Log">
        <StackPanel />
    </ScrollViewer>
    <arc:BackToTop Target="{Binding #Log}"
                   HorizontalAlignment="Right"
                   VerticalAlignment="Bottom"
                   Margin="16" />
</Grid>
```

上方的 Preview 就是這樣處理一份請求日誌。平滑捲動到達頂部時，按鈕觸發 `ScrolledToTop`，範例用它更新說明文字；
`ScrollToTop()` 與按下按鈕效果相同。

### 附加到容器

在容器上寫 `arc:BackToTop.IsAttached="True"`，按鈕會自動加上。它放在容器的裝飾層（AdornerLayer）裡，內容捲動時保持不動，
也不改變容器的版面配置。`BackToTop.Placement` 選擇檢視區的哪一角（預設 `BottomRight`），`BackToTop.Inset` 設定與邊緣的
距離（16 px）。把 `IsAttached` 改回 false，或把容器從視窗移除，按鈕就會移除。這裡把它附加到 `Timeline` 上，`Timeline` 的
`ScrollViewer` 在範本裡。

<Demo name="Attached" />

### 門檻與位置

`Threshold`（或容器上的 `BackToTop.Threshold`）是內容需要捲動多遠按鈕才出現；設為 0 時，內容一離開頂部就顯示。
選一個不會遮住內容本身操作的角落。

<Demo name="ThresholdAndPlacement" />

### 尺寸與名稱

`Size` 讓圓形直徑為 36（`Small`）、44（`Medium`，預設）或 50（`Large`）像素。`Label` 取代內建的名稱與提示「回到頂部」，
否則內建文字跟隨應用程式的語言。

```xml
<arc:BackToTop xmlns:arc="https://github.com/AIDotNet/TKCode.Arc"
               Size="Small"
               Label="Back to the newest entries" />
```

## API 參考

<!-- api: BackToTop -->

## 協助工具

- 按鈕的協助工具名稱與提示是 `Label`，或在地化的「回到頂部」。
- 與其他按鈕一樣，Enter 與 Space 觸發；焦點環只在鍵盤聚焦時顯示。
- 隱藏時按鈕不接收指標輸入，也不在 Tab 順序中。隱藏時若持有焦點，焦點保持原處。
- 捲回頂部後焦點仍在按鈕上，因此在讀者再次向下捲動之前，再按一次不會有任何作用。

## 動態效果

- 出現與隱藏：完整動態效果下按鈕淡入、從 0.86 放大到原尺寸並上升 8 px（snappy 彈簧），隱藏時反向；減少動態效果只淡入淡出。
  第一個畫面不播放動畫。
- 捲回頂部：完整動態效果下位移從目前位置以 480 ms（in-out）緩動到頂部；減少動態效果與關閉動態效果下直接跳到頂部。捲動途中讀者
  用滾輪、捲軸或鍵盤捲動時，動畫立即停止，位移留在讀者設定的位置，也不會觸發 `ScrolledToTop`。

## 響應式

- 按鈕跟隨它監看的檢視區尺寸：視窗變高、內容不再需要捲動時，按鈕隱藏。
- 容器尺寸改變時，附加的按鈕保持在檢視區的同一角。
- 窄螢幕上優先用 `Size="Small"` 和較小的 `Inset`，讓按鈕少遮住內容。

## 相關元件

- `ScrollArea`：具備覆蓋式捲軸與邊緣淡出的捲動容器，可以作為目標。
- `CyclicPanel`：無限循環的清單，沒有可以回去的頂部。
