---
title: Scroll story
summary: 隨頁面滚動講述的故事：文字步驟依次經過閱讀線，旁邊固定在視野中的舞台隨之切換到當前步驟的畫面。
source-hash: b1a8c4d387d0
---

<Demo name="Preview" />

## 何時使用

`ScrollStory` 在讀者滚動時講解一個流程。文字步驟排在一列，每一步占一個較高的位置；另一列的舞台始終留在視野中：區塊經過窗口時，舞台保持在可見部分的中間，並在區塊的頂部和底部停住。越過閱讀線（窗口中間略偏上）的步驟成為當前步驟：它的文字完全顯示，其他步驟變暗，舞台交叉淡化到這一步的畫面。畫面是內建的小應用窗口繪圖（收件箱、看板、審閱中的文檔、上升的圖表），也可以換成你自己的圖片。

- **登陸頁的「工作方式」區**，三到六個步驟。
- **產品導覽**，每個功能配一張畫面。
- **講究閱讀順序的故事頁**。

## 何時不用

- **需要並排比較的內容**：請使用 `FeatureBento` 或 `SpotlightGrid`。
- **不會滚動的頁面**：只有祖先元素滚動時舞台才會移動。
- **長文章**：請使用 `Markdown`；這裡的步驟位置是有意做得很高的。

## 用法

```xml
<arc:ScrollStory Title="From request to release">
    <arc:ScrollStoryStep Kicker="Capture" Title="Every request lands in one inbox" Scene="Capture" Body="Forms, email and chat feed the same list." />
    <arc:ScrollStoryStep Kicker="Launch" Title="Ship it and watch it land" Scene="Launch" Body="Release and follow adoption." />
</arc:ScrollStory>
```

把區塊放在會滚動的頁面中（任何祖先 `ScrollViewer`）。沒有步驟時它講一個虛構的四步故事；把 `ShowsSampleSteps` 設為 false 則保持為空。步驟的 `Scene` 選擇內建畫面（`Capture`、`Arrange`、`Review`、`Launch`），`Image` 可替換它。`Kicker` 預設是步驟編號。

`ActiveIndex` 報告舞台上的步驟，變化時觸發 `ActiveStepChanged`。`ScrollToStep(index)` 把某一步滚動到視野中。

## API 參考

<!-- api: ScrollStory -->

## 無障礙

- 區塊以標題命名，標題是二級標題。
- 每一步都可聚焦，以標題命名，正文作為幫助文字。聚焦某一步會把它滚動到視野中並設為當前步驟，所以舞台也跟隨鍵盤。
- 舞台以當前步驟命名；畫面本身僅作裝飾。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 下一步或上一步。 |
| `Down` / `Up` | 下一步或上一步。 |
| `Home` / `End` | 第一步或最後一步。 |

## 動效

| 變化 | 完整 | 減弱 | 關閉 |
|---|---|---|---|
| 舞台跟隨滚動 | 隨頁面移動並固定在視野中（自身沒有動畫） | 相同 | 相同 |
| 畫面切換 | 240 毫秒交叉淡化，新畫面上升 10 像素 | 交叉淡化 | 立即 |
| 步驟變暗和變亮 | 240 毫秒透明度變化 | 160 毫秒 | 立即 |

固定效果直接跟隨滚動位置：檢視區變化時只更新渲染變換，不會觸發版面配置。

## 響應式

- 區塊自身寬度小於 760（`:compact`）時，舞台隱藏，步驟排成一列，每一步在文字下方顯示自己 200 高的畫面，並且全部清晰顯示。
- 寬螢幕上兩列平分寬度，間隔 56，每一步的位置至少 380 高，步驟逐一經過閱讀線。

## 相關元件

- `FeatureBento` 和 `SpotlightGrid`：一起展示的功能。
- `Stepper`：應用中由用戶逐步完成的流程。
