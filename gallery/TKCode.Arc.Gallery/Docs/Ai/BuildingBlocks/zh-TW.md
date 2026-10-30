---
title: Building blocks
summary: 為 AI 應用程式產生器準備的載入畫面：圓角積木無回彈地拼進一個小小的應用程式版面，填上內容並染上強調色，標籤則從「正在配置版面」一步步走到「完成」。
source-hash: 99c1dbd6aadf
---

<Demo name="Preview" />

## 何時使用

`BuildingBlocks` 在 AI 產生應用程式時，呈現應用程式正被組裝起來的過程。導覽列、主卡片、圖表、頭像列、設定列和按鈕這些圓角積木
飛入，無回彈地吸附進一個小小的應用程式版面。它們先以著色的預留位置落定，接著填上逼真的內容，再於樣式階段染上主題色；狀態列則
依序顯示 Laying out、Adding components、Styling 和 Ready。

- **生成式 UI 產品**：需要一個載入狀態，呈現應用程式、頁面或儀表板正在被搭建。
- **將回傳介面的助理回覆**：在程式碼或版面串流抵達期間顯示。
- **空狀態或導覽步驟**：一眼說明 AI 產生器在做什麼。

## 何時不用

- **純文字回答**：改用閃爍的標籤或思考指示。
- **影像生成**：直接逐步顯示影像本身。
- **步驟與日誌都已知的真實進度**：使用 `AgentStatus` 或 `AgentPlan`，讓人看到實際發生了什麼。
- **與頁面真實版面一致的預留位置**：使用 `Skeleton`。

## 用法

把請求狀態對應到 `Status`：請求進行中設為 `Building`，結果抵達設為 `Done`，第一次提示之前設為 `Idle`。建置途中收到的
`Done` 會快轉剩下的階段並停在 Ready。不設定 `Status` 時播放內建的模擬建置，它在 Ready 上短暫停留後循環。

```csharp
blocks.Status = BuildingBlocksStatus.Building;
var result = await GenerateAsync(prompt);
blocks.Status = BuildingBlocksStatus.Done;
```

### 由應用程式驅動

<Demo name="HostDriven" />

`Duration` 是一次建置的時長，從第一塊積木到樣式結束，平均分配給三個階段（預設 4.8 秒，最少 2.4 秒）。把它設得接近你通常的
產生時間。`Status` 為 `Building` 且 `IsLooping` 開啟時，較長的產生過程會不斷重新組裝而不是停住；關閉 `IsLooping` 則停在
Styling。

`AllowsReplay` 在 Ready 時顯示 Replay 按鈕。按下會觸發 `ReplayRequested`：重新開始產生，並把 `Status` 設回 `Building`。
`PhaseChanged` 回報狀態列顯示的每個階段，可用於分析或讓其他介面保持同步，`Phase` 保存目前的階段。

### 結構

1. **框架**：鋪在柔和底色上的迷你應用程式，每塊積木都有一個虛線槽位。
2. **積木**：導覽列、帶商品照片的主卡片、圖表、頭像列、帶開關的設定列和按鈕。
3. **狀態列**：標記（建置時是四個小方塊，Ready 時是勾號）、階段標籤和 Replay。它的高度固定，換標籤或出現 Replay 都不會
   移動任何東西。

### 尺寸

<Demo name="Sizes" />

`Size` 決定框架能長到的寬度：`Small` 280px、`Medium` 400px（預設）和 `Large` 560px。`StageWidth` 可以改用任意像素寬度。
框架永遠不會超出容器，版面、圓角和描邊都隨寬度縮放，因此在 240px 到 880px 之間都維持比例。窄於 320px 時狀態列使用 12px
字級。

### 色調與著色

<Demo name="Tones" />

`Tone` 決定預留位置、按鈕、圖表、開關和狀態標記的顏色：`Accent`（預設）或使用前景色的 `Neutral`。`Tint` 可以改用任意顏色，
適合品牌色或依產品著色。

### 只播放一次

<Demo name="PlayOnce" />

沒有狀態時，`IsLooping="False"` 讓模擬建置只播放一次，停在 Ready 並提供 Replay，按下會重新建置。

### 不顯示狀態列

<Demo name="NoLabel" />

周圍介面已經說明正在發生什麼時，用 `IsLabelVisible="False"` 隱藏狀態列。

### 自訂文字

`Labels` 保存所有文字。可以寫明正在建置的東西，或進行翻譯：

```csharp
blocks.Labels = new BuildingBlocksLabels
{
    Ready = "Your dashboard is ready",
    Replay = "Build again",
};
```

### 減少動態效果

<Demo name="ReducedMotion" />

減少動態效果時，預留位置、內容和樣式都以單純的交叉淡化出現，沒有位移，建置只播放一次。關閉動態效果時每一步都立即切換。階段仍會
推進：它們是資訊，而不是裝飾。

### 內容建議

- 在 Ready 標籤裡寫明結果（「Your CRM is ready」），而不是只重複「Ready」。
- 模擬建置留給展示和空狀態；真實產品裡請由請求驅動 `Status`。
- 不要假裝回報進度：階段只是節奏，不是量測。

### 最佳做法

- 把 `Duration` 設得接近你通常的產生時間，讓 Ready 與結果同時到來。
- 每個待回傳的結果用一個執行個體；頁面上要顯示很多個時優先使用 `Small`。
- 把它放在結果將出現的位置（回覆泡泡、畫布、空狀態），讓版面自然交接給結果。

### 常見問題

**結果提前抵達怎麼辦？** 把 `Status` 設為 `Done`：所有積木立即落定、填充並著色，轉場很短，標籤顯示 Ready。

**捲出畫面時還在執行嗎？** 不會。框架被捲走、隱藏或位於隱藏的視窗時，排程和狀態標記都會暫停，回來後從原處繼續。

## API 參考

<!-- api: BuildingBlocks -->

<!-- api: BuildingBlocksLabels -->

<!-- api: BuildingBlocksPhaseChangedEventArgs -->

## 無障礙

- 框架是裝飾性的，對輔助科技隱藏；階段透過禮貌（polite）的即時區域朗讀，螢幕閱讀器會聽到 Laying out、Adding components、
  Styling 和 Ready。
- 模擬建置會說明自己是模擬的（說明文字「Simulated build, nothing was generated」），循環播放時不會在每一輪都朗讀標籤。
- Ready 同時顯示勾號和標籤，狀態從不只依賴顏色。
- Replay 是帶可見文字的 Arc `Button`。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| Tab | Ready 時出現 Replay 後可聚焦到它；框架本身不取得焦點 |
| Enter、Space | 按下 Replay |

## 動態效果

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 積木抵達 | 從槽位外側稍小的位置飛入，0.52 秒臨界阻尼彈簧（無過衝），間隔 40ms，全部在第一塊之後 200ms 內出發 | 原地淡入 240ms | 立即 |
| 預留位置填充 | 320ms 內轉為中性內容，間隔 60ms；長條從左緣長出，圖表折線在 600ms 內畫出 | 240ms 交叉淡化 | 立即 |
| 樣式階段 | 著色、照片和頭像在 400ms 內淡入，間隔 50ms；開關滑動一次 | 240ms 交叉淡化；開關直接換邊，不滑動 | 立即 |
| 階段標籤 | 舊標籤 100ms 淡出，新標籤 160ms 淡入並上升 2px，行位置固定 | 120ms 交叉淡化 | 立即 |
| Replay | Ready 時淡入 | 淡入 | 立即 |
| 狀態標記 | 一道光依序繞過四個小方塊，每塊 300ms | 每個階段點亮一塊 | 每個階段點亮一塊 |
| 循環 | 完成的版面原地在 320ms 內淡出，在看不見時重設，再於同樣的虛線槽位上重新建置 | 只播放一次 | 只播放一次 |

每次變化都從畫面上的目前值開始，所以建置途中改變狀態也不會生硬切換。框架離開畫面或被隱藏時，排程和標記都會暫停。

## 相關元件

- `AgentStatus`：呈現含真實步驟的代理程式執行狀態。
- `AgentPlan`：代理程式遵循的計畫。
- `Skeleton`：與頁面真實版面一致的預留位置。
