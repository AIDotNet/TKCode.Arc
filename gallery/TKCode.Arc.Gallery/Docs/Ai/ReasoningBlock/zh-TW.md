---
title: Reasoning block
summary: AI 回覆中可摺疊的思考區塊：一張卡片，模型思考時標籤逐秒計時，最新的推理在其下方串流出現，完成後以軌道上的步驟呈現完整推理。
source-hash: 3c55fed32f02
---

<Demo name="Preview" />

## 何時使用

`ReasoningBlock` 是 AI 回覆裡的「思考」部分。模型思考時，一個閃爍的 "Thinking" 標籤逐秒計時，其推理最新的幾個字詞在下方串流出現，最多兩行。完成後，卡片摺疊為帶箭頭的 "Thought for 7s"，點擊一次即可把完整推理展開為帶標題的步驟，並在底部說明結果如何，還有一個複製按鈕。答案始終是焦點，推理只需一次點擊。

- **會推理的模型所產生的聊天回覆**，位於答案上方。
- **代理日誌**：每一輪都以模型想明白了什麼開頭。
- **還原歷史訊息**：推理保持摺疊，並保留原來的思考時間。
- **稽核與覆核檢視**：人們需要閱讀結果是如何得出的。

## 何時不用

- **代理呼叫的工具**：使用 `ToolCall`；推理區塊容納的是文字，而不是帶輸入與輸出的動作。
- **代理的計畫**：使用 `AgentPlan`；計畫是人可以編輯的清單，推理是一份紀錄。
- **沒有推理可顯示的單行忙碌指示**。
- **模型不應透露的內容**：只傳入你希望人們閱讀的推理。

## 用法

設定 `Status`，並把推理作為一個不斷增長的字串傳給 `Reasoning`：空行分隔段落，寫成 `**Heading**` 或 `## Heading` 的行會開始一個新的步驟。仍在串流輸出中的標題會先被隱藏，所以不會出現裸露的星號。沒有資料時，該區塊顯示 "Thought process"，也沒有可展開的內容。

```csharp
// While streaming: set the growing text, then flip the status when the answer starts.
block.Reasoning = thinkingText;
block.Status = done ? ReasoningStatus.Done : ReasoningStatus.Thinking;

// A finished message from history.
block.Reasoning = message.Reasoning;
block.Elapsed = TimeSpan.FromSeconds(message.ThinkingSeconds);
```

或者自行提供步驟，並在模型串流輸出時向其 `Text` 追加內容：

```xml
<arc:ReasoningBlock Elapsed="0:0:07">
    <arc:ReasoningStep Heading="Check what is open"
                       Text="Two reconciliations are unmatched and one export has not finished." />
</arc:ReasoningBlock>
```

思考期間由該區塊自行計秒，結束後保留結果。如果你的服務會回報耗時，請自行設定 `Elapsed`。沒有時間的已完成區塊顯示 "Thought process"。

### 結構

1. **頭部**：整個頂部一行就是一個按鈕：標記（18px，強調色）、標籤及其時間、需要時出現的停止或重試按鈕，以及最右端的箭頭。
2. **預覽**：思考中且處於收合狀態時，顯示最新步驟最新的幾個字詞，最多兩行，顏色偏淡。
3. **步驟**：展開時，每個步驟是標題加內文，旁邊是帶空心圓點的軌道。
4. **底部**：一個勾選加 "Done"，或 "Stopped"，以及複製推理的按鈕。

### 狀態

上方的頁籤展示了即時、已完成、已中斷和沒有資料的區塊；各狀態顯示如下：

| 狀態 | 頭部顯示 | 展開部分 |
|---|---|---|
| `Thinking` | 閃爍的 "Thinking for 3s"，可帶停止按鈕 | 目前為止的步驟，串流出現 |
| `Done` | "Thought for 7s" | 步驟，然後是 "Done" 和複製 |
| `Stopped` | "Stopped after 4s" | 已串流出的內容，然後是 "Stopped" 和複製 |
| `Error` | 警示圖示和 "Thinking interrupted"，帶重試 | 步驟，然後是 `ErrorText` 中的原因 |

### 停止

<Demo name="Stop" />

`AllowsStop` 會在模型思考時顯示停止按鈕。它引發 `StopRequested`：終止你的請求，然後把 `Status` 設為 `Stopped`。失敗的區塊會顯示重試並引發 `RetryRequested`。範例播放的是一段模擬，停止按鈕會自行結束它。

### 精簡樣式

<Demo name="Plain" />

`Variant="Plain"` 去掉邊框和左右內距，使該區塊貼合地放在訊息氣泡中。

### 模擬模型

用於沒有模型的展示或原型時，給該區塊一個 `ReasoningBlockSimulation`，並傳入要播放的推理。該區塊會進入思考、逐秒計時，並以每 45ms 三個詞的速度串流輸出文字，然後完成；只有在它沒有自己的推理、步驟、狀態或時間時才會播放，每次接手模擬時都會從頭開始，離開螢幕時暫停。停止按鈕會把它結束為 Stopped，`Restart()` 會重新播放，`Finished` 會在播放完成時通知你。你自己的資料始終優先：設定 `Reasoning` 後，模擬就會讓位。

```xml
<arc:ReasoningBlock AllowsStop="True">
    <arc:ReasoningBlock.Simulation>
        <arc:ReasoningBlockSimulation Reasoning="The export job is still running, so the close waits for it." />
    </arc:ReasoningBlock.Simulation>
</arc:ReasoningBlock>
```

```csharp
// Or from code, with a reasoning you recorded.
block.Simulation = new ReasoningBlockSimulation { Reasoning = recordedReasoning };
```

### 你自己的文字

`Labels` 包含該區塊顯示或朗讀的每一個詞。替換該物件即可在地化或改寫：

```csharp
block.Labels = new ReasoningBlockLabels
{
    Thinking = "Reflexion",
    Thought = "Réfléchi",
    Duration = seconds => $"pendant {seconds}s",
};
```

`Mark` 可以用你自己的 24 × 24 描邊圖示替換星芒，`MarkBrush` 設定其顏色。

### 減少動態效果

減少動態效果時，標籤不再閃爍，箭頭與面板立即變化，文字以短暫淡入淡出交替。關閉動態效果時一切立即變化。秒數仍會計時：它是資訊，不是裝飾。

### 內容指引

- 標題只有幾個詞，說明模型做了什麼（"Check what is open"），而不是它的感受。
- 一個步驟是一小段文字。把長推理拆成多個步驟，不要寫成一大塊。
- 時間寫作 "for 7s" 或 "for 1m 5s"；已停止的區塊寫作 "after 4s"。

### 最佳實務

- 把該區塊放在答案上方，答案出現後保持收合。
- 透過設定不斷增長的字串，或向 `ReasoningStep.Text` 追加來串流輸出；不要在每個 token 上重建清單。
- 失敗時傳入 `ErrorText`，讓人在重試旁邊看到停止的原因。

### 常見問題

**思考時人可以展開它嗎？** 可以。預覽會收合，步驟在原地串流出現。

**如何顯示固定的時間？** 設定 `Elapsed`；該區塊隨後顯示該時間且不再計時。

## API 參考

<!-- api: ReasoningBlock -->

<!-- api: ReasoningStep -->

<!-- api: ReasoningBlockSimulation -->

<!-- api: ReasoningBlockLabels -->

## 無障礙

- 頭部是一個按鈕，會回報其展開或收合；它的名稱先讀出標籤與時間，再讀出按下後的結果（"Thought for 7s, Show reasoning"）。
- 已停止或失敗的區塊不只靠顏色表達：頭部有警示圖示和 "Thinking interrupted"。
- 鍵盤焦點在頭部內顯示 2px 的焦點環；指標焦點不繪製。
- 每個文字都位於 `ReasoningBlockLabels` 中。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| Tab | 移動到頭部，然後是出現的停止或重試，再到展開區塊內的複製按鈕 |
| Enter, Space | 在頭部展開或收合步驟；在按鈕上則按下該按鈕 |

## 動效

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 思考中的標籤 | 閃爍，每 1.8 秒一次 | 靜止 | 靜止 |
| 標籤變為 "Thought" | 舊文字 100ms 淡出，新文字 160ms 淡入並上升 2px | 淡入淡出，130ms | 立即 |
| 預覽出現或離開 | 高度彈簧變化（0.26 秒，無回彈），文字淡入 160ms | 立即 | 立即 |
| 預覽中某一行換行 | 文字以 0.24 秒的彈簧向上滑動，而不是跳動 | 立即 | 立即 |
| 步驟展開或收合 | 同一根 0.26 秒的高度彈簧，內容淡入 160ms | 立即 | 立即 |
| 展開時來了新步驟 | 追加並單獨淡入；其他內容不動 | 立即 | 立即 |
| 箭頭 | 以彈簧轉動 | 立即 | 立即 |

頭部在每種狀態下都是 44px 高，預覽始終是兩行高，所以出現停止或重試按鈕、或一行文字流入，都不會改變區塊的高度。

## 相關

- `ToolCall`：代理發起的工具呼叫。
- `AgentPlan`：代理在行動前寫下的計畫。
- `AgentStatus`：整次執行的即時狀態。
