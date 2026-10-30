---
title: Image reveal
summary: 影像生成畫布：由影像本身顏色產生的柔和模糊預覽，在一條邊緣柔和的光帶後方由上而下逐漸變清晰，帶有細緻的底片顆粒、半透明的進度膠囊，以及安靜的排隊與錯誤狀態。
source-hash: 11bcd64820da
---

<Demo name="Preview" />

## 何時使用

`ImageReveal` 以當今影像工具的方式呈現影像模型的工作過程。一張由圖片本身顏色產生、高度模糊的柔和預覽帶著細緻的底片顆粒淡入，接著一條邊緣柔和的光帶由上而下移動：光帶上方的圖片已經清晰，光帶內部混入一份接近清晰的副本，光帶下方的預覽維持柔和，並慢慢對焦。一個半透明的膠囊顯示進度環與步數。排隊中的工作是一塊安靜的表面，上面掠過一道寬闊的微光；失敗的工作則顯示警示圖示、錯誤訊息與 Retry。

- **影像畫布**，或模型算繪圖片時聊天回覆中的內嵌圖片。
- **一組變體圖片**，一張接一張地生成完成。
- **排隊中的工作**，在開始之前也應該顯得有生氣。
- **空狀態**，第一張圖片仍在算繪時。

## 何時不用

- **一般的圖片載入**（來自伺服器或磁碟）：使用模糊預留位置或骨架畫面。
- **文字生成或工具呼叫**：使用文字微光、`ToolCall` 或 `AgentStatus`。
- **進度確定的檔案上傳**：使用進度列。

## 用法

傳入工作的 `Status` 與 `Progress`（目前步數除以總步數），並在預覽圖或最終圖片一出現時就把它交給 `Source`：柔和預覽從它取樣，在它出現之前畫布維持安靜的排隊外觀。粗略的進度更新（每個模型步驟一次）會被平滑成一段連續的顯現。

```xml
<arc:ImageReveal Source="{Binding Image}"
                 AltText="A calm alpine lake at golden hour"
                 Status="{Binding Status}"
                 Progress="{Binding Progress}"
                 Steps="30"
                 AspectRatio="0.8" />
```

沒有狀態也沒有產生器時，`Source` 中的圖片會直接以完成狀態顯示。什麼都沒有設定時，畫布停在它的素色表面上：沒有膠囊、沒有微光，名稱為 "No image"（上方的 Empty 分頁）。用於展示時，可以讓模擬在你提供的圖片上循環播放生成過程（見下文）。

### 結構

1. **畫布**：填滿容器，最寬 560px，並維持 `AspectRatio`（寬度除以高度）；圖片以覆蓋方式裁切。圓角隨 `Size` 變化。
2. **柔和預覽**：圖片寬 6 到 48 像素的副本，平滑放大並模糊，依進度沿這條模糊鏈交叉淡化，帶有細緻的單色顆粒與極其緩慢的漂移。
3. **光帶**：一條約為畫布四分之一高、邊緣柔和的條帶，向下移動時讓圖片變清晰。
4. **膠囊**：位於左上角，一個載入圈、進度環或勾號，搭配一個簡短標籤與步數（"18 / 30"，等寬數字，寬度預留）。圖片顯現完成後不久淡出。
5. **錯誤狀態**：畫布中央的警示圖示、錯誤訊息與 Retry 按鈕。

### 狀態

| `ActualStatus` | 畫布 | 膠囊 |
|---|---|---|
| `Queued` | 表面底色，掠過一道寬闊柔和的微光 | 載入圈與 `QueueLabel`（"Queued, 2 ahead"） |
| `Generating` | 柔和預覽在光帶後方由上而下變清晰 | 進度環、"Generating" 與 "18 / 30" |
| `Done` | 清晰的圖片；一道柔和的光澤橫掃而過 | 勾號與 "Done"，接著膠囊淡出 |
| `Error` | 清晰的圖層在變暗的預覽上重新模糊淡出 | 隱藏；顯示警示圖示、`ErrorMessage` 與 Retry |

每次變化都從目前的姿態出發：重新開始、失敗或更換圖片時，清晰圖層會先重新模糊淡出，底下的狀態才會重設；新圖片從舊畫面的快照交叉淡入。

### 產生器

<Demo name="Generator" />

`Generator` 接受一個執行一次工作的非同步委派：回報 `ImageRevealProgress` 值（進度，以及一拿到就附上的圖片），並傳回最終圖片。擲回例外會顯示錯誤狀態，Retry 會再次執行產生器。`Restart()`（或新的 `RunKey`）會取消正在執行的工作並開始新的工作。

```csharp
reveal.Generator = async (progress, cancellationToken) =>
{
    var job = await images.StartAsync(prompt, cancellationToken);
    await foreach (var update in job.UpdatesAsync(cancellationToken))
    {
        progress.Report(new ImageRevealProgress(update.Step / 30.0, update.Preview));
    }
    return job.Image;
};
```

### 沒有進度事件

<Demo name="Estimated" />

服務不回報進度時，只設定 `Status` 與預估的 `Duration`。膠囊會緩緩走向終點並在那裡等待；當你設定 `Source` 與 `Done` 後，光帶會把圖片掃入。

### 模擬

用於展示或原型時，不要設定 `Status`、`Generator` 與 `Source`，而是給畫布一個 `ImageRevealSimulation`，並傳入你自己的圖片（每張是一個 `ImageRevealSimulationPicture`：圖片及其替代文字）。每次執行先在佇列中等待 `QueueDuration`（900ms），再於 `Duration`（5.2s）內生成並揭示下一張圖片；開啟 `IsLooping` 時，完成的圖片會停留 `HoldDuration`（2.6s），然後重新模糊淡出並再次執行。`Seed` 決定第一張圖片，播報會附加 "simulated"。不會生成或下載任何內容。把 `Simulation` 設為 null 會停止模擬並清空畫布。

```csharp
reveal.Simulation = new ImageRevealSimulation
{
    Pictures =
    [
        new ImageRevealSimulationPicture { Image = lakeBitmap, AltText = "A calm alpine lake at golden hour" },
        new ImageRevealSimulationPicture { Image = dunesBitmap, AltText = "Desert dunes at dusk" },
    ],
};
```

給每個控制項各自的模擬執行個體：它會記住自己播放到哪張圖片。上方預覽揭示的是 Gallery 在程式碼中繪製的三幅場景（高山湖泊、沙漠沙丘、燈塔），它們是展示資料，不屬於元件庫。

### 一組變體

<Demo name="Grid" />

在格線中使用 `Size="Small"`：膠囊只顯示進度環與步數。給每個圖塊不同的 `Seed`，讓顆粒與漂移各不相同（這裡每個圖塊也在同一組圖片上播放各自的模擬）；用 `StartDelay` 錯開各次執行，關閉 `IsLooping`，並設定 `MaxWidth="Infinity"` 讓圖塊填滿各自的儲存格。變更 `RunKey`（或呼叫 `Restart()`）會讓每個圖塊重新模糊淡出並再次執行。

### 自訂文字

`Labels` 包含控制項顯示或朗讀的每一個詞；`QueueLabel` 與 `ErrorMessage` 修改單一工作的標籤與錯誤訊息，`AltText` 為完成的圖片命名。

```csharp
reveal.Labels = new ImageRevealLabels
{
    Generating = "Génération",
    Done = "Terminé",
    Count = (step, total) => $"{step} sur {total}",
};
```

`RingBrush` 設定進度環的顏色（預設為強調色），`ShowsProgress` 隱藏膠囊，`ShowsSheen` 隱藏完成時的光澤。

### 內容指引

- `AltText` 描述圖片內容（"A calm alpine lake at golden hour"），而不是工作本身。
- `QueueLabel` 保持簡短："Queued"、"Queued, 2 ahead"。
- `ErrorMessage` 用一句話說明發生了什麼、該怎麼做。

### 最佳實務

- 只要有任何預覽圖就立即傳入；顯現效果從它取樣。
- 回報每一個模型步驟：200ms 的跟隨會把它們變成一段平滑的顯現。
- 在格線中使用 Small 圖塊、不同的種子，並錯開約 400ms。

### 常見問題

**它會讀取圖片的像素嗎？** 不會。副本只被繪製，從不回讀，因此任何 `IImage` 都可以使用。

**為什麼生成中仍維持排隊外觀？** 還沒有圖片；傳入 `Source`（或由產生器回報一張圖片），預覽就會淡入。

## API 參考

<!-- api: ImageReveal -->

<!-- api: ImageRevealStatus -->

<!-- api: ImageRevealProgress -->

<!-- api: ImageRevealStatusChangedEventArgs -->

<!-- api: ImageRevealSimulation -->

<!-- api: ImageRevealSimulationPicture -->

<!-- api: ImageRevealLabels -->

## 無障礙

- 畫布回報為影像；工作期間它的名稱說明目前狀態（"Generating image"），完成後變為 `AltText`。空白畫布的名稱為 "No image"，不播報任何內容。
- 一個禮貌型即時區域會播報 Queued、Generating image、Image ready 與錯誤訊息，而不是每一步。模擬模式會附加 "simulated"。
- 生成期間，膠囊是一個唯讀進度列，其值為目前步數，名稱為 "Step 18 of 30"。
- 狀態從不只靠顏色表達：膠囊把載入圈、進度環或勾號與簡短標籤搭配，錯誤狀態把警示圖示與錯誤訊息搭配。

### 鍵盤

| 按鍵 | 動作 |
|---|---|
| Tab | 在錯誤狀態下移到 Retry 按鈕。畫布本身無法取得焦點。 |
| Enter、Space | 啟用 Retry。 |

## 動效

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 排隊 | 一道寬闊柔和的微光，每 2.6s 從畫布外掠到畫布外 | 只有表面底色 | 只有表面底色 |
| 預覽出現 | 從 6px 副本開始在 420ms 內淡入，依進度沿模糊鏈交叉淡化 | 同樣的淡入，沒有漂移 | 立即 |
| 生成中 | 邊緣柔和的光帶由上而下移動；顆粒每秒更新 24 次；極其緩慢的漂移隨顯現逐漸停下 | 沒有光帶：接近清晰的副本與圖片依進度依序交叉淡入；顆粒靜止 | 同樣的交叉淡化，立即跟隨進度 |
| 進度更新 | 200ms 的指數跟隨；模擬模式使用緩出曲線，讓構圖更早成形 | 相同 | 立即 |
| 顯現完成 | 一道柔和的光澤橫掃而過（1.1s），接著膠囊淡出（240ms） | 沒有光澤 | 立即 |
| 重新開始、失敗、新圖片 | 清晰圖層先重新模糊淡出（500ms），顯現才會重設；新圖片從快照交叉淡入（450ms） | 相同 | 立即 |
| 膠囊標籤 | 舊文字在 100ms 內淡出，間隔 60ms 後新文字在 160ms 內淡入；膠囊寬度以無回彈的彈簧變化 | 120ms 交叉淡化，寬度立即變化 | 立即 |

畫框迴圈在畫布捲出螢幕或視窗隱藏時暫停，圖片顯現完成後不再繪製。

## 相關

- `AgentStatus`：代理程式執行的即時狀態。
- `ToolCall`：代理程式發出的工具呼叫。
- `ReasoningBlock`：AI 回覆中的思考部分。
