---
title: Agent loop
summary: AI 代理會收集上下文、呼叫工具並檢查結果，循環往復直到任務完成。
source-hash: dc59ab4f64b0
---

<Demo name="SampleRun" />

## 何時使用

`AgentLoop` 說明使用工具的 AI 代理如何運作：它收集上下文、用工具採取一個動作、檢查結果，若檢查未通過就再繞一圈。一個明亮的光點拖著柔和的拖尾，繞著模型周圍的圓環經過三個站點。上下文區塊會加入圓環旁的堆疊，工具呼叫從圓環落下並完成，檢查則會通過，或讓循環再走一圈。執行結束時，圓環會完整描繪出來，並顯示摘要。它是你所撰寫之執行過程的示意圖，而不是真實代理的記錄，其中也沒有任何東西會呼叫服務。

- **代理功能的產品與說明文件頁面**：告訴客戶助理如何修復失敗的建置或分流支援工單的頁面。卡片說明代理為何需要多次處理，而不是一次就回答。
- **說明失敗與重試**：第一次檢查失敗並附上重試註記，或工具呼叫逾時後在同一步驟中縮小範圍，顯示代理是靠重新檢視來復原，而不是靠猜測。
- **內部代理平台的入門指南**：給即將撰寫第一批工具的工程師看的簡短視覺圖，旁邊附上說明平台如何把工具結果回饋給模型的文字。
- **著陸頁與演講**：一段自成一體、無障礙的動畫，捲動進入可視範圍時播放一次，並結束於一個可作為截圖的靜態摘要。
- **已完成執行過程的回顧**：在減少動態效果下，或把演示過程直接跳到結尾時，卡片會呈現代理建立的上下文、最後一次工具呼叫與最後一次檢查的摘要。

## 何時不用

- **串流真實代理的步驟**：請使用 `ChatThread` 或記錄檢視，呈現代理的實際訊息與工具輸出。`AgentLoop` 播放的是你描述的執行過程，播放期間不接受任何事件。
- **對話紀錄中的單一工具呼叫**：將該呼叫顯示為聊天或記錄中的一列。此卡片說明的是循環，而只有一次呼叫加上一次通過的檢查，說明的內容太少。
- **搜尋大量候選思路**：請使用 `ReasoningTree`，它會為分支評分並剪枝。代理循環只是重複的單一路徑。
- **顯示上下文視窗的使用量**：這裡的 token 總數只計算故事中的區塊，它不是預算或計量表，也不會對任何限制發出警告。
- **即時作業的狀態**：請使用 `Alert`、`Badge` 或顯示真實狀態的表格。靜態的插圖無法回報正式環境的執行已經失敗。

## 用法

未提供循環內容時，卡片會播放內建的範例執行過程：三次循環，內容是虛構的分流情境，搭配虛構的模型與工具。卡片大部分進入可視範圍時開始播放，並在完成狀態時停止。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:AgentLoop />
</UserControl>
```

`AgentLoop` 衍生自 `Explainer`。`Explainer` 是 Special 套件中動畫 AI 解說控制項的基底類別，而它又衍生自 Avalonia 的 `TemplatedControl`。基底類別提供故事時鐘、`IsPlaying`、`Speed`、`AutoPlay`、`IsLooping`、播放、單步與重新開始按鈕、禮貌即時區域，以及減少動態效果的開啟狀態。`AgentLoop` 另外新增循環內容、圓環與其站點、泳道和上下文堆疊。請在 `UseArc()` 旁呼叫 `UseArcSpecial()` 來註冊該套件。

卡片中的數字（token、時長、結果）都是示意值。若頁面談的是真實產品，請在卡片周圍的文字中說明這一點。

### 結構

1. **標頭**：左側為 `Title`（預設為 "Agent loop"），右側為狀態：一個圖示加上 Ready、Running、Paused、Done 或 Stopped 之一。其下的說明文字保留兩行空間：循環就緒或執行中時顯示 "Task"，結束後顯示 "Summary"。
2. **圓環**：1.5 px 的軌道，上面有三個順時針指向的小箭頭。半徑最多 94 px；在窄欄中會縮小，好讓下方的標籤能放在節點旁邊。
3. **站點**：頂部是 Gather context，右下是 Take action，左下是 Verify，每個都是帶標籤與圖示的節點。閒置的站點顯示為淡色；作用中的站點以強調色填滿，外加細環，名稱上有平和的閃爍效果；已拜訪過的站點則保留強調色外框。
4. **光點與拖尾**：一個明亮的光點，會在站點之間緩動移動，拖出一條漸層拖尾。拖尾隨每趟移動而變長或變短，光點抵達時消失。
5. **中央**：模型的標記與名稱（`Model`），其下一行文字為 Ready、循環計數，或結束語（"Done in 3 loops" 或 "Stopped after 2 loops"）。
6. **圖釘與泳道**：從每個下方站點垂下一條短線到其泳道。工具呼叫落在 Take action 下方的泳道，檢查落在 Verify 下方的泳道，各以標籤塊呈現。在第一個標籤塊出現之前，虛線佔位框會填滿泳道。
7. **上下文堆疊**：標題列顯示執行中的 token 總數，每次循環對應一個槽位。每個區塊顯示一個標記、一個標籤、其 token 數和一行詳細資訊。在第一個區塊加入之前，虛線佔位框會填滿堆疊。
8. **頁尾**：左側為播放或暫停、Next step 與 Restart，右側為 0.5×、1× 與 2× 的速度控制。`AreControlsVisible` 會隱藏整列。

### 故事與時間安排

執行過程是根據循環內容一次建立的時間軸，因此暫停、單步與重播都能精確進行。在 1× 速度下，每次循環耗時如下：

| 部分 | 時間 |
|---|---|
| 位於 Gather context（區塊在 0.1 s 時加入） | 0.85 s |
| 前往 Take action | 0.7 s |
| 位於 Take action，一次呼叫 | 1.35 s |
| 同一循環中每多一次呼叫 | + 1.15 s |
| 前往 Verify | 0.5 s |
| 位於 Verify（檢查在 0.1 s 時落下） | 0.95 s |
| 返回 Gather context | 0.7 s |

因此每次循環為 3.7 s 加上在 Take action 的時間，而執行在 Ready 之後 0.22 s 開始。範例執行分別有一、二、二次呼叫，因此總時長為 0.22 + 5.05 + 6.2 + 6.2 = 17.67 秒。呼叫在光點抵達後 0.1 s 落下，顯示 "Calling" 與其工具 0.75 s，然後變成其結果；下一個呼叫會在同一泳道中接替它的位置。最後一次循環後，光點回到 Gather context 時，執行即結束。若最後一次檢查通過，圓環會完整描繪出來，狀態顯示 Done；若失敗，圓環保持中性，狀態顯示 Stopped。

token 總數是目前已收集區塊的總和。低於 1,000 的數字以整數顯示，較大的數字則以一位小數加 K 表示，因此範例執行結束時，1,400 + 5,800 + 8,600 會顯示為 15.8K。

### 你自己的執行過程

把循環作為內容交給卡片。每個 `AgentLoopIteration` 有一個 `Gather` 區塊、一到三個作為其內容的呼叫，以及一個 `Verify` 檢查。標記是 24 × 24 的描邊字符，例如 `ArcIcons` 的成員。

<Demo name="BuildFix" />

```xml
<arc:AgentLoop Task="Fix the failing nightly build and tell #builds."
               Summary="The build cache key had changed. Clearing it fixed the build.">
    <arc:AgentLoopIteration>
        <arc:AgentLoopIteration.Gather>
            <arc:AgentLoopContext Label="Build log" Detail="Stage 3 of 7 failed" Tokens="2900"
                                  Mark="{x:Static arc:ArcIcons.Terminal}" />
        </arc:AgentLoopIteration.Gather>
        <arc:AgentLoopCall Tool="CI" Action="Read pipeline run" Result="Cache key changed"
                           Mark="{x:Static arc:ArcIcons.GitBranch}" />
        <arc:AgentLoopIteration.Verify>
            <arc:AgentLoopCheck Passed="False" Note="Fix not applied yet" />
        </arc:AgentLoopIteration.Verify>
    </arc:AgentLoopIteration>
    <!-- More loops; the last one should pass its check. -->
</arc:AgentLoop>
```

`Task` 是執行播放時的說明文字；`Summary` 是結束時的說明文字，若未設定，則顯示循環與工具呼叫的計數。每次循環從一開始就在上下文堆疊中保留一個槽位，因此卡片在播放時不會變高。兩到四次循環最容易閱讀。

在呼叫上設定 `Status="Error"` 可顯示失敗的呼叫：旋轉圖示會變成叉號，結果文字會以失敗原因呈現，螢幕閱讀器會讀出 "failed"。接著在同一循環中加入第二次呼叫，即可展示代理再次嘗試的過程。

### 停止的執行過程

當最後一次循環的檢查失敗時，執行即告停止：狀態顯示 Stopped，圓環保持中性，中央顯示執行了幾次循環。若沒有設定 `Summary`，說明文字會寫明執行在多少次循環與呼叫後停止。

<Demo name="StoppedRun" />

### 播放、單步與速度

`IsPlaying` 與 `Speed` 是雙向的：頁尾會寫入它們，應用程式也可以繫結它們。`Speed` 縮放故事時鐘（0 會讓它靜止，最大為 4）；頁尾提供 0.5×、1× 與 2×。標籤落下或圓環完整描繪等過渡效果，無論速度為何都保持各自的節奏。`Play()`、`Pause()`、`StepForward()` 與 `Restart()` 可在程式碼中執行相同的操作。

Next step 會播放到下一個節拍後停止：新的上下文區塊、每個工具結果、每個檢查與結束。它以 2.5 倍速播放（至少 2.5×），因此單步很快，但仍能看到移動的過程。Restart 會回到 Ready 並開始播放。`StationChanged` 在光點每次抵達站點時觸發一次，參數為站點與以零為起點的循環編號；`Finished` 則在執行到達結尾時觸發。

<Demo name="EventLog" />

```csharp
loop.StationChanged += (_, e) => log.Add($"Loop {e.Loop + 1}: {e.Station}");
loop.Finished += (_, _) => status.Text = loop.Phase == AgentLoopPhase.Done ? "Done" : "Stopped";
```

`Phase` 是狀態列顯示的狀態：Ready、Running、Paused、Done 或 Stopped。`IsLooping` 會在執行結束四秒後重新播放。

### 窄容器

版面配置依循卡片本身的寬度，而不是視窗的寬度。在 640px 的容器中，上下文堆疊會以欄的形式位於圓環旁，其下有兩條泳道。更窄時，堆疊會移到圓環下方：寬度 460px 以上每列兩個區塊，以下每列一個。圓環欄低於 360px 時，站點標籤會去掉圖示並縮小，兩條泳道會合併為一條，顯示最新的工具呼叫或檢查。在最窄的卡片上，下方兩個站點會再往下移，讓標籤能放得下，速度控制則會在按鈕下方換行。

<Demo name="Narrow" />

### 減少動態效果

在減少動態效果下，卡片會以已完成的執行過程開啟：圓環已完整描繪，堆疊中的每個區塊、最後的標籤塊與摘要都已顯示。再播放一次與 Next step 仍然可用。光點在站點之間直接跳動，沒有拖尾，所有變化都會原地套用，不會有落下或淡入淡出。由於卡片開啟時執行過程會立即播放到結尾，`StationChanged` 會在那一刻為所有站點一起觸發，接著觸發 `Finished`。

<Demo name="ReducedMotion" />

### 由資料驅動

當循環來自檢視模型時，請繫結 `IterationsSource`。新的集合，或可觀察集合的變更，都會重建故事。卡片被看見之前，重建是靜默進行的，因此自動播放仍會在第一次看見時開始；之後則會從頭開始。

```csharp
public sealed class AgentDemoViewModel
{
    public ObservableCollection<AgentLoopIteration> Loops { get; } =
    [
        new(new AgentLoopContext("Support ticket", "Refund not received", 1200, ArcIcons.Inbox),
            [new AgentLoopCall("Billing", "Find the payment", "Refund issued on May 2", mark: ArcIcons.CreditCard)],
            new AgentLoopCheck(true, "Customer told"))
    ];
}
```

```xml
<arc:AgentLoop IterationsSource="{Binding Loops}"
               Task="Answer a refund question."
               Speed="{Binding Speed}" />
```

空的 `IterationsSource` 會顯示靜止的圓環，且控制項關閉。`Labels` 接受一個 `AgentLoopLabels` 物件，裡面包含卡片顯示或播報的每一個文字，包括站點名稱；只設定你需要的即可。

### 文案規範

撰寫執行過程時，每一段都要能放在一行內：上下文標籤為一到三個字，詳細資訊約 30 個字元以內，呼叫動作為兩到四個字，結果約 30 個字元以內。請用工具在你的產品中的實際用途來命名，並使用通用的字符，不要用品牌標誌。

| 不要寫 | 改為 |
|---|---|
| Context | 建置紀錄 |
| 已取得完整建置紀錄以供分析 | 第 3 階段（共 7 階段）失敗 |
| 工具呼叫 1 | 讀取管線執行 |
| 工具回傳成功的回應 | 快取金鑰已變更 |
| 驗證失敗 | 尚未套用修正 |
| 代理已成功完成任務 | 快取金鑰已變更，清除後建置即修復。 |

站點名稱保持在約兩個字以內，過長的名稱會被截在站點旁。任務為一句話；摘要可以占兩行。

### 最佳做法

**建議**

- 讓最後一次循環的檢查通過，讓較早的一次以重試註記失敗。失敗的檢查正是說明代理為何會循環的關鍵。
- 展示一次失敗的工具呼叫，接著在同一循環中以較窄的呼叫重試，因為在單一步驟內復原，正是此圖的第二個重點。
- 將執行過程控制在兩到四次循環。循環更多會讓卡片變高，故事也會長到讀者不願看完。
- 在文案中說明此執行過程僅為示意，並讓其中的工具與你的產品實際擁有的工具相符。

**避免**

- 把數字當成測量值呈現。token 與時間只是在講述故事，並非從模型讀取而來。
- 在標籤塊中使用真實服務的品牌標誌。請使用通用字符，並在文字中說明工具名稱。
- 以計時器取代 `IterationsSource`。每次變更都會讓執行過程從頭開始。
- 同一畫面上有多張正在播放的卡片。每張卡片在播放時都會逐影格繪製圓環。

### 常見問題

**執行過程要多久？** 0.22 s 加上每次循環 3.7 s，再加上在 Take action 的 1.35 s，以及每多一次呼叫再加 1.15 s。除以速度即可得出實際時間。

**單步為什麼會停在那裡？** 單步會在讀者能指出的每個節拍後停下：區塊加入、每個結果、每個檢查與結束。開始的呼叫不算停止點，因為它之後會接著出現結果。

**卡片能在一次循環中顯示三次以上的呼叫嗎？** 不能。超過第三次的呼叫會被忽略，因為泳道一次只顯示一個標籤塊，過長的步驟會掩蓋故事。請改為把工作分散到多次循環中。

**沒有呼叫的循環會怎麼樣？** 光點仍會在 Take action 停留一次呼叫的時間，泳道中不會落下任何東西。

**如何隱藏模型？** 將 `Model` 設為 null。中央就只會顯示循環計數。

## API 參考

<!-- api: AgentLoop -->

<!-- api: AgentLoopIteration -->

<!-- api: AgentLoopContext -->

<!-- api: AgentLoopCall -->

<!-- api: AgentLoopCheck -->

<!-- api: AgentLoopLabels -->

<!-- api: AgentLoopStationChangedEventArgs -->

列舉：

| 類型 | 值 |
|---|---|
| `AgentLoopStation` | `Gather`, `Act`, `Verify` |
| `AgentLoopCallStatus` | `Success` (default), `Error` |
| `AgentLoopPhase` | `Ready`, `Running`, `Paused`, `Done`, `Stopped` |

`AgentLoopDefaults` 保存範例執行過程（`CreateIterations()`、`CreateModel()`、`Task`、`Summary`），讓頁面能從它開始，並只修改其中一次循環。

## 無障礙

- 卡片是一個以其 `Title` 命名的群組。圓環的繪圖（軌道、箭頭、圖釘、光點與拖尾）對輔助技術隱藏；站點名稱、中央、標籤塊與上下文區塊則是真正的文字。
- 上下文堆疊是一個名為 "Context blocks" 的清單，依區塊被收集的順序朗讀。區塊的 token 數之後會接著朗讀 "tokens" 一詞。
- 結果會先朗讀工具名稱（"Tracker returned: …"），失敗的呼叫會說 "failed"。檢查會顯示圖示以及 "Check passed" 或 "Retry" 字樣，因此絕不只依賴顏色。
- 狀態會將每種狀態與一個圖示和一個字配對：Ready、Running、Paused、Done 與 Stopped。
- 禮貌即時區域會在你單步或暫停時朗讀每個節拍（區塊加入、呼叫進行中、結果、檢查），以及結束時的摘要。執行過程自行播放時則保持安靜。
- 單步到達結尾時，Next step 會關閉，鍵盤焦點會移到 Restart，而不是掉到頁面上。
- 鍵盤焦點會在按鈕與速度控制周圍顯示 2px 的焦點環；滑鼠焦點則不顯示。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 在播放或暫停、Next step、Restart 與速度控制之間移動 |
| `Enter` / `Space` 於播放 | 播放或暫停；執行結束後則重新播放 |
| `Enter` / `Space` 於 Next step | 播放到下一個節拍後停止 |
| `Enter` / `Space` 於 Restart | 從 Ready 重新開始並播放 |
| `Left` / `Right`（或 `Up` / `Down`）於速度控制 | 在 0.5×、1× 與 2× 之間移動，並循環繞回 |
| `Home` / `End` 於速度控制 | 選擇 0.5× 或 2× |

## 動態效果

- 光點在站點之間緩動（1× 時為 0.7 s、0.5 s 與 0.7 s），沿圓環拖出一條漸層拖尾：拖尾在每趟移動的中途最長，光點落下時消失，因此它不會停在原地還拖著尾巴。
- 作用中的站點會填滿，一個細環會落在其節點周圍，名稱帶有平和的閃爍效果。
- 工具呼叫會從圓環落下：Take action 下方的圖釘在 180ms 內向下繪製，標籤塊在其末端落下，帶有 6px 的下落與輕微縮放，採用無回彈的彈簧效果。它的旋轉圖示會交叉淡入淡出為勾號或叉號。第二次呼叫在第一次於 80ms 內淡出後接替其位置。
- 上下文區塊以短暫的下落淡入，同時 token 總數在 500ms 內向上計數。
- 循環計數器會滾動其數字；狀態、說明文字與中央那一行則交叉淡入淡出。
- 結束時，圓環會在 760ms 內順時針描繪完成，光點淡出。停止的執行過程則讓圓環保持中性。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 開啟 | 首次看到時播放 | 以已完成的執行過程開啟 | 以已完成的執行過程開啟 |
| 站點之間的光點 | 沿圓環緩動並帶有拖尾 | 在每趟移動結束時直接跳動 | 在每趟移動結束時直接跳動 |
| 標籤塊與區塊 | 以彈簧效果落下，被取代時淡出 | 原地出現 | 原地出現 |
| token 總數 | 在 500ms 內向上計數 | 直接跳到總數 | 直接跳到總數 |
| 站點顏色與標籤塊外框 | 在 240ms 內變化（新區塊為 480ms） | 立即變化 | 立即變化 |
| 完成的圓環 | 在 760ms 內描繪完成 | 直接顯示為已描繪 | 直接顯示為已描繪 |

## 相關元件

- `ReasoningTree`：適用於搜尋候選思路，而不是工具呼叫的循環。
- `PromptCache`：說明共用的提示前綴如何在多個請求之間重複使用。
- `SpeculativeDecoding` 與 `MixtureOfExperts`：說明模型如何產生其 token。

| 元件 | 適用時機 |
|---|---|
| `ChatThread` | 你要呈現與代理的真實對話，逐則訊息顯示 |
| `Timeline` | 你要依時間順序列出真實執行過程做了什麼 |
| `Alert` | 你要以文字回報真實作業的狀態 |
| `ReasoningTree` | 故事的重點是在候選思路之間做選擇 |
| `DataFlow` | 故事是資料流經一條管線 |
