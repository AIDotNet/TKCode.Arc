---
title: Research run
summary: 從問題到帶引用報告的一次深度研究：階段步驟條、計畫、邊讀邊填滿的資料、從資料中記下的筆記，以及點擊編號引用即可開啟資料的報告。
source-hash: dafdca216e00
---

<Demo name="Preview" />

## 何時使用

`ResearchRun` 展示一個研究智慧體處理需要大量資料的問題。一個小步驟條說明它進行到哪裡（計畫、閱讀、撰寫、報告）以及用了多長時間。計畫列出智慧體為自己定下的步驟並逐一打勾。它找到的每份資料在閱讀時填滿一條進度線，被捨棄的資料會劃掉並寫明原因。發現的內容進入筆記，並帶上所依據資料的編號。報告完成後，整個過程收起成一行，報告佔據主要位置，其中的編號引用可以開啟對應資料。

- **深度研究和文獻回顧助手**：要花幾分鐘閱讀幾十個頁面。
- **盡職調查、市場掃描和客服調查**：讀者必須核對每個說法的來源。
- **展示儲存下來的研究報告**，連同資料和筆記。

## 何時不用

- **只有幾個來源的快速回答。** 請使用 `AI answer`，它在一次回覆中直接標註來源。
- **動手做事而不是閱讀的智慧體**（修改檔案、呼叫工具）。請使用 `Agent run`。
- **單純的搜尋結果清單。** 請使用 `Search results`。

## 用法

區塊衍生自 Avalonia 的 `TemplatedControl`。不設定任何屬性時，它會播放一次內建的範例執行。在應用程式中，設定問題和計畫，在智慧體找到資料時加入 `ResearchRunSource` 並更新其狀態和進度，記下筆記時加入筆記，撰寫時設定報告：

```xml
<arc:ResearchRun Question="{Binding Question}"
                 Phase="{Binding Phase}"
                 Plan="{Binding Plan}"
                 CompletedPlanSteps="{Binding PlanDone}"
                 Sources="{Binding Sources}"
                 Notes="{Binding Notes}"
                 Report="{Binding Report}" />
```

```csharp
research.StopRequested += (_, _) => agent.Cancel();
research.RestartRequested += (_, _) => StartAgain();
research.SourceInvoked += (_, e) => { OpenInReader(e.ResearchSource); e.Handled = true; };
```

### 階段與計時

`Phase` 從 `Planning` 依次到 `Reading`、`Writing` 和 `Done`（或 `Stopped`）。在計畫、閱讀或撰寫時，計時從 `Elapsed` 開始自動計數，按鈕顯示 Stop；之後顯示 Research again。階段變為 `Done` 時，過程收起成一行摘要（"Read 6 sources, took 9 notes in 2m 31s"），並帶有 Show process。

### 資料、筆記與引用

`ResearchRunSource` 是一個 `AiSource`（編號、標題、網站、位址、摘錄），另有 `Status`（排隊、閱讀中、已讀、已捨棄）和從 0 到 1 的 `Progress`。筆記是 `ResearchRunNote` 記錄：一條發現以及其資料的編號。在報告中，在某個說法後寫 `[3]` 即引用第 3 份資料；這個標記會變成一個帶編號的小按鈕，提示文字是資料的標題。按下引用或資料列會引發 `SourceInvoked`；如果沒人處理，就開啟資料的 http 或 https 位址。

<Demo name="Finished" />

## API 參考

<!-- api: ResearchRun -->

<!-- api: ResearchRunSource -->

<!-- api: ResearchRunNote -->

<!-- api: ResearchRunLabels -->

## 無障礙

- 執行是名為 "Research" 的區域；問題是標題，每個部分都有標題。
- **Tab** 可到達 Stop、資料列、引用、Show process 和 Copy。**Enter** 或 **Space** 開啟資料。
- 資料列會讀出編號、標題、網站和狀態（"Source 2: Sizing guide, retrofit-guild.example, Read"）。
- 引用讀作 "Source 3: Radiators that work at low flow temperatures"。
- 一個禮貌式即時區域會在每個階段開始和報告完成時播報。
- 狀態由文字和圖示表示，從不只靠顏色。

## 動效

- 步驟條的圓點和連線在 240ms 內變色；閱讀進度線平滑填滿。
- 過程以彈簧收起和展開，並帶短暫淡入淡出。
- 減少動效時只保留短暫淡入淡出；關閉動效時所有變化立即生效。

## 響應式

- 執行最寬 860，小於這個寬度時跟隨容器。
- 寬度在 640 或以下時，計畫和資料上下排列，內距收緊。
- 過長的標題和網站用省略號縮短；報告自動換行。

## 相關元件

- `Agent run`，用於透過工具做事的智慧體。
- `AI answer`，用於一次帶引用的回覆。
- `Agent tasks`，用於同時查看多個智慧體。
