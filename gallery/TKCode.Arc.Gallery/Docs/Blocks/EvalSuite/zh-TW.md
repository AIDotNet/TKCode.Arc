---
title: 評測套件
summary: 用於提示詞或模型的評測套件：測試案例在基準版本和候選版本上執行，顯示兩者的通過率和相差的百分點，每個案例標為通過、失敗、退步或修復，可以只看退步的案例，打開一個案例可以並排查看兩個輸出，或查看逐詞差異。
source-hash: bbec12b9b38e
---

<Demo name="Preview" />

## 何時使用

評測套件在提示詞或模型的改動上線之前回答一個問題：新版本是否比現在使用的版本更好，又弄壞了什麼？它是一張卡片。頂部選擇要比較的兩個版本：基準版本（現在在用的）和候選版本（這次的改動），然後執行套件。三個方塊給出兩個版本各自的通過率以及相差的百分點。下面每個測試案例佔一行，顯示它的狀態、輸入、檢查方式，以及每個版本的結果。一個案例只會有四種變化：兩邊都通過、兩邊都失敗、**退步**（原來通過，現在失敗）或**修復**（原來失敗，現在通過）。篩選膠囊可以直接跳到退步的案例；打開一個案例會顯示兩個輸出以及檢查給出的原因，可以並排查看，也可以看成一份逐詞差異。執行時所有案例先排隊，每次執行幾個，每個案例一有結果就翻成結果；執行可以隨時停止。

- **審查提示詞改動**：更短的系統提示詞、新的語氣或新的指令，在上線前用重要的案例檢查一遍。
- **比較模型**：同一組案例分別在現有模型和更便宜或更新的模型上執行。
- **發布前的關口**：有沒有退步一眼就能看到，每一個退步都可以打開閱讀和討論。
- **保留歷史**：較早版本的結果留在 `Results` 中，切換基準版本就能看到以前的比較，不必再執行。

## 何時不用

- **對開放式回答做兩兩投票**，沒有固定檢查：請使用 `ModelArena`。
- **編輯並試用單個提示詞**：請使用 `PromptStudio`；需要在很多案例上檢查改動時再來這裡。
- **即時監控線上流量**：隨時間變化的圖表屬於儀表板；套件是一組固定的案例。
- **每個案例有幾百個指標**：每行只顯示一個狀態和每個版本的一個結果。寬的數字結果請使用 `Table` 或 `DataGrid`。

## 用法

`EvalSuite` 衍生自 Avalonia 的 `TemplatedControl`。不設定任何屬性時，它顯示一個虛構的範例套件：一家虛構的共享單車的十四個客服回覆案例，三個提示詞版本及其結果，比較 v12 和 v13，**Run suite** 後面是模擬的執行。

```xml
<arc:EvalSuite Title="Support replies"
               Cases="{Binding Cases}"
               Versions="{Binding Versions}"
               Results="{Binding Results, Mode=TwoWay}"
               BaselineVersionId="v12"
               CandidateVersionId="v13" />
```

### 結構

- **頂部**：標題；一行文字說明案例數量和執行進行到哪裡；**Run suite** 按鈕（執行時變為 **Stop**）；以及兩個版本選擇，中間有一個從基準指向候選的箭頭。
- **進度**：執行時顯示一條細的進度條。
- **方塊**：基準和候選的通過率，下面是「10 of 14 passed」；以及相差的百分點，帶一個箭頭和「2 regressions · 3 fixed」。相差的值是兩個取整後通過率的差，所以三個數字總是對得上。
- **篩選**：All、Regressions、Fixed 和 Failing（候選失敗：退步加上兩邊都失敗的案例），每個都帶數量。
- **案例列表**：狀態標籤、案例標題和下面的輸入、檢查方式（窄時隱藏），以及每個版本的結果：勾號加「Pass」、叉號加「Fail」，或警告加「Error」；評分案例還會加上分數。
- **打開的案例**：標題和狀態、輸入和檢查，以及兩個輸出各自的結果和原因；或者從基準輸出到候選輸出的一份逐詞差異。

### 案例、檢查與結果

一個 `EvalCase` 有 `Id`、簡短的 `Title`、`Input`、`Expected` 文字和一個 `Check`：

| 檢查 | 通過條件 |
|---|---|
| `Exact` | 去掉首尾空白後的輸出等於 `Expected` |
| `Contains` | 輸出包含 `Expected`，不區分大小寫 |
| `Regex` | 輸出符合 `Expected` 中的模式，不區分大小寫 |
| `Rubric` | 評分器給出的 `Score`（0 到 1）達到 `Threshold`（預設 0.7）；`Expected` 描述評分標準 |

一個 `EvalResult` 屬於一個案例和一個版本（`CaseId`、`VersionId`），包含 `Output`、`Passed`、可選的 `Score`、一行 `Detail`（原因）和 `Duration`。`Results` 可以包含任意多個版本的結果；套件從中取出要比較的兩個。`EvalSuite.Grade(case, versionId, output, score)` 會套用案例的檢查，並替你寫好原因。

### 執行

**Run suite**（或 `RunAsync()`）先刪除這兩個版本在這些案例上的舊結果，把所有案例排隊，每次執行 `MaxConcurrency` 個案例（預設 2 個），每個案例同時在兩個版本上執行。一行先顯示 Queued，然後是帶旋轉圖示的 Running，兩個結果都到達後顯示它的狀態；數量、方塊和篩選都隨每個案例更新。每個案例完成後 `Results` 會換成一個新清單，所以雙向繫結可以邊執行邊儲存。**Stop**（或 `Stop()`）會取消正在執行的案例；已完成的案例保留結果，其餘顯示「Not run」。

設定 `RunCaseAsync` 可以用你自己的程式碼執行案例。傳回評過分的結果；拋出的例外會變成帶有其訊息的「Error」結果；執行停止或套件離開視窗時，權杖會被取消。

```csharp
suite.RunCaseAsync = async (evalCase, version, cancellation) =>
{
    var reply = await client.CompleteAsync(version.Id, evalCase.Input, cancellation);
    var score = evalCase.Check == EvalCheckKind.Rubric
        ? await grader.ScoreAsync(evalCase.Expected, reply, cancellation)
        : (double?)null;
    return EvalSuite.Grade(evalCase, version.Id, reply, score);
};
```

<Demo name="CustomRunner" />

`RunStarted`、`CaseCompleted`（帶兩個結果和狀態）和 `RunCompleted`（帶 `Summary`、是否被停止以及用時）回報一次執行。`Phase`、`FinishedCount`、`RunTotal` 和 `Summary` 可以讀取或繫結。

### 退步與打開的案例

選擇一個篩選膠囊，或設定 `Filter`。點擊一行，或在行上按 Enter，即可在列表下方打開該案例（`SelectedCaseId`）；再點一次、按 Escape 或按關閉按鈕即可關閉。打開一個案例時，方向鍵會移到下一個案例並打開它。`DetailMode` 在 **Side by side** 和 **Word diff** 之間切換；差異視圖把新增的詞標為綠色、刪除的詞以紅色刪除線標出，每段都帶有符號，因此不只依靠顏色。

<Demo name="Regressions" />

### 文案

所有文字都來自 `Labels`（`EvalSuiteLabels`）。以 `Format` 結尾的項目在 `{0}`、`{1}` … 中接收已格式化的值，因此可以調整詞序。`CultureName` 決定數字的格式。

```csharp
suite.Labels = new EvalSuiteLabels
{
    Run = "執行",
    Stop = "停止",
    PassedOfFormat = "{1} 個中通過 {0} 個",
};
```

### 內容建議

- **標題說明案例檢查什麼**，而不是它問什麼：「Greets by name」、「Quotes the day pass」。
- **優先使用仍然公平的最嚴格檢查**：事實用 `Contains`，格式用 `Regex`，只有語氣或長度這類特點才用 `Rubric`。
- **原因要簡短**：原因只佔一句（「Missing “30 days”」、「Scored 0.62, needs 0.70」）。
- **版本名要短**，把區別寫在備註裡：「v13 · Shorter prompt」。

## API 參考

<!-- api: EvalSuite -->

<!-- api: EvalCase -->

<!-- api: EvalVersion -->

<!-- api: EvalResult -->

<!-- api: EvalSummary -->

<!-- api: EvalSuiteLabels -->

### 事件

| 事件 | 觸發時機 | 參數 |
|---|---|---|
| `RunStarted` | 一次執行開始 | `Baseline`、`Candidate`、`Cases` |
| `CaseCompleted` | 一個案例有了兩個結果 | `Case`、`Baseline`、`Candidate`、`Status` |
| `RunCompleted` | 一次執行完成或被停止 | `Summary`、`WasStopped`、`Finished`、`Elapsed` |

## 無障礙

- 卡片是一個以標題命名的區域。每個方塊有一段無障礙文字（「v13: 79% pass rate, 11 of 14 cases」；「Change: +8 points. 2 regressions · 3 fixed」）；滾動的數字本身對螢幕閱讀器隱藏。
- 案例列表只佔一個 Tab 位置。它的名稱是鍵盤游標所在的行（「2 of 14: Greets by name, Regression. Baseline Pass, candidate Fail」），說明文字說明顯示了多少個案例，因此移動游標就會讀出每一行。
- 狀態和結果從不只依靠顏色：每一種都有圖示（退步是向下的箭頭，修復是向上的箭頭，以及勾號、叉號、警告）和一個詞。
- 副標題是一個禮貌的即時區域；另有一個隱藏的即時區域在執行開始和結束時播報（「Run complete. v13 passed 11 of 14, +8 points. 2 regressions.」），或說明執行在哪裡停止。
- 版本選擇、篩選膠囊、詳情切換和關閉按鈕都是帶名稱的 Arc 控制項；輸出是可以選取的文字。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| `Tab` | Run、兩個版本選擇、篩選膠囊、案例列表，然後是打開案例的切換和關閉按鈕 |
| 列表中的 `ArrowUp` / `ArrowDown` | 上一個或下一個案例（有案例打開時會打開它） |
| `Home` / `End`、`PageUp` / `PageDown` | 第一個或最後一個案例，向上或向下一頁 |
| 列表中的 `Enter` 或 `Space` | 打開或關閉游標所在的案例 |
| `Escape` | 關閉打開的案例並回到列表 |
| 膠囊上的 `ArrowLeft` / `ArrowRight` | 上一個或下一個篩選 |

## 動效

- 通過率和相差的值以彈簧滾動到新值；相差的箭頭和顏色立即切換。
- 完成的案例的標籤和結果在 240 ms 內淡入，並上升 4 px；正在執行的案例的圖示會旋轉。
- 打開的案例以高度彈簧展開和收起；並排視圖和逐詞差異之間立即切換。
- 選中的篩選膠囊在膠囊之間滑動。
- 減弱動效時保留淡入但不上升，旋轉的圖示停止；高度和數字直接跳到位。關閉動效時所有變化立即生效。

| 變化 | 完整 | 減弱 | 關閉 |
|---|---|---|---|
| 案例完成 | 淡入並上升 4 px，240 ms | 淡入，240 ms | 立即 |
| 正在執行的案例 | 圖示旋轉 | 圖示靜止 | 圖示靜止 |
| 通過率和相差 | 以彈簧滾動 | 直接跳到位 | 直接跳到位 |
| 打開或關閉案例 | 高度彈簧 | 立即 | 立即 |

## 響應式

- `EvalSuite` 按自身寬度變化，而不是按視窗寬度。卡片最寬 960 px（`CardMaxWidth`）。
- 寬度為 620 px 或更小時（`CompactWidth`、`:compact`），內距收緊，三個方塊上下排列，兩個版本選擇平分寬度，檢查欄隱藏，結果欄只保留圖示，打開案例的兩個輸出上下排列。
- 案例列表超過 448 px（`ListMaxHeight`）時在卡片內捲動，並且只繪製可見的行，所以很長的套件也很快。標題和輸入以省略號結尾；打開的案例會完整顯示它們。
- 讓卡片拉伸（`HorizontalAlignment="Stretch"`），並給它 400 px 或更寬的空間。

<Demo name="Narrow" />

## 相關元件

- `PromptStudio`：在這裡檢查之前，先編輯和試用一個提示詞。
- `ModelArena`：在兩個模型之間做匿名偏好投票。
- `ModelPicker`：選擇一個版本使用的模型。

| 元件 | 何時選擇 |
|---|---|
| `PromptStudio` | 正在撰寫和試用一個提示詞 |
| `ModelArena` | 人們在沒有固定檢查的情況下為兩個回答投票 |
| `Table` | 每個案例的結果有很多數字欄 |
