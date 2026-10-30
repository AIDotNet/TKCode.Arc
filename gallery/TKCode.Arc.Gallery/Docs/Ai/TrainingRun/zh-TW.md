---
title: Training run
summary: 微調任務卡片：訓練與驗證損失曲線沿著固定的步數軸即時繪製，這條軸同時也是訓練週期進度列；卡片還顯示學習率、已用時間、預估剩餘時間、最近事件、暫停與取消，以及附 Deploy 的結果。
source-hash: c7fd2a8ac687
---

<Demo name="Preview" />

## 何時使用

`TrainingRun` 把一個微調或訓練任務放在一張卡片上。損失曲線沿著涵蓋整個執行的步數軸即時繪製，這條軸同時也是訓練週期進度列，所以訓練過程中什麼都不會重新縮放。旁邊是步數、學習率、已用時間與預估剩餘時間、最新的三則事件，以及暫停和取消。執行完成後，結果取代即時統計，Deploy 把模型部署到一個端點。

- **AI 平台中的微調或訓練任務頁面**：使用者在這裡查看損失、暫停、取消與部署。
- **評測或蒸餾執行**：回報損失曲線、檢查點和最終分數。
- **新手引導中的一個步驟**：以客戶的資料訓練自訂模型，最後部署。

## 何時不用

- **有多條序列、沒有任務生命週期的圖表**：使用 `LineChart`。
- **由步驟組成的簡短代理任務**，而不是損失曲線：使用 `AgentStatus`。
- **任務清單**：每個任務呈現一列精簡的項目，選取時再開啟這張卡片。

## 用法

依任務設定設置 `Status`、`TotalSteps` 和 `Epochs`，這樣從第一個樣本起座標軸就是正確的。接著持續更新 `Step`、`LearningRate`、`Elapsed` 和 `Eta`，並在資料串流進來時附加到 `Loss` 和 `Events`。不需要節流：曲線會緩動到每個新樣本，所以成批抵達的資料也會以均勻的速度繪製。

```csharp
run.TotalSteps = job.TotalSteps;
run.Epochs = job.Epochs;
run.Status = TrainingRunStatus.Running;

// On every report from the job:
run.Loss.Add(new TrainingRunLossPoint(report.Step, report.TrainLoss, report.ValidationLoss));
run.Step = report.Step;
run.LearningRate = report.LearningRate;
run.Elapsed = report.Elapsed;
run.Eta = report.Eta;
```

`Apply` 接受一個只包含變動內容的 `TrainingRunProgress`，`FollowAsync` 則在 UI 執行緒上依序套用整個更新串流：

```csharp
await run.FollowAsync(jobs.WatchAsync(job.Id, token), token);
```

既未設定 `Status` 也沒有 `Simulation` 時，卡片顯示一個排隊中、尚無資料的任務：名稱為「Fine-tuning job」，統計為空，圖表為空。未設定 `Status` 時，卡片會改為播放它的 `Simulation`，方便在串接真正的任務 API 之前先行開發：`TrainingRunSimulation` 為任務命名（`JobName`、`BaseModel`、`Details`、`Dataset`），給出它等候與訓練所用的 `Hardware`，以及模擬部署傳回的 `Endpoint`。卡片讓它排隊、啟動，並以程式產生的損失曲線、檢查點與評估，在三個 epoch 中訓練 2,400 步。`Scenario="Oom"` 讓它在第 940 步失敗一次，以便查看重試流程。卡片離開畫面時模擬會暫停，你自己設定的 `Status` 優先於它，把 `Simulation` 設為 null 會停止它。

```csharp
run.Simulation = new TrainingRunSimulation
{
    JobName = "support-v4",
    BaseModel = new AgentModel("Base model"),
    Details = ["LoRA r16", "8× H200"],
    Dataset = new TrainingRunDataset("your-org/support-tickets", 48210),
    Hardware = "8× H200",
    Endpoint = "https://inference.example.com/v1/support-v4",
};
```

本頁的示範從 `TrainingRunDemoData` 取得任務。基礎模型使用中性的標籤；任務、資料集與所有損失值都是虛構的。

### 結構

1. **標頭**：基礎模型 24px 的標誌（`BaseModel` 沒有標誌時顯示首字母）、任務名稱、模型及其 `Details`，以及以圖示加文字表示的狀態。
2. **圖例**：最新樣本（或正在檢視的樣本）的訓練損失與驗證損失，以及目前的訓練週期。
3. **圖表**：每隔一個訓練週期一條淺色背景帶、原始訓練損失（淺色）、平滑後的訓練損失，以及每個樣本帶一個圓點的驗證損失。繪圖區下方的步數軸就是訓練週期進度列，每個週期一段。
4. **統計**：步數、學習率、已用時間和預估剩餘時間，數字以滾動方式變化。完成後由結果取代。
5. **事件**：最新的三則，每種類型一個圖示，並標出步數。
6. **頁尾**：資料集、失敗原因或已部署的端點，以及操作按鈕。

### 由應用程式驅動

<Demo name="HostDriven" />

按鈕會引發 `PauseRequested`、`ResumeRequested`、`CancelRequested`、`RetryRequested` 和 `RestartRequested`。在你的任務上執行對應操作，然後設定 `Status`。取消需要按兩次：第一次按下後它會變成 Confirm cancel，持續 3 秒。

`Actions` 決定哪些按鈕可以顯示（預設全部）。唯讀監控設定 `Actions="None"`。每個按鈕還需要對應的狀態：執行中顯示 Pause，暫停時顯示 Resume，排隊、執行或暫停時顯示 Cancel，失敗後顯示 Retry from checkpoint，執行結束後顯示 Run again。

### 結果與部署

<Demo name="Finished" />

完成後，結果取代即時統計。預設是最終訓練損失與驗證損失、步數以及耗時。`Metrics` 可以設定最多四項自訂結果。設定了 `DeployHandler` 時顯示 Deploy：

```csharp
run.DeployHandler = async token =>
{
    var deployment = await api.DeployAsync(job.Id, token);
    return deployment.Endpoint;
};
```

任務執行期間按鈕顯示載入狀態並保持焦點。之後頁尾顯示端點和一個複製按鈕。若任務擲回例外，則顯示例外訊息和 Retry deploy。卡片卸載或執行離開 Completed 時，權杖會被取消。

### 窄版面

<Demo name="Narrow" />

卡片填滿容器，最寬 640px。寬度小於 440px 時，內距降為 16px，統計改為兩欄，圖例把 Validation 縮寫為 Val，頁尾把資料集放在操作按鈕上方。

### 自訂文字

`Labels` 包含卡片顯示或朗讀的所有文字。替換整個物件即可在地化或改寫：

```csharp
run.Labels = new TrainingRunLabels
{
    Training = "Entrenando",
    Pause = "Pausar",
    StepAt = step => $"paso {step:N0}",
};
```

### 內容準則

- 以任務產出的模型為任務命名，例如 `support-v4`。
- `Details` 只放兩三個簡短事實：方法和硬體。
- 事件標籤寫成簡短的已發生事實："Checkpoint saved"、"Loss spike, gradients clipped"。
- `ErrorText` 寫明原因和下一步，而不是堆疊追蹤。

### 最佳做法

- 在第一個樣本之前傳入 `TotalSteps` 和 `Epochs`，座標軸就永遠不會移動。
- 超過數千個樣本的執行，把 `Loss` 降取樣到約 500 個點。
- 回滾到某個檢查點時，移除該檢查點之後的樣本；曲線會直接跳回，而不是緩動。

### 常見問題

**損失突然升高時，為什麼 y 軸不擴大？** 它依最初的幾個樣本固定下來，所以曲線在執行中途不會重新縮放。更高的值畫在頂端邊緣。

**圖例顯示什麼？** 最新的平滑訓練損失與最新的驗證損失，或是你以指標或方向鍵檢視的那個樣本的值。

## API 參考

<!-- api: TrainingRun -->

<!-- api: TrainingRunStatus -->

<!-- api: TrainingRunActions -->

<!-- api: TrainingRunScenario -->

<!-- api: TrainingRunLossPoint -->

<!-- api: TrainingRunEvent -->

<!-- api: TrainingRunEventKind -->

<!-- api: TrainingRunMetric -->

<!-- api: TrainingRunDataset -->

<!-- api: TrainingRunProgress -->

<!-- api: TrainingRunLabels -->

<!-- api: TrainingRunSimulation -->

## 無障礙

- 一個禮貌的即時區域播報狀態變化（Queued、Training、Paused、Completed、Failed、Cancelled、Deployed）和 `Notice`，從不逐步播報。
- 狀態以圖示和文字共同表示，絕不只靠顏色。失敗原因以文字形式出現在頁尾和事件記錄中。
- 圖表是一個有標籤的群組，其名稱包含步數和兩個損失。方向鍵可逐一檢視樣本，圖例顯示其數值。
- 取消是兩步驟操作，誤按一次不會停止耗時很長的執行。Deploy 在載入期間保持焦點。
- 統計是一組術語及其數值。步數和數字使用等寬數字。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| Tab | 移到損失圖表，再到頁尾的操作按鈕 |
| Left、Right | 在圖表上逐一檢視樣本；圖例顯示該步的損失 |
| Shift + Left、Shift + Right | 跳過十個樣本 |
| Home、End | 跳到第一個或最新的樣本 |
| Escape | 清除正在檢視的樣本 |
| Enter、Space | 按下取得焦點的操作；Cancel 需要再按一次 Confirm cancel |

## 動態效果

| 變化 | 完整 | 減弱 | 關閉 |
|---|---|---|---|
| 新樣本 | 曲線和進度列緩動到它（指數，120ms） | 立即 | 立即 |
| 回滾或重設 | 立即 | 立即 | 立即 |
| 新事件 | 清單以 0.24s 無回彈彈簧下移，新列淡入 | 立即 | 立即 |
| 狀態與頁尾文字 | 舊文字 100ms 淡出，新文字 160ms 淡入並上升 2px | 立即 | 立即 |
| 計數數值 | 數字以彈簧滾動 | 立即 | 立即 |
| 載入圈與活動文字微光 | 旋轉、閃爍 | 靜止 | 靜止 |

x 軸涵蓋整個執行，y 軸依最初的樣本固定，所以執行中途什麼都不會重新縮放。模擬在減弱動態效果下仍會繼續執行（它是內容），但卡片離開畫面時會暫停。

## 相關元件

- `AgentStatus`：代理執行的即時狀態。
- `ToolCall`：代理記錄中的一次工具呼叫。
- `LineChart`：通用的多序列圖表。
- `CopyButton`：複製一個值並給予確認。
