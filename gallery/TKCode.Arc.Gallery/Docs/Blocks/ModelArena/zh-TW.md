---
title: 模型競技場
summary: 兩個模型的盲測：一個提示會送給兩個匿名模型，兩個答案以相同速度串流出現，投票後才揭露模型；每個答案附首字時間、速度與費用，並有一份戰績榜。
source-hash: a76b69badd77
---

<Demo name="Preview" />

## 何時使用

模型競技場用人們自己的判斷回答「這兩個哪一個對我們比較好？」，而不是靠基準測試。兩個答案會以相同速度並排出現，因此速度不會左右投票，而模型要等投票結束後才會公開。

- **選擇供應商或模型版本**：用你的產品真正會問的問題跑一輪，讓團隊投票。
- **檢視提示或模型變更**：同一個提示、兩個候選、一次投票，並把每個答案的費用放在旁邊。
- **並排顯示延遲與價格**：每個答案都會回報它的首字時間、速度與費用，這些數字是在真實串流上測得的。
- **共享的戰績榜**：人們投票時，勝、平與負會逐步累積；透過 `Standings` 儲存與還原。

## 何時不用

- **與單一助理的對話**：使用 `AiChat`。
- **自動評分數百個提示**：競技場一次只跑一輪，由人來判斷。
- **挑選預設模型**：使用 `ModelPicker`。
- **比較同一個提示的兩個版本**：使用 `PromptStudio`，它以字詞差異來比較。

## 用法

`ModelArena` 衍生自 Avalonia 的 `TemplatedControl`，由資料驅動：給它 `Contestants`、範例 `Prompts`，以及一個串流單一模型答案的 `Runner`。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:ModelArena x:Name="Arena" MaxWidth="800" />
</UserControl>
```

```csharp
arena.Contestants.AddRange(models);
arena.Prompts.AddRange(samplePrompts);
arena.Runner = new MyArenaRunner();

public sealed class MyArenaRunner : IArenaRunner
{
    public async IAsyncEnumerable<ArenaStreamEvent> RunAsync(
        AiModel model, string prompt, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var piece in provider.StreamAsync(model.Id, prompt, cancellationToken))
        {
            yield return new ArenaStreamEvent(piece.Text, piece.InputTokens, piece.OutputTokens);
        }
    }
}
```

### 一輪

執行（在輸入框中按 Enter，或按 Ctrl+Enter；Mac 上為 Cmd+Enter）會把提示送給從 `Contestants` 挑出的一對模型。每個答案在第一段文字抵達前顯示 "Thinking"（思考中），之後顯示 "Writing"（撰寫中）；先完成的那一個會顯示 "Finished first"（先完成）。兩個答案都完成後，投票列隨即開啟："A is better"（A 較好）、"Tie"（平手）、"B is better"（B 較好）或 "Both bad"（都不好）。投票會揭露模型、加入戰績榜，並提供 "Next round"（下一輪），它會挑選新的一對、移到下一個範例提示並執行。

- **兩者同速**：真實串流的速度可能差異很大，但文字會依同一個共用時鐘，以整個字為單位顯示，所以你看到的內容不取決於哪個模型比較快。速度與費用數字則是在原始串流上測得的。
- **盲測**：投票前，模型名稱或標記都不會出現在卡片、其提示文字或無障礙名稱中。可以關閉「盲測」開關，立即看到模型；投票仍然計入。
- **停止**：一輪執行中，執行按鈕會變成停止，在提示列按 `Escape` 也會停止。尚未完成的欄會顯示 "Stopped"（已停止）；已完成的欄則保留其答案。
- **失敗**：串流失敗時，該欄會保留已收到的內容，並顯示一張錯誤卡片，上面有 "Retry model A"（重試模型 A）。重試只會執行該模型。若錯誤無法靠重試修復，請拋出 `isRetryable: false` 的 `ArenaRunException`；此時卡片會改為提供一個範例提示。

<Demo name="Programmatic" />

### 指標

每個欄位會顯示首字時間（等待時持續計數）、以每秒權杖數表示的速度，以及費用。若執行器在 `ArenaStreamEvent` 中回報權杖數，就會採用該數字；否則會以每四個字元估算為一個權杖。費用是把權杖數乘上模型每百萬權杖的 `InputPrice` 與 `OutputPrice`；沒有定價的模型，費用為零。

### 戰績榜

獎盃按鈕會開啟戰績榜：列出有紀錄的模型，依勝率排序（平手算半勝），只顯示前 `LeaderboardSize` 名。投票後按鈕會出現一個小點，之後第一次開啟時，各列會從舊位置滑到新位置。`Standings` 是以模型 ID 為鍵、`ArenaRecord` 為值的字典：在第一次投票前填入它，就能從已儲存的結果開始；並在 `VoteCast` 觸發時將它儲存。

### 窄版容器

寬度小於 560 時，兩個答案會變成兩個頁籤（每個頁籤顯示名稱與狀態）；小於 480 時，獎盃按鈕只保留圖示，鍵盤提示隱藏，投票按鈕則排成二乘二的格狀。這些寬度指的是卡片本身，而不是視窗。

<Demo name="Narrow" />

### 文字

每個字都來自 `Labels`（`ModelArenaLabels`）：要翻譯或改寫時，請替換該實例；有格式的文字則是函式。

### 內容指引

- **範例提示**：一個簡短的膠囊標籤，加上一個能讓模型表現出差異的完整提示。
- **模型名稱**：完整名稱，加上供頁籤使用的簡稱。
- **價格**：註明它們只是範例。

### 最佳實務

- 在你的執行器中傳遞取消權杖，這樣「停止」與新的一輪才能真正中止串流。
- 你的提供者有回報權杖數時就使用；估算只是後備方案。
- 配對要隨機但不重複：預設挑選器會避開上一組，或設定 `PairPicker`。
- 在 `VoteCast` 觸發時儲存 `Standings`。

### 常見問題

**為什麼兩個答案會以相同速度出現？** 文字由同一個共用時鐘釋放；每個答案下方顯示的速度則是真實速度。

**為什麼投票前卡片不顯示任何模型資訊？** 這是盲測；關閉「盲測」即可看到模型。

**可以不設定執行器來執行嗎？** 不行：沒有 `Runner` 時，執行按鈕會停用。

## API 參考

<!-- api: ModelArena -->

<!-- api: ArenaPrompt -->

<!-- api: ArenaPaneState -->

### 事件

| 事件 | 觸發時機 | 引數 |
|---|---|---|
| `VoteCast` | 人們投票時 | `Vote` |
| `RoundStarted` | 一輪開始時 | `Round`、`Prompt`、`ModelA`、`ModelB` |
| `RunStopped` | 兩個答案完成前，執行被停止時 | `Round`、`Prompt`、`ModelA`、`ModelB` |
| `PaneFailed` | 一個答案失敗時 | `Slot`、`Model`、`Error` |
| `LeaderboardReset` | 戰績榜被重設時 | |

### 串流

| 型別 | 說明 |
|---|---|
| `IArenaRunner` | `RunAsync(model, prompt, cancellationToken)` 以 `IAsyncEnumerable<ArenaStreamEvent>` 回傳一個模型的答案 |
| `ArenaStreamEvent` | 一些文字、輸入權杖數、輸出權杖數，或三者皆有；字串會轉換為一段文字 |
| `ArenaRunException` | 執行器能描述的失敗：訊息、可選的標題，以及是否可以重試 |
| `IArenaPairPicker` | 挑選一輪的兩個模型 |
| `ArenaVote` | 輪次、提示、兩個模型、選擇、勝者，以及兩個答案的指標 |
| `ArenaMetrics` | 首字時間、每秒權杖數、總時間、權杖數、費用，以及權杖數是否為估算 |
| `ArenaRecord` | 勝、平與負；`WinRate` 將平手算為半勝 |

## 無障礙

- 每個答案是一個名為 "Model A" 或 "Model B" 的區域（投票後會加上模型名稱）；其答案區域是可聚焦、有名稱、可捲動的區域，其指標則是一個有名稱的群組。
- 投票前，無障礙樹中任何地方都不存在模型名稱。
- 狀態以文字加圖示呈現，不只靠顏色。頁籤是一個頁籤列，選取會跟隨焦點。
- 一個禮貌型即時區域會宣布：一輪已開始、哪個答案先完成、投票已開放、結果（含兩個模型的名稱）、戰績榜已更新、停止、失敗與重設。串流文字不會逐字朗讀。
- 有尚未查看的結果時，獎盃按鈕會說 "Standings, updated"（戰績榜已更新）。
- 減少動態效果時，仍會保留每個狀態與宣布。

### 鍵盤

| 按鍵 | 動作 |
|---|---|
| `Enter`（在提示框中） | 執行提示框中的文字 |
| `Ctrl+Enter` / `Cmd+Enter` | 執行提示框中的文字，在執行按鈕或膠囊上也有效 |
| `Escape`（在提示列中） | 停止執行 |
| `Tab` | 依序移動：獎盃、盲測、提示框、執行、膠囊、各答案、投票按鈕 |
| `Enter` / `Space`（在投票按鈕上） | 投票；焦點移到 "Next round"（下一輪） |
| `ArrowLeft` / `ArrowRight`（在頁籤上） | 移動並選取，到頭後循環；`Home` 與 `End` 移到兩端 |
| `ArrowUp`、`PageUp`、`Home`（在答案中） | 向上捲動，並停止跟隨串流 |
| `Escape`（在戰績榜中） | 關閉戰績榜；焦點回到獎盃 |

## 動態效果

- 狀態文字與名稱會以短暫的淡入淡出切換，欄位寬度則以快速的彈簧跟隨；載入圖示每秒旋轉一次，模型工作時狀態文字會閃爍。
- 新文字會一次淡入幾個字，答案會跟隨文字結尾，直到人們向上捲動為止。
- 投票列會在各狀態之間淡入淡出，高度以彈簧跟隨；模型的揭露也以相同的淡入淡出呈現。
- 速度與費用數字會逐位滾動。投票後開啟戰績榜會重播動態：各列滑到新位置，長條則放大或縮小。
- 減少動態效果時會移除彈簧與位移，保留文字的節奏但不淡入；「關閉」則會立即套用每個變化。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 狀態、名稱、投票列 | 淡入淡出，寬度與高度彈簧 | 120 ms 淡入淡出 | 立即 |
| 答案文字 | 以整個字為單位，160 ms 淡入 | 相同步進，不淡入 | 相同步進，不淡入 |
| 戰績榜重播 | 列滑動，長條縮放 | 列與長條原地變化 | 立即 |
| 跟隨串流 | 每一格緩動 | 直接跳到結尾 | 直接跳到結尾 |

## 相關元件

- `AiChat`：與單一助理的對話。
- `PromptStudio`：編輯提示並比較其版本。
- `ModelPicker`：單獨挑選模型。

| 元件 | 適用時機 |
|---|---|
| `AiChat` | 由一個助理回答 |
| `PromptStudio` | 被比較的對象是提示 |
| `ModelPicker` | 只需要選擇模型 |
