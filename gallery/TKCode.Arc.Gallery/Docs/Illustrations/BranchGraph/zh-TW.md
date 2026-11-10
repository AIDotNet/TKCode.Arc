---
title: Branch graph
summary: 逐一提交繪製的 git 風格歷史；分支分出到各自的軌道再合併回來，附有分支與標籤名稱，選取某個提交時會顯示它的歷史。
source-hash: 841346a92975
---

<Demo name="History" />

## 何時使用

`BranchGraph` 像 git 工具那樣繪製版本歷史：每個提交占一列，每個分支有自己帶顏色的軌道，分支分出與合併回來的地方各畫一條曲線，合併提交畫成圓環，
每個分支的最新提交與每個標籤都有一個膠囊標籤。以故事播放時，它會逐一提交地繪製歷史（連線畫出，圓點彈出，分支標籤往上移動），同時由說明文字講出每個提交
做了什麼：第一個提交、分支上的提交、新的分支、帶有兩個父提交的合併。選取一個提交時，它的所有祖先都會亮起，其餘部分變暗，一眼就能回答
「這個修正有沒有包含在那個版本裡？」。

- **關於版本控制的說明文件與課程**：以圖呈現分支、合併與發行分支，而不只是用文字說明。
- **版本說明與變更記錄**：一個版本是如何形成的，以及它的標籤。
- **開發者工具**：放在提取要求或部署旁的精簡歷史。

## 何時不用

- **很長的歷史**：每個提交都占一列，而且沒有虛擬化；請控制在幾十個提交以內。完整的儲存庫請使用 `Table` 或專門的記錄檢視。
- **差異與檔案變更**：圖只顯示歷史的形狀，不顯示改了什麼。
- **同一個頁面上有多張卡片**：每張卡片在播放時都會產生動畫。只需要靜態圖的卡片請設定 `StartsFinished`。

## 用法

卡片不需要資料：它播放一段虛構的歷史，包含 `main`、一個功能分支與一個緊急修正分支上的九個提交，其中有兩次合併與一個發行標籤。

```xml
<arc:BranchGraph />
```

`BranchGraph` 衍生自 `Explainer`，此基底提供故事時鐘（`IsPlaying`、`Speed`、`IsLooping`、`Play()`、`Pause()`、`StepForward()`、`Restart()`）、
進入畫面時開始播放的行為、播放按鈕與禮貌的即時區域；此控制項則加上速度控制項與圖。

### 你自己的歷史

依從舊到新的順序加入 `BranchGraphCommit` 項目。`Branch` 選擇軌道（預設 `main`）。未設定 `Parents` 時，提交的父提交是同一分支上的前一個提交；
如果是分支的第一個提交，則是任一分支上的前一個提交。設定 `Parents` 可以從別處分出分支，或者給出兩個 id（分支自己的放在前面）表示合併。
軌道依分支出現的順序分配，分支合併後它的軌道會被重新使用。

<Demo name="PlainGraph" />

```xml
<arc:BranchGraph Title="Release branch" StartsFinished="True" IsNewestFirst="True" AreControlsVisible="False">
    <arc:BranchGraphCommit Id="5e1a20" Branch="main" Message="Start the 2.0 cycle" Author="Rhea" />
    <arc:BranchGraphCommit Id="6f2b31" Branch="release/2.0" Message="Freeze features for 2.0" Author="Rhea" />
    <arc:BranchGraphCommit Id="7a3c42" Branch="main" Message="Add dark mode settings" Author="Sam" />
    <arc:BranchGraphCommit Id="8b4d53" Branch="release/2.0" Message="Fix crash on startup" Author="Rhea" Tag="v2.0.0" />
    <arc:BranchGraphCommit Id="9c5e64" Branch="main" Message="Merge release/2.0" Parents="7a3c42, 8b4d53" Author="Sam" />
</arc:BranchGraph>
```

`StartsFinished` 讓卡片開啟時顯示完整歷史（播放仍會重播），`IsNewestFirst` 像 `git log` 一樣把最新的提交放在最上面，`ItemsSource` 會從檢視模型取得提交。
`RevealedCount` 回報故事進行到哪裡，每個提交繪製時都會引發 `CommitDrawn`，並附上它的 `Kind`：`Commit`、`Branch` 或 `Merge`。

### 選取

按一下某個提交，或在圖取得焦點時按方向鍵，會設定 `SelectedCommit`（雙向）。該提交所在的列會被醒目提示，它的所有祖先保持亮起，其餘部分變暗。
再按一下它或按 Escape 可以清除選取。

<Demo name="Selection" />

`Labels` 包含卡片顯示或朗讀的所有文字；替換它即可翻譯或改寫。

### 減弱動效

在減弱動效下，卡片開啟時直接顯示完整歷史。播放與 Next step 仍可使用，每個提交會在原處直接出現。

<Demo name="ReducedMotion" />

## API 參考

<!-- api: BranchGraph -->

<!-- api: BranchGraphCommit -->

<!-- api: BranchGraphLabels -->

<!-- api: BranchGraphCommitEventArgs -->

`BranchGraphSamples.CreateCommits()` 會傳回一份新的範例歷史副本。

## 無障礙

- 卡片是以 `Title` 命名的群組（"Branch history"）。狀態列說明 Ready、Drawing、Paused 或 Up to date。
- 圖是一個 Tab 停駐點。上下鍵在提交之間移動選取，Home 與 End 跳到第一列與最後一列，Escape 清除選取。它的協助工具名稱是選取的提交
  （"Merge feature/search, 2c8d0e on main by Mira, v1.2.0"），沒有選取時則是已繪製的最新提交。
- 只有讀者主動推進故事（單步、暫停、重新開始）時，即時區域才會讀出說明文字。
- 軌道除了顏色，也靠位置區分；合併提交靠圓環區分；分支名稱寫在膠囊標籤裡。

## 動效

| 變化 | 完整 | 減弱 | 關閉 |
|---|---|---|---|
| 連向新提交的連線 | 在 0.5 s 內畫出 | 直接出現 | 直接出現 |
| 提交圓點 | 彈出，略有回彈 | 直接出現 | 直接出現 |
| 列文字與膠囊標籤 | 淡入 | 直接出現 | 直接出現 |
| 說明文字 | 交叉淡入淡出 | 原處更換 | 原處更換 |

在速度 1 下，每個提交需要 1.6 秒。所有動畫都依循故事時鐘，因此會跟著暫停、單步，並依 `Speed` 縮放。

## 響應式

- 每條軌道占 18 px；提交訊息占據該列剩下的寬度，過長時以省略號截斷。
- 寬度達到 480 px 時，作者與短 id 顯示在每列右側；更窄時則不顯示（螢幕報讀程式仍會讀出）。
- 卡片最寬 760 px；窄於 560 px 時內距收緊，窄於 420 px 時會隱藏 "Speed" 文字。
- 輕觸會選取一個提交，與按一下的效果相同。

## 相關元件

| 元件 | 何時選擇 |
|---|---|
| `Timeline` | 依順序排列的事件，沒有分支 |
| `ReasoningTree` | 故事是一次會分支與剪枝的搜尋 |
| `AgentTimeline` | 多個代理隨時間工作 |
